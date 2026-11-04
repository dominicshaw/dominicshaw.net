namespace DominicShawNet.Contact;

public sealed class ContactEmailOptions
{
    public const string SectionName = "ContactEmail";

    public string Endpoint { get; init; } = string.Empty;

    public string SenderAddress { get; init; } = string.Empty;

    public string RecipientAddress { get; init; } = string.Empty;
}
