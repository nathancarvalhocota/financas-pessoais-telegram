# Arquitetura

O FinanceBot recebe comandos do Telegram por um webhook, executa os fluxos de aplicação e persiste as compras e os limites no PostgreSQL.

## Projetos e dependências

- **FinanceBot.Api:** inicializa a aplicação e compõe as dependências. Organiza o webhook em `Endpoints`, o envio HTTP ao Telegram em `Messaging`, as opções em `Configuration` e o router, handlers, parsing e formatação em `Commands`.
- **FinanceBot.Domain:** contém compras, categorias, limites, regras de ciclo e alerta e contratos de persistência. `ICompraRepository` fica em `Expenses` e `ILimiteCategoriaRepository` em `CategoryLimits`, junto das entidades correspondentes.
- **FinanceBot.Infrastructure:** implementa os contratos de persistência com EF Core e PostgreSQL, configura o `DbContext` e contém as migrations.

A API referencia Domain e Infrastructure. Infrastructure referencia apenas Domain entre os projetos da solução. Domain não depende dos outros projetos nem de ASP.NET Core, EF Core ou Telegram.

## Fluxo de uma mensagem

1. `TelegramWebhookEndpoint` exige um único header `X-Telegram-Bot-Api-Secret-Token` igual a `Telegram:WebhookSecret`. Ausência, valor incorreto ou múltiplos valores retornam HTTP 401 sem execução ou envio. Depois, ignora mensagens sem texto válido e verifica se o chat está em `Telegram:AllowedChatIds`; updates autenticados ignorados e chats não autorizados recebem HTTP 200.
2. `TelegramCommandRouter` identifica o comando e delega ao handler correspondente.
3. O handler consulta ou altera os dados pelo contrato do repositório em Domain.
4. A implementação do repositório em Infrastructure consulta ou grava no PostgreSQL.
5. A API envia a resposta ao Telegram pelo `TelegramMessageSender`.

`/start` e `/info` respondem sem consultar o banco. A lista de comandos e seus formatos fica em [`commands.md`](commands.md).

O router mantém um `switch` para selecionar os quatro handlers financeiros e as respostas estáticas, com saída `Task<string>`. Os handlers recebem texto e contratos de repositório e podem ser instanciados sem servidor HTTP, `HttpContext`, sender ou `DbContext`. O handler de `/limite` retorna `CategoryLimitResult`, com variantes `Queried`, `Updated`, `Removed` e `Failure` e erros específicos; o router chama `CategoryLimitFormatter` para apresentar o resultado. Consulta sem limite é um resultado normal com progresso ausente. O handler de `/compra` retorna `RegisterExpenseResult`, apresentado por `RegisterExpenseFormatter`. `/deletar` retorna `DeleteExpenseResult`, apresentado por `DeleteExpenseFormatter`. `/listar` ainda retorna mensagens.

`CategoryLimitCommandHandler` coordena consulta, remoção e upsert e traduz erros de criação do domínio para erros de comando. O formatter só apresenta dados e erros, sem acessar repositórios ou calcular regras financeiras. `LimiteCategoria.AvaliarProgresso` recebe o total já consultado pelo handler e devolve os dados do progresso.

`RegisterExpenseCommandHandler` chama o parser puro `ExpenseParser`, traduz falhas tipadas de parsing e criação de `Compra` para erros específicos e persiste antes de consultar limite e gastos. Uma única leitura do ciclo atual alimenta progresso e alerta; o ciclo anterior completo tem uma consulta separada. O sucesso contém `ExpenseItem`, progresso opcional e `SpendingPaceAlert` opcional. O handler prepara percentuais, diferenças e os estados de comparação por categoria (`NoSpending`, `NoHistory` e `Comparable`); o formatter apresenta esses dados, mantendo confirmação, limite e alerta nessa ordem. Exceções inesperadas de persistência ou consulta continuam sendo propagadas.

`DeleteExpenseCommandHandler` mantém entrada textual e `CancellationToken`, valida sintaxe e ID antes de acessar o repositório e coordena busca e exclusão. `DeleteExpenseResult.Success` contém `ExpenseItem` da compra removida, `NotFound` contém o ID solicitado e `Failure` distingue `InvalidSyntax` de `InvalidId`. O repositório continua responsável por persistir a exclusão. O formatter apenas apresenta o resultado; falhas inesperadas de busca/exclusão e cancelamento são propagadas, sem virar ausência ou confirmação.

## Composição e integrações

`Program.cs` é o ponto de composição: registra os handlers, o router, o sender do Telegram, as opções e o registro de Infrastructure. `AddInfrastructure` configura o EF Core com Npgsql e conecta os contratos de repositório às suas implementações.

O endpoint de webhook é o adaptador de entrada do Telegram. `TelegramMessageSender` é o adaptador de saída: transforma a resposta da aplicação em uma chamada HTTP `sendMessage`.

`AddTelegramMessaging` registra e valida as opções e remove os loggers automáticos do HttpClient do Telegram, inclusive em Trace. Os logs de envio preservam etapa, tipo e status sem registrar URI com token, mensagem/objeto de exceção, secret ou corpo bruto. O sender mantém contrato `Task`: resposta HTTP não exitosa é registrada e retorna normalmente; exceção de transporte é registrada com campos seguros e capturada pelo endpoint, que confirma HTTP 200 após o comando concluído. Exceções da execução do comando permanecem fora desse tratamento e não viram sucesso silencioso.

## Persistência e inicialização

`FinanceBotDbContext` mapeia `Compra` para `compras` e `LimiteCategoria` para `limite_categorias`. A categoria é armazenada como texto; o valor é persistido como `double precision`. A tabela de limites tem índice único por categoria.

`Compra` e `LimiteCategoria` têm construtores e setters privados; o EF materializa as propriedades encapsuladas sem transformar registros existentes. A criação de compra usa `Compra.Criar`; não há alterações públicas de seus campos. O upsert de limite usa `AlterarValor` ao atualizar um limite existente, preserva seu ID e continua chamando `SaveChanges` no repositório. A refatoração não altera o mapeamento nem adiciona migration.

Antes de resolver o DbContext ou aplicar migrations, a API valida token não vazio, secret distinto do token com 1 a 256 caracteres ASCII permitidos e lista não vazia de chats. A validação identifica chaves sem seus valores e não chama o Telegram. A configuração usa os provedores padrão, com User Secrets em Development e variáveis de ambiente em produção.

Em bancos relacionais, a API aplica migrations durante a inicialização, antes de começar a atender requisições. Para preparar conexão, secrets e execução local, consulte [`config.md`](config.md).
