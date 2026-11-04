using Azure;
using Azure.Communication.Email;
using Azure.Core;

namespace DominicShawNet.Contact;

public sealed class AzureCommunicationServicesContactEmailSender : IContactEmailSender
{
    private readonly EmailClient? _client;

    public AzureCommunicationServicesContactEmailSender(
        Microsoft.Extensions.Options.IOptions<ContactEmailOptions> options,
        TokenCredential credential)
    {
        var configuredEndpoint = options.Value.Endpoint;
        if (Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out var endpoint) &&
            string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            _client = new EmailClient(endpoint, credential);
        }
    }

    public async Task SendAsync(ContactEmailMessage message, CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("The Azure Communication Services endpoint is not configured.");
        }

        var content = new EmailContent(message.Subject)
        {
            PlainText = message.PlainTextBody
        };
        var email = new EmailMessage(message.SenderAddress, message.RecipientAddress, content);
        email.ReplyTo.Add(new EmailAddress(message.ReplyToAddress, message.ReplyToName));

        await _client.SendAsync(WaitUntil.Started, email, cancellationToken);
    }
}
