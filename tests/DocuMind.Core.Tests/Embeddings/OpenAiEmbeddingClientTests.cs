using System.Net;
using System.Text;
using System.Text.Json;
using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Configuration;
using DocuMind.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DocuMind.Core.Tests.Embeddings;

public sealed class OpenAiEmbeddingClientTests
{
    [Fact]
    public async Task GenerateEmbeddingsAsync_ShouldReturnEmbeddingsInInputOrderForBatch()
    {
        var handler = new StubHttpMessageHandler(
        [
            (_, _) => Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                $$"""
                {
                  "data": [
                    { "index": 1, "embedding": {{CreateEmbeddingJson(2)}} },
                    { "index": 0, "embedding": {{CreateEmbeddingJson(1)}} }
                  ]
                }
                """))
        ]);
        var client = CreateClient(handler);

        var result = await client.GenerateEmbeddingsAsync(["alpha", "beta"]);

        Assert.Equal(2, result.Count);
        Assert.Equal(EmbeddingConstants.ExpectedDimensions, result[0].Length);
        Assert.Equal(EmbeddingConstants.ExpectedDimensions, result[1].Length);
        Assert.Equal(1f, result[0][0]);
        Assert.Equal(2f, result[1][0]);
        Assert.Equal(1, handler.CallCount);

        using var body = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("text-embedding-3-small", body.RootElement.GetProperty("model").GetString());
        var input = body.RootElement.GetProperty("input").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Equal(["alpha", "beta"], input);
        Assert.Equal("Bearer test-key", handler.Requests[0].Authorization);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData((HttpStatusCode)429)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GenerateEmbeddingsAsync_ShouldRetryTransientStatusCodes(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler(
        [
            (_, _) => Task.FromResult(CreateJsonResponse(statusCode, """{ "error": { "message": "temporary" } }""")),
            (_, _) => Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                $$"""
                {
                  "data": [
                    { "index": 0, "embedding": {{CreateEmbeddingJson(7)}} }
                  ]
                }
                """))
        ]);
        var client = CreateClient(handler);

        var result = await client.GenerateEmbeddingAsync("retry me");

        Assert.Equal(EmbeddingConstants.ExpectedDimensions, result.Length);
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GenerateEmbeddingsAsync_ShouldNotRetryPermanentStatusCodes(HttpStatusCode statusCode)
    {
        var handler = new StubHttpMessageHandler(
        [
            (_, _) => Task.FromResult(CreateJsonResponse(statusCode, """{ "error": { "message": "permanent" } }"""))
        ]);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<EmbeddingClientException>(() =>
            client.GenerateEmbeddingAsync("do not retry"));

        Assert.False(exception.IsTransient);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains(((int)statusCode).ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ShouldRetryTransportFailure()
    {
        var handler = new StubHttpMessageHandler(
        [
            (_, _) => throw new HttpRequestException("network down"),
            (_, _) => Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                $$"""
                {
                  "data": [
                    { "index": 0, "embedding": {{CreateEmbeddingJson(3)}} }
                  ]
                }
                """))
        ]);
        var client = CreateClient(handler);

        var result = await client.GenerateEmbeddingAsync("transport");

        Assert.Equal(EmbeddingConstants.ExpectedDimensions, result.Length);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ShouldRetryTimeoutFailure()
    {
        var handler = new StubHttpMessageHandler(
        [
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            },
            (_, _) => Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                $$"""
                {
                  "data": [
                    { "index": 0, "embedding": {{CreateEmbeddingJson(4)}} }
                  ]
                }
                """))
        ]);
        var client = CreateClient(
            handler,
            requestTimeout: TimeSpan.FromMilliseconds(20),
            maxRetries: 1);

        var result = await client.GenerateEmbeddingAsync("timeout");

        Assert.Equal(EmbeddingConstants.ExpectedDimensions, result.Length);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ShouldRejectInvalidDimensionWithoutRetry()
    {
        var handler = new StubHttpMessageHandler(
        [
            (_, _) => Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                """
                {
                  "data": [
                    { "index": 0, "embedding": [1.0, 2.0] }
                  ]
                }
                """))
        ]);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<EmbeddingClientException>(() =>
            client.GenerateEmbeddingAsync("bad dimension"));

        Assert.False(exception.IsTransient);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("1536", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ShouldWrapInvalidJsonAsPermanentFailure()
    {
        var handler = new StubHttpMessageHandler(
        [
            (_, _) => Task.FromResult(CreateJsonResponse(
                HttpStatusCode.OK,
                """{ "data": [ { "index": 0, "embedding": "invalid" } ] }"""))
        ]);
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<EmbeddingClientException>(() =>
            client.GenerateEmbeddingAsync("bad json"));

        Assert.False(exception.IsTransient);
        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ShouldFailAfterRetryExhaustionForTransientStatus()
    {
        var handler = new StubHttpMessageHandler(
        [
            (_, _) => Task.FromResult(CreateJsonResponse(
                HttpStatusCode.InternalServerError,
                """{ "error": { "message": "temporary" } }""")),
            (_, _) => Task.FromResult(CreateJsonResponse(
                HttpStatusCode.InternalServerError,
                """{ "error": { "message": "still temporary" } }"""))
        ]);
        var client = CreateClient(handler, maxRetries: 1);

        var exception = await Assert.ThrowsAsync<EmbeddingClientException>(() =>
            client.GenerateEmbeddingAsync("give up"));

        Assert.True(exception.IsTransient);
        Assert.Equal(2, handler.CallCount);
        Assert.Contains("500", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ShouldFailAfterRetryExhaustionForTimeout()
    {
        var handler = new StubHttpMessageHandler(
        [
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            },
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            }
        ]);
        var client = CreateClient(
            handler,
            requestTimeout: TimeSpan.FromMilliseconds(20),
            maxRetries: 1);

        var exception = await Assert.ThrowsAsync<EmbeddingClientException>(() =>
            client.GenerateEmbeddingAsync("still timeout"));

        Assert.True(exception.IsTransient);
        Assert.Equal(2, handler.CallCount);
        Assert.Contains("timed out", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ShouldRejectEmptyInput()
    {
        var handler = new StubHttpMessageHandler([]);
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GenerateEmbeddingsAsync([]));
    }

    private static OpenAiEmbeddingClient CreateClient(
        HttpMessageHandler handler,
        TimeSpan? requestTimeout = null,
        int maxRetries = 3)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/"),
            Timeout = Timeout.InfiniteTimeSpan
        };

        return new OpenAiEmbeddingClient(
            httpClient,
            Options.Create(new OpenAiOptions
            {
                Endpoint = "https://api.openai.com/v1/",
                ApiKey = "test-key",
                ChatModel = "gpt-4.1-mini",
                EmbeddingModel = "text-embedding-3-small"
            }),
            NullLogger<OpenAiEmbeddingClient>.Instance,
            requestTimeout ?? TimeSpan.FromSeconds(20),
            maxRetries);
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static string CreateEmbeddingJson(int seed)
    {
        var values = string.Join(
            ", ",
            Enumerable.Range(0, EmbeddingConstants.ExpectedDimensions)
                .Select(index => (seed + index).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)));

        return $"[{values}]";
    }

    private sealed class StubHttpMessageHandler(
        IReadOnlyList<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responses)
        : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses =
            new(responses);

        public int CallCount { get; private set; }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;

            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new CapturedRequest(
                request.Method.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization?.ToString(),
                body));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No stubbed response was configured.");
            }

            return await _responses.Dequeue()(request, cancellationToken);
        }
    }

    private sealed record CapturedRequest(
        string Method,
        string Url,
        string? Authorization,
        string Body);
}
