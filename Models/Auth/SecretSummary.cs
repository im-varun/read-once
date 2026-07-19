namespace ReadOnce.Models.Auth;

public record SecretSummary(string Id, DateTime CreatedAt, DateTime ExpiresAt, bool IsRead);
