using ReadOnce.Models;
using ReadOnce.Models.Auth;

namespace ReadOnce.Services;

public enum SecretCreationError
{
    InvalidRequest,
    StorageFailure
}

public sealed record SecretCreationResult(
    bool Succeeded,
    CreateSecretResponse? Response,
    SecretCreationError? ErrorType,
    string? Error
);

public interface ISecretService
{
    Task<SecretCreationResult> CreateSecretAsync(CreateSecretRequest request, string userId);
    Task<string?> GetAndDeleteSecretAsync(string id);
    Task<List<SecretSummary>> GetUserSecretsAsync(string userId);
}
