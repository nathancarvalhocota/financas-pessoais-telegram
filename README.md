# FinanceBot — controle financeiro pelo Telegram

Bot pessoal para registrar despesas pelo Telegram e consultá-las em um PostgreSQL.

## Motivação

Extratos e faturas mostram o estabelecimento, mas não necessariamente o propósito real de uma compra. No mesmo supermercado, por exemplo, é possível comprar itens do mês (Mercado) ou bebidas para uma festa (Lazer). Por isso, o projeto prioriza o registro manual por comandos, para que cada compra seja categorizada de acordo com sua finalidade.

## Como o programa funciona

1. O usuário envia um comando no chat do bot, como `/compra`.
2. O Telegram envia uma atualização (Update) em JSON ao webhook HTTPS.
3. A API identifica o comando, interpreta e valida os argumentos, executa a operação e envia uma resposta ao chat autorizado.
4. Futuramente, a API poderá consultar e agregar as compras para gerar análises. A IA poderá ajudar a interpretar os registros e apresentar insights; essa funcionalidade ainda não está implementada.

## Tecnologias

- .NET 8 e ASP.NET Core Minimal API
- PostgreSQL com Entity Framework Core e Npgsql
- Telegram Bot API com a biblioteca `Telegram.Bot`

## Projetos

```text
src/
  FinanceBot.Api/            # Webhook, configuração e composição da aplicação
  FinanceBot.Application/    # Comandos e fluxos de aplicação
  FinanceBot.Domain/         # Entidades, categorias e regras financeiras
  FinanceBot.Infrastructure/ # Persistência e repositórios PostgreSQL
tests/
  FinanceBot.UnitTests/
  FinanceBot.IntegrationTests/
```

## Documentação

- [Comandos do bot](docs/commands.md): formatos, filtros e exemplos de uso.
- [Configuração e desenvolvimento local](docs/config.md): credenciais, banco, execução e webhook.
- [Arquitetura](docs/architecture.md): projetos, dependências e fluxo de uma mensagem.
- [Regras de domínio](docs/domain.md): ciclos, datas, categorias, limites e alertas.
- [Testes](docs/testing.md): comandos, escopo atual e limites dos testes.
