using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using DominicShawNet.Contact;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DominicShawNet.Tests;

public sealed class ContactApiTests
{
    [Fact]
    public void Health_check_returns_ok()
    {
        var result = new HealthFunction().Run();

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal("text/plain", content.ContentType);
        Assert.Equal("Healthy", content.Content);
    }

    [Fact]
    public async Task Valid_request_is_accepted_and_sends_one_plain_text_message()
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);
        var request = CreateJsonRequest(ValidRequest());

        var result = await function.RunAsync(request);

        AssertStatusCode(result, HttpStatusCode.Accepted);
        var sent = Assert.Single(sender.Messages);
        Assert.Equal("Website Enquiry: Azure migration", sent.Subject);
        Assert.Equal("Visitor", sent.ReplyToName);
        Assert.Equal("visitor@example.com", sent.ReplyToAddress);
        Assert.Equal("dominicshaw@gmail.com", sent.RecipientAddress);
        Assert.Contains("From: Visitor (visitor@example.com)", sent.PlainTextBody, StringComparison.Ordinal);
        Assert.Contains("Please get in touch.", sent.PlainTextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<p>", sent.PlainTextBody, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Invalid_request_returns_problem_details(ContactRequest requestBody)
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);

        var result = await function.RunAsync(CreateJsonRequest(requestBody));

        AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Boundary_length_fields_are_accepted()
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);
        var requestBody = new ContactRequest(
            new string('n', 100),
            new string('a', 242) + "@example.com",
            new string('s', 150),
            new string('m', 5_000),
            null);

        var result = await function.RunAsync(CreateJsonRequest(requestBody));

        AssertStatusCode(result, HttpStatusCode.Accepted);
        Assert.Single(sender.Messages);
    }

    [Theory]
    [InlineData("name", "Visitor\r\nBcc: victim@example.com")]
    [InlineData("email", "visitor@example.com\r\nBcc: victim@example.com")]
    [InlineData("subject", "Hello\r\nBcc: victim@example.com")]
    public async Task Header_injection_is_rejected(string field, string injectedValue)
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);
        var valid = ValidRequest();
        var requestBody = field switch
        {
            "name" => valid with { Name = injectedValue },
            "email" => valid with { Email = injectedValue },
            "subject" => valid with { Subject = injectedValue },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var result = await function.RunAsync(CreateJsonRequest(requestBody));

        AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Malformed_json_returns_problem_details()
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);
        var request = CreateRequest("{\"name\":", "application/json");

        var result = await function.RunAsync(request);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Unsupported_content_type_returns_problem_details()
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);
        var request = CreateRequest(JsonSerializer.Serialize(ValidRequest()), "application/json-invalid");

        var result = await function.RunAsync(request);

        AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Oversized_request_body_returns_problem_details()
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);
        var oversized = JsonSerializer.Serialize(ValidRequest() with { Website = new string('x', 70_000) });

        var result = await function.RunAsync(CreateRequest(oversized, "application/json"));

        AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Honeypot_request_is_accepted_without_sending()
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);

        var result = await function.RunAsync(CreateJsonRequest(
            ValidRequest() with { Website = "https://spam.example" }));

        AssertStatusCode(result, HttpStatusCode.Accepted);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Sixth_attempt_from_one_ip_within_window_is_rate_limited()
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var accepted = await function.RunAsync(CreateJsonRequest(ValidRequest(), "203.0.113.10"));
            AssertStatusCode(accepted, HttpStatusCode.Accepted);
        }

        var result = await function.RunAsync(CreateJsonRequest(ValidRequest(), "203.0.113.10"));

        AssertProblem(result, StatusCodes.Status429TooManyRequests);
        Assert.Equal(5, sender.Messages.Count);
    }

    [Fact]
    public async Task Different_client_ips_use_separate_rate_limit_partitions()
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var accepted = await function.RunAsync(CreateJsonRequest(ValidRequest(), "203.0.113.10"));
            AssertStatusCode(accepted, HttpStatusCode.Accepted);
        }

        var result = await function.RunAsync(CreateJsonRequest(ValidRequest(), "203.0.113.11"));

        AssertStatusCode(result, HttpStatusCode.Accepted);
        Assert.Equal(6, sender.Messages.Count);
    }

    [Fact]
    public async Task Azure_forwarded_client_ips_use_separate_rate_limit_partitions()
    {
        var sender = new FakeContactEmailSender();
        var function = CreateFunction(sender);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var request = CreateJsonRequest(ValidRequest());
            request.Headers["X-Forwarded-For"] = "203.0.113.20:43210";
            var accepted = await function.RunAsync(request);
            AssertStatusCode(accepted, HttpStatusCode.Accepted);
        }

        var otherRequest = CreateJsonRequest(ValidRequest());
        otherRequest.Headers["X-Forwarded-For"] = "203.0.113.21:43210";
        var result = await function.RunAsync(otherRequest);

        AssertStatusCode(result, HttpStatusCode.Accepted);
        Assert.Equal(6, sender.Messages.Count);
    }

    [Fact]
    public async Task Sender_failure_returns_service_unavailable_problem_details()
    {
        var sender = new FakeContactEmailSender(shouldFail: true);
        var function = CreateFunction(sender);

        var result = await function.RunAsync(CreateJsonRequest(ValidRequest()));

        AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
    }

    public static TheoryData<ContactRequest> InvalidRequests => new()
    {
        ValidRequest() with { Name = "" },
        ValidRequest() with { Name = new string('n', 101) },
        ValidRequest() with { Email = "not-an-address" },
        ValidRequest() with { Email = "Visitor <visitor@example.com>" },
        ValidRequest() with { Email = new string('a', 243) + "@example.com" },
        ValidRequest() with { Subject = " " },
        ValidRequest() with { Subject = new string('s', 151) },
        ValidRequest() with { Message = "" },
        ValidRequest() with { Message = new string('m', 5_001) }
    };

    private static ContactFunction CreateFunction(FakeContactEmailSender sender) => new(
        sender,
        Options.Create(new ContactEmailOptions
        {
            SenderAddress = "DoNotReply@example.azurecomm.net",
            RecipientAddress = "dominicshaw@gmail.com"
        }),
        new FixedWindowContactRateLimiter(
            new FakeContactRateLimitStore(),
            TimeProvider.System),
        NullLogger<ContactFunction>.Instance);

    private static ContactRequest ValidRequest() => new(
        "Visitor",
        "visitor@example.com",
        "Azure migration",
        "Please get in touch.",
        null);

    private static HttpRequest CreateJsonRequest(ContactRequest request, string ipAddress = "203.0.113.10") =>
        CreateRequest(JsonSerializer.Serialize(request), "application/json", ipAddress);

    private static HttpRequest CreateRequest(
        string body,
        string contentType,
        string ipAddress = "203.0.113.10")
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Request.ContentLength = context.Request.Body.Length;
        return context.Request;
    }

    private static void AssertStatusCode(IActionResult result, HttpStatusCode expectedStatus)
    {
        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal((int)expectedStatus, statusResult.StatusCode);
    }

    private static void AssertProblem(IActionResult result, int expectedStatus)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        Assert.Contains("application/problem+json", objectResult.ContentTypes);
        Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
    }
}

internal sealed class FakeContactEmailSender : IContactEmailSender
{
    private readonly bool _shouldFail;

    public FakeContactEmailSender(bool shouldFail = false)
    {
        _shouldFail = shouldFail;
    }

    public ConcurrentQueue<ContactEmailMessage> Messages { get; } = new();

    public Task SendAsync(ContactEmailMessage message, CancellationToken cancellationToken)
    {
        if (_shouldFail)
        {
            throw new InvalidOperationException("Simulated email provider failure.");
        }

        Messages.Enqueue(message);
        return Task.CompletedTask;
    }
}
