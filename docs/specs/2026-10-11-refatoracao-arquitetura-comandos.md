# Refatoração da arquitetura e dos comandos do FinanceBot

Data: 2026-10-11.

Status: desenho geral aprovado; tickets #5 (reorganização) e #6 (autenticação, configuração e proteção de credenciais) implementados, validados e publicados pelo usuário no Railway em 2026-10-12, com verificações reais do webhook e Telegram. Tickets #7 (períodos), #8 (`/limite` e proteção de `LimiteCategoria`), #9 (`/compra` e proteção de `Compra`) e #10 (`/deletar`) implementados localmente; validação registrada ao final. Os tickets #11 e #12 e a publicação das mudanças de #7 em diante permanecem pendentes; contratos dos tickets ainda pendentes abaixo são propostas para revisão antes da implementação.

Origem: [issue #4](https://github.com/nathancarvalhocota/financas-pessoais-telegram/issues/4) e decisões da revisão arquitetural. Esta spec substitui as extrações sugeridas na issue pelas alternativas escolhidas durante o debate.

## 1. Problema e objetivo

Antes desta refatoração, `RegisterExpenseCommandHandler` misturava interpretação do comando, validação, consultas, persistência, avaliação de limite e alerta e construção da resposta. Os demais handlers também retornavam strings que representavam tanto sucesso quanto falha, dificultando testar o comportamento sem depender do texto apresentado.

A divisão anterior em quatro projetos acrescentava uma fronteira física que o projeto decidiu dispensar. O objetivo é manter responsabilidades claras em três projetos, proteger as regras financeiras no domínio e separar resultados de comandos de suas mensagens, preservando o comportamento atual do bot.

Referências do comportamento vigente: [comandos](../commands.md), [domínio](../domain.md), [arquitetura](../architecture.md), [configuração](../config.md) e [testes](../testing.md). Esses documentos descrevem a implementação atual; deverão ser atualizados junto das entregas correspondentes.

## 2. Decisões aprovadas

| Tema | Decisão |
|---|---|
| Projetos | Incorporar `FinanceBot.Application` à API e remover esse projeto da solução |
| API | Organizar entrada, saída, configuração e aplicação em `Endpoints`, `Messaging`, `Configuration` e `Commands` |
| Persistência | Interfaces no domínio; implementações em Infrastructure |
| Entidades | Proteger criação e alterações contra estados inválidos; falhas esperadas com erros tipados |
| Comandos | Resultados estruturados específicos por comando; erros próprios e dados quando necessários |
| Apresentação | Formatters produzem as mensagens; router chama o formatter e retorna `Task<string>` ao endpoint |
| Router | Manter o `switch`, sem registry ou dispatcher |
| Parsing | Extrair `ExpenseParser`; parser chamado dentro do handler; manter entrada textual dos handlers |
| Limites | Avaliação em `LimiteCategoria`; consultas e persistência nos handlers |
| Alertas | Regra em `SpendingAlertPolicy`; consultas e preparação dos dados no handler |
| Serviços adicionais | Não criar `CategoryLimitService`, `CategoryLimitProgressService` ou `SpendingPaceAlertService` nesta entrega |
| Consultas | Reutilizar gastos do ciclo atual no registro; início inclusivo e fim exclusivo explícitos |
| Categorias e dinheiro | Manter enum, catálogo estático, `double` e colunas atuais |
| Webhook | Exigir secret do Telegram e manter autorização por chat |
| Configuração | Validar opções na inicialização; usar carregamento padrão de User Secrets em Development |
| Operação | Manter migrations na inicialização e política atual de falha de envio; melhorar logs e guia Railway |

## 3. Fronteiras e estrutura final

```text
src/
  FinanceBot.Api/
    Program.cs
    appsettings.json
    Properties/
      launchSettings.json
    Endpoints/
      TelegramWebhookEndpoint.cs
      TelegramWebhookRequest.cs
    Messaging/
      TelegramMessageSender.cs
    Configuration/
      TelegramOptions.cs
    Commands/
      TelegramCommandRouter.cs
      DateTimeConverter.cs
      Handlers/
        RegisterExpenseCommandHandler.cs
        DeleteExpenseCommandHandler.cs
        ListExpensesCommandHandler.cs
        CategoryLimitCommandHandler.cs
      Parsing/
        TelegramCommandParser.cs
        ExpenseParser.cs
        ExpenseListQueryParser.cs
      Results/
        RegisterExpenseResult.cs
        DeleteExpenseResult.cs
        ListExpensesResult.cs
        CategoryLimitResult.cs
        ... dados compartilhados realmente usados pelos resultados
      Formatting/
        TelegramCommandMessages.cs
        CategoryCommandMessages.cs
        BrazilianCurrencyFormatter.cs
        RegisterExpenseFormatter.cs
        DeleteExpenseFormatter.cs
        ExpenseListFormatter.cs
        CategoryLimitFormatter.cs
  FinanceBot.Domain/
    Expenses/
      Compra.cs
      ICompraRepository.cs
      ... erros e retorno de criação de Compra
    CategoryLimits/
      LimiteCategoria.cs
      ILimiteCategoriaRepository.cs
      ... progresso, erros e retorno de criação/alteração do limite
    Categories/
      Categoria.cs
      CategoryCatalog.cs
    Billing/
      BillingCycle.cs
      SpendingAlertPolicy.cs
  FinanceBot.Infrastructure/
    DependencyInjection/
      InfrastructureServiceCollectionExtensions.cs
    Persistence/
      FinanceBotDbContext.cs
    Repositories/
      CompraRepository.cs
      LimiteCategoriaRepository.cs
    Migrations/
      ... migrations e snapshot existentes
```

Os nomes de arquivos adicionais são propostas, não obrigação de criar um arquivo por tipo. Tipos pequenos e relacionados podem permanecer juntos, como os erros de um comando no arquivo de seu resultado.

Dependências de compilação:

```text
Api -> Domain
Api -> Infrastructure -> Domain
```

`Domain` não referencia os demais projetos, ASP.NET Core, EF Core ou Telegram. `Infrastructure` deixa de referenciar Application e não referencia Api. `Api` é o ponto de composição e também abriga os casos de uso: handlers não dependem de `HttpContext`, sender ou `DbContext`.

Atualizar namespaces, referências dos testes e `FinanceBot.sln`; eliminar referências ao projeto removido. O `Dockerfile` atual publica a API e mantém esse ponto de entrada. Verificar o publish após a reorganização. Não introduzir MediatR, repositório genérico, Unit of Work próprio, interfaces para todos os handlers/formatters ou classes-base de comandos.

## 4. Domínio e persistência

### 4.1. Entidades válidas

`Compra` deve exigir valor positivo, descrição preenchida e categoria válida. `LimiteCategoria` deve exigir valor positivo e categoria válida. Como detalhe proposto, a validação de valor também rejeita `NaN` e infinitos, que não representam valores financeiros válidos.

Criação e alteração públicas devem passar por caminhos controlados, sem setters públicos que permitam contornar as regras. Falhas esperadas retornam dados tipados definidos no domínio, sem mensagens prontas e sem dependência dos erros de comandos da API. O handler traduz o erro de domínio para o erro específico do comando.

Proposta de contrato: cada entidade expõe criação que retorna uma variante de sucesso com a entidade ou falha com seu erro. Uma alteração de limite deve validar o novo valor antes de modificar o estado; em falha, preservar o valor anterior. A forma exata desses pequenos tipos deve ser revisada com os contratos dos comandos, sem adicionar um framework genérico de Result.

`/limite <categoria> 0` é solicitação de remoção interpretada pelo handler. Não cria uma entidade de limite com valor zero. Remover um limite inexistente continua recebendo a confirmação atual. A descrição continua sendo aparada pelo parsing; esta entrega não adiciona uma nova regra de tamanho de descrição.

Adaptar a configuração do EF para criação/materialização e propriedades encapsuladas, preservando tabelas, colunas, IDs e índice único por categoria. O `UpsertAsync` atual altera `existing.Valor` diretamente: deverá utilizar o caminho controlado da entidade. Não mover conhecimento de tracking ou `SaveChanges` para as entidades. A leitura de dados existentes não deve ser usada para executar uma limpeza ou transformação silenciosa de registros.

### 4.2. Progresso e alerta

`LimiteCategoria` deve avaliar um total já conhecido e retornar progresso com categoria, valor do limite, total gasto, percentual e indicação de ultrapassagem. Proposta de método: `AvaliarProgresso(totalGasto)`. O retorno pertence ao domínio e pode ser utilizado pelos resultados dos comandos.

Preservar a regra `totalGasto > limite`, inclusive quando o total é exatamente igual ao limite, e o arredondamento atual de percentual. A entidade não consulta repositórios. A ausência de limite é tratada pelo handler.

`SpendingAlertPolicy.GetCrossedLevel` continua determinando o maior limiar cruzado: 125%, 150% ou 200% do ciclo anterior completo. Sem total anterior positivo, não há alerta. Não introduzir projeção diária, comparação proporcional ao dia do ciclo ou estado persistido de alertas.

As interfaces no domínio permitem uso por serviços de domínio quando houver uma regra que justifique isso. Nesta entrega, os handlers continuam coordenando o acesso aos repositórios.

### 4.3. Repositórios e períodos

Mover os contratos atuais sem ampliar suas operações. `AddAsync`, `UpsertAsync` e `DeleteAsync` continuam persistindo suas alterações; não redefinir a transação ou adiar `SaveChanges` nesta refatoração.

Explicitar o contrato de consulta:

```csharp
Task<IReadOnlyList<Compra>> ListByPeriodAsync(
    DateTime periodStartUtc,
    DateTime periodEndExclusiveUtc,
    CancellationToken cancellationToken);
```

A consulta usa `Data >= periodStartUtc && Data < periodEndExclusiveUtc`. Calcular o fim exclusivo a partir do início do dia seguinte ao último dia no calendário de São Paulo e então convertê-lo para UTC. Centralizar isso em `DateTimeConverter`, sem ajustes `AddDays(1)` espalhados nos handlers depois da conversão para UTC.

O ciclo de domínio continua tendo início no dia 15 e último dia no dia 14. Preservar datas inclusivas na apresentação: 15/04 a 14/05 corresponde à consulta até o início de 15/05, exclusivo.

## 5. Parsing, resultados e apresentação

### 5.1. Parsing

`ExpenseParser` interpreta o texto de `/compra` e retorna dados ou falha tipada. Não acessa banco, persiste entidades ou constrói mensagens. Preservar separadores decimal vírgula/ponto, duas casas aceitas pela sintaxe e normalização atual de categorias.

Os parsers podem identificar falhas da entrada, mas as entidades continuam protegendo suas próprias condições de validade. Um parser não é a única defesa contra uma compra ou limite inválido.

`ExpenseListQueryParser` deve deixar de retornar `ErrorMessage` e de preparar `PeriodDescription`. Retornar erro tipado ou dados do período e filtro. A descrição de apresentação passa ao formatter. Usar dados de calendário e uma indicação de seleção por ciclo ou intervalo explícito para preservar o formato das mensagens.

Manter categorias fixas e `CategoryCatalog` estático; parsers usam o catálogo e formatters usam os nomes. A mensagem completa de categoria inválida fica em `CategoryCommandMessages`. Não criar um catálogo injetável.

### 5.2. Contratos propostos dos comandos

Cada comando tem resultado próprio, com variantes de sucesso e falha. Usar records e composição de dados para evitar uma classe com propriedades opcionais de todos os comandos. Não criar enum global de erros nem uma hierarquia genérica obrigatória.

| Resultado | Variantes e dados propostos |
|---|---|
| `RegisterExpenseResult` | `Success`: compra registrada, progresso opcional e alerta opcional; `Failure`: erro específico |
| `DeleteExpenseResult` | `Success`: dados da compra removida; `NotFound`: ID solicitado; `Failure`: erro de entrada |
| `ListExpensesResult` | `Success`: período, filtro, itens e total; `Failure`: erro específico. Lista vazia é sucesso |
| `CategoryLimitResult` | `Queried`: categoria e progresso opcional; `Updated`: categoria e valor; `Removed`: categoria; `Failure`: erro específico |

Limite ausente em `Queried` é um estado normal, não uma falha de infraestrutura. Despesa não encontrada tem variante própria e não deve ser confundida com ID inválido.

Erros propostos, com nomes a revisar:

| Comando | Casos |
|---|---|
| Registrar compra | `InvalidSyntax`, `UnknownCategory`, `InvalidAmount`, `MissingDescription` |
| Excluir compra | `InvalidSyntax`, `InvalidId` |
| Listar compras | `InvalidSyntax`, `UnknownCategory`, `InvalidDate`, `InvalidMonth`, `InvertedPeriod`, `MissingExcludedCategories` |
| Limite | `InvalidSyntax`, `UnknownCategory`, `InvalidLimit` |

Erros que precisam de dados devem carregá-los, por exemplo o texto da categoria desconhecida. Podem ser representados por records específicos dentro da família de erros do comando; não usar uma string pronta como identidade do erro. Compartilhar a apresentação de erros equivalentes, sem obrigar todos os comandos a aceitarem todos os códigos. Preservar a precedência das mensagens vigentes quando várias partes da entrada forem inválidas.

Dados de resposta propostos em `Api/Commands/Results`:

- `ExpenseItem`: ID, valor `double`, descrição, categoria e instante UTC.
- `ExpensePeriod`: datas de calendário de início/fim inclusivos e tipo de seleção; limites UTC da consulta devem ter o fim exclusivo inequívoco.
- `SpendingPaceAlert`: nível, períodos, totais e comparação por categoria. É dado da resposta, não um novo serviço.
- Comparação por categoria: totais atual/anterior, diferença e percentual quando houver base anterior positiva. Sem base, não inventar percentual ou dividir por zero.

`CategoryLimitProgress` pertence ao domínio como resultado de sua avaliação; não duplicar esse cálculo em DTOs da API. O percentual do alerta representa gasto atual como porcentagem do anterior: 125% do anterior corresponde a aumento de 25%, não de 125%.

### 5.3. Router e formatters

Manter as assinaturas de entrada dos handlers com texto, data quando utilizada e `CancellationToken`; substituir o retorno de mensagem pelo resultado específico. Não exigir requests tipados na entrada do router nesta entrega.

```text
Endpoint -> Router -> Handler -> resultado estruturado
               -> Formatter -> texto
Endpoint -> TelegramMessageSender
```

O router mantém o `switch` e `Task<string>` na saída. Cada ramo financeiro aguarda o handler e chama seu formatter. `/start`, `/info` e comando desconhecido continuam usando mensagens estáticas. Formatters não acessam banco nem executam regras de negócio.

Preservar conteúdo, valores, ordem e informação das respostas atuais conforme código e testes. Mudanças editoriais de texto devem ser explicitadas na revisão, não aparecer como efeito colateral da troca dos contratos. Não alterar silenciosamente o fuso de exibição dos instantes da listagem.

## 6. Fluxo de registro e compatibilidade

O registro deverá:

1. Interpretar a entrada pelo parser e obter uma compra válida pelo domínio.
2. Persistir a compra, preservando a ordem atual de gravação antes das consultas de alerta.
3. Consultar o limite da categoria e os gastos do ciclo atual.
4. Reutilizar os mesmos gastos atuais para progresso e alerta; consultar o ciclo anterior separadamente.
5. Pedir a avaliação do limite à entidade e o nível do alerta à política de domínio.
6. Preparar dados e retornar `RegisterExpenseResult`, sem construir texto.

Manter a data da mensagem para registro e ciclo de `/listar`; `/limite` continua usando `TimeProvider` para o ciclo atual. Preservar fallback para `DateTimeOffset.UtcNow` quando o webhook não trouxer data. Não mudar os limiares nem o fuso `America/Sao_Paulo`.

Os contratos tipados representam falhas esperadas. Exceções de banco e outras falhas inesperadas não devem virar automaticamente erro de entrada ou sucesso. Não acrescentar captura global que retorne 200 para qualquer falha do comando.

## 7. Webhook, configuração e logs

### 7.1. Autenticação e configuração

Adicionar `Telegram:WebhookSecret`. Exigir o header `X-Telegram-Bot-Api-Secret-Token` antes de rotear comandos ou enviar respostas. Manter a autorização independente por `AllowedChatIds`. O Telegram envia o header quando o registro usa `secret_token`; o valor aceita 1 a 256 caracteres de `A-Z`, `a-z`, `0-9`, `_` e `-`. [Telegram: setWebhook](https://core.telegram.org/bots/api#setwebhook).

Resposta aprovada para header ausente ou incorreto: HTTP 401, sem operação financeira ou envio ao Telegram. Headers com múltiplos valores devem ser recusados, sem escolher um deles como credencial válida. Updates autenticados ignorados e chats não autorizados preservam o HTTP 200 atual.

Validar na inicialização: BotToken preenchido, WebhookSecret válido e ao menos um chat permitido. Informar as chaves inválidas, nunca seus valores. O startup de testes também deverá fornecer opções válidas. Não adicionar checagem de token na rede durante a inicialização.

Remover o `AddUserSecrets` incondicional acrescentado depois de `WebApplication.CreateBuilder`; usar os provedores padrão e env vars em produção. A convenção `__` representa hierarquia nas variáveis de ambiente. [ASP.NET Core: configuração](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0).

### 7.2. Envio e inicialização do banco

Manter o contrato `Task` do sender e a política vigente: falha ao enviar a resposta é registrada, e o webhook confirma com 200 quando a operação já concluiu. Não prometer entrega a partir do simples retorno de `SendMessageAsync`: atualmente um HTTP não exitoso é logado e o método retorna normalmente.

Manter `MigrateAsync` na inicialização de banco relacional. Ele verifica migrations pendentes; não reaplica todas a cada início. Não adicionar retry de conexão, fila de envio, deduplicação por update ou compensação de compra nesta entrega. `/health` mantém o comportamento atual; não é uma verificação de entrega ao Telegram ou de prontidão do banco em cada chamada.

### 7.3. Logs mínimos

Manter `ILogger` e saída de console, sem nova biblioteca de observabilidade.

- Registrar término do processamento com identificador da requisição, duração, resultado operacional e comando reconhecido, quando aplicável.
- Diferenciar requisição ignorada, recusada, processada e falha; conclusão do processamento não significa entrega da resposta.
- Registrar início, término e falha da inicialização do banco, com contexto da etapa. Não registrar connection string.
- Não registrar texto completo de comandos, descrições, valores financeiros, headers secretos, bot token ou corpos brutos de resposta do Telegram. Logs HTTP e exceções devem ser tratados para não expor a URL com token.
- Suprimir os logs automáticos de URI do HttpClient usado para Telegram, inclusive em níveis detalhados. No .NET 8, o handler padrão registra informações da requisição; o token está no caminho da URL, não apenas em headers. [Implementação do .NET 8](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/Microsoft.Extensions.Http/src/Logging/LoggingHttpMessageHandler.cs).
- Não habilitar `EnableSensitiveDataLogging` do EF em produção. Manter nível suficiente para observar inicialização e falhas sem exigir consultas SQL em Information.

Reutilizar o identificador HTTP existente para correlação; esta entrega não exige adicionar `update_id` ao contrato. O registro de falhas deve preservar o diagnóstico necessário por etapa/tipo/status, evitando propagar dados sensíveis de mensagens de exceção HTTP.

## 8. Procedimento a incorporar em docs/config.md

Esta seção define o roteiro que deverá acompanhar os tickets de autenticação e publicação. Não contém configurações reais de produção. A execução do roteiro é uma etapa operacional futura, não realizada ao escrever esta spec.

### 8.1. Variáveis no Railway e configuração local

No serviço da API, no ambiente escolhido, revisar:

| Variável Railway | Configuração .NET |
|---|---|
| `Telegram__BotToken` | `Telegram:BotToken` |
| `Telegram__WebhookSecret` | `Telegram:WebhookSecret` |
| `Telegram__AllowedChatIds__0` | Primeiro chat autorizado |
| `Telegram__AllowedChatIds__1` | Segundo chat, se utilizado |
| `ConnectionStrings__DefaultConnection` | Conexão existente do PostgreSQL |

Guardar valores reais apenas no gerenciador de segredos/configuração. Não copiar tokens, chat IDs reais, endereços internos ou saídas completas para a documentação. Não substituir a conexão existente por uma URL de outro formato sem verificar sua compatibilidade com Npgsql.

Railway: abrir projeto, ambiente e serviço da API; entrar em **Variables**; cadastrar/revisar as chaves; revisar e publicar as alterações pendentes. Alterar uma variável exige aplicar o deployment para afetar o serviço. [Railway: variáveis](https://docs.railway.com/variables).

Em Development, acrescentar o secret aos User Secrets da API, junto das configurações já descritas no guia:

```powershell
dotnet user-secrets set "Telegram:WebhookSecret" "<WEBHOOK_SECRET>" --project src/FinanceBot.Api
```

O marcador deve ser substituído localmente por um secret válido. Evitar inserir credenciais reais em histórico compartilhado ou gravado. O guia final deve apresentar uma alternativa de entrada protegida, como no registro abaixo.

### 8.2. Ativação inicial sem desencontro de secret

1. Confirmar domínio HTTPS, `/health`, ambiente e versão atualmente em produção. O container atual escuta na porta 8080 e `railway.toml` aponta o healthcheck para `/health`.
2. Escolher e guardar um WebhookSecret; cadastrar as opções necessárias no serviço da API. Aplicar essas variáveis ainda com uma versão que aceite o webhook sem exigir o novo header. Conferir qual versão será publicada antes de aplicar mudanças.
3. Registrar o webhook de produção com a mesma URL e `secret_token` correspondente. A versão antiga ignora o header adicional. Conferir sucesso da chamada.
4. Publicar a versão que exige o header, com as variáveis já aplicadas. Manter os logs da API abertos para conferir startup e validação.
5. Consultar `getWebhookInfo`, verificar a URL e eventuais erros recentes; enviar `/start` de um chat autorizado. Conferir a resposta e o registro de processamento.
6. Validar que uma chamada direta sem secret é recusada, sem operação ou envio. Esse cenário também deve estar nos testes automatizados.

Se a versão estrita já tiver sido publicada, cadastrar/aplicar as opções e registrar o mesmo secret no Telegram para restabelecer a entrega. Não usar `drop_pending_updates=true` como procedimento normal, pois descartaria comandos pendentes. Reenvios continuam sujeitos à limitação atual de ausência de idempotência.

### 8.3. Registrar e conferir o webhook por PowerShell

Exemplo para execução manual futura. As entradas protegidas evitam digitar o token e o secret como literais no comando. O token continuará existindo em memória durante a chamada; não compartilhar dumps ou transcrições de diagnóstico que o contenham.

```powershell
$telegramBotToken = [System.Net.NetworkCredential]::new(
    '', (Read-Host 'Token do bot' -AsSecureString)).Password
$telegramWebhookSecret = [System.Net.NetworkCredential]::new(
    '', (Read-Host 'WebhookSecret configurado na API' -AsSecureString)).Password
$financeBotPublicUrl = (Read-Host 'URL HTTPS publica, sem /telegram/webhook').TrimEnd('/')

try {
    $webhookRegistration = Invoke-RestMethod -Method Post `
        -Uri "https://api.telegram.org/bot$telegramBotToken/setWebhook" `
        -Body @{
            url = "$financeBotPublicUrl/telegram/webhook"
            secret_token = $telegramWebhookSecret
        } -ErrorAction Stop

    if (-not $webhookRegistration.ok -or -not $webhookRegistration.result) {
        throw 'Registro recusado.'
    }

    $webhookInfo = Invoke-RestMethod -Method Get `
        -Uri "https://api.telegram.org/bot$telegramBotToken/getWebhookInfo" `
        -ErrorAction Stop

    if (-not $webhookInfo.ok) {
        throw 'Consulta recusada.'
    }

    $webhookInfo.result | Select-Object `
        url, pending_update_count, last_error_date, last_error_message
}
catch {
    Write-Warning 'Falha ao registrar ou consultar o webhook. Confira URL, credenciais e rede; nao compartilhe a excecao bruta, que pode conter o token.'
}
finally {
    Remove-Variable telegramBotToken, telegramWebhookSecret -ErrorAction SilentlyContinue
}
```

`getWebhookInfo` não revela o secret cadastrado; comprovar correspondência pelo teste de entrega. O horário de último erro pode representar uma falha anterior, então comparar com a tentativa atual. Um redeploy com URL e secret iguais não exige registrar novamente o webhook. Alteração de URL ou secret exige atualizar o registro.

Para túnel local, registrar a URL do túnel com o secret da API local. Um bot tem um webhook ativo: o túnel substitui a entrega de produção. Preferir bot separado para desenvolvimento ou restaurar explicitamente URL e secret de produção ao terminar. Não alterar `/setcommands`, pois esta refatoração mantém os comandos existentes.

### 8.4. Logs e diagnóstico no Railway

Abrir o serviço da **API**, selecionar o deployment e consultar seus logs de execução. Build Logs mostram a compilação; logs do serviço PostgreSQL mostram o banco. Para consultar serviços em conjunto e filtrar por período, usar **Observability / Log Explorer**. Railway captura stdout/stderr. [Railway: logs](https://docs.railway.com/observability/logs).

| Sintoma | Conferir |
|---|---|
| API não inicia | Chaves ausentes/invalidas; conexão e logs da etapa de migrations |
| HTTP 401 no webhook | Mesmo secret na versão ativa da API e no registro do Telegram |
| Bot ignora comando | Chat permitido, conteúdo do update e ambiente/serviço consultado |
| HTTP 502/503 | Deployment ativo, porta, domínio/túnel e startup completo |
| Comando persistido, sem resposta | Falha de `sendMessage`; não reenviar `/compra` automaticamente |
| PostgreSQL com 57P03 | Banco em inicialização/recuperação; conferir quando ficou pronto e se a API reiniciou |

Mensagens de inicialização do PostgreSQL não comprovam cold start: podem acompanhar reinício, recuperação ou manutenção. Esta entrega não altera Serverless ou a política de reinício do Railway. Não documentar que migrations são reaplicadas integralmente a cada início.

## 9. Verificação e critérios de aceite

### 9.1. Estrutura e comandos

- [ ] A solução contém três projetos de produção e os dois projetos de testes atuais; nenhuma referência de projeto depende de Application.
- [ ] Domain não depende da API, Infrastructure ou frameworks de transporte/persistência; Infrastructure depende de Domain.
- [ ] Handlers podem ser instanciados e testados sem iniciar servidor HTTP.
- [ ] Os quatro handlers financeiros retornam resultados estruturados; parsers não retornam mensagens prontas.
- [ ] Router preserva o `switch`, formata os resultados e mantém `/start`, `/info` e comando desconhecido.
- [ ] Entrada, mensagens e regras vigentes são preservadas, exceto autenticação/configuração aprovadas e diferenças editoriais explicitamente revisadas.
- [ ] Compra inválida e limite inválido não podem ser criados/alterados por caminhos públicos; falhas de alteração preservam estado.
- [ ] Limite igual ao total não é ultrapassado; zero remove; ausência de limite e listagem vazia são estados normais.
- [ ] Alertas mantêm os limiares, maior cruzamento e ausência de alerta sem base anterior positiva.
- [ ] Registro reutiliza a consulta atual para limite e alerta; os dados das duas avaliações vêm da mesma leitura.
- [ ] Consultas incluem o início e excluem o fim; calendário e apresentação mantêm dias inclusivos em São Paulo.

### 9.2. API e operação

- [ ] Header correto permite o fluxo; header ausente/incorreto retorna HTTP 401 e não persiste nem envia mensagem.
- [ ] Chat não permitido continua sem operação e envio, com confirmação HTTP vigente.
- [ ] Startup rejeita opções inválidas com nomes das chaves e sem valores secretos.
- [ ] Falha no envio após a operação mantém confirmação HTTP 200 e registro seguro; falha inesperada na operação não vira sucesso silencioso.
- [ ] Logs mostram processamento normal, duração, correlação e etapas de inicialização; logs do HttpClient e falhas não revelam tokens.
- [ ] Docs incluem configuração local, Railway, ativação inicial, nova configuração, restauração após túnel e diagnóstico.

### 9.3. Testes e checks da implementação futura

Distribuir os testes unitários em `Domain` e `Api/Commands` (parsing, handlers, formatting e router); adaptar fixtures/repositórios em memória aos caminhos controlados das entidades. Manter cobertura de infraestrutura e sender onde já existe. Priorizar comportamento e estados de retorno, deixando comparações de texto nos formatters e em cenários selecionados de router/webhook.

Cobrir materialização EF das entidades encapsuladas e upsert de limite existente; não apenas inserção. Cobrir fronteiras temporais e falhas do webhook com configuração explícita nos testes. Um teste de logs deve usar tokens/secrets fictícios identificáveis e verificar sua ausência, inclusive no envio HTTP.

Os testes de integração atuais usam EF InMemory e não comprovam comportamento PostgreSQL/migrations. Registrar essa limitação ao reportar validação; não afirmar execução contra PostgreSQL sem realizá-la. Esta spec não exige instalar um novo ambiente de Testcontainers ou migrar toda a suíte.

Após cada entrega, executar os checks proporcionais à alteração. Na conclusão:

```powershell
dotnet build FinanceBot.sln
dotnet test FinanceBot.sln
dotnet publish src/FinanceBot.Api/FinanceBot.Api.csproj -c Release
```

Validar compatibilidade dos mapeamentos com o schema vigente. Nenhuma migration de dados/tipos monetários é prevista; uma mudança de modelo inesperada deve ser investigada antes de gerar migrations.

## 10. Divisão aprovada dos tickets

Os dois primeiros tickets compõem uma única entrega, podendo usar commits separados na mesma PR e uma publicação conjunta. Não é necessário publicar uma versão intermediária só com a reorganização. Cada ticket deve compilar, passar seus testes e atualizar a documentação que descreve seu estado real. A criação dos tickets não executa a implementação nem a publicação em produção.

| Ordem | Ticket | Bloqueado por |
|---|---|---|
| 1 | [Reorganizar a solução em três projetos, preservando comportamento](https://github.com/nathancarvalhocota/financas-pessoais-telegram/issues/5) | Nenhum |
| 2 | [Autenticar o webhook, validar configuração e proteger credenciais nos logs](https://github.com/nathancarvalhocota/financas-pessoais-telegram/issues/6) | 1, para trabalhar sobre a composição final da API |
| 3 | [Uniformizar períodos nas consultas financeiras](https://github.com/nathancarvalhocota/financas-pessoais-telegram/issues/7) | 1 |
| 4 | [Estruturar `/limite` e proteger os estados de LimiteCategoria](https://github.com/nathancarvalhocota/financas-pessoais-telegram/issues/8) | 3 |
| 5 | [Separar parsing, execução e apresentação de `/compra`](https://github.com/nathancarvalhocota/financas-pessoais-telegram/issues/9) | 4, que fornece a avaliação de progresso |
| 6 | [Estruturar o resultado e a apresentação de `/deletar`](https://github.com/nathancarvalhocota/financas-pessoais-telegram/issues/10) | 1 |
| 7 | [Estruturar parsing e resposta de `/listar`](https://github.com/nathancarvalhocota/financas-pessoais-telegram/issues/11) | 3 |
| 8 | [Completar logs de processamento e inicialização do banco](https://github.com/nathancarvalhocota/financas-pessoais-telegram/issues/12) | Nenhum |

A ordem da tabela orienta a execução; as dependências registram apenas os pré-requisitos de cada ticket. A autenticação não bloqueia tecnicamente os comandos financeiros. A proteção contra exposição de tokens e secrets nos logs pertence ao ticket 2 e deve acompanhar sua publicação; o ticket 8 não repete esse escopo.

Atualizar `AGENTS.md`, `docs/architecture.md`, `docs/domain.md`, `docs/testing.md` e `docs/config.md` quando as respectivas mudanças forem implementadas. Conferir `docs/commands.md`, `/info` e lista de `/setcommands`; mantê-los consistentes, sem anunciar comandos novos inexistentes.

## 11. Fora do escopo e pontos de revisão

Fora do escopo aprovado: migração para decimal, categorias dinâmicas, registry/dispatcher, novos serviços de limite/alerta, retries de banco/envio, idempotência, mensageria, mudança do fallback de data, novo healthcheck de prontidão e substituição integral da suíte por PostgreSQL real.

Os nomes/assinaturas finais dos pequenos tipos de resultado e erro, a variante de período da listagem e o contrato do método de atualização de limite podem ser concretizados nos respectivos tickets, respeitando as responsabilidades e os estados descritos nesta spec. HTTP 401 para credencial ausente/incorreta foi aprovado na revisão dos tickets.

Os oito tickets foram publicados no GitHub com a skill to-tickets, como sub-issues da issue de origem, com label ready-for-agent e dependências nativas. O título, a descrição e o estado da issue de origem foram preservados. Nos tickets #5 e #6, build, 57 testes unitários, 20 testes de integração com EF InMemory e publish Release passaram. Docker indisponível: PostgreSQL e migrations não foram exercitados. Em 2026-10-12, o usuário publicou essa versão no Railway pela branch `refactor/handler-cleanup-and-docs` e confirmou POST sem secret e com secret incorreto retornando 401, secret correto retornando 200, `/start`, `/info`, `/listar`, `/limite Outros` e criação, listagem e exclusão de uma compra de teste. Testes manuais adicionais de alteração de limite e entradas inválidas foram dispensados pelo usuário.

O ticket #7 uniformiza o contrato `ListByPeriodAsync` e seus consumidores para fim UTC exclusivo, centraliza o cálculo no calendário de São Paulo e preserva datas inclusivas na apresentação. Em 2026-10-12, `dotnet build FinanceBot.sln -m:1` passou sem avisos/erros e `dotnet test FinanceBot.sln -m:1` passou com 66 testes unitários e 21 de integração. A cobertura inclui fronteiras de consulta, viradas de mês/ano, ano bissexto e mudança histórica de offset; integração continua usando EF InMemory, sem validar PostgreSQL ou migrations. Não houve mudança de schema nem nova migration.

O ticket #8 separa `CategoryLimitResult` e seus erros de `CategoryLimitFormatter`, mantendo o switch e `Task<string>` do router. `LimiteCategoria` protege criação/alteração, retorna erros próprios do domínio e avalia progresso puro; zero continua sendo remoção pelo handler e ausência continua sendo consulta normal. O upsert usa a alteração controlada; os testes exercitam materialização em contextos distintos e atualização com ID preservado, além de leitura de registro legado inválido sem limpeza silenciosa. Um teste de metadados Npgsql sem conexão confirma o mapeamento vigente; não houve migration nem execução contra PostgreSQL. `/compra` recebeu apenas a adaptação para consumir a avaliação da entidade, permanecendo pendente a separação do ticket #9. Em 2026-10-12, `dotnet build FinanceBot.sln -m:1` passou sem avisos/erros e `dotnet test FinanceBot.sln -m:1` passou com 108 testes unitários e 26 de integração com EF InMemory. Ao concluir o ticket #8, os tickets #9 a #12 e a publicação das mudanças de #7 em diante ainda estavam pendentes. Não houve fechamento de issues nem merge em `main` nesta implementação.

O ticket #9 separa o parser puro `ExpenseParser`, `RegisterExpenseResult` e `RegisterExpenseFormatter`, preservando mensagens, precedência das falhas, sintaxe, data UTC e o switch do router. `Compra.Criar` protege valor positivo e finito, descrição preenchida e categoria válida com erros de domínio; construtores e setters são privados, sem nova regra de tamanho de descrição. `ExpenseItem` representa os campos da compra como dados de resposta, disponível para os próximos contratos. O handler persiste antes de consultar limite e períodos, reutiliza uma única leitura do ciclo atual para progresso e alerta e consulta o ciclo anterior separadamente. A política mantém o maior limiar cruzado; o handler prepara percentuais, diferenças e os estados de comparação sem gastos, sem histórico e com base positiva. Exceções inesperadas de persistência e consulta continuam sendo propagadas, com testes cobrindo cada fronteira de repositório. O formatter tem uma comparação exata da mensagem completa de alerta e o webhook cobre os três limiares, limite igual/ultrapassado e erros sem gravação.

Em 2026-10-12, a validação do ticket #9 passou com `dotnet build FinanceBot.sln -m:1` sem avisos/erros e `dotnet test FinanceBot.sln -m:1` com 153 testes unitários e 35 de integração. EF InMemory verifica materialização em contextos distintos, campos/IDs preservados e registros legados sem transformação; metadados Npgsql são inspecionados sem conexão. Não houve execução contra PostgreSQL, nova migration ou mudança de schema. Os tickets #10 a #12 e a publicação das mudanças de #7 em diante permanecem pendentes. Esta etapa não realizou commit, push, merge, alteração/fechamento de issues ou publicação em produção.

O ticket #10 separa `DeleteExpenseResult` e `DeleteExpenseFormatter`, preservando o `switch` e `Task<string>` do router. O handler mantém texto e `CancellationToken`, valida sintaxe e ID antes das consultas e coordena busca/exclusão pelo repositório. Sucesso contém todos os campos da compra removida em `ExpenseItem`; `NotFound` contém o ID solicitado; falhas distinguem `InvalidSyntax` e `InvalidId`. Regex, precedência e mensagens foram preservadas: negativos, sinal positivo, argumentos adicionais e menção ao bot são sintaxe inválida; zero e overflow são ID inválido. Exceções inesperadas e cancelamento de busca/exclusão continuam sendo propagados. Não há framework genérico de resultado, enum global de erros ou interface para formatter.

Em 2026-10-12, `dotnet build FinanceBot.sln -m:1` passou sem avisos/erros e `dotnet test FinanceBot.sln -m:1` passou com 180 testes unitários e 42 de integração. A cobertura de exclusão verifica dados estruturados, ausência de IO para entrada inválida, ID máximo/zeros iniciais, inexistência sem exclusão, exclusão seletiva, ordem das operações, token e exceções em ambas as fronteiras do repositório. O webhook com Telegram simulado confere mensagens exatas, exclusão efetiva, repetição como não encontrado e preservação dos dados de outra compra; integração continua usando EF InMemory, sem executar PostgreSQL ou migrations. `/info` e a lista de `/setcommands` foram conferidos e continuam coerentes, sem alteração dos comandos disponíveis. Não houve mudança de schema nem nova migration. Os tickets #11 e #12 e a publicação das mudanças de #7 em diante permanecem pendentes. Esta etapa não realizou commit, push, merge, alteração/fechamento de issues ou publicação em produção.
