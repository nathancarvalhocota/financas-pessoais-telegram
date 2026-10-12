using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class DeleteExpenseFormatterTests
{
    [Fact]
    public void Format_Success_PreservesCompleteConfirmation()
    {
        var expense = new ExpenseItem(42, 1234.56, "Uber para trabalho", Categoria.Uber,
            new DateTime(2026, 1, 20, 10, 30, 0, DateTimeKind.Utc));

        Assert.Equal("Compra removida: R$ 1.234,56 - Uber para trabalho (Uber)",
            DeleteExpenseFormatter.Format(new DeleteExpenseResult.Success(expense)));
    }

    [Fact]
    public void Format_NotFound_PreservesMessage()
    {
        Assert.Equal("ID nao encontrado. Nada foi removido.",
            DeleteExpenseFormatter.Format(new DeleteExpenseResult.NotFound(42)));
    }

    [Fact]
    public void Format_InvalidSyntax_PreservesUsage()
    {
        Assert.Equal("Uso correto: /deletar <ID>. Exemplo: /deletar 42",
            DeleteExpenseFormatter.Format(new DeleteExpenseResult.Failure(new DeleteExpenseError.InvalidSyntax())));
    }

    [Fact]
    public void Format_InvalidId_PreservesMessage()
    {
        Assert.Equal("ID invalido para o comando /deletar.",
            DeleteExpenseFormatter.Format(new DeleteExpenseResult.Failure(new DeleteExpenseError.InvalidId())));
    }
}
