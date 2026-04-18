using FinanceBot.Domain.Enums;

namespace FinanceBot.Domain.Entities;

public sealed class LimiteCategoria
{
    public int Id { get; set; }

    public Categoria Categoria { get; set; }

    public double Valor { get; set; }
}
