# FinanceBot – Context for Claude

## O que é este projeto
Bot do Telegram para controle financeiro pessoal. Uso individual. O usuário envia comandos via chat, o Telegram entrega via webhook para a API, que valida, persiste no PostgreSQL e responde.

## Stack
- .NET 8, ASP.NET Core Minimal API
- PostgreSQL + EF Core (Npgsql)
- Telegram Bot API (biblioteca `Telegram.Bot`)
- Credenciais via user-secrets (nunca em appsettings)

## Estrutura de projetos
```
src/
  FinanceBot.Api/            # Webhook endpoint, DI root, Program.cs
  FinanceBot.Application/    # Use cases: TelegramCommandRouter, interfaces
  FinanceBot.Domain/         # Entidades (Compra), enums (Categoria)
  FinanceBot.Infrastructure/ # EF Core, repositórios, TelegramMessageSender
tests/
  FinanceBot.UnitTests/
  FinanceBot.IntegrationTests/
```

## Comandos do bot
| Comando | Formato | Exemplo |
|---------|---------|---------|
| `/compra` | `/compra <VALOR>, <DESCRICAO>, <CATEGORIA>` | `/compra 58,90, Almoço, Mercado` |
| `/listar` | `/listar <MM/YY>` (mês atual se omitido) | `/listar 04/26` |
| `/deletar` | `/deletar <ID>` | `/deletar 42` |

## Categorias válidas (chave de lookup normalizada → display)
| Digitar | Exibe |
|---------|-------|
| `educacao` | Educacao |
| `lazer` | Lazer |
| `lanches` | Lanches |
| `uber` | Uber |
| `mercado` | Mercado |
| `moto` | Moto |
| `compras` | Compras |
| `outros` | Outros |
| `estetica` | Estetica |
| `limpeza` | Limpeza |
| `saude` | Saude |

A normalização remove acentos, espaços e maiúsculas — `Lázèr`, `LAZER`, `lazer` são equivalentes.

