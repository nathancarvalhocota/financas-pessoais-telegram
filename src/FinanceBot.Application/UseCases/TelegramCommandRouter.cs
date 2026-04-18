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

    private static readonly Regex LimiteCommandRegex = new Regex(
        "^/limite(?:@[A-Za-z0-9_]+)?\\s+(\\S+)\\s+([0-9]+(?:[.,][0-9]{1,2})?)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LimiteConsultaCommandRegex = new Regex(
        "^/limite(?:@[A-Za-z0-9_]+)?\\s+(\\S+)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, Categoria> CategoriasPorTextoNormalizado =
        new Dictionary<string, Categoria>(StringComparer.Ordinal)
        {
            ["educacao"] = Categoria.Educacao,
            ["lazer"] = Categoria.Lazer,
            ["lanches"] = Categoria.Lanches,
            ["uber"] = Categoria.Uber,
            ["mercado"] = Categoria.Mercado,
            ["moto"] = Categoria.Moto,
            ["compras"] = Categoria.Compras,
            ["outros"] = Categoria.Outros,
            ["estetica"] = Categoria.Estetica,
            ["limpeza"] = Categoria.Limpeza,
            ["saude"] = Categoria.Saude
        };

    private const string InfoText =
        "FinanceBot - Comandos disponíveis:\n\n" +
        "/compra <VALOR>, <DESCRICAO>, <CATEGORIA>\n" +
        "Registra uma despesa. Valor aceita virgula ou ponto.\n" +
        "Ex: /compra 58,90, Almoço, Mercado\n\n" +
        "/listar <MM/AA>\n" +
        "Lista as compras do mês com ID, data, valor e categoria.\n" +
        "Omita o mês para listar o mês atual.\n" +
        "Ex: /listar 04/26\n\n" +
        "/deletar <ID>\n" +
        "Remove uma compra pelo ID exibido no /listar.\n" +
        "Ex: /deletar 42\n\n" +
        "/analise <MM/AA>\n" +
        "Exibe total gasto, breakdown por categoria e comparação com mês anterior.\n" +
        "Ex: /analise 04/26\n\n" +
        "/limite <CATEGORIA> <VALOR>\n" +
        "Define um limite mensal de gastos para a categoria.\n" +
        "Use valor 0 para remover o limite.\n" +
        "Ex: /limite Mercado 800\n" +
        "Ex: /limite Mercado 0  (remove o limite)\n\n" +
        "/limite <CATEGORIA>\n" +
        "Consulta o limite definido e o gasto atual do mês.\n" +
        "Ex: /limite Mercado\n\n" +
        "Categorias: Educacao, Lazer, Lanches, Uber, Mercado, Moto, Compras, Outros, Estetica, Limpeza, Saude";

    private readonly ICompraRepository _compraRepository;
    private readonly ILimiteCategoriaRepository _limiteCategoriaRepository;

    public TelegramCommandRouter(
        ICompraRepository compraRepository,
        ILimiteCategoriaRepository limiteCategoriaRepository)
    {
        _compraRepository = compraRepository;
        _limiteCategoriaRepository = limiteCategoriaRepository;
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

        if (normalizedMessageText.StartsWith("/limite", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleLimiteAsync(normalizedMessageText, cancellationToken);
        }

        if (normalizedMessageText.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            return "FinanceBot online. Use /info para ver todos os comandos.";
        }

        if (normalizedMessageText.StartsWith("/info", StringComparison.OrdinalIgnoreCase))
        {
            return InfoText;
        }

        return "Comando nao reconhecido. Use /info para ver os comandos disponíveis.";
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
        string baseResponse = $"Compra registrada: R$ {formattedValor} - {categoriaDisplayName}";

        LimiteCategoria? limite = await _limiteCategoriaRepository
            .GetByCategoriaAsync(categoria, cancellationToken);

        if (limite is null)
        {
            return baseResponse;
        }

        DateTime periodStartUtc = new DateTime(
            compraDateUtc.Year,
            compraDateUtc.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);
        DateTime periodEndUtc = periodStartUtc.AddMonths(1);

        IReadOnlyList<Compra> comprasDoMes = await _compraRepository.ListByPeriodAsync(
            periodStartUtc,
            periodEndUtc,
            cancellationToken);

        double gastoAcumulado = 0;
        foreach (Compra c in comprasDoMes)
        {
            if (c.Categoria == categoria)
            {
                gastoAcumulado += c.Valor;
            }
        }

        string formattedGasto = gastoAcumulado.ToString("N2", PtBrCulture);
        string formattedLimite = limite.Valor.ToString("N2", PtBrCulture);

        if (gastoAcumulado > limite.Valor)
        {
            return $"{baseResponse}\n⚠ Limite de {categoriaDisplayName} ultrapassado: R$ {formattedGasto} de R$ {formattedLimite}";
        }

        int percentual = (int)Math.Round(gastoAcumulado / limite.Valor * 100);
        return $"{baseResponse}\n{categoriaDisplayName}: R$ {formattedGasto} de R$ {formattedLimite} ({percentual}%)";
    }

    private async Task<string> HandleLimiteAsync(
        string messageText,
        CancellationToken cancellationToken)
    {
        Match consultaMatch = LimiteConsultaCommandRegex.Match(messageText);
        if (consultaMatch.Success && !LimiteCommandRegex.IsMatch(messageText))
        {
            return await HandleLimiteConsultaAsync(consultaMatch, cancellationToken);
        }

        Match commandMatch = LimiteCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return "Uso correto: /limite <CATEGORIA> <VALOR>. Exemplo: /limite Mercado 800\n" +
                "Para consultar: /limite <CATEGORIA>. Exemplo: /limite Mercado";
        }

        string categoriaText = commandMatch.Groups[1].Value.Trim();
        string valorText = commandMatch.Groups[2].Value;

        bool categoriaReconhecida = TryParseCategoria(categoriaText, out Categoria categoria);
        if (!categoriaReconhecida)
        {
            return $"Categoria '{categoriaText}' inexistente.\n\nCategorias disponiveis:\n" +
                BuildCategoriasValidas();
        }

        string normalizedValorText = valorText.Replace(',', '.');
        bool parsedValor = double.TryParse(
            normalizedValorText,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out double valor);

        if (!parsedValor || valor < 0)
        {
            return "Valor invalido. Use um numero positivo ou 0 para remover o limite.";
        }

        string categoriaDisplayName = GetCategoriaDisplayName(categoria);

        if (valor == 0)
        {
            LimiteCategoria? existing = await _limiteCategoriaRepository
                .GetByCategoriaAsync(categoria, cancellationToken);

            if (existing is not null)
            {
                await _limiteCategoriaRepository.DeleteAsync(existing, cancellationToken);
            }

            return $"Limite de {categoriaDisplayName} removido";
        }

        LimiteCategoria limiteCategoria = new LimiteCategoria
        {
            Categoria = categoria,
            Valor = valor
        };

        await _limiteCategoriaRepository.UpsertAsync(limiteCategoria, cancellationToken);

        string formattedValor = valor.ToString("N2", PtBrCulture);
        return $"Limite de {categoriaDisplayName} definido: R$ {formattedValor}";
    }

    private async Task<string> HandleLimiteConsultaAsync(
        Match consultaMatch,
        CancellationToken cancellationToken)
    {
        string categoriaText = consultaMatch.Groups[1].Value.Trim();
        bool categoriaReconhecida = TryParseCategoria(categoriaText, out Categoria categoria);
        if (!categoriaReconhecida)
        {
            return $"Categoria '{categoriaText}' inexistente.\n\nCategorias disponiveis:\n" +
                BuildCategoriasValidas();
        }

        string categoriaDisplayName = GetCategoriaDisplayName(categoria);
        LimiteCategoria? limite = await _limiteCategoriaRepository
            .GetByCategoriaAsync(categoria, cancellationToken);

        if (limite is null)
        {
            return $"Nenhum limite definido para {categoriaDisplayName}.";
        }

        DateTime now = DateTime.UtcNow;
        DateTime periodStartUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEndUtc = periodStartUtc.AddMonths(1);

        IReadOnlyList<Compra> comprasDoMes = await _compraRepository.ListByPeriodAsync(
            periodStartUtc, periodEndUtc, cancellationToken);

        double totalGasto = comprasDoMes
            .Where(c => c.Categoria == categoria)
            .Sum(c => c.Valor);

        double percentual = totalGasto / limite.Valor * 100;
        string formattedGasto = totalGasto.ToString("N2", PtBrCulture);
        string formattedLimite = limite.Valor.ToString("N2", PtBrCulture);
        string formattedPercentual = percentual.ToString("0", PtBrCulture);

        return $"{categoriaDisplayName}: R$ {formattedGasto} de R$ {formattedLimite} ({formattedPercentual}%)";
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
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"[{compra.Id}] {categoriaDisplayName} — R$ {formattedValue}");
            messageBuilder.AppendLine($"    {compra.Descricao} · {compra.Data:dd/MM HH:mm}");
        }

        string formattedTotalMonthValue = totalMonthValue.ToString("N2", PtBrCulture);
        messageBuilder.Append($"\nTotal: R$ {formattedTotalMonthValue}");
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
            categoriasValidas.Add(GetCategoriaDisplayName(categoria));
        
        return string.Join("\n", categoriasValidas);
    }

    private static string GetCategoriaDisplayName(Categoria categoria)
    {
        return categoria switch
        {
            Categoria.Educacao => "Educacao",
            Categoria.Lazer => "Lazer",
            Categoria.Lanches => "Lanches",
            Categoria.Uber => "Uber",
            Categoria.Mercado => "Mercado",
            Categoria.Moto => "Moto",
            Categoria.Compras => "Compras",
            Categoria.Outros => "Outros",
            Categoria.Estetica => "Estetica",
            Categoria.Limpeza => "Limpeza",
            Categoria.Saude => "Saude",
            _ => categoria.ToString()
        };
    }
}
