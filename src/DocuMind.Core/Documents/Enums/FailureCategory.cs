namespace DocuMind.Core.Documents;

public enum FailureCategory
{
    RetryableDependency = 0,
    PermanentInput = 1,
    PermanentInvariant = 2,
    PersistenceFailure = 3,
    Cancelled = 4
}
