using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FinanceBot.Application.Abstractions;
using FinanceBot.Domain.Entities;
using FinanceBot.Domain.Enums;

namespace FinanceBot.Application.UseCases;

public sealed class TelegramCommandRouter : ITelegramCommandRouter
{
    private static readonly CultureInfo PtBrCulture = new CultureInfo("pt-BR");

    private static readonly Regex CompraCommandRegex = new Regex(
        "^/compra(?:@[A-Za-z0-9_]+)?\\s+([0-9]+(?:[.,][0-9]{1,2})?)\\s*,\\s*(.+?)\\s*,\\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ListarCommandRegex = new Regex(
        "^/listar(?:\\s+(.+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DeletarCommandRegex = new Regex(
        "^/deletar\\s+(\\d+)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, Categoria> CategoriasPorTextoNormalizado =
        new Dictionary<string, Categoria>(StringComparer.Ordinal)
        {
            ["educacao"] = Categoria.Educacao,
            ["lazerfesta"] = Categoria.LazerFesta,
            ["restaurantelanche"] = Categoria.RestauranteLanche,
            ["uber"] = Categoria.Uber,
            ["mercado"] = Categoria.Mercado,
            ["moto"] = Categoria.Moto,
            ["compras"] = Categoria.Compras,
            ["outros"] = Categoria.Outros,
            ["estetica"] = Categoria.Estetica,
            ["limpeza"] = Categoria.Limpeza,
            ["saudeefarmacia"] = Categoria.SaudeEFarmacia
        };

    private readonly ICompraRepository _compraRepository;

    public TelegramCommandRouter(ICompraRepository compraRepository)
    {
        _compraRepository = compraRepository;
    }

    public async Task<string> RouteAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        string normalizedMessageText = messageText.Trim();
        if (normalizedMessageText.StartsWith("/compra", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleCompraAsync(normalizedMessageText, messageDateUtc, cancellationToken);
        }

        if (normalizedMessageText.StartsWith("/listar", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleListarAsync(normalizedMessageText, messageDateUtc, cancellationToken);
        }

        if (normalizedMessageText.StartsWith("/deletar", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleDeletarAsync(normalizedMessageText, cancellationToken);
        }

        if (normalizedMessageText.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            return "FinanceBot online. Use /compra, /listar ou /deletar.";
        }

        return "Comando nao reconhecido. Use /compra, /listar ou /deletar.";
    }

    private async Task<string> HandleCompraAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        Match commandMatch = CompraCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return "Uso correto: /compra <VALOR>, <DESCRICAO>, <CATEGORIA>. " +
                "Exemplo: /compra 58,90, Almoco, Mercado";
        }

        string valorText = commandMatch.Groups[1].Value;
        string descricaoText = commandMatch.Groups[2].Value.Trim();
        string categoriaText = commandMatch.Groups[3].Value.Trim();

        string normalizedValorText = valorText.Replace(',', '.');
        bool parsedValor = double.TryParse(
            normalizedValorText,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out double valor);

        if (!parsedValor || valor <= 0)
        {
            return "Valor invalido. Use um numero positivo, exemplo: 58,90";
        }

        if (string.IsNullOrWhiteSpace(descricaoText))
        {
            return "Descricao obrigatoria no comando /compra.";
        }

        bool categoriaReconhecida = TryParseCategoria(categoriaText, out Categoria categoria);
        if (!categoriaReconhecida)
        {
            return $"Categoria '{categoriaText}' inexistente.\n\nCategorias disponiveis:\n" +
                BuildCategoriasValidas();
        }

        DateTime compraDateUtc = messageDateUtc.UtcDateTime;
        Compra compra = new Compra
        {
            Valor = valor,
            Descricao = descricaoText,
            Categoria = categoria,
            Data = compraDateUtc
        };

        await _compraRepository.AddAsync(compra, cancellationToken);

        string formattedValor = valor.ToString("N2", PtBrCulture);
        string categoriaDisplayName = GetCategoriaDisplayName(categoria);
        return $"Compra registrada: R$ {formattedValor} - {categoriaDisplayName}";
    }

    private async Task<string> HandleListarAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        Match commandMatch = ListarCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return "Uso correto: /listar <MM/YY>. Exemplo: /listar 09/26";
        }

        string monthYearText = commandMatch.Groups[1].Value.Trim();
        DateTime referenceDate = messageDateUtc.UtcDateTime;
        if (!string.IsNullOrWhiteSpace(monthYearText))
        {
            bool parsedMonthYear = TryParseMonthYear(monthYearText, out DateTime parsedReferenceDate);
            if (!parsedMonthYear)
            {
                return "Mes/ano invalido. Use o formato MM/YY, exemplo: 09/26";
            }

            referenceDate = parsedReferenceDate;
        }

        DateTime periodStartUtc = new DateTime(
            referenceDate.Year,
            referenceDate.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
        DateTime periodEndUtc = periodStartUtc.AddMonths(1);

        IReadOnlyList<Compra> compras = await _compraRepository.ListByPeriodAsync(
            periodStartUtc,
            periodEndUtc,
            cancellationToken);

        if (compras.Count == 0)
        {
            return $"Nenhuma compra encontrada para {periodStartUtc:MM/yy}.";
        }

        StringBuilder messageBuilder = new StringBuilder();
        messageBuilder.AppendLine($"Compras de {periodStartUtc:MM/yy}:");

        double totalMonthValue = 0;
        foreach (Compra compra in compras)
        {
            totalMonthValue += compra.Valor;
            string formattedValue = compra.Valor.ToString("N2", PtBrCulture);
            string categoriaDisplayName = GetCategoriaDisplayName(compra.Categoria);
            messageBuilder.AppendLine(
                $"{compra.Id} | {compra.Data:dd/MM/yyyy HH:mm} | R$ {formattedValue} | " +
                $"{compra.Descricao} | {categoriaDisplayName}");
        }

        string formattedTotalMonthValue = totalMonthValue.ToString("N2", PtBrCulture);
        messageBuilder.Append($"Total: R$ {formattedTotalMonthValue}");
        return messageBuilder.ToString();
    }

    private async Task<string> HandleDeletarAsync(
        string messageText,
        CancellationToken cancellationToken)
    {
        Match commandMatch = DeletarCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return "Uso correto: /deletar <ID>. Exemplo: /deletar 42";
        }

        bool parsedId = int.TryParse(commandMatch.Groups[1].Value, out int compraId);
        if (!parsedId || compraId <= 0)
        {
            return "ID invalido para o comando /deletar.";
        }

        Compra? compra = await _compraRepository.FindByIdAsync(compraId, cancellationToken);
        if (compra is null)
        {
            return "ID nao encontrado. Nada foi removido.";
        }

        await _compraRepository.DeleteAsync(compra, cancellationToken);
        string formattedValue = compra.Valor.ToString("N2", PtBrCulture);
        string categoriaDisplayName = GetCategoriaDisplayName(compra.Categoria);
        return $"Compra removida: R$ {formattedValue} - {compra.Descricao} ({categoriaDisplayName})";
    }

    private static bool TryParseMonthYear(string monthYearText, out DateTime referenceDateUtc)
    {
        referenceDateUtc = default;
        Match monthYearMatch = Regex.Match(
            monthYearText,
            "^(0[1-9]|1[0-2])/(\\d{2})$",
            RegexOptions.CultureInvariant);

        if (!monthYearMatch.Success)
        {
            return false;
        }

        int month = int.Parse(monthYearMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        int year = 2000 + int.Parse(monthYearMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        referenceDateUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        return true;
    }

    private static bool TryParseCategoria(string categoriaText, out Categoria categoria)
    {
        string normalizedCategoryText = NormalizeText(categoriaText);
        return CategoriasPorTextoNormalizado.TryGetValue(normalizedCategoryText, out categoria);
    }

    private static string NormalizeText(string text)
    {
        string normalizedUnicodeText = text.Normalize(NormalizationForm.FormD);
        StringBuilder normalizedBuilder = new StringBuilder();

        foreach (char character in normalizedUnicodeText)
        {
            UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                normalizedBuilder.Append(char.ToLowerInvariant(character));
            }
        }

        return normalizedBuilder.ToString();
    }

    private static string BuildCategoriasValidas()
    {
        Categoria[] categorias = Enum.GetValues<Categoria>();
        List<string> categoriasValidas = new List<string>(categorias.Length);
        foreach (Categoria categoria in categorias)
        {
            categoriasValidas.Add(GetCategoriaDisplayName(categoria));
        }

        return string.Join("\n", categoriasValidas);
    }

    private static string GetCategoriaDisplayName(Categoria categoria)
    {
        return categoria switch
        {
            Categoria.Educacao => "Educacao",
            Categoria.LazerFesta => "Lazer / Festa",
            Categoria.RestauranteLanche => "Restaurante / Lanche",
            Categoria.Uber => "Uber",
            Categoria.Mercado => "Mercado",
            Categoria.Moto => "Moto",
            Categoria.Compras => "Compras",
            Categoria.Outros => "Outros",
            Categoria.Estetica => "Estetica",
            Categoria.Limpeza => "Limpeza",
            Categoria.SaudeEFarmacia => "Saude e Farmacia",
            _ => categoria.ToString()
        };
    }
}
