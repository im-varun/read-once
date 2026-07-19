using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;

namespace ReadOnce.Services;

public sealed class UserService : IUserService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IPasswordHasher<UserService> _passwordHasher;

    public UserService(IConnectionMultiplexer redis, IPasswordHasher<UserService> passwordHasher)
    {
        _redis = redis;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserRegistrationResult> RegisterAsync(string username, string password)
    {
        var normalizedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(password))
        {
            return new UserRegistrationResult(false, null, "Username and password are required.");
        }

        var userId = Guid.NewGuid().ToString();
        var passwordHash = _passwordHasher.HashPassword(this, password);
        var key = GetUserKey(normalizedUsername);
        
        var db = _redis.GetDatabase();
        var transaction = db.CreateTransaction();

        transaction.AddCondition(Condition.KeyNotExists(key));
        var writeTask = transaction.HashSetAsync(
            key,
            [
                new HashEntry("userId", userId),
                new HashEntry("passwordHash", passwordHash)
            ]
        );

        var wasCreated = await transaction.ExecuteAsync();
        await writeTask;

        return wasCreated
            ? new UserRegistrationResult(true, userId, null)
            : new UserRegistrationResult(false, null, "Username is already taken.");
    }

    public async Task<CredentialValidationResult> ValidateCredentialsAsync(string username, string password)
    {
        var normalizedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(password))
        {
            return new CredentialValidationResult(false, null);
        }

        var db = _redis.GetDatabase();
        var values = await db.HashGetAsync(
            GetUserKey(normalizedUsername),
            [new RedisValue("userId"), new RedisValue("passwordHash")]);

        if (values.Length != 2 || values[0].IsNullOrEmpty || values[1].IsNullOrEmpty)
        {
            return new CredentialValidationResult(false, null);
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            this,
            values[1].ToString(),
            password
        );

        return verificationResult == PasswordVerificationResult.Failed
            ? new CredentialValidationResult(false, null)
            : new CredentialValidationResult(true, values[0].ToString());
    }

    private static string GetUserKey(string username) => $"user:{username}";
}
