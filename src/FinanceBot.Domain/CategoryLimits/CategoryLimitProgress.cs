using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.CategoryLimits;

public sealed record CategoryLimitProgress(
    Category Category,
    double Limit,
    double TotalSpent,
    int Percentage,
    bool IsExceeded);
