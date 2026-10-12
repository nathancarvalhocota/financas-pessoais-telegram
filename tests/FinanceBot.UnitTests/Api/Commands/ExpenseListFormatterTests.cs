using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class ExpenseListFormatterTests
{
    private static readonly ExpensePeriod Cycle = new(ExpensePeriodKind.BillingCycle, new DateOnly(2026, 4, 15), new DateOnly(2026, 5, 14));

    [Theory]
    [InlineData(CategoryFilterMode.None, "")]
    [InlineData(CategoryFilterMode.Include, " (Mercado, Uber, Mercado)")]
    [InlineData(CategoryFilterMode.Exclude, " (sem Mercado, Uber, Mercado)")]
    public void Format_Items_PreservesCompleteMessageOrderDuplicatesAndUtcInstants(CategoryFilterMode mode, string suffix)
    {
        var result = new ListExpensesResult.Success(Cycle, Filter(mode),
            [new ExpenseItem(9, 1234.56, "Último dia", Category.Mercado, new DateTime(2026, 5, 15, 2, 59, 0, DateTimeKind.Utc)),
             new ExpenseItem(2, 10, "Primeiro dia", Category.Uber, new DateTime(2026, 4, 15, 3, 0, 0, DateTimeKind.Utc))], 1244.56);
        string newline = Environment.NewLine;

        string actual = ExpenseListFormatter.Format(result);

        Assert.Equal($"Compras de 15/04 a 14/05{suffix}:{newline}" +
            $"{newline}[9] Mercado — R$ 1.234,56{newline}    Último dia · 15/05 02:59{newline}" +
            $"{newline}[2] Uber — R$ 10,00{newline}    Primeiro dia · 15/04 03:00{newline}" +
            "\nTotal: R$ 1.244,56", actual);
    }

    [Fact]
    public void Format_PreparedResult_UsesFinalItemsAndTotal()
    {
        var result = new ListExpensesResult.Success(Cycle,
            new CategoryFilter(CategoryFilterMode.Exclude, [Category.Mercado]),
            [new ExpenseItem(1, 10, "Item final", Category.Mercado, new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc))], 55.25);

        string actual = ExpenseListFormatter.Format(result);

        Assert.Contains("[1] Mercado — R$ 10,00", actual);
        Assert.EndsWith("Total: R$ 55,25", actual);
    }

    [Theory]
    [InlineData(ExpensePeriodKind.BillingCycle, CategoryFilterMode.None, "Nenhuma compra encontrada de 15/04 a 14/05.")]
    [InlineData(ExpensePeriodKind.BillingCycle, CategoryFilterMode.Include, "Nenhuma compra de Mercado, Uber, Mercado de 15/04 a 14/05.")]
    [InlineData(ExpensePeriodKind.BillingCycle, CategoryFilterMode.Exclude, "Nenhuma compra de 15/04 a 14/05 (sem Mercado, Uber, Mercado).")]
    [InlineData(ExpensePeriodKind.DateRange, CategoryFilterMode.None, "Nenhuma compra encontrada de 15/04/2026 a 14/05/2026.")]
    [InlineData(ExpensePeriodKind.DateRange, CategoryFilterMode.Include, "Nenhuma compra de Mercado, Uber, Mercado de 15/04/2026 a 14/05/2026.")]
    [InlineData(ExpensePeriodKind.DateRange, CategoryFilterMode.Exclude, "Nenhuma compra de 15/04/2026 a 14/05/2026 (sem Mercado, Uber, Mercado).")]
    public void Format_EmptySuccess_PreservesPeriodAndFilterMessages(ExpensePeriodKind kind, CategoryFilterMode mode, string expected)
    {
        Assert.Equal(expected, ExpenseListFormatter.Format(new ListExpensesResult.Success(Cycle with { Kind = kind }, Filter(mode), [], 0)));
    }

    [Fact]
    public void Format_ExplicitRange_ShowsInclusiveDatesWithYears()
    {
        var result = new ListExpensesResult.Success(
            new ExpensePeriod(ExpensePeriodKind.DateRange, new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 1)), Filter(CategoryFilterMode.None),
            [new ExpenseItem(1, 10, "Virada", Category.Uber, new DateTime(2026, 1, 2, 2, 0, 0, DateTimeKind.Utc))], 10);

        string actual = ExpenseListFormatter.Format(result);

        Assert.StartsWith("Compras de 31/12/2025 a 01/01/2026:", actual);
        Assert.Contains("Virada · 02/01 02:00", actual);
    }

    [Fact]
    public void Format_Errors_PreservesExactMessagesAndSharedCategoryPresentation()
    {
        (ListExpensesError Error, string Message)[] cases =
        [
            (new ListExpensesError.InvalidSyntax(), "Uso correto: /listar <MM/YY>. Exemplo: /listar 09/26"),
            (new ListExpensesError.InvalidDate(), "Data invalida. Use DD/MM/AA ou DD/MM/AAAA. Exemplo: /listar 10/02/26 - 20/02/2026"),
            (new ListExpensesError.InvalidMonth(), "Mes/ano invalido. Use o formato MM/YY, exemplo: 09/26"),
            (new ListExpensesError.InvertedPeriod(), "Data inicial nao pode ser posterior a data final."),
            (new ListExpensesError.MissingExcludedCategories(), "Informe ao menos uma categoria após 'sem'. Exemplo: /listar sem Mercado"),
            (new ListExpensesError.UnknownCategory("teste"), "Categoria 'teste' inexistente.\n\nCategorias disponiveis:\nEducacao\nLazer\nLanches\nUber\nMercado\nMoto\nCompras\nOutros\nEstetica\nLimpeza\nSaude")
        ];

        foreach (var (error, message) in cases)
            Assert.Equal(message, ExpenseListFormatter.Format(new ListExpensesResult.Failure(error)));
    }

    private static CategoryFilter Filter(CategoryFilterMode mode) => new(mode,
        mode == CategoryFilterMode.None ? [] : [Category.Mercado, Category.Uber, Category.Mercado]);
}
