namespace ReadOnce.Services;

public interface ITokenService
{
    string GenerateToken(string userId, string username);
}
