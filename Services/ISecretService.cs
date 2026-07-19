using ReadOnce.Models;

namespace ReadOnce.Services;

public interface ISecretService
{
    Task<CreateSecretResponse> CreateSecretAsync(CreateSecretRequest request);
    Task<string?> GetAndDeleteSecretAsync(string id);
}
