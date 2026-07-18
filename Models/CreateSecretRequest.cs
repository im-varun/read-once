namespace ReadOnce.Models;

public record CreateSecretRequest(string Content, int TtlSeconds);
