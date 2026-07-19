using System.Globalization;
using StackExchange.Redis;
using ReadOnce.Models;
using ReadOnce.Models.Auth;

namespace ReadOnce.Services;

public sealed class SecretService : ISecretService
{
    private const string GetAndMarkReadScript = """
        local value = redis.call('GETDEL', KEYS[1])
        if value and redis.call('EXISTS', KEYS[2]) == 1 then
            redis.call('HSET', KEYS[2], 'isRead', 'true')
        end
        return value
        """;

    private readonly IConnectionMultiplexer _redis;

    public SecretService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<SecretCreationResult> CreateSecretAsync(CreateSecretRequest request, string userId)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return new SecretCreationResult(
                false,
                null,
                SecretCreationError.InvalidRequest,
                "Content cannot be empty."
            );
        }

        if (request.TtlSeconds <= 0)
        {
            return new SecretCreationResult(
                false,
                null,
                SecretCreationError.InvalidRequest,
                "TTL must be greater than zero."
            );
        }

        var db = _redis.GetDatabase();
        var id = Guid.NewGuid().ToString();
        var secretKey = $"secret:{id}";
        var metadataKey = $"secret:{id}:meta";
        var userSecretsKey = $"user:{userId}:secrets";
        var ttl = TimeSpan.FromSeconds(request.TtlSeconds);
        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.Add(ttl);
        var transaction = db.CreateTransaction();

        transaction.AddCondition(Condition.KeyNotExists(secretKey));
        var contentTask = transaction.StringSetAsync(secretKey, request.Content, ttl);
        var metadataTask = transaction.HashSetAsync(
            metadataKey,
            [
                new HashEntry("ownerId", userId),
                new HashEntry("createdAt", createdAt.ToString("O", CultureInfo.InvariantCulture)),
                new HashEntry("expiresAt", expiresAt.ToString("O", CultureInfo.InvariantCulture)),
                new HashEntry("isRead", "false")
            ]
        );
        var metadataExpiryTask = transaction.KeyExpireAsync(metadataKey, ttl);
        var historyTask = transaction.SetAddAsync(userSecretsKey, id);

        var wasCreated = await transaction.ExecuteAsync();
        await contentTask;
        await metadataTask;
        await metadataExpiryTask;
        await historyTask;

        if (!wasCreated)
        {
            return new SecretCreationResult(
                false,
                null,
                SecretCreationError.StorageFailure,
                "Failed to create secret. Please try again."
            );
        }

        return new SecretCreationResult(true, new CreateSecretResponse(id), null, null);
    }

    public async Task<string?> GetAndDeleteSecretAsync(string id)
    {
        var db = _redis.GetDatabase();
        var result = await db.ScriptEvaluateAsync(
            GetAndMarkReadScript,
            [$"secret:{id}", $"secret:{id}:meta"]
        );

        return result.IsNull ? null : result.ToString();
    }

    public async Task<List<SecretSummary>> GetUserSecretsAsync(string userId)
    {
        var db = _redis.GetDatabase();
        var ids = await db.SetMembersAsync($"user:{userId}:secrets");
        var summaries = new List<SecretSummary>();

        foreach (var idValue in ids)
        {
            var id = idValue.ToString();
            var values = await db.HashGetAsync(
                $"secret:{id}:meta",
                [
                    new RedisValue("ownerId"),
                    new RedisValue("createdAt"),
                    new RedisValue("expiresAt"),
                    new RedisValue("isRead")
                ]
            );

            if (values.Length != 4
                || values.Any(static value => value.IsNull)
                || values[0].ToString() != userId
                || !DateTime.TryParse(
                    values[1].ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var createdAt)
                || !DateTime.TryParse(
                    values[2].ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var expiresAt)
                || !bool.TryParse(values[3].ToString(), out var isRead))
            {
                continue;
            }

            summaries.Add(new SecretSummary(id, createdAt, expiresAt, isRead));
        }

        return summaries
            .OrderByDescending(static summary => summary.CreatedAt)
            .ToList();
    }
}
