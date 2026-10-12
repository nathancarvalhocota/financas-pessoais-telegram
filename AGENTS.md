# FinanceBot — contexto para agentes

## Projeto

Bot pessoal do Telegram para registrar e consultar despesas. O webhook recebe os comandos, a aplicação valida e executa as operações, e as respostas vão aos chats autorizados.

## Stack

- .NET 8 e ASP.NET Core Minimal API
- PostgreSQL com Entity Framework Core e Npgsql
- Telegram Bot API via biblioteca `Telegram.Bot`

## Estrutura

```text
src/
  FinanceBot.Api/            # Endpoints, Messaging, Configuration e Commands
  FinanceBot.Domain/         # Entidades, regras financeiras e contratos de repositório
  FinanceBot.Infrastructure/ # Persistência e repositórios
tests/
  FinanceBot.UnitTests/
  FinanceBot.IntegrationTests/
```

## Referências por tarefa

- Ao alterar formato ou comportamento de comando, consulte [`docs/commands.md`](docs/commands.md).
- Ao alterar ciclo, fuso, categoria, limite ou alerta, consulte [`docs/domain.md`](docs/domain.md).
- Ao alterar dependências entre projetos ou fluxo do webhook, consulte [`docs/architecture.md`](docs/architecture.md).
- Ao escrever ou mudar testes, consulte [`docs/testing.md`](docs/testing.md).
- Ao configurar a execução local, credenciais, banco ou webhook, consulte [`docs/config.md`](docs/config.md).

## Regras do projeto

- Mantenha credenciais fora de arquivos versionados; use User Secrets ou variáveis de ambiente.
- Ao adicionar, remover ou alterar comandos, atualize `docs/commands.md`, a lista usada em `/setcommands` em `docs/config.md` e a resposta de `/info` para refletir os comandos implementados em `TelegramCommandRouter`.
- Trate o comportamento implementado no código e nos testes como referência atual; atualize a documentação afetada junto com a mudança.
