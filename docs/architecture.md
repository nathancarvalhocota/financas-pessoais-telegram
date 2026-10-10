# Arquitetura

O FinanceBot recebe comandos do Telegram por um webhook, executa os fluxos de aplicação e persiste as compras e os limites no PostgreSQL.

## Projetos e dependências

- **FinanceBot.Api:** inicializa a aplicação, registra a composição por injeção de dependências e expõe o webhook. Também contém as opções e a comunicação HTTP com o Telegram.
- **FinanceBot.Application:** roteia comandos, coordena seus handlers e define os contratos de persistência usados pelos fluxos.
- **FinanceBot.Domain:** contém compras, categorias, limites e regras de ciclo e alerta, sem dependências dos outros projetos.
- **FinanceBot.Infrastructure:** implementa os contratos de persistência com EF Core e PostgreSQL, configura o `DbContext` e contém as migrations.

## Fluxo de uma mensagem

1. `TelegramWebhookEndpoint` recebe o update, ignora mensagens sem texto válido e verifica se o chat está em `Telegram:AllowedChatIds`.
2. `TelegramCommandRouter` identifica o comando e delega ao handler correspondente.
3. O handler consulta ou altera os dados pelo contrato do repositório em Application.
4. A implementação do repositório em Infrastructure consulta ou grava no PostgreSQL.
5. A API envia a resposta ao Telegram pelo `TelegramMessageSender`.

`/start` e `/info` respondem sem consultar o banco. A lista de comandos e seus formatos fica em [`commands.md`](commands.md).

## Composição e integrações

`Program.cs` é o ponto de composição: registra os handlers, o router, o sender do Telegram, as opções e o registro de Infrastructure. `AddInfrastructure` configura o EF Core com Npgsql e conecta os contratos de repositório às suas implementações.

O endpoint de webhook é o adaptador de entrada do Telegram. `TelegramMessageSender` é o adaptador de saída: transforma a resposta da aplicação em uma chamada HTTP `sendMessage`.

## Persistência e inicialização

`FinanceBotDbContext` mapeia `Compra` para `compras` e `LimiteCategoria` para `limite_categorias`. A categoria é armazenada como texto; o valor é persistido como `double precision`. A tabela de limites tem índice único por categoria.

Em bancos relacionais, a API aplica migrations durante a inicialização, antes de começar a atender requisições. Para preparar conexão, secrets e execução local, consulte [`config.md`](config.md).
