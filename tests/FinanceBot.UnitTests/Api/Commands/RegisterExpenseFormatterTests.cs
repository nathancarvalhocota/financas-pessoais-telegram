using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Billing;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class RegisterExpenseFormatterTests
{
    private static readonly ExpenseItem Expense = new(42, 130, "Compra", Categoria.Mercado, new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Format_WithoutLimitOrAlert_PreservesConfirmation()
    {
        Assert.Equal("Compra registrada: R$ 130,00 - Mercado",
            RegisterExpenseFormatter.Format(new RegisterExpenseResult.Success(Expense, null, null)));
    }

    [Theory]
    [InlineData(130, 100, false, "Mercado: R$ 130,00 de R$ 130,00 (100%)")]
    [InlineData(100, 130, true, "⚠ Limite de Mercado ultrapassado: R$ 130,00 de R$ 100,00")]
    public void Format_WithProgress_PreservesEqualityAndExceedingMessages(double limit, int percentage, bool exceeded, string expected)
    {
        var progress = new CategoryLimitProgress(Categoria.Mercado, limit, 130, percentage, exceeded);

        Assert.Equal("Compra registrada: R$ 130,00 - Mercado\n" + expected,
            RegisterExpenseFormatter.Format(new RegisterExpenseResult.Success(Expense, progress, null)));
    }

    [Fact]
    public void Format_Errors_PreserveMessagesAndCategoryOrder()
    {
        Assert.Equal("Uso correto: /compra <VALOR>, <DESCRICAO>, <CATEGORIA>. Exemplo: /compra 58,90, Almoco, Mercado",
            RegisterExpenseFormatter.Format(new RegisterExpenseResult.Failure(new RegisterExpenseError.InvalidSyntax())));
        Assert.Equal("Valor invalido. Use um numero positivo, exemplo: 58,90",
            RegisterExpenseFormatter.Format(new RegisterExpenseResult.Failure(new RegisterExpenseError.InvalidAmount())));
        Assert.Equal("Descricao obrigatoria no comando /compra.",
            RegisterExpenseFormatter.Format(new RegisterExpenseResult.Failure(new RegisterExpenseError.MissingDescription())));
        Assert.Equal("Categoria 'Desconhecida' inexistente.\n\nCategorias disponiveis:\nEducacao\nLazer\nLanches\nUber\nMercado\nMoto\nCompras\nOutros\nEstetica\nLimpeza\nSaude",
            RegisterExpenseFormatter.Format(new RegisterExpenseResult.Failure(new RegisterExpenseError.UnknownCategory("Desconhecida"))));
    }

    [Fact]
    public void Format_Alert_PreservesCompleteMessageOrderAndCategoryComparisonStates()
    {
        var categories = CategoryCatalog.GetAll().Select(category => category switch
        {
            Categoria.Mercado => new CategorySpendingComparison(category, 130, 60, new SpendingComparison.Comparable(217, 70)),
            Categoria.Uber => new CategorySpendingComparison(category, 0, 40, new SpendingComparison.Comparable(0, -40)),
            Categoria.Lazer => new CategorySpendingComparison(category, 10, 0, new SpendingComparison.NoHistory()),
            _ => new CategorySpendingComparison(category, 0, 0, new SpendingComparison.NoSpending())
        }).ToList();
        var alert = new SpendingPaceAlert(SpendingAlertLevel.Warning,
            new BillingCycle(new DateOnly(2026, 4, 15), new DateOnly(2026, 5, 14)),
            new BillingCycle(new DateOnly(2026, 3, 15), new DateOnly(2026, 4, 14)),
            140, 100, 140, 40, categories);
        var progress = new CategoryLimitProgress(Categoria.Mercado, 100, 130, 130, true);
        string expectedAlert = string.Join(Environment.NewLine, new[]
        {
            "⚠️ ALERTA AMARELO: gasto do ciclo acima do anterior",
            "",
            "Ciclo atual (15/04/2026 a 14/05/2026): R$ 140,00",
            "Ciclo anterior (15/03/2026 a 14/04/2026): R$ 100,00",
            "Gasto atual: 140% do ciclo anterior (+R$ 40,00).",
            "",
            "Categorias:",
            "• Educacao: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)",
            "• Lazer: R$ 10,00 vs R$ 0,00 (sem histórico comparável)",
            "• Lanches: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)",
            "• Uber: R$ 0,00 vs R$ 40,00 (0%, -R$ 40,00)",
            "• Mercado: R$ 130,00 vs R$ 60,00 (217%, +R$ 70,00)",
            "• Moto: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)",
            "• Compras: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)",
            "• Outros: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)",
            "• Estetica: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)",
            "• Limpeza: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)",
            "• Saude: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)"
        });

        Assert.Equal("Compra registrada: R$ 130,00 - Mercado\n⚠ Limite de Mercado ultrapassado: R$ 130,00 de R$ 100,00\n\n" + expectedAlert,
            RegisterExpenseFormatter.Format(new RegisterExpenseResult.Success(Expense, progress, alert)));
    }
}
