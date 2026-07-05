using DocuMind.Core.Documents;

namespace DocuMind.Infrastructure.Documents.Ingestion;

public sealed class IngestionFailurePolicy
{
    private const int MaxFailureReasonLength = 2048;

    public IngestionFailure ClassifyExtractionFailure(TextExtractionResult extractionResult)
    {
        ArgumentNullException.ThrowIfNull(extractionResult);

        if (extractionResult.IsSuccess)
        {
            throw new ArgumentException("Extraction result must represent a failure.", nameof(extractionResult));
        }

        return new IngestionFailure(
            FailureCategory.PermanentInput,
            Normalize(extractionResult.FailureReason ?? "Text extraction failed."));
    }

    public IngestionFailure ClassifyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            EmbeddingClientException embeddingClientException when embeddingClientException.IsTransient =>
                new IngestionFailure(
                    FailureCategory.RetryableDependency,
                    Normalize($"Embedding generation failed: {embeddingClientException.Message}")),
            EmbeddingClientException embeddingClientException =>
                new IngestionFailure(
                    FailureCategory.PermanentInvariant,
                    Normalize($"Embedding generation failed: {embeddingClientException.Message}")),
            InvalidOperationException invalidOperationException when IsPersistenceFailure(invalidOperationException) =>
                new IngestionFailure(
                    FailureCategory.PersistenceFailure,
                    Normalize($"Document ingestion persistence failed: {invalidOperationException.Message}")),
            _ => new IngestionFailure(
                FailureCategory.PermanentInvariant,
                Normalize($"Document ingestion failed: {exception.Message}"))
        };
    }

    public IngestionFailure PersistenceFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new IngestionFailure(
            FailureCategory.PersistenceFailure,
            Normalize($"Document ingestion persistence failed: {exception.Message}"));
    }

    private static bool IsPersistenceFailure(InvalidOperationException exception)
    {
        return exception.Message.Contains("update", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("persistence", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("database", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "Document ingestion failed."
            : reason.Trim();

        return normalizedReason.Length <= MaxFailureReasonLength
            ? normalizedReason
            : normalizedReason[..MaxFailureReasonLength];
    }
}

public sealed record IngestionFailure(
    FailureCategory Category,
    string Reason);
