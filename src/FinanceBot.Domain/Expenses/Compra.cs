using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.Expenses;

public sealed class Compra
{
    private Compra() { }

    private Compra(double valor, string descricao, Categoria categoria, DateTime data)
    {
        Valor = valor;
        Descricao = descricao;
        Categoria = categoria;
        Data = data;
    }

    public int Id { get; private set; }
    public double Valor { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public Categoria Categoria { get; private set; }
    public DateTime Data { get; private set; }

    public static CompraCreationResult Criar(double valor, string descricao, Categoria categoria, DateTime data)
    {
        if (!double.IsFinite(valor) || valor <= 0)
            return new CompraCreationResult.Failure(CompraError.InvalidAmount);
        if (string.IsNullOrWhiteSpace(descricao))
            return new CompraCreationResult.Failure(CompraError.MissingDescription);
        if (!Enum.IsDefined(categoria))
            return new CompraCreationResult.Failure(CompraError.InvalidCategory);

        return new CompraCreationResult.Success(new Compra(valor, descricao, categoria, data));
    }
}
