using System.Collections.Concurrent;
using System.Globalization;
using DominicShawNet.Contact;

namespace DominicShawNet.Tests;

public sealed class ContactRateLimiterTests
{
    private static readonly DateTimeOffset WindowStart = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Recreated_limiter_preserves_the_active_window()
    {
        var store = new FakeContactRateLimitStore();
        var timeProvider = new ManualTimeProvider(WindowStart);
        var firstLimiter = new FixedWindowContactRateLimiter(store, timeProvider);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.True(await firstLimiter.TryAcquireAsync("203.0.113.10"));
        }

        var recreatedLimiter = new FixedWindowContactRateLimiter(store, timeProvider);

        Assert.False(await recreatedLimiter.TryAcquireAsync("203.0.113.10"));
    }

    [Fact]
    public async Task Exact_window_boundary_starts_a_new_window()
    {
        var store = new FakeContactRateLimitStore();
        var timeProvider = new ManualTimeProvider(WindowStart);
        var limiter = new FixedWindowContactRateLimiter(store, timeProvider);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.True(await limiter.TryAcquireAsync("203.0.113.10"));
        }

        timeProvider.Advance(TimeSpan.FromMinutes(10));
        var recreatedLimiter = new FixedWindowContactRateLimiter(store, timeProvider);

        Assert.True(await recreatedLimiter.TryAcquireAsync("203.0.113.10"));
    }

    [Fact]
    public async Task Concurrent_limiters_atomically_allow_only_five_attempts()
    {
        var store = new FakeContactRateLimitStore();
        var timeProvider = new ManualTimeProvider(WindowStart);
        var attempts = Enumerable.Range(0, 20)
            .Select(_ => new FixedWindowContactRateLimiter(store, timeProvider)
                .TryAcquireAsync("203.0.113.10"));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(5, results.Count(accepted => accepted));
    }

    [Fact]
    public async Task Store_receives_only_a_sha256_partition_key()
    {
        const string ClientAddress = "203.0.113.10";
        var store = new FakeContactRateLimitStore();
        var limiter = new FixedWindowContactRateLimiter(
            store,
            new ManualTimeProvider(WindowStart));

        await limiter.TryAcquireAsync(ClientAddress);

        var partitionHash = Assert.Single(store.PartitionHashes);
        Assert.DoesNotContain(ClientAddress, partitionHash, StringComparison.Ordinal);
        Assert.Matches("^[0-9A-F]{64}$", partitionHash);
    }
}

internal sealed class FakeContactRateLimitStore : IContactRateLimitStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public IEnumerable<string> PartitionHashes => _entries.Keys;

    public Task<ContactRateLimitWindow?> GetAsync(
        string partitionHash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = _entries.TryGetValue(partitionHash, out var entry)
            ? new ContactRateLimitWindow(
                entry.StartedAt,
                entry.Attempts,
                entry.Version.ToString(CultureInfo.InvariantCulture))
            : null;
        return Task.FromResult(window);
    }

    public Task<bool> TryAddAsync(
        string partitionHash,
        ContactRateLimitWindow window,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_entries.TryAdd(
            partitionHash,
            new Entry(window.StartedAt, window.Attempts, 1)));
    }

    public Task<bool> TryUpdateAsync(
        string partitionHash,
        ContactRateLimitWindow current,
        ContactRateLimitWindow replacement,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!long.TryParse(current.Version, CultureInfo.InvariantCulture, out var version) ||
            !_entries.TryGetValue(partitionHash, out var stored) ||
            stored.Version != version)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_entries.TryUpdate(
            partitionHash,
            new Entry(replacement.StartedAt, replacement.Attempts, version + 1),
            stored));
    }

    private sealed record Entry(DateTimeOffset StartedAt, int Attempts, long Version);
}

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow += duration;
}
