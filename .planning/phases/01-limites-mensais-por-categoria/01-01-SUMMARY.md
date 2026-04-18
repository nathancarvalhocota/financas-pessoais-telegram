---
phase: 01-limites-mensais-por-categoria
plan: "01"
subsystem: persistence
tags: [efcore, migration, repository, domain-entity]
dependency_graph:
  requires: []
  provides: [LimiteCategoria entity, ILimiteCategoriaRepository, limite_categorias table]
  affects: [plan 01-02 command handling]
tech_stack:
  added: []
  patterns: [Repository pattern, EF Core fluent configuration, upsert via track-and-mutate]
key_files:
  created:
    - src/FinanceBot.Domain/Entities/LimiteCategoria.cs
    - src/FinanceBot.Application/Abstractions/ILimiteCategoriaRepository.cs
    - src/FinanceBot.Infrastructure/Repositories/LimiteCategoriaRepository.cs
    - src/FinanceBot.Infrastructure/Migrations/20260418231323_AddLimiteCategoria.cs
    - src/FinanceBot.Infrastructure/Migrations/20260418231323_AddLimiteCategoria.Designer.cs
  modified:
    - src/FinanceBot.Infrastructure/Persistence/FinanceBotDbContext.cs
    - src/FinanceBot.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
    - src/FinanceBot.Infrastructure/Migrations/FinanceBotDbContextModelSnapshot.cs
    - src/FinanceBot.Api/FinanceBot.Api.csproj
decisions:
  - Upsert implementado como read-then-mutate (sem AsNoTracking) para aproveitar change tracking do EF Core
  - GetByCategoriaAsync usa AsNoTracking pois é leitura pura (consulta de progresso pós /compra)
  - Microsoft.EntityFrameworkCore.Design adicionado ao Api.csproj para suporte ao dotnet ef CLI
metrics:
  duration: "3m 32s"
  completed_date: "2026-04-18"
  tasks_completed: 2
  files_changed: 9
---

# Phase 1 Plan 01: Camada de dados para limites por categoria — Summary

**One-liner:** EF Core entity + repository + migration para tabela `limite_categorias` com unique constraint em `categoria` e upsert via change tracking.

## Tasks Completed

| Task | Name | Commit | Key Files |
|------|------|--------|-----------|
| 1 | Criar entidade, interface e repositório | 48ee601 | LimiteCategoria.cs, ILimiteCategoriaRepository.cs, LimiteCategoriaRepository.cs |
| 2 | Configurar DbContext, registrar DI e aplicar migration | 50a47e6 | FinanceBotDbContext.cs, InfrastructureServiceCollectionExtensions.cs, migration |

## Interface Exported

```csharp
public interface ILimiteCategoriaRepository
{
    Task<LimiteCategoria?> GetByCategoriaAsync(Categoria categoria, CancellationToken cancellationToken);
    Task UpsertAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken);
    Task DeleteAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken);
}
```

## Migration Generated

File: `src/FinanceBot.Infrastructure/Migrations/20260418231323_AddLimiteCategoria.cs`

Creates table `limite_categorias` with:
- `id` integer, identity, primary key
- `categoria` varchar(50), not null
- `valor` double precision, not null
- Unique index `IX_limite_categorias_categoria` on `categoria`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Microsoft.EntityFrameworkCore.Design ausente no Api.csproj**
- **Found during:** Task 2 — ao executar `dotnet ef migrations add`
- **Issue:** `dotnet ef` requer `Microsoft.EntityFrameworkCore.Design` no startup project. O pacote estava apenas na Infrastructure com `PrivateAssets=all`, que impede transitividade.
- **Fix:** Adicionado `<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.4">` ao `FinanceBot.Api.csproj` com os mesmos atributos `PrivateAssets/IncludeAssets` (tooling-only, sem impacto em runtime).
- **Files modified:** `src/FinanceBot.Api/FinanceBot.Api.csproj`
- **Commit:** 50a47e6

## Known Stubs

None — este plan é puramente de infraestrutura de dados sem UI ou lógica de negócio.

## Threat Flags

None — sem novos endpoints ou surfaces de entrada. A migration é executada uma vez em deploy (aceito no threat model como T-01-01-03).

## Self-Check: PASSED

- [x] src/FinanceBot.Domain/Entities/LimiteCategoria.cs — EXISTS
- [x] src/FinanceBot.Application/Abstractions/ILimiteCategoriaRepository.cs — EXISTS
- [x] src/FinanceBot.Infrastructure/Repositories/LimiteCategoriaRepository.cs — EXISTS
- [x] src/FinanceBot.Infrastructure/Migrations/20260418231323_AddLimiteCategoria.cs — EXISTS
- [x] Commit 48ee601 — EXISTS
- [x] Commit 50a47e6 — EXISTS
- [x] Build: zero erros (dotnet build src/FinanceBot.Api/FinanceBot.Api.csproj)
- [x] Migration aplicada ao banco com sucesso
