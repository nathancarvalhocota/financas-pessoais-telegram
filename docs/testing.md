# Testes

Execute os testes pela raiz do repositório:

```powershell
dotnet test FinanceBot.sln
```

Para executar apenas um dos projetos:

```powershell
dotnet test tests/FinanceBot.UnitTests/FinanceBot.UnitTests.csproj
dotnet test tests/FinanceBot.IntegrationTests/FinanceBot.IntegrationTests.csproj
```

Para compilar sem executar os testes:

```powershell
dotnet build FinanceBot.sln
```

## Projetos e escopo atual

- **FinanceBot.UnitTests:** cobre regras de categoria, ciclos, limites e limiares de alerta; também testa o roteamento de comandos, registro e exclusão em repositórios em memória, composição de Infrastructure e envio HTTP ao Telegram.
- **FinanceBot.IntegrationTests:** usa `WebApplicationFactory<Program>` para enviar updates ao endpoint real da aplicação. Os testes verificam respostas, autorização de chats, persistência de comandos e mensagens de saída capturadas por um `HttpMessageHandler` de teste.

Os testes de comandos ficam em `FinanceBot.UnitTests/Api/Commands` e instanciam o router e seus handlers com repositórios em memória, sem iniciar um servidor HTTP. Os testes de domínio continuam em `FinanceBot.UnitTests/Domain`.

Os testes de `/compra` cobrem o parser puro e seus erros tipados, vírgula/ponto, menção ao bot, normalização de categoria, precedência das falhas e valor não finito por overflow. `CompraTests` verifica criação válida/inválida, ausência de setters/construtores públicos e ausência de nova regra de tamanho de descrição. Os testes do handler verificam dados de `RegisterExpenseResult`, UTC, ausência de limite/histórico, igualdade/ultrapassagem, maior limiar cruzado e não repetição, além dos três estados de comparação por categoria. Um cenário focado comprova persistência antes das consultas, propagação do CancellationToken e uma única leitura atual compartilhada por limite e alerta, seguida de uma leitura anterior. Falhas de persistência e de cada consulta são propagadas, preservando a compra quando já gravada.

O formatter de compra tem comparações exatas de confirmação, erros, progresso e uma mensagem completa de alerta, incluindo ordem das categorias, percentuais e diferenças. O webhook cobre compra sem limite, limite igual/ultrapassado, os três limiares de alerta e precedência de falhas sem gravação. `/info` e `/setcommands` mantêm a lista existente, pois não há mudança de comandos ou sintaxe.

Os testes de `/deletar` verificam `DeleteExpenseResult.Success` com todos os campos de `ExpenseItem`, `NotFound` com o ID solicitado e erros específicos de sintaxe e ID. Cobrem zeros iniciais, maiúsculas/espaços, ID máximo, zero, overflow, negativos, menção ao bot e precedência da sintaxe sobre o valor. Entradas inválidas não acessam o repositório; ausência não exclui, e sucesso exclui apenas a compra buscada. Verificam também ordem de busca/exclusão, propagação de `CancellationToken` e exceções de ambas as operações, inclusive cancelamento. Comparações exatas de texto ficam no formatter e em cenários do webhook com Telegram simulado. O fluxo HTTP confirma exclusão efetiva, preservação de outra compra, repetição como não encontrado e erros sem alterar os campos persistidos.

Os testes de `/listar` separam parser, handler e formatter. O parser verifica datas inclusivas, seleção por ciclo ou intervalo, limites UTC com fim exclusivo, formatos vigentes, normalização, ordem e duplicatas dos filtros e erros tipados com precedência preservada. O handler verifica `ListExpensesResult.Success`, itens e total após inclusão/exclusão, lista vazia como sucesso, uma única consulta com limites/token exatos e ausência de IO em falhas de entrada. Também verifica propagação de falhas inesperadas e cancelamento da consulta. O formatter compara mensagens completas, cabeçalhos de ciclo/intervalo, duplicatas, todas as variantes de lista vazia e erros; um teste confirma que apresenta itens e total já preparados. O webhook com Telegram simulado verifica mensagens exatas, fronteiras inclusiva/exclusiva, ordem decrescente, filtros e totais, data da mensagem na virada do ciclo, ausência de alterações persistidas e exibição dos instantes em UTC. Os testes legados de texto do router foram mantidos.

Os testes de `/limite` verificam as variantes e erros de `CategoryLimitResult`, consulta sem limite, definição/atualização, remoção com zero inclusive sem registro existente e precedência entre sintaxe, categoria e valor. A entidade é testada contra criação/alteração inválidas, preservação de estado e progresso com arredondamento e igualdade ao limite. As comparações de mensagens ficam no formatter, nos testes legados do router e em cenários selecionados do webhook; o fluxo HTTP cobre consulta, definição, atualização, progresso, valor inválido e remoção.

`LimiteCategoriaRepositoryTests` verifica inserção, materialização em contextos distintos, atualização preservando ID/categoria e exclusão com EF InMemory. Um registro legado com categoria inválida verifica que a materialização preserva os dados e que a alteração controlada recusa o estado inválido. Um teste de metadados com Npgsql, sem conexão ao banco, confere tabela, colunas, chave gerada, categoria textual, índice único e `double precision`. Isso verifica a configuração do modelo, sem executar PostgreSQL ou migrations.

`CompraRepositoryTests` também verifica inserção/materialização em contextos distintos com todos os campos e ID preservados, exclusão e leitura de dados legados inválidos sem transformação. Metadados Npgsql, sem conexão, conferem o schema existente da compra, inclusive categoria textual, `double precision`, instante e tamanhos de colunas. O modelo permanece sem nova migration.

Os testes de períodos verificam as consultas observáveis dos comandos: início UTC inclusivo, fim UTC exclusivo, último dia completo, viradas de mês/ano e ano bissexto. Um intervalo explícito em fevereiro de 2019 cobre um dia de 25 horas em São Paulo e verifica que o fim é calculado no calendário local antes da conversão. Também cobrem a escolha do ciclo pela data da mensagem em `/compra` e `/listar`, por `TimeProvider` em `/limite`, os totais de limite/alerta, as datas inclusivas das respostas, a ordenação decrescente e a exibição vigente dos instantes. `CompraRepositoryTests` exercita o filtro e a ordenação da implementação real com EF InMemory nas fronteiras do intervalo.

## Limites dos testes de integração

Os testes de integração substituem o `FinanceBotDbContext` de produção pelo provedor EF Core InMemory. Eles não exercitam PostgreSQL, Npgsql ou migrations. Embora o projeto declare o pacote `Testcontainers.PostgreSql`, os testes atuais não criam um banco PostgreSQL com Testcontainers.

As chamadas de saída ao Telegram são simuladas; os testes não precisam de token real, rede externa nem chat do bot.

As fixtures fornecem token, secret e chats explicitamente, sem depender de credenciais locais. Os testes HTTP cobrem header correto, ausente, incorreto, múltiplo e agregado; recusa não persiste nem envia. Cobrem também updates autenticados ignorados, chat não autorizado, HTTP 200 após falha de envio de uma compra já gravada e propagação de exceção na execução do comando.

Os testes de opções exercitam caracteres e comprimentos do secret, token vazio, secret igual ao token e lista vazia. Os testes de startup verificam que opções inválidas falham antes de resolver o DbContext. Testes de logs usam o registro de HttpClient de produção em Trace e credenciais, mensagem, corpo HTTP e exceções fictícios identificáveis, cobrindo sucesso, erro HTTP e transporte; o endpoint também é verificado com mensagem e exceção interna contendo dados sensíveis. Os testes não habilitam logs sensíveis do EF.
