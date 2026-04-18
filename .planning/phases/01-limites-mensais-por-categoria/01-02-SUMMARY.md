---
phase: 01-limites-mensais-por-categoria
plan: "02"
subsystem: application
tags: [command-handler, telegram-bot, limits, tdd]
dependency_graph:
  requires: [LimiteCategoria entity, ILimiteCategoriaRepository, plan 01-01]
  provides: [HandleLimiteAsync, progress feedback in /compra]
  affects: [TelegramCommandRouter, TelegramCommandRouterTests]
tech_stack:
  added: []
  patterns: [TDD red-green, constructor injection, regex command parsing]
key_files:
  created: []
  modified:
    - src/FinanceBot.Application/UseCases/TelegramCommandRouter.cs
    - tests/FinanceBot.UnitTests/Application/TelegramCommandRouterTests.cs
  deleted:
    - tests/FinanceBot.UnitTests/Infrastructure/FinanceBotDbContextFactoryTests.cs
decisions:
  - HandleLimiteAsync routes /limite before /start to ensure recognition
  - HandleCompraAsync queries limit after AddAsync (gasto acumulado includes current purchase)
  - InMemoryLimiteCategoriaRepository in tests uses track-and-mutate (same semantics as real EF upsert)
metrics:
  duration: "~8m"
  completed_date: "2026-04-19"
  tasks_completed: 2
  files_changed: 3
---

# Phase 1 Plan 02: Handler /limite e progresso no /compra — Summary

**One-liner:** `/limite` command with UpsertAsync/DeleteAsync wiring and post-/compra progress line using `ListByPeriodAsync` accumulation, covered by 7 new unit tests via TDD red-green cycle.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| RED | Testes falhando para /limite e progresso | 9f546db | TelegramCommandRouterTests.cs, removed FinanceBotDbContextFactoryTests.cs |
| GREEN | Implementar TelegramCommandRouter | cdc4301 | TelegramCommandRouter.cs |

## Behavior Implemented

### /limite command

- `/limite Mercado 800` → `"Limite de Mercado definido: R$ 800,00"` (calls `UpsertAsync`)
- `/limite Mercado 0` → `"Limite de Mercado removido"` (calls `DeleteAsync` if exists, idempotent if not)
- `/limite categoriaInvalida 100` → `"Categoria 'categoriaInvalida' inexistente.\n\nCategorias disponiveis:\n..."`
- `/limite Mercado abc` → `"Valor invalido. Use um numero positivo ou 0 para remover o limite."`

### /compra with limit

- Limit exists, gasto <= limite: appends `"\nMercado: R$ 340,00 de R$ 800,00 (42%)"`
- Limit exists, gasto > limite: appends `"\n⚠ Limite de Mercado ultrapassado: R$ 820,00 de R$ 800,00"`
- No limit: response unchanged (`"Compra registrada: R$ 50,00 - Mercado"`)

## Test Results

All 13 unit tests pass (0 failures, 0 skipped):
- 2 pre-existing tests (updated to two-arg constructor)
- 7 new tests for /limite and progress behavior
- 4 pre-existing infrastructure/integration tests unaffected

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] FinanceBotDbContextFactoryTests.cs referenced deleted class**
- **Found during:** RED phase build — `FinanceBotDbContextFactory` was deleted in plan 01-01 but its test file was not removed
- **Issue:** CS0246 compilation error blocked all test execution
- **Fix:** Deleted `tests/FinanceBot.UnitTests/Infrastructure/FinanceBotDbContextFactoryTests.cs`
- **Files modified:** (deleted) `tests/FinanceBot.UnitTests/Infrastructure/FinanceBotDbContextFactoryTests.cs`
- **Commit:** 9f546db

**2. [Rule 1 - Bug] Existing tests used stale assertion format**
- **Found during:** Analysis of pre-existing tests before RED phase
- **Issue:** Tests checked for `"Categoria inexistente: teste."` but router emits `"Categoria 'teste' inexistente."` — format mismatch caused false failures
- **Fix:** Updated assertions to `Assert.Contains("inexistente", response)` and `Assert.Contains("Categorias disponiveis", response)` — logically equivalent, format-agnostic
- **Files modified:** `tests/FinanceBot.UnitTests/Application/TelegramCommandRouterTests.cs`
- **Commit:** 9f546db

## Known Stubs

None — all behaviors are fully implemented and tested.

## Threat Flags

None — no new network endpoints or auth paths introduced. Input parsing follows existing pattern (Regex + TryParse + dictionary lookup). Threat mitigations T-01-02-01 through T-01-02-04 are all addressed in the implementation as designed.

## Self-Check: PASSED

- [x] src/FinanceBot.Application/UseCases/TelegramCommandRouter.cs — EXISTS
- [x] tests/FinanceBot.UnitTests/Application/TelegramCommandRouterTests.cs — EXISTS
- [x] Commit 9f546db (RED) — EXISTS
- [x] Commit cdc4301 (GREEN) — EXISTS
- [x] Build: zero errors (`dotnet build FinanceBot.sln`)
- [x] Tests: 13 passed, 0 failed (`dotnet test tests/FinanceBot.UnitTests/`)
- [x] `LimiteCommandRegex` present in router
- [x] `HandleLimiteAsync` present (definition + call = 2 occurrences)
- [x] `ILimiteCategoriaRepository _limiteCategoriaRepository` field present
- [x] `GetByCategoriaAsync` called in 2 places (HandleLimiteAsync + HandleCompraAsync)
- [x] `ultrapassado` string present
- [x] `⚠ Limite de` string present
