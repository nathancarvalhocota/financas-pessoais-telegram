# Roadmap: FinanceBot

## Overview

MVP já entregue (comandos /compra, /listar, /deletar, /analise, /info). Este roadmap cobre o primeiro milestone pós-MVP: implementação de limites mensais de gastos por categoria.

## Phases

- [ ] **Phase 1: Limites mensais por categoria** — Novo comando /limite + feedback de progresso no /compra

## Phase Details

### Phase 1: Limites mensais por categoria

**Goal**: Usuário consegue definir limites mensais por categoria e recebe feedback de progresso/alerta após cada /compra
**Depends on**: Nothing (MVP já existe)
**Requirements**: LIMITE-01, LIMITE-02, LIMITE-03, LIMITE-04, LIMITE-05
**Success Criteria** (what must be TRUE):
  1. `/limite Mercado 800` persiste R$ 800,00 como limite para Mercado no banco; `/limite Mercado 0` remove o registro
  2. Após `/compra 50, Pão, Mercado`, resposta inclui linha de progresso "Mercado: R$ 50,00 de R$ 800,00 (6%)" quando limite existe para a categoria
  3. Quando soma de compras da categoria no mês corrente supera o limite, resposta inclui linha de alerta em vez da linha de progresso
  4. Em mês seguinte ao cadastro do limite, progresso é calculado do zero (apenas gastos do mês corrente contam)
  5. Categoria sem limite definida não exibe linha de progresso — comportamento do /compra original é preservado
**Plans**: 2 plans

Plans:
- [ ] 01-01-PLAN.md — Data layer: LimiteCategoria entity, ILimiteCategoriaRepository, EF Core config e migration
- [ ] 01-02-PLAN.md — Command handling: handler /limite, progresso no /compra, testes unitários

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Limites mensais por categoria | 0/2 | Not started | - |
