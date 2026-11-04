namespace DominicShawNet.Contact;

internal sealed class ContactRateLimitOptions
{
    public const string SectionName = "ContactRateLimit";

    public string Endpoint { get; init; } = string.Empty;

    public string TableName { get; init; } = string.Empty;
}
