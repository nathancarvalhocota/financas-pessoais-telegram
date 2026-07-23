using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.CategoryLimits;

public sealed class LimiteCategoria
{
    public int Id { get; set; }

    public Categoria Categoria { get; set; }

    public double Valor { get; set; }

    public bool IsExceededBy(double spending)
    {
        return spending > Valor;
    }

    public int CalculateUsagePercentage(double spending)
    {
        return (int)Math.Round(spending / Valor * 100);
    }
}
