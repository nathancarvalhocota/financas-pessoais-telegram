# FinanceBot

## What This Is

Bot do Telegram para controle financeiro pessoal. O usuário envia comandos via chat, o Telegram entrega via webhook para uma API .NET 8, que valida, persiste no PostgreSQL e responde com texto formatado. Uso individual — sem multi-usuário.

## Core Value

O usuário consegue registrar e consultar seus gastos sem sair do Telegram.

## Requirements

### Validated

- ✓ Usuário registra compra com `/compra <VALOR>, <DESCRICAO>, <CATEGORIA>` — MVP
- ✓ Usuário lista compras de um mês com `/listar <MM/YY>` — MVP
- ✓ Usuário remove compra pelo ID com `/deletar <ID>` — MVP
- ✓ Usuário vê análise por categoria com `/analise <MM/YY>` — MVP
- ✓ Usuário consulta comandos disponíveis com `/info` — MVP

### Active

- [ ] Usuário define limite mensal por categoria com `/limite <CATEGORIA> <VALOR>`
- [ ] Após cada `/compra`, bot exibe progresso de gastos da categoria no mês corrente
- [ ] Bot emite alerta quando limite da categoria é ultrapassado
- [ ] Limite persiste entre meses; gastos acumulados são recalculados a cada mês

### Out of Scope

- Multi-usuário — bot é pessoal, sem isolamento por chat_id
- Limite global (sem categoria) — por categoria é suficiente para o caso de uso
- Histórico de alterações de limites — apenas o valor atual importa
- Notificação proativa (sem polling) — alertas são apenas no momento da /compra

## Context

Stack: .NET 8, ASP.NET Core Minimal API, PostgreSQL, EF Core (Npgsql), Telegram.Bot.

Estrutura em camadas:
- `FinanceBot.Api` — webhook endpoint, Program.cs, DI root
- `FinanceBot.Application` — interfaces (ICompraRepository, ITelegramCommandRouter) e TelegramCommandRouter
- `FinanceBot.Domain` — entidade Compra, enum Categoria
- `FinanceBot.Infrastructure` — FinanceBotDbContext, CompraRepository, TelegramMessageSender, migrations

Padrão estabelecido: interface em Application.Abstractions, entidade em Domain.Entities, implementação e configuração EF em Infrastructure, registro via InfrastructureServiceCollectionExtensions.

## Constraints

- **Tech stack**: .NET 8 + EF Core + PostgreSQL — sem trocar ORM nem banco
- **Credenciais**: user-secrets apenas, nunca em appsettings
- **Estilo**: código existente segue conventions do projeto — manter consistência

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Limite persiste, gasto é calculado on-the-fly | Evita lógica de reset periódico; `ListByPeriodAsync` já existe para somar gastos do mês | — Pending |
| Nova entidade `LimiteCategoria` em tabela separada | Não misturar com `Compra`; categoria tem no máximo um limite | — Pending |

---
*Last updated: 2026-04-18 after GSD bootstrap*
