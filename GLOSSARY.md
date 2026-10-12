# FinanceBot

Vocabulário do registro de despesas pessoais e da importação de notas por foto. Os termos de importação descrevem a funcionalidade em planejamento.

## Language

**Expense (despesa)**:
Um gasto registrado, com valor, descrição, categoria e data. Na importação de uma nota, cada linha aceita corresponde a uma despesa.
_Avoid_: Compra como nome de tipo no código novo.

**Invoice (nota)**:
Uma nota fiscal ou cupom que apresenta os produtos comprados e seus valores.
_Avoid_: Receipt nos nomes da funcionalidade.

**Importação de nota**:
Uma tentativa de registrar despesas a partir de uma foto enviada pelo usuário. Cada novo envio é uma importação independente, mesmo que retrate uma nota enviada anteriormente.

**Item da nota**:
Uma linha de produto da nota, que pode representar uma ou várias unidades do mesmo produto. Um item identificado na foto ainda não é uma despesa registrada.

**Valor da linha**:
O valor total referente à quantidade de produto representada por um item da nota.
_Avoid_: Preço unitário como sinônimo.

**Total da nota**:
O valor total da compra apresentado na nota, usado na conferência dos itens.
_Avoid_: Total registrado como sinônimo.

**Total registrado**:
A soma dos valores das despesas efetivamente salvas em uma importação de nota.
_Avoid_: Total extraído como sinônimo.
