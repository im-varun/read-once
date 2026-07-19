namespace ReadOnce.Services;

public sealed record UserRegistrationResult(bool Succeeded, string? UserId, string? Error);

public sealed record CredentialValidationResult(bool IsValid, string? UserId);

public interface IUserService
{
    Task<UserRegistrationResult> RegisterAsync(string username, string password);
    Task<CredentialValidationResult> ValidateCredentialsAsync(string username, string password);
}
