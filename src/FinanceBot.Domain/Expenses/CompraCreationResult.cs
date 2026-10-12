namespace FinanceBot.Domain.Expenses;

public enum CompraError
{
    InvalidAmount,
    MissingDescription,
    InvalidCategory
}

public abstract record CompraCreationResult
{
    private CompraCreationResult() { }

    public sealed record Success(Compra Compra) : CompraCreationResult;
    public sealed record Failure(CompraError Error) : CompraCreationResult;
}
