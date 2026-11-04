using System.Security.Cryptography;
using System.Text;

namespace DominicShawNet.Contact;

public interface IContactRateLimiter
{
    Task<bool> TryAcquireAsync(string partition, CancellationToken cancellationToken = default);
}

internal sealed class FixedWindowContactRateLimiter : IContactRateLimiter
{
    private const int PermitLimit = 5;
    private static readonly TimeSpan WindowLength = TimeSpan.FromMinutes(10);

    private readonly IContactRateLimitStore _store;
    private readonly TimeProvider _timeProvider;

    public FixedWindowContactRateLimiter(
        IContactRateLimitStore store,
        TimeProvider timeProvider)
    {
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<bool> TryAcquireAsync(
        string partition,
        CancellationToken cancellationToken = default)
    {
        var partitionHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(partition)));
        var now = _timeProvider.GetUtcNow();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = await _store.GetAsync(partitionHash, cancellationToken);
            if (current is null)
            {
                if (await _store.TryAddAsync(
                    partitionHash,
                    new ContactRateLimitWindow(now, 1, string.Empty),
                    cancellationToken))
                {
                    return true;
                }

                continue;
            }

            var windowExpired = now - current.StartedAt >= WindowLength;
            if (!windowExpired && current.Attempts >= PermitLimit)
            {
                return false;
            }

            var replacement = windowExpired
                ? new ContactRateLimitWindow(now, 1, string.Empty)
                : new ContactRateLimitWindow(current.StartedAt, current.Attempts + 1, string.Empty);
            if (await _store.TryUpdateAsync(
                partitionHash,
                current,
                replacement,
                cancellationToken))
            {
                return true;
            }
        }
    }
}

internal interface IContactRateLimitStore
{
    Task<ContactRateLimitWindow?> GetAsync(
        string partitionHash,
        CancellationToken cancellationToken);

    Task<bool> TryAddAsync(
        string partitionHash,
        ContactRateLimitWindow window,
        CancellationToken cancellationToken);

    Task<bool> TryUpdateAsync(
        string partitionHash,
        ContactRateLimitWindow current,
        ContactRateLimitWindow replacement,
        CancellationToken cancellationToken);
}

internal sealed record ContactRateLimitWindow(
    DateTimeOffset StartedAt,
    int Attempts,
    string Version);
