namespace FinanceBot.Domain.CategoryLimits;

public enum LimiteCategoriaError
{
    InvalidCategory,
    InvalidValue
}

public abstract record LimiteCategoriaCreationResult
{
    private LimiteCategoriaCreationResult() { }

    public sealed record Success(LimiteCategoria Limite) : LimiteCategoriaCreationResult;
    public sealed record Failure(LimiteCategoriaError Error) : LimiteCategoriaCreationResult;
}
