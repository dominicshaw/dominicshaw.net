using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace DominicShawNet.Contact;

internal sealed class AzureTableContactRateLimitStore : IContactRateLimitStore
{
    private const string TablePartitionKey = "contact";

    private readonly TableClient? _client;

    public AzureTableContactRateLimitStore(
        IOptions<ContactRateLimitOptions> options,
        TokenCredential credential)
    {
        var configured = options.Value;
        if (Uri.TryCreate(configured.Endpoint, UriKind.Absolute, out var endpoint) &&
            string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(configured.TableName))
        {
            _client = new TableClient(endpoint, configured.TableName, credential);
        }
    }

    public async Task<ContactRateLimitWindow?> GetAsync(
        string partitionHash,
        CancellationToken cancellationToken)
    {
        var response = await GetClient().GetEntityIfExistsAsync<ContactRateLimitEntity>(
            TablePartitionKey,
            partitionHash,
            cancellationToken: cancellationToken);
        if (!response.HasValue)
        {
            return null;
        }

        var entity = response.Value!;
        return new ContactRateLimitWindow(
            entity.WindowStartedAt,
            entity.Attempts,
            entity.ETag.ToString());
    }

    public async Task<bool> TryAddAsync(
        string partitionHash,
        ContactRateLimitWindow window,
        CancellationToken cancellationToken)
    {
        try
        {
            await GetClient().AddEntityAsync(ToEntity(partitionHash, window), cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status == 409)
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(
        string partitionHash,
        ContactRateLimitWindow current,
        ContactRateLimitWindow replacement,
        CancellationToken cancellationToken)
    {
        try
        {
            await GetClient().UpdateEntityAsync(
                ToEntity(partitionHash, replacement),
                new ETag(current.Version),
                TableUpdateMode.Replace,
                cancellationToken);
            return true;
        }
        catch (RequestFailedException exception) when (exception.Status is 404 or 412)
        {
            return false;
        }
    }

    private TableClient GetClient() => _client ?? throw new InvalidOperationException(
        "The contact rate-limit table is not configured.");

    private static ContactRateLimitEntity ToEntity(
        string partitionHash,
        ContactRateLimitWindow window) => new()
        {
            PartitionKey = TablePartitionKey,
            RowKey = partitionHash,
            WindowStartedAt = window.StartedAt,
            Attempts = window.Attempts
        };

    private sealed class ContactRateLimitEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset WindowStartedAt { get; set; }

        public int Attempts { get; set; }

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }
    }
}
