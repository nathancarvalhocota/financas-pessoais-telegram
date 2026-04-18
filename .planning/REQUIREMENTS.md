# Requirements: FinanceBot — Limites mensais por categoria

**Defined:** 2026-04-18
**Core Value:** O usuário consegue registrar e consultar seus gastos sem sair do Telegram.

## v1 Requirements

### Limites

- [ ] **LIMITE-01**: Usuário define ou altera limite mensal de uma categoria com `/limite <CATEGORIA> <VALOR>` (valor > 0)
- [ ] **LIMITE-02**: Usuário remove limite de uma categoria com `/limite <CATEGORIA> 0`
- [ ] **LIMITE-03**: Após registrar uma compra, o bot exibe progresso de gastos da categoria no mês corrente (ex: "Mercado: R$ 340,00 de R$ 800,00 (42%)") quando há limite definido
- [ ] **LIMITE-04**: Quando gasto acumulado do mês supera o limite, bot emite alerta explícito (ex: "⚠ Limite de Mercado ultrapassado: R$ 820,00 de R$ 800,00")
- [ ] **LIMITE-05**: Limite persiste entre meses; o progresso é recalculado a partir dos gastos do mês corrente, sem necessidade de re-cadastro

## Out of Scope

| Feature | Reason |
|---------|--------|
| Reset manual de limite por mês | Limite é contínuo; gastos são recalculados por período automaticamente |
| Histórico de valores de limite anteriores | Não agrega valor para uso pessoal |
| Limite em percentual (ao invés de valor fixo) | Valor absoluto é mais direto para o usuário |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| LIMITE-01 | Phase 1 | Pending |
| LIMITE-02 | Phase 1 | Pending |
| LIMITE-03 | Phase 1 | Pending |
| LIMITE-04 | Phase 1 | Pending |
| LIMITE-05 | Phase 1 | Pending |

**Coverage:**
- v1 requirements: 5 total
- Mapped to phases: 5
- Unmapped: 0 ✓

---
*Requirements defined: 2026-04-18*
*Last updated: 2026-04-18 after GSD bootstrap*
