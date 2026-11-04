namespace DominicShawNet.Contact;

public interface IContactEmailSender
{
    Task SendAsync(ContactEmailMessage message, CancellationToken cancellationToken);
}

public sealed record ContactEmailMessage(
    string SenderAddress,
    string RecipientAddress,
    string ReplyToName,
    string ReplyToAddress,
    string Subject,
    string PlainTextBody);
