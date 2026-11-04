namespace DominicShawNet.Contact;

public sealed record ContactRequest(
    string Name,
    string Email,
    string Subject,
    string Message,
    string? Website);
