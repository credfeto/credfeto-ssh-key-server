using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Keys.Server.Helpers;
using Credfeto.Keys.Services;
using FunFair.Test.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.Extensions;
using Xunit;

namespace Credfeto.Keys.Server.Tests;

public sealed class EndpointsTests : TestBase
{
    private const string HOST = "server1.example.com";
    private const string USER = "mark";
    private const string KEYS_PATH = "/keys/" + HOST + "/" + USER;
    private const string KEY_ID = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string KEY_PATH = KEYS_PATH + "/" + KEY_ID;
    private const string CHALLENGE_TOKEN = "challenge-token";
    private const string SSH_NAMESPACE = "ssh-key-server-v1";
    private const string ADD_KEY_BODY = """{"key":"ssh-ed25519 AAAA test","challenge":"c","signature":"s"}""";
    private const string DELETE_KEY_BODY = """{"challenge":"c","signature":"s"}""";

    private static readonly Guid KeyId = new(KEY_ID);

    private readonly IKeyManagementService _keyManagementService;

    public EndpointsTests()
    {
        this._keyManagementService = GetSubstitute<IKeyManagementService>();
    }

    [Fact]
    public async Task PingReturnsPongAsync()
    {
        EndpointResponse response = await this.SendAsync(method: HttpMethod.Get, path: "/ping");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.Equal(expected: """{"message":"Pong!"}""", actual: response.Body);
    }

    [Fact]
    public async Task GetKeysReturnsAuthorizedKeysAsTextAsync()
    {
        const string authorizedKeys = "ssh-ed25519 AAAA test\n";
        MockKeyManagementServiceAsyncResult(
            keyManagementService: this._keyManagementService,
            result: KeyListResult.Success(authorizedKeys)
        );

        EndpointResponse response = await this.SendAsync(method: HttpMethod.Get, path: KEYS_PATH);

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.Equal(expected: "text/plain", actual: response.MediaType);
        Assert.Equal(expected: authorizedKeys, actual: response.Body);
    }

    [Fact]
    public async Task GetKeysReturnsEmptyStringWhenNoKeysAsync()
    {
        MockKeyManagementServiceAsyncResult(
            keyManagementService: this._keyManagementService,
            result: KeyListResult.Success(string.Empty)
        );

        EndpointResponse response = await this.SendAsync(method: HttpMethod.Get, path: KEYS_PATH);

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.Equal(expected: "\"\"", actual: response.Body);
    }

    [Fact]
    public async Task GetKeysReturnsBadRequestForInvalidRequestAsync()
    {
        MockKeyManagementServiceAsyncResult(
            keyManagementService: this._keyManagementService,
            result: KeyListResult.InvalidRequest
        );

        EndpointResponse response = await this.SendAsync(method: HttpMethod.Get, path: KEYS_PATH);

        Assert.Equal(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        Assert.Equal(expected: string.Empty, actual: response.Body);
    }

    [Fact]
    public async Task GetAddChallengeReturnsChallengeAsync()
    {
        this._keyManagementService.GenerateAddChallenge(host: HOST, user: USER).Returns(CreateChallengeResult());

        EndpointResponse response = await this.SendAsync(method: HttpMethod.Get, path: KEYS_PATH + "/add-challenge");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.Equal(expected: ExpectedChallengeJson(), actual: response.Body);
    }

    [Fact]
    public async Task GetAddChallengeReturnsBadRequestForInvalidRequestAsync()
    {
        this._keyManagementService.GenerateAddChallenge(host: HOST, user: USER)
            .Returns(KeyChallengeResult.InvalidRequest);

        EndpointResponse response = await this.SendAsync(method: HttpMethod.Get, path: KEYS_PATH + "/add-challenge");

        Assert.Equal(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        Assert.Equal(expected: string.Empty, actual: response.Body);
    }

    [Fact]
    public async Task AddKeyReturnsCreatedKeyIdAsync()
    {
        MockKeyManagementServiceAsyncResult(
            keyManagementService: this._keyManagementService,
            result: AddKeyResult.Created(KeyId)
        );

        EndpointResponse response = await this.SendAsync(
            method: HttpMethod.Post,
            path: KEYS_PATH,
            jsonBody: ADD_KEY_BODY
        );

        Assert.Equal(expected: HttpStatusCode.Created, actual: response.StatusCode);
        Assert.Equal(expected: "{\"keyId\":\"" + KEY_ID + "\"}", actual: response.Body);
        await this
            ._keyManagementService.Received(1)
            .AddKeyAsync(
                host: HOST,
                user: USER,
                key: "ssh-ed25519 AAAA test",
                challenge: "c",
                signature: "s",
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData(KeyManagementStatus.InvalidChallenge, "Invalid or expired challenge.")]
    [InlineData(KeyManagementStatus.InvalidKey, "Invalid SSH public key format.")]
    [InlineData(KeyManagementStatus.InvalidSignature, "SSH signature verification failed.")]
    [InlineData(KeyManagementStatus.InvalidRequest, "Invalid host or username.")]
    public async Task AddKeyReturnsBadRequestWhenRejectedAsync(KeyManagementStatus status, string expectedMessage)
    {
        MockKeyManagementServiceAsyncResult(
            keyManagementService: this._keyManagementService,
            result: AddKeyResult.Failed(status)
        );

        EndpointResponse response = await this.SendAsync(
            method: HttpMethod.Post,
            path: KEYS_PATH,
            jsonBody: ADD_KEY_BODY
        );

        Assert.Equal(expected: HttpStatusCode.BadRequest, actual: response.StatusCode);
        Assert.Equal(expected: "\"" + expectedMessage + "\"", actual: response.Body);
    }

    [Fact]
    public async Task GetDeleteChallengeReturnsChallengeAsync()
    {
        MockKeyManagementServiceAsyncResult(
            keyManagementService: this._keyManagementService,
            result: CreateChallengeResult()
        );

        EndpointResponse response = await this.SendAsync(method: HttpMethod.Get, path: KEY_PATH + "/challenge");

        Assert.Equal(expected: HttpStatusCode.OK, actual: response.StatusCode);
        Assert.Equal(expected: ExpectedChallengeJson(), actual: response.Body);
    }

    [Theory]
    [InlineData(KeyManagementStatus.NotFound, HttpStatusCode.NotFound)]
    [InlineData(KeyManagementStatus.InvalidRequest, HttpStatusCode.BadRequest)]
    public async Task GetDeleteChallengeReturnsErrorWhenNoChallengeAsync(
        KeyManagementStatus status,
        HttpStatusCode expectedStatusCode
    )
    {
        MockKeyManagementServiceAsyncResult(
            keyManagementService: this._keyManagementService,
            result: new KeyChallengeResult(Status: status, Challenge: null)
        );

        EndpointResponse response = await this.SendAsync(method: HttpMethod.Get, path: KEY_PATH + "/challenge");

        Assert.Equal(expected: expectedStatusCode, actual: response.StatusCode);
        Assert.Equal(expected: string.Empty, actual: response.Body);
    }

    [Theory]
    [InlineData(KeyManagementStatus.Success, HttpStatusCode.NoContent, "")]
    [InlineData(KeyManagementStatus.InvalidChallenge, HttpStatusCode.BadRequest, "\"Invalid or expired challenge.\"")]
    [InlineData(
        KeyManagementStatus.InvalidSignature,
        HttpStatusCode.BadRequest,
        "\"SSH signature verification failed.\""
    )]
    [InlineData(KeyManagementStatus.NotFound, HttpStatusCode.NotFound, "")]
    [InlineData(KeyManagementStatus.InvalidRequest, HttpStatusCode.BadRequest, "")]
    public async Task DeleteKeyMapsStatusToResponseAsync(
        KeyManagementStatus status,
        HttpStatusCode expectedStatusCode,
        string expectedBody
    )
    {
        MockKeyManagementServiceAsyncResult(keyManagementService: this._keyManagementService, result: status);

        EndpointResponse response = await this.SendAsync(
            method: HttpMethod.Delete,
            path: KEY_PATH,
            jsonBody: DELETE_KEY_BODY
        );

        Assert.Equal(expected: expectedStatusCode, actual: response.StatusCode);
        Assert.Equal(expected: expectedBody, actual: response.Body);
        await this
            ._keyManagementService.Received(1)
            .DeleteKeyAsync(
                host: HOST,
                user: USER,
                keyId: KeyId,
                challenge: "c",
                signature: "s",
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UnhandledExceptionReturnsInternalServerErrorAsync()
    {
        this._keyManagementService.ReturnsForAll(static _ =>
            ValueTask.FromException<KeyListResult>(new InvalidOperationException("Boom"))
        );

        EndpointResponse response = await this.SendAsync(method: HttpMethod.Get, path: KEYS_PATH);

        Assert.Equal(expected: HttpStatusCode.InternalServerError, actual: response.StatusCode);
        Assert.Equal(expected: "application/json", actual: response.MediaType);
        Assert.Equal(expected: """{"error":"Internal Server Error"}""", actual: response.Body);
    }

    private static void MockKeyManagementServiceAsyncResult<TResult>(
        IKeyManagementService keyManagementService,
        TResult result
    )
    {
        keyManagementService.ReturnsForAll(_ => ValueTask.FromResult(result));
    }

    private static KeyChallengeResult CreateChallengeResult()
    {
        return KeyChallengeResult.Success(
            new(Token: CHALLENGE_TOKEN, Namespace: SSH_NAMESPACE, ValidUntil: DateTimeOffset.UnixEpoch)
        );
    }

    private static string ExpectedChallengeJson()
    {
        return "{\"challenge\":\""
            + CHALLENGE_TOKEN
            + "\",\"namespace\":\""
            + SSH_NAMESPACE
            + "\",\"validUntil\":\"1970-01-01T00:00:00+00:00\"}";
    }

    private async Task<EndpointResponse> SendAsync(HttpMethod method, string path, string? jsonBody = null)
    {
        CancellationToken cancellationToken = this.CancellationToken();

        await using WebApplication app = this.CreateApp();
        await app.StartAsync(cancellationToken);

        try
        {
            using HttpClient client = new() { BaseAddress = new(app.Urls.First()) };
            using HttpRequestMessage request = new(method: method, requestUri: path);

            if (jsonBody is not null)
            {
                request.Content = new StringContent(
                    content: jsonBody,
                    encoding: Encoding.UTF8,
                    mediaType: "application/json"
                );
            }

            using HttpResponseMessage response = await client.SendAsync(
                request: request,
                cancellationToken: cancellationToken
            );

            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new(
                StatusCode: response.StatusCode,
                MediaType: response.Content.Headers.ContentType?.MediaType,
                Body: body
            );
        }
        finally
        {
            await app.StopAsync(cancellationToken);
        }
    }

    private WebApplication CreateApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(this._keyManagementService).AddAppJsonSerialization();

        WebApplication app = builder.Build();

        return app.UseUnhandledExceptionLogging().ConfigureEndpoints();
    }

    [DebuggerDisplay("{StatusCode}: {Body}")]
    private readonly record struct EndpointResponse(HttpStatusCode StatusCode, string? MediaType, string Body);
}
