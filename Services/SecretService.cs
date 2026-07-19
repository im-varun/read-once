using StackExchange.Redis;
using ReadOnce.Models;

namespace ReadOnce.Services;

public class SecretService : ISecretService
{
    private readonly IConnectionMultiplexer _redis;

    public SecretService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<CreateSecretResponse> CreateSecretAsync(CreateSecretRequest request)
    {
        var db = _redis.GetDatabase();

        var id = Guid.NewGuid().ToString();
        var key = $"secret:{id}";
        var ttl = TimeSpan.FromSeconds(request.TtlSeconds);

        var wasSet = await db.StringSetAsync(key, request.Content, ttl, When.NotExists);
        if (!wasSet)
        {
            throw new Exception("Failed to create secret. Please try again.");
        }

        return new CreateSecretResponse(id);
    }

    public async Task<string?> GetAndDeleteSecretAsync(string id)
    {
        var db = _redis.GetDatabase();
        
        var key = $"secret:{id}";

        var value = await db.StringGetDeleteAsync(key);

        return value.IsNullOrEmpty ? null : value.ToString();
    }
}
