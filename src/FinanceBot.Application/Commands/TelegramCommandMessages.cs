namespace FinanceBot.Application.Commands;

internal static class TelegramCommandMessages
{
    public const string Start = "FinanceBot online. Use /info para ver todos os comandos.";

    public const string UnknownCommand = "Comando não reconhecido. Use /info para ver os comandos disponíveis.";

    public const string Info =
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
        "/limite <CATEGORIA> <VALOR>\n" +
        "Define um limite mensal de gastos para a categoria.\n" +
        "Use valor 0 para remover o limite.\n" +
        "Ex: /limite Mercado 800\n" +
        "Ex: /limite Mercado 0  (remove o limite)\n\n" +
        "/limite <CATEGORIA>\n" +
        "Consulta o limite definido e o gasto atual do mês.\n" +
        "Ex: /limite Mercado\n\n" +
        "Categorias: Educacao, Lazer, Lanches, Uber, Mercado, Moto, Compras, Outros, Estetica, Limpeza, Saude";
}
