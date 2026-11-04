using System.Buffers;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DominicShawNet.Contact;

public sealed class ContactFunction
{
    private const int MaximumRequestBodyBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IContactEmailSender _emailSender;
    private readonly ContactEmailOptions _emailOptions;
    private readonly IContactRateLimiter _rateLimiter;
    private readonly ILogger<ContactFunction> _logger;

    public ContactFunction(
        IContactEmailSender emailSender,
        IOptions<ContactEmailOptions> emailOptions,
        IContactRateLimiter rateLimiter,
        ILogger<ContactFunction> logger)
    {
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    [Function("Contact")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "contact")] HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        bool permitAcquired;
        try
        {
            permitAcquired = await _rateLimiter.TryAcquireAsync(
                ResolveClientIpAddress(request),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _logger.LogWarning("The contact rate-limit store is unavailable.");
            return Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Contact service unavailable",
                "The message could not be sent. Please try again later.");
        }

        if (!permitAcquired)
        {
            return Problem(
                StatusCodes.Status429TooManyRequests,
                "Too many contact requests",
                "Please wait before trying again.");
        }

        if (!request.HasJsonContentType())
        {
            return InvalidRequest("The request body must be JSON.");
        }

        var readResult = await ReadRequestAsync(request, cancellationToken);
        if (readResult.Error is not null)
        {
            return InvalidRequest(readResult.Error);
        }

        var contactRequest = readResult.Request!;
        if (!string.IsNullOrWhiteSpace(contactRequest.Website))
        {
            return new StatusCodeResult(StatusCodes.Status202Accepted);
        }

        var validationErrors = Validate(contactRequest);
        if (validationErrors.Count > 0)
        {
            return ValidationProblem(validationErrors);
        }

        var replyToName = contactRequest.Name.Trim();
        var replyToAddress = contactRequest.Email.Trim();
        var message = new ContactEmailMessage(
            _emailOptions.SenderAddress,
            _emailOptions.RecipientAddress,
            replyToName,
            replyToAddress,
            $"Website Enquiry: {contactRequest.Subject.Trim()}",
            $"From: {replyToName} ({replyToAddress}){Environment.NewLine}{Environment.NewLine}{contactRequest.Message}");

        try
        {
            await _emailSender.SendAsync(message, cancellationToken);
            return new StatusCodeResult(StatusCodes.Status202Accepted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            _logger.LogWarning("The contact email provider could not accept a submission.");
            return Problem(
                StatusCodes.Status503ServiceUnavailable,
                "Contact service unavailable",
                "The message could not be sent. Please try again later.");
        }
    }

    private static async Task<ContactRequestReadResult> ReadRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumRequestBodyBytes)
        {
            return ContactRequestReadResult.Failure("The request body is too large.");
        }

        var rentedBuffer = ArrayPool<byte>.Shared.Rent(MaximumRequestBodyBytes + 1);
        var totalBytesRead = 0;
        try
        {
            totalBytesRead = await request.Body.ReadAtLeastAsync(
                rentedBuffer.AsMemory(0, MaximumRequestBodyBytes + 1),
                MaximumRequestBodyBytes + 1,
                throwOnEndOfStream: false,
                cancellationToken);

            if (totalBytesRead > MaximumRequestBodyBytes)
            {
                return ContactRequestReadResult.Failure("The request body is too large.");
            }

            try
            {
                var contactRequest = JsonSerializer.Deserialize<ContactRequest>(
                    rentedBuffer.AsSpan(0, totalBytesRead),
                    JsonOptions);
                return contactRequest is null
                    ? ContactRequestReadResult.Failure("The JSON request body is required.")
                    : ContactRequestReadResult.Success(contactRequest);
            }
            catch (JsonException)
            {
                return ContactRequestReadResult.Failure("The JSON request body is malformed.");
            }
        }
        finally
        {
            rentedBuffer.AsSpan(0, totalBytesRead).Clear();
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private static string ResolveClientIpAddress(HttpRequest request)
    {
        var forwardedAddress = request.Headers["X-Forwarded-For"]
            .ToString()
            .Split(',', 2, StringSplitOptions.TrimEntries)[0];
        if (IPAddress.TryParse(forwardedAddress, out var address))
        {
            return address.ToString();
        }

        if (IPEndPoint.TryParse(forwardedAddress, out var endpoint))
        {
            return endpoint.Address.ToString();
        }

        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static Dictionary<string, string[]> Validate(ContactRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        ValidateRequiredLength(errors, "name", request.Name, 100);
        ValidateRequiredLength(errors, "email", request.Email, 254);
        ValidateRequiredLength(errors, "subject", request.Subject, 150);
        ValidateRequiredLength(errors, "message", request.Message, 5_000, rejectNewLines: false);

        var trimmedEmail = request.Email?.Trim();
        if (!errors.ContainsKey("email") &&
            (!MailAddress.TryCreate(trimmedEmail, out var parsedEmail) ||
             !string.Equals(parsedEmail.Address, trimmedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            errors["email"] = ["Email must be a valid email address."];
        }

        return errors;
    }

    private static void ValidateRequiredLength(
        IDictionary<string, string[]> errors,
        string field,
        string? value,
        int maximumLength,
        bool rejectNewLines = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
        }
        else if (value.Length > maximumLength)
        {
            errors[field] = [$"{field} must be {maximumLength} characters or fewer."];
        }
        else if (rejectNewLines && (value.Contains('\r') || value.Contains('\n')))
        {
            errors[field] = [$"{field} contains invalid characters."];
        }
    }

    private static IActionResult InvalidRequest(string detail) => Problem(
        StatusCodes.Status400BadRequest,
        "Invalid contact request",
        detail);

    private static IActionResult ValidationProblem(IDictionary<string, string[]> errors)
    {
        var details = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred."
        };
        return Problem(details);
    }

    private static IActionResult Problem(int status, string title, string detail) => Problem(
        new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        });

    private static IActionResult Problem(ProblemDetails details)
    {
        var result = new ObjectResult(details)
        {
            StatusCode = details.Status
        };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    private sealed record ContactRequestReadResult(ContactRequest? Request, string? Error)
    {
        public static ContactRequestReadResult Success(ContactRequest request) => new(request, null);

        public static ContactRequestReadResult Failure(string error) => new(null, error);
    }
}
