using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.CategoryLimits;

public sealed record CategoryLimitProgress(
    Categoria Categoria,
    double Limite,
    double TotalGasto,
    int Percentual,
    bool Excedido);
