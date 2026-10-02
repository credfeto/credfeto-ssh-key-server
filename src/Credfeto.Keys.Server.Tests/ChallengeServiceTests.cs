using System;
using System.Security.Cryptography;
using System.Text;
using Credfeto.Keys.Server.Config;
using Credfeto.Keys.Server.Services;
using FunFair.Test.Common;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class ChallengeServiceTests : LoggingTestBase
{
    private const string HOST = "server1.example.com";
    private const string USER = "mark";
    private const int MAX_NONCE_SEARCH = 1000;
    private const string BASE64_URL_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    private static readonly Guid KeyId = new("12345678-1234-1234-1234-123456789012");
    private static readonly byte[] Secret = new byte[32];

    public ChallengeServiceTests(ITestOutputHelper output)
        : base(output) { }

    private static int GetSignatureStart(string token)
    {
        return token.LastIndexOf(value: '.', comparisonType: StringComparison.Ordinal) + 1;
    }

    private IChallengeService CreateService(FakeTimeProvider? timeProvider = null)
    {
        IOptions<ChallengeOptions> options = GetSubstitute<IOptions<ChallengeOptions>>();
        options.Value.Returns(
            new ChallengeOptions
            {
                HmacSecret = Convert.ToBase64String(Secret),
                TtlSeconds = 300,
                SshNamespace = "ssh-key-server-v1",
            }
        );

        return new ChallengeService(
            options: options,
            timeProvider: timeProvider ?? TimeProvider.System,
            logger: this.GetTypedLogger<ChallengeService>()
        );
    }

    [Fact]
    public void AddChallengeCanBeVerified()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: token);

        Assert.Equal(expected: ChallengeVerificationResult.Valid, actual: result);
    }

    [Fact]
    public void DeleteChallengeCanBeVerified()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateDeleteChallenge(host: HOST, user: USER, keyId: KeyId);

        ChallengeVerificationResult result = service.VerifyDeleteChallenge(
            host: HOST,
            user: USER,
            keyId: KeyId,
            token: token
        );

        Assert.Equal(expected: ChallengeVerificationResult.Valid, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForWrongHost()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        ChallengeVerificationResult result = service.VerifyAddChallenge(
            host: "other.example.com",
            user: USER,
            token: token
        );

        Assert.Equal(expected: ChallengeVerificationResult.ContextMismatch, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForWrongUser()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: "other", token: token);

        Assert.Equal(expected: ChallengeVerificationResult.ContextMismatch, actual: result);
    }

    [Fact]
    public void DeleteChallengeFailsForWrongKeyId()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateDeleteChallenge(host: HOST, user: USER, keyId: KeyId);

        ChallengeVerificationResult result = service.VerifyDeleteChallenge(
            host: HOST,
            user: USER,
            keyId: Guid.NewGuid(),
            token: token
        );

        Assert.Equal(expected: ChallengeVerificationResult.ContextMismatch, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForTamperedToken()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);
        int signatureStart = GetSignatureStart(token);

        // Replacing the first signature character keeps the encoding canonical; the final character carries
        // padding bits that .NET 11+ requires to be zero, so altering it produces a format error instead.
        char replacement = token[signatureStart] == 'A' ? 'B' : 'A';
        string tampered = token[..signatureStart] + replacement + token[(signatureStart + 1)..];

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: tampered);

        Assert.Equal(expected: ChallengeVerificationResult.InvalidSignature, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForMalformedSignature()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);
        int signatureStart = GetSignatureStart(token);
        string malformed = token[..signatureStart] + "!!!!";

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: malformed);

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Theory]
    [InlineData(-1, "=")]
    [InlineData(-1, "==")]
    [InlineData(10, "=")]
    [InlineData(10, " ")]
    [InlineData(10, "\n")]
    [InlineData(0, "\t")]
    [InlineData(-1, " ")]
    public void AddChallengeFailsForSignatureWithNonAlphabetCharacters(int offset, string inserted)
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);
        int insertAt = offset < 0 ? token.Length : GetSignatureStart(token) + offset;
        string malformed = token.Insert(startIndex: insertAt, value: inserted);

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: malformed);

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    public void AddChallengeFailsForTruncatedSignature(int signatureLength)
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);
        string truncated = token[..(GetSignatureStart(token) + signatureLength)];

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: truncated);

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForOverlongSignature()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: token + "AAAA");

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForNonCanonicalFinalSignatureCharacter()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        // A 32-byte HMAC encodes to 43 characters; the final one carries 4 unused bits that must be zero, so
        // flipping its lowest bit decodes to the same bytes under a lenient decoder but is not canonical.
        int finalIndex = BASE64_URL_ALPHABET.IndexOf(token[^1], StringComparison.Ordinal);
        string nonCanonical = token[..^1] + BASE64_URL_ALPHABET[finalIndex ^ 1];

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: nonCanonical);

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Fact]
    public void AddChallengeFailsForStandardBase64AlphabetSignature()
    {
        FakeTimeProvider timeProvider = new();
        IChallengeService service = this.CreateService(timeProvider);
        string token = CreateTokenWithStandardBase64Signature(timeProvider.GetUtcNow().ToUnixTimeMilliseconds());

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: token);

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Fact]
    public void AddChallengeSignatureIsUnpaddedBase64UrlOfHmac()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);
        int signatureStart = GetSignatureStart(token);
        string payload = token[..(signatureStart - 1)];

        string expected = ToLegacyBase64Url(HMACSHA256.HashData(key: Secret, source: Encoding.UTF8.GetBytes(payload)));

        Assert.Equal(expected: expected, actual: token[signatureStart..]);
    }

    private static string ToLegacyBase64Url(byte[] data)
    {
        return Convert
            .ToBase64String(data)
            .TrimEnd('=')
            .Replace(oldChar: '+', newChar: '-')
            .Replace(oldChar: '/', newChar: '_');
    }

    private static string CreateTokenWithStandardBase64Signature(long unixMs)
    {
        // Searches deterministically for a payload whose HMAC encodes with '+' or '/', which only the standard
        // base64 alphabet uses, so the signature is otherwise genuine.
        for (int nonce = 0; nonce < MAX_NONCE_SEARCH; nonce++)
        {
            string payload = $"add:{HOST}:{USER}:{unixMs}:{nonce:x32}";
            string signature = Convert
                .ToBase64String(HMACSHA256.HashData(key: Secret, source: Encoding.UTF8.GetBytes(payload)))
                .TrimEnd('=');

            if (signature.AsSpan().IndexOfAny('+', '/') >= 0)
            {
                return $"{payload}.{signature}";
            }
        }

        throw new InvalidOperationException("No payload produced a signature using the standard base64 alphabet");
    }

    [Fact]
    public void AddChallengeFailsWhenExpired()
    {
        FakeTimeProvider timeProvider = new();
        IChallengeService service = this.CreateService(timeProvider);
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        timeProvider.Advance(TimeSpan.FromSeconds(301));

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: token);

        Assert.Equal(expected: ChallengeVerificationResult.Expired, actual: result);
    }

    [Fact]
    public void AddChallengeCannotBeUsedAsDeleteChallenge()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateAddChallenge(host: HOST, user: USER);

        ChallengeVerificationResult result = service.VerifyDeleteChallenge(
            host: HOST,
            user: USER,
            keyId: KeyId,
            token: token
        );

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Fact]
    public void DeleteChallengeCannotBeUsedAsAddChallenge()
    {
        IChallengeService service = this.CreateService();
        string token = service.GenerateDeleteChallenge(host: HOST, user: USER, keyId: KeyId);

        ChallengeVerificationResult result = service.VerifyAddChallenge(host: HOST, user: USER, token: token);

        Assert.Equal(expected: ChallengeVerificationResult.InvalidFormat, actual: result);
    }

    [Fact]
    public void SshNamespaceMatchesConfiguration()
    {
        IChallengeService service = this.CreateService();

        Assert.Equal(expected: "ssh-key-server-v1", actual: service.SshNamespace);
    }
}
