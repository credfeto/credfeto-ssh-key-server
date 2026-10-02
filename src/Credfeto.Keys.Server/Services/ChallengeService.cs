using System;
using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Credfeto.Keys.Server.Config;
using Credfeto.Keys.Server.Services.LoggingExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Credfeto.Keys.Server.Services;

public sealed class ChallengeService : IChallengeService
{
    // Only the unpadded base64url alphabet the encoder emits is accepted, so padding, whitespace and the
    // standard base64 '+' and '/' characters (all of which a decoder may tolerate) fail closed as malformed.
    private static readonly SearchValues<char> Base64UrlAlphabet = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"
    );

    private readonly byte[] _secretKey;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ttl;
    private readonly ILogger<ChallengeService> _logger;

    public ChallengeService(
        IOptions<ChallengeOptions> options,
        TimeProvider timeProvider,
        ILogger<ChallengeService> logger
    )
    {
        this._logger = logger;
        this._timeProvider = timeProvider;

        ChallengeOptions opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.HmacSecret))
        {
            throw new InvalidOperationException("Challenge:HmacSecret must be configured");
        }

        this._secretKey = Convert.FromBase64String(opts.HmacSecret);
        this._ttl = TimeSpan.FromSeconds(opts.TtlSeconds);
        this.SshNamespace = opts.SshNamespace;
    }

    public string SshNamespace { get; }

    public string GenerateAddChallenge(string host, string user)
    {
        long unixMs = this._timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        string nonce = GenerateNonce();
        string payload = $"add:{host}:{user}:{unixMs}:{nonce}";

        return this.CreateToken(payload);
    }

    public string GenerateDeleteChallenge(string host, string user, Guid keyId)
    {
        long unixMs = this._timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        string nonce = GenerateNonce();
        string payload = $"del:{host}:{user}:{keyId:D}:{unixMs}:{nonce}";

        return this.CreateToken(payload);
    }

    public ChallengeVerificationResult VerifyAddChallenge(string host, string user, string token)
    {
        (ChallengeVerificationResult result, _) = this.VerifyTokenAndCommonContext(
            token: token,
            expectedOp: "add",
            host: host,
            user: user,
            expectedPartCount: 5
        );

        return result;
    }

    public ChallengeVerificationResult VerifyDeleteChallenge(string host, string user, Guid keyId, string token)
    {
        (ChallengeVerificationResult result, string[]? parts) = this.VerifyTokenAndCommonContext(
            token: token,
            expectedOp: "del",
            host: host,
            user: user,
            expectedPartCount: 6
        );

        if (result != ChallengeVerificationResult.Valid || parts is null)
        {
            return result;
        }

        if (!Guid.TryParseExact(input: parts[3], format: "D", out Guid tokenKeyId) || tokenKeyId != keyId)
        {
            this._logger.ChallengeVerificationFailed(
                host: host,
                user: user,
                operation: "del",
                reason: "context mismatch"
            );

            return ChallengeVerificationResult.ContextMismatch;
        }

        return ChallengeVerificationResult.Valid;
    }

    private (ChallengeVerificationResult Result, string[]? Parts) VerifyTokenAndCommonContext(
        string token,
        string expectedOp,
        string host,
        string user,
        int expectedPartCount
    )
    {
        (ChallengeVerificationResult result, string[]? parts) = this.VerifyToken(token);

        if (result != ChallengeVerificationResult.Valid || parts is null)
        {
            return (result, null);
        }

        if (parts.Length != expectedPartCount)
        {
            this._logger.ChallengeVerificationFailed(
                host: host,
                user: user,
                operation: expectedOp,
                reason: "invalid part count"
            );

            return (ChallengeVerificationResult.InvalidFormat, null);
        }

        if (
            !string.Equals(a: parts[0], b: expectedOp, comparisonType: StringComparison.Ordinal)
            || !string.Equals(a: parts[1], b: host, comparisonType: StringComparison.Ordinal)
            || !string.Equals(a: parts[2], b: user, comparisonType: StringComparison.Ordinal)
        )
        {
            this._logger.ChallengeVerificationFailed(
                host: host,
                user: user,
                operation: expectedOp,
                reason: "context mismatch"
            );

            return (ChallengeVerificationResult.ContextMismatch, null);
        }

        return (ChallengeVerificationResult.Valid, parts);
    }

    private (ChallengeVerificationResult Result, string[]? Parts) VerifyToken(string token)
    {
        int dotIdx = token.LastIndexOf(value: '.', comparisonType: StringComparison.Ordinal);

        if (dotIdx < 0)
        {
            return (ChallengeVerificationResult.InvalidFormat, null);
        }

        string payload = token[..dotIdx];
        string hmacBase64Url = token[(dotIdx + 1)..];

        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        using HMACSHA256 hmac = new(key: this._secretKey);
        byte[] expectedHmac = hmac.ComputeHash(payloadBytes);

        Span<byte> actualHmac = stackalloc byte[HMACSHA256.HashSizeInBytes];

        if (!TryDecodeHmac(hmacBase64Url: hmacBase64Url, destination: actualHmac))
        {
            return (ChallengeVerificationResult.InvalidFormat, null);
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedHmac, actualHmac))
        {
            return (ChallengeVerificationResult.InvalidSignature, null);
        }

        string[] parts = payload.Split(':');

        if (parts.Length < 4)
        {
            return (ChallengeVerificationResult.InvalidFormat, null);
        }

        // Timestamp is the second-to-last part (before the nonce)
        if (
            !long.TryParse(
                s: parts[^2],
                style: NumberStyles.None,
                provider: CultureInfo.InvariantCulture,
                result: out long unixMs
            )
        )
        {
            return (ChallengeVerificationResult.InvalidFormat, null);
        }

        DateTimeOffset issued = DateTimeOffset.FromUnixTimeMilliseconds(unixMs);

        if (this._timeProvider.GetUtcNow() - issued > this._ttl)
        {
            return (ChallengeVerificationResult.Expired, null);
        }

        return (ChallengeVerificationResult.Valid, parts);
    }

    private string CreateToken(string payload)
    {
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        using HMACSHA256 hmac = new(key: this._secretKey);
        byte[] hash = hmac.ComputeHash(payloadBytes);

        return $"{payload}.{Base64Url.EncodeToString(hash)}";
    }

    private static string GenerateNonce()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(16);

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool TryDecodeHmac(string hmacBase64Url, Span<byte> destination)
    {
        ReadOnlySpan<char> source = hmacBase64Url.AsSpan();

        if (source.ContainsAnyExcept(Base64UrlAlphabet))
        {
            return false;
        }

        OperationStatus status = Base64Url.DecodeFromChars(
            source: source,
            destination: destination,
            charsConsumed: out _,
            bytesWritten: out int bytesWritten,
            isFinalBlock: true
        );

        return status == OperationStatus.Done && bytesWritten == destination.Length;
    }
}
