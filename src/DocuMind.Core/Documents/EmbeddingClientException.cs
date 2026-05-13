namespace DocuMind.Core.Documents;

public class EmbeddingClientException : Exception
{
    public EmbeddingClientException(string message, bool isTransient)
        : base(message)
    {
        IsTransient = isTransient;
    }

    public EmbeddingClientException(string message, bool isTransient, Exception innerException)
        : base(message, innerException)
    {
        IsTransient = isTransient;
    }

    public bool IsTransient { get; }
}
