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
    private static readonly TimeZoneInfo SaoPauloTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static readonly Regex CompraCommandRegex = new Regex(
        "^/compra(?:@[A-Za-z0-9_]+)?\\s+([0-9]+(?:[.,][0-9]{1,2})?)\\s*,\\s*(.+?)\\s*,\\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ListarCommandRegex = new Regex(
        "^/listar(?:\\s+(.+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex DateRangeRegex = new Regex(
        "^(?<start>\\d{2}/\\d{2}/(?:\\d{2}|\\d{4}))\\s*-\\s*(?<end>\\d{2}/\\d{2}/(?:\\d{2}|\\d{4}))(?:\\s+(?<filter>.+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DeletarCommandRegex = new Regex(
        "^/deletar\\s+(\\d+)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LimiteCommandRegex = new Regex(
        "^/limite(?:@[A-Za-z0-9_]+)?\\s+(\\S+)\\s+([0-9]+(?:[.,][0-9]{1,2})?)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LimiteConsultaCommandRegex = new Regex(
        "^/limite(?:@[A-Za-z0-9_]+)?\\s+(\\S+)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex MonthYearShapeRegex = new Regex(
        "^\\d{2}/\\d{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
        "/listar <MM/AA> [<CATEGORIAS>] | [sem <CATEGORIAS>]\n" +
        "Lista as compras do mês com ID, data, valor e categoria.\n" +
        "Omita o mês para listar o mês atual.\n" +
        "Filtre por categoria, ou use 'sem' para excluir categorias.\n" +
        "Ex: /listar 04/26\n" +
        "Ex: /listar 04/26 Mercado, Uber\n" +
        "Ex: /listar 04/26 sem Lazer\n" +
        "Ex: /listar 10/02/26 - 20/02/2026\n" +
        "Ex: /listar 10/02/26 - 20/02/2026 Mercado, Uber\n\n" +
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

    private enum ListarFiltroModo
    {
        Nenhum,
        Inclusao,
        Exclusao
    }

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
            return await HandlePurchaseAsync(normalizedMessageText, messageDateUtc, cancellationToken);
        }

        if (normalizedMessageText.StartsWith("/listar", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleListAsync(normalizedMessageText, messageDateUtc, cancellationToken);
        }

        if (normalizedMessageText.StartsWith("/deletar", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleDeleteAsync(normalizedMessageText, cancellationToken);
        }

        if (normalizedMessageText.StartsWith("/limite", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleLimitAsync(normalizedMessageText, cancellationToken);
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

    private async Task<string> HandlePurchaseAsync(
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

        bool categoriaReconhecida = TryParseCategory(categoriaText, out Categoria categoria);
        if (!categoriaReconhecida)
        {
            return $"Categoria '{categoriaText}' inexistente.\n\nCategorias disponiveis:\n" +
                BuildValidCategories();
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
        string categoriaDisplayName = GetCategoryDisplayName(categoria);
        string baseResponse = $"Compra registrada: R$ {formattedValor} - {categoriaDisplayName}";

        LimiteCategoria? limite = await _limiteCategoriaRepository
            .GetByCategoryAsync(categoria, cancellationToken);

        if (limite is null)
        {
            return await AppendPaceAlertAsync(baseResponse, compra, cancellationToken);
        }

        var (periodStartUtc, periodEndUtc) = GetBillingPeriod(GetBusinessDateUtc(compraDateUtc));

        IReadOnlyList<Compra> comprasDoMes = await _compraRepository.ListByPeriodAsync(
            periodStartUtc,
            periodEndUtc.AddDays(1),
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
            return await AppendPaceAlertAsync(
                $"{baseResponse}\n⚠ Limite de {categoriaDisplayName} ultrapassado: R$ {formattedGasto} de R$ {formattedLimite}",
                compra,
                cancellationToken);
        }

        int percentual = (int)Math.Round(gastoAcumulado / limite.Valor * 100);
        return await AppendPaceAlertAsync(
            $"{baseResponse}\n{categoriaDisplayName}: R$ {formattedGasto} de R$ {formattedLimite} ({percentual}%)",
            compra,
            cancellationToken);
    }

    private async Task<string> AppendPaceAlertAsync(
        string response,
        Compra compra,
        CancellationToken cancellationToken)
    {
        var (currentStartUtc, currentEndUtc) = GetBillingPeriod(GetBusinessDateUtc(compra.Data));
        DateTime previousStartUtc = currentStartUtc.AddMonths(-1);
        DateTime previousEndUtc = currentEndUtc.AddMonths(-1);

        IReadOnlyList<Compra> currentPurchases = await _compraRepository.ListByPeriodAsync(
            currentStartUtc, currentEndUtc.AddDays(1), cancellationToken);
        IReadOnlyList<Compra> previousPurchases = await _compraRepository.ListByPeriodAsync(
            previousStartUtc, previousEndUtc.AddDays(1), cancellationToken);

        double currentTotal = currentPurchases.Sum(item => item.Valor);
        double previousTotal = previousPurchases.Sum(item => item.Valor);
        if (previousTotal <= 0)
        {
            return response;
        }

        double totalBeforePurchase = Math.Max(0, currentTotal - compra.Valor);
        bool crossedMegaUrgent = totalBeforePurchase < previousTotal * 2 && currentTotal >= previousTotal * 2;
        bool crossedDanger = totalBeforePurchase < previousTotal * 1.5 && currentTotal >= previousTotal * 1.5;
        bool crossedWarning = totalBeforePurchase < previousTotal * 1.25 && currentTotal >= previousTotal * 1.25;
        if (!crossedMegaUrgent && !crossedDanger && !crossedWarning)
        {
            return response;
        }

        StringBuilder alert = new StringBuilder();
        alert.AppendLine(crossedMegaUrgent
            ? "🚨 ALERTA URGENTE: gasto do ciclo o dobro do anterior"
            : crossedDanger
                ? "🚨 ALERTA VERMELHO: gasto do ciclo muito acima do anterior"
                : "⚠️ ALERTA AMARELO: gasto do ciclo acima do anterior");
        alert.AppendLine();
        alert.AppendLine($"Ciclo atual ({currentStartUtc:dd/MM/yyyy} a {currentEndUtc:dd/MM/yyyy}): R$ {currentTotal.ToString("N2", PtBrCulture)}");
        alert.AppendLine($"Ciclo anterior ({previousStartUtc:dd/MM/yyyy} a {previousEndUtc:dd/MM/yyyy}): R$ {previousTotal.ToString("N2", PtBrCulture)}");
        alert.AppendLine($"Gasto atual: {(int)Math.Round(currentTotal / previousTotal * 100)}% do ciclo anterior ({FormatSignedCurrency(currentTotal - previousTotal)}).");
        alert.AppendLine();
        alert.AppendLine("Categorias:");

        foreach (Categoria categoria in Enum.GetValues<Categoria>())
        {
            double currentCategoryTotal = currentPurchases.Where(item => item.Categoria == categoria).Sum(item => item.Valor);
            double previousCategoryTotal = previousPurchases.Where(item => item.Categoria == categoria).Sum(item => item.Valor);
            alert.Append($"• {GetCategoryDisplayName(categoria)}: R$ {currentCategoryTotal.ToString("N2", PtBrCulture)} vs R$ {previousCategoryTotal.ToString("N2", PtBrCulture)} ");

            if (previousCategoryTotal <= 0 && currentCategoryTotal <= 0)
                alert.Append("(sem gastos nos dois períodos)");
            else if (previousCategoryTotal <= 0)
                alert.Append("(sem histórico comparável)");
            else
                alert.Append($"({(int)Math.Round(currentCategoryTotal / previousCategoryTotal * 100)}%, {FormatSignedCurrency(currentCategoryTotal - previousCategoryTotal)})");

            alert.AppendLine();
        }

        return $"{response}\n\n{alert.ToString().TrimEnd()}";
    }

    private static string FormatSignedCurrency(double value)
    {
        string signal = value >= 0 ? "+" : "-";
        return $"{signal}R$ {Math.Abs(value).ToString("N2", PtBrCulture)}";
    }

    private async Task<string> HandleLimitAsync(
        string messageText,
        CancellationToken cancellationToken)
    {
        Match consultaMatch = LimiteConsultaCommandRegex.Match(messageText);
        if (consultaMatch.Success && !LimiteCommandRegex.IsMatch(messageText))
        {
            return await HandleLimitQueryAsync(consultaMatch, cancellationToken);
        }

        Match commandMatch = LimiteCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return "Uso correto: /limite <CATEGORIA> <VALOR>. Exemplo: /limite Mercado 800\n" +
                "Para consultar: /limite <CATEGORIA>. Exemplo: /limite Mercado";
        }

        string categoriaText = commandMatch.Groups[1].Value.Trim();
        string valorText = commandMatch.Groups[2].Value;

        bool categoriaReconhecida = TryParseCategory(categoriaText, out Categoria categoria);
        if (!categoriaReconhecida)
        {
            return $"Categoria '{categoriaText}' inexistente.\n\nCategorias disponiveis:\n" +
                BuildValidCategories();
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

        string categoriaDisplayName = GetCategoryDisplayName(categoria);

        if (valor == 0)
        {
            LimiteCategoria? existing = await _limiteCategoriaRepository
                .GetByCategoryAsync(categoria, cancellationToken);

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

    private async Task<string> HandleLimitQueryAsync(
        Match consultaMatch,
        CancellationToken cancellationToken)
    {
        string categoriaText = consultaMatch.Groups[1].Value.Trim();
        bool categoriaReconhecida = TryParseCategory(categoriaText, out Categoria categoria);
        if (!categoriaReconhecida)
        {
            return $"Categoria '{categoriaText}' inexistente.\n\nCategorias disponiveis:\n" +
                BuildValidCategories();
        }

        string categoriaDisplayName = GetCategoryDisplayName(categoria);
        LimiteCategoria? limite = await _limiteCategoriaRepository
            .GetByCategoryAsync(categoria, cancellationToken);

        if (limite is null)
        {
            return $"Nenhum limite definido para {categoriaDisplayName}.";
        }

        var (periodStartUtc, periodEndUtc) = GetBillingPeriod(GetBusinessDateUtc(DateTime.UtcNow));

        IReadOnlyList<Compra> comprasDoMes = await _compraRepository.ListByPeriodAsync(
            periodStartUtc, periodEndUtc.AddDays(1), cancellationToken);

        double totalGasto = comprasDoMes
            .Where(c => c.Categoria == categoria)
            .Sum(c => c.Valor);

        double percentual = totalGasto / limite.Valor * 100;
        string formattedGasto = totalGasto.ToString("N2", PtBrCulture);
        string formattedLimite = limite.Valor.ToString("N2", PtBrCulture);
        string formattedPercentual = percentual.ToString("0", PtBrCulture);

        return $"{categoriaDisplayName}: R$ {formattedGasto} de R$ {formattedLimite} ({formattedPercentual}%)";
    }

    private async Task<string> HandleListAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        Match commandMatch = ListarCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return "Uso correto: /listar <MM/YY>. Exemplo: /listar 09/26";
        }

        string argsText = commandMatch.Groups[1].Value.Trim();
        Match dateRangeMatch = DateRangeRegex.Match(argsText);
        if (dateRangeMatch.Success)
        {
            if (!TryParseDate(dateRangeMatch.Groups["start"].Value, out DateTime rangeStartUtc) ||
                !TryParseDate(dateRangeMatch.Groups["end"].Value, out DateTime rangeEndUtc))
            {
                return "Data invalida. Use DD/MM/AA ou DD/MM/AAAA. Exemplo: /listar 10/02/26 - 20/02/2026";
            }

            if (rangeStartUtc > rangeEndUtc)
            {
                return "Data inicial nao pode ser posterior a data final.";
            }

            return await HandleDateRangeListAsync(
                rangeStartUtc,
                rangeEndUtc,
                dateRangeMatch.Groups["filter"].Value.Trim(),
                cancellationToken);
        }

        if (Regex.IsMatch(argsText, "^\\d{2}/\\d{2}/", RegexOptions.CultureInvariant))
        {
            return "Data invalida. Use DD/MM/AA ou DD/MM/AAAA. Exemplo: /listar 10/02/26 - 20/02/2026";
        }

        if (!TryParseListArgs(
            argsText,
            messageDateUtc,
            out DateTime referenceDate,
            out ListarFiltroModo filtroModo,
            out List<Categoria> categoriasFiltro,
            out string parseError))
        {
            return parseError;
        }

        var (periodStartUtc, periodEndUtc) = GetBillingPeriod(referenceDate);

        IReadOnlyList<Compra> compras = await _compraRepository.ListByPeriodAsync(
            periodStartUtc,
            periodEndUtc.AddDays(1),
            cancellationToken);

        IReadOnlyList<Compra> comprasFiltradas = ApplyFilter(compras, filtroModo, categoriasFiltro);

        if (comprasFiltradas.Count == 0)
        {
            return BuildEmptyListMessage(filtroModo, categoriasFiltro, periodStartUtc, periodEndUtc);
        }

        string headerSuffix = filtroModo == ListarFiltroModo.Nenhum
            ? string.Empty
            : $" ({BuildFilterSuffix(filtroModo, categoriasFiltro)})";

        StringBuilder messageBuilder = new StringBuilder();
        messageBuilder.AppendLine($"Compras de {periodStartUtc:dd/MM} a {periodEndUtc:dd/MM}{headerSuffix}:");

        double totalMonthValue = 0;
        foreach (Compra compra in comprasFiltradas)
        {
            totalMonthValue += compra.Valor;
            string formattedValue = compra.Valor.ToString("N2", PtBrCulture);
            string categoriaDisplayName = GetCategoryDisplayName(compra.Categoria);
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"[{compra.Id}] {categoriaDisplayName} — R$ {formattedValue}");
            messageBuilder.AppendLine($"    {compra.Descricao} · {compra.Data:dd/MM HH:mm}");
        }

        string formattedTotalMonthValue = totalMonthValue.ToString("N2", PtBrCulture);
        messageBuilder.Append($"\nTotal: R$ {formattedTotalMonthValue}");
        return messageBuilder.ToString();
    }

    private async Task<string> HandleDateRangeListAsync(
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        string rangeFilterText,
        CancellationToken cancellationToken)
    {
        ListarFiltroModo filtroModo = ListarFiltroModo.Nenhum;
        List<Categoria> categoriasFiltro = new List<Categoria>();

        if (rangeFilterText.Length > 0 &&
            !TryParseCategoryFilter(rangeFilterText, out filtroModo, out categoriasFiltro, out string parseError))
        {
            return parseError;
        }

        IReadOnlyList<Compra> compras = await _compraRepository.ListByPeriodAsync(
            rangeStartUtc,
            rangeEndUtc.AddDays(1),
            cancellationToken);
        IReadOnlyList<Compra> comprasFiltradas = ApplyFilter(compras, filtroModo, categoriasFiltro);
        string periodRange = $"{rangeStartUtc:dd/MM/yyyy} a {rangeEndUtc:dd/MM/yyyy}";

        if (comprasFiltradas.Count == 0)
        {
            if (filtroModo == ListarFiltroModo.Inclusao)
                return $"Nenhuma compra de {BuildFilterSuffix(filtroModo, categoriasFiltro)} de {periodRange}.";
            if (filtroModo == ListarFiltroModo.Exclusao)
                return $"Nenhuma compra de {periodRange} ({BuildFilterSuffix(filtroModo, categoriasFiltro)}).";

            return $"Nenhuma compra encontrada de {periodRange}.";
        }

        string headerSuffix = filtroModo == ListarFiltroModo.Nenhum
            ? string.Empty
            : $" ({BuildFilterSuffix(filtroModo, categoriasFiltro)})";
        StringBuilder messageBuilder = new StringBuilder();
        messageBuilder.AppendLine($"Compras de {periodRange}{headerSuffix}:");

        double totalValue = 0;
        foreach (Compra compra in comprasFiltradas)
        {
            totalValue += compra.Valor;
            string formattedValue = compra.Valor.ToString("N2", PtBrCulture);
            string categoriaDisplayName = GetCategoryDisplayName(compra.Categoria);
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"[{compra.Id}] {categoriaDisplayName} — R$ {formattedValue}");
            messageBuilder.AppendLine($"    {compra.Descricao} · {compra.Data:dd/MM HH:mm}");
        }

        messageBuilder.Append($"\nTotal: R$ {totalValue.ToString("N2", PtBrCulture)}");
        return messageBuilder.ToString();
    }

    private static bool TryParseListArgs(
        string argsText,
        DateTimeOffset messageDateUtc,
        out DateTime referenceDateUtc,
        out ListarFiltroModo filtroModo,
        out List<Categoria> categoriasFiltro,
        out string errorMessage)
    {
        referenceDateUtc = GetBusinessDateUtc(messageDateUtc.UtcDateTime);
        filtroModo = ListarFiltroModo.Nenhum;
        categoriasFiltro = new List<Categoria>();
        errorMessage = string.Empty;

        string filterText = argsText;
        int firstSpaceIndex = argsText.IndexOf(' ');
        string firstToken = firstSpaceIndex < 0 ? argsText : argsText.Substring(0, firstSpaceIndex);

        if (MonthYearShapeRegex.IsMatch(firstToken))
        {
            if (!TryParseMonthYear(firstToken, out referenceDateUtc))
            {
                errorMessage = "Mes/ano invalido. Use o formato MM/YY, exemplo: 09/26";
                return false;
            }

            filterText = firstSpaceIndex < 0 ? string.Empty : argsText.Substring(firstSpaceIndex + 1).Trim();
        }

        if (filterText.Length == 0)
        {
            return true;
        }

        return TryParseCategoryFilter(filterText, out filtroModo, out categoriasFiltro, out errorMessage);
    }

    private static bool TryParseCategoryFilter(
        string filterText,
        out ListarFiltroModo filtroModo,
        out List<Categoria> categoriasFiltro,
        out string errorMessage)
    {
        filtroModo = ListarFiltroModo.Inclusao;
        categoriasFiltro = new List<Categoria>();
        errorMessage = string.Empty;

        string categoriasText = filterText;
        int firstSpaceIndex = filterText.IndexOf(' ');
        string firstToken = firstSpaceIndex < 0 ? filterText : filterText.Substring(0, firstSpaceIndex);

        if (NormalizeText(firstToken) == "sem")
        {
            filtroModo = ListarFiltroModo.Exclusao;
            categoriasText = firstSpaceIndex < 0 ? string.Empty : filterText.Substring(firstSpaceIndex + 1).Trim();
            if (categoriasText.Length == 0)
            {
                errorMessage = "Informe ao menos uma categoria após 'sem'. Exemplo: /listar sem Mercado";
                return false;
            }
        }

        return TryParseCategoryList(categoriasText, out categoriasFiltro, out errorMessage);
    }

    private static bool TryParseCategoryList(
        string categoriasText,
        out List<Categoria> categoriasFiltro,
        out string errorMessage)
    {
        categoriasFiltro = new List<Categoria>();
        errorMessage = string.Empty;

        foreach (string rawCategoria in categoriasText.Split(','))
        {
            string categoriaText = rawCategoria.Trim();
            if (!TryParseCategory(categoriaText, out Categoria categoria))
            {
                errorMessage = $"Categoria '{categoriaText}' inexistente.\n\nCategorias disponiveis:\n" +
                    BuildValidCategories();
                return false;
            }

            categoriasFiltro.Add(categoria);
        }

        return true;
    }

    private static IReadOnlyList<Compra> ApplyFilter(
        IReadOnlyList<Compra> compras,
        ListarFiltroModo filtroModo,
        List<Categoria> categoriasFiltro)
    {
        if (filtroModo == ListarFiltroModo.Nenhum)
        {
            return compras;
        }

        HashSet<Categoria> categoriaSet = new HashSet<Categoria>(categoriasFiltro);
        bool manterQuandoPresente = filtroModo == ListarFiltroModo.Inclusao;
        return compras
            .Where(compra => categoriaSet.Contains(compra.Categoria) == manterQuandoPresente)
            .ToList();
    }

    private static string BuildFilterSuffix(
        ListarFiltroModo filtroModo,
        List<Categoria> categoriasFiltro)
    {
        string nomes = string.Join(", ", categoriasFiltro.Select(GetCategoryDisplayName));
        return filtroModo == ListarFiltroModo.Exclusao ? $"sem {nomes}" : nomes;
    }

    private static string BuildEmptyListMessage(
        ListarFiltroModo filtroModo,
        List<Categoria> categoriasFiltro,
        DateTime periodStartUtc,
        DateTime periodEndUtc)
    {
        string periodRange = $"{periodStartUtc:dd/MM} a {periodEndUtc:dd/MM}";

        if (filtroModo == ListarFiltroModo.Inclusao)
        {
            return $"Nenhuma compra de {BuildFilterSuffix(filtroModo, categoriasFiltro)} de {periodRange}.";
        }

        if (filtroModo == ListarFiltroModo.Exclusao)
        {
            return $"Nenhuma compra de {periodRange} ({BuildFilterSuffix(filtroModo, categoriasFiltro)}).";
        }

        return $"Nenhuma compra encontrada de {periodRange}.";
    }

    private async Task<string> HandleDeleteAsync(
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
        string categoriaDisplayName = GetCategoryDisplayName(compra.Categoria);
        return $"Compra removida: R$ {formattedValue} - {compra.Descricao} ({categoriaDisplayName})";
    }

    private static bool TryParseDate(string dateText, out DateTime dateUtc)
    {
        dateUtc = default;
        Match match = Regex.Match(
            dateText,
            "^(\\d{2})/(\\d{2})/(\\d{2}|\\d{4})$",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return false;
        }

        int year = match.Groups[3].Value.Length == 2
            ? 2000 + int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
            : int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

        try
        {
            DateTime localDate = new DateTime(
                year,
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                0, 0, 0,
                DateTimeKind.Unspecified);
            dateUtc = TimeZoneInfo.ConvertTimeToUtc(localDate, SaoPauloTimeZone);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
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
        referenceDateUtc = new DateTime(year, month, 15, 0, 0, 0, DateTimeKind.Utc);
        return true;
    }

    private static bool TryParseCategory(string categoriaText, out Categoria categoria)
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

    private static string BuildValidCategories()
    {
        Categoria[] categorias = Enum.GetValues<Categoria>();
        List<string> categoriasValidas = new List<string>(categorias.Length);
        
        foreach (Categoria categoria in categorias)
            categoriasValidas.Add(GetCategoryDisplayName(categoria));
        
        return string.Join("\n", categoriasValidas);
    }

    private static string GetCategoryDisplayName(Categoria categoria)
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

    private static (DateTime PeriodStartUtc, DateTime PeriodEndUtc) GetBillingPeriod(DateTime dateUtc)
    {
        int year = dateUtc.Year;
        int month = dateUtc.Month;

        if (dateUtc.Day < 15)
        {
            month--;
            if (month == 0) { month = 12; year--; }
        }

        DateTime start = new DateTime(year, month, 15, 0, 0, 0, DateTimeKind.Unspecified);
        DateTime end = start.AddMonths(1).AddDays(-1);
        return (
            TimeZoneInfo.ConvertTimeToUtc(start, SaoPauloTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(end, SaoPauloTimeZone));
    }

    private static DateTime GetBusinessDateUtc(DateTime dateUtc)
    {
        DateTimeOffset localDateTime = TimeZoneInfo.ConvertTime(
            new DateTimeOffset(dateUtc, TimeSpan.Zero),
            SaoPauloTimeZone);

        return new DateTime(
            localDateTime.Year,
            localDateTime.Month,
            localDateTime.Day,
            0,
            0,
            0,
            DateTimeKind.Utc);
    }
}
