using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocuMind.Infrastructure.Embeddings;

public sealed class OpenAiEmbeddingClient : IEmbeddingClient
{
    private const int DefaultMaxRetries = 3;
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiEmbeddingClient> _logger;
    private readonly OpenAiOptions _options;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maxRetries;

    public OpenAiEmbeddingClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiEmbeddingClient> logger)
        : this(httpClient, options, logger, DefaultRequestTimeout, DefaultMaxRetries)
    {
    }

    internal OpenAiEmbeddingClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiEmbeddingClient> logger,
        TimeSpan requestTimeout,
        int maxRetries)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _requestTimeout = requestTimeout > TimeSpan.Zero
            ? requestTimeout
            : throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        _maxRetries = maxRetries >= 0
            ? maxRetries
            : throw new ArgumentOutOfRangeException(nameof(maxRetries));
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await GenerateEmbeddingsAsync([text], cancellationToken);
        return embeddings[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ValidateTexts(texts);

        var payload = new EmbeddingRequest(_options.EmbeddingModel, texts);
        var attempt = 0;

        while (true)
        {
            attempt++;
            using var request = new HttpRequestMessage(HttpMethod.Post, "embeddings")
            {
                Content = JsonContent.Create(payload, options: SerializerOptions)
            };
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_requestTimeout);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token);

                if (ShouldRetry(response.StatusCode) && attempt <= _maxRetries)
                {
                    await LogAndDelayRetryAsync(
                        attempt,
                        texts.Count,
                        $"status {(int)response.StatusCode}",
                        stopwatch.Elapsed,
                        cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw await CreateExceptionAsync(response, stopwatch.Elapsed, cancellationToken);
                }

                EmbeddingResponse? body;
                try
                {
                    body = await response.Content.ReadFromJsonAsync<EmbeddingResponse>(
                        SerializerOptions,
                        timeoutCts.Token);
                }
                catch (JsonException exception)
                {
                    throw new EmbeddingClientException(
                        "OpenAI embeddings response returned invalid JSON.",
                        isTransient: false,
                        exception);
                }

                var embeddings = ValidateResponse(body, texts.Count);

                _logger.LogInformation(
                    "Generated embeddings using model {Model} for {ItemCount} item(s) on attempt {Attempt} in {DurationMs}ms.",
                    _options.EmbeddingModel,
                    texts.Count,
                    attempt,
                    stopwatch.ElapsedMilliseconds);

                return embeddings;
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
            {
                if (attempt <= _maxRetries)
                {
                    await LogAndDelayRetryAsync(
                        attempt,
                        texts.Count,
                        "timeout",
                        stopwatch.Elapsed,
                        cancellationToken);
                    continue;
                }

                throw new EmbeddingClientException(
                    $"OpenAI embeddings request timed out after {_requestTimeout.TotalSeconds:0.###} seconds.",
                    isTransient: true,
                    exception);
            }
            catch (HttpRequestException exception)
            {
                if (attempt <= _maxRetries)
                {
                    await LogAndDelayRetryAsync(
                        attempt,
                        texts.Count,
                        "transport failure",
                        stopwatch.Elapsed,
                        cancellationToken);
                    continue;
                }

                throw new EmbeddingClientException(
                    "OpenAI embeddings request failed due to a transport error.",
                    isTransient: true,
                    exception);
            }
        }
    }

    private async Task<EmbeddingClientException> CreateExceptionAsync(
        HttpResponseMessage response,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        var isTransient = ShouldRetry(response.StatusCode);
        var details = await ReadErrorDetailsAsync(response, cancellationToken);

        _logger.LogWarning(
            "OpenAI embeddings request failed with status {StatusCode} after {DurationMs}ms. Transient={IsTransient}.",
            (int)response.StatusCode,
            duration.TotalMilliseconds,
            isTransient);

        var suffix = string.IsNullOrWhiteSpace(details)
            ? string.Empty
            : $" Details: {details}";

        return new EmbeddingClientException(
            $"OpenAI embeddings request failed with status {(int)response.StatusCode}.{suffix}",
            isTransient);
    }

    private static IReadOnlyList<float[]> ValidateResponse(EmbeddingResponse? body, int expectedCount)
    {
        if (body?.Data is null || body.Data.Count != expectedCount)
        {
            throw new EmbeddingClientException(
                "OpenAI embeddings response did not contain the expected number of items.",
                isTransient: false);
        }

        var ordered = body.Data
            .OrderBy(item => item.Index)
            .ToArray();

        if (ordered.Select(item => item.Index).Distinct().Count() != expectedCount)
        {
            throw new EmbeddingClientException(
                "OpenAI embeddings response contained duplicated or missing item indexes.",
                isTransient: false);
        }

        var embeddings = new float[expectedCount][];
        foreach (var item in ordered)
        {
            if (item.Index < 0 || item.Index >= expectedCount)
            {
                throw new EmbeddingClientException(
                    "OpenAI embeddings response contained an out-of-range item index.",
                    isTransient: false);
            }

            if (item.Embedding is null || item.Embedding.Length != EmbeddingConstants.ExpectedDimensions)
            {
                throw new EmbeddingClientException(
                    $"OpenAI embeddings response returned an invalid vector dimension. Expected {EmbeddingConstants.ExpectedDimensions}.",
                    isTransient: false);
            }

            embeddings[item.Index] = item.Embedding;
        }

        return embeddings;
    }

    private async Task LogAndDelayRetryAsync(
        int attempt,
        int itemCount,
        string reason,
        TimeSpan elapsed,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Retrying OpenAI embeddings request for {ItemCount} item(s). Attempt {Attempt} failed due to {Reason} after {DurationMs}ms.",
            itemCount,
            attempt,
            reason,
            elapsed.TotalMilliseconds);

        await Task.Delay(GetRetryDelay(attempt), cancellationToken);
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        var jitterMs = Random.Shared.Next(0, 100);
        return TimeSpan.FromMilliseconds((200 * attempt * attempt) + jitterMs);
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == (HttpStatusCode)429
            || (int)statusCode >= 500;
    }

    private static void ValidateTexts(IReadOnlyList<string> texts)
    {
        if (texts is null || texts.Count == 0)
        {
            throw new ArgumentException("At least one text is required.", nameof(texts));
        }

        for (var index = 0; index < texts.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(texts[index]))
            {
                throw new ArgumentException(
                    $"Text at index {index} is required.",
                    nameof(texts));
            }
        }
    }

    private static async Task<string?> ReadErrorDetailsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var body = await JsonSerializer.DeserializeAsync<OpenAiErrorEnvelope>(
                stream,
                SerializerOptions,
                cancellationToken);

            return body?.Error?.Message;
        }
        catch
        {
            return null;
        }
    }

    private sealed record EmbeddingRequest(string Model, IReadOnlyList<string> Input);

    private sealed record EmbeddingResponse(IReadOnlyList<EmbeddingItem>? Data);

    private sealed record EmbeddingItem(int Index, float[]? Embedding);

    private sealed record OpenAiErrorEnvelope(OpenAiError? Error);

    private sealed record OpenAiError(string? Message);
}
