namespace DocuMind.Core.Documents;

public sealed record TextExtractionResult(
    bool IsSuccess,
    string? Text,
    TextExtractionFailureCode? FailureCode,
    string? FailureReason)
{
    public static TextExtractionResult Success(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new TextExtractionResult(true, text, null, null);
    }

    public static TextExtractionResult Failure(
        TextExtractionFailureCode failureCode,
        string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        return new TextExtractionResult(false, null, failureCode, failureReason.Trim());
    }
}
