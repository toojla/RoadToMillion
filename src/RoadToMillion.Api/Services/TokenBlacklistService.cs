using System.Collections.Concurrent;

namespace RoadToMillion.Api.Services;

/// <summary>
/// In-memory token blacklist that tracks revoked JWT token IDs (jti).
/// Automatically evicts expired entries to prevent unbounded memory growth.
/// </summary>
public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _blacklistedTokens = new();

    public void BlacklistToken(string jti, DateTimeOffset expiration)
    {
        _blacklistedTokens.TryAdd(jti, expiration);
        EvictExpired();
    }

    public bool IsBlacklisted(string jti) => _blacklistedTokens.ContainsKey(jti);

    private void EvictExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _blacklistedTokens)
        {
            if (kvp.Value < now)
                _blacklistedTokens.TryRemove(kvp.Key, out _);
        }
    }
}

public interface ITokenBlacklistService
{
    void BlacklistToken(string jti, DateTimeOffset expiration);
    bool IsBlacklisted(string jti);
}
