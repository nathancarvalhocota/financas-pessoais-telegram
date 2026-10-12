using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.CategoryLimits;

public sealed class LimiteCategoria
{
    private LimiteCategoria() { }

    private LimiteCategoria(Categoria categoria, double valor)
    {
        Categoria = categoria;
        Valor = valor;
    }

    public int Id { get; private set; }

    public Categoria Categoria { get; private set; }

    public double Valor { get; private set; }

    public static LimiteCategoriaCreationResult Criar(Categoria categoria, double valor)
    {
        if (!Enum.IsDefined(categoria))
        {
            return new LimiteCategoriaCreationResult.Failure(LimiteCategoriaError.InvalidCategory);
        }

        if (!IsValidValue(valor))
        {
            return new LimiteCategoriaCreationResult.Failure(LimiteCategoriaError.InvalidValue);
        }

        return new LimiteCategoriaCreationResult.Success(new LimiteCategoria(categoria, valor));
    }

    public LimiteCategoriaError? AlterarValor(double valor)
    {
        if (!Enum.IsDefined(Categoria))
        {
            return LimiteCategoriaError.InvalidCategory;
        }

        if (!IsValidValue(valor))
        {
            return LimiteCategoriaError.InvalidValue;
        }

        Valor = valor;
        return null;
    }

    public CategoryLimitProgress AvaliarProgresso(double totalGasto) => new(
        Categoria,
        Valor,
        totalGasto,
        (int)Math.Round(totalGasto / Valor * 100),
        totalGasto > Valor);

    private static bool IsValidValue(double valor) => double.IsFinite(valor) && valor > 0;
}
