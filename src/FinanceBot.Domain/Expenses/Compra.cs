using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.Expenses;

public sealed class Compra
{
    public int Id { get; set; }

    public double Valor { get; set; }

    public string Descricao { get; set; } = string.Empty;

    public Categoria Categoria { get; set; }

    public DateTime Data { get; set; }
}
