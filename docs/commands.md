# Comandos do bot

Os comandos abaixo são processados pela API. As categorias aceitas são `educacao`, `lazer`, `lanches`, `uber`, `mercado`, `moto`, `compras`, `outros`, `estetica`, `limpeza` e `saude`. A busca ignora acentos, maiúsculas, espaços e pontuação; categorias compostas não são aceitas.

## `/compra` — registrar uma compra

```text
/compra <VALOR>, <DESCRICAO>, <CATEGORIA>
```

Exemplo:

```text
/compra 58,90, Almoço da semana, Mercado
```

Valor, descrição e categoria são obrigatórios. Use valor positivo com até duas casas decimais; `,` e `.` podem separar os centavos. A categoria deve corresponder a uma das categorias válidas acima. A API usa a data da mensagem recebida pelo Telegram.

Se houver limite configurado para a categoria, a resposta informa o total do ciclo e o progresso do limite. A resposta também inclui um alerta de ritmo quando o total do ciclo atual cruza um dos limiares comparados ao ciclo anterior completo.

## `/listar` — consultar compras

```text
/listar [<MM/YY> | <DD/MM/AA> - <DD/MM/AAAA>] [<CATEGORIAS> | sem <CATEGORIAS>]
```

Exemplos:

```text
/listar
/listar 04/26
/listar 10/02/26 - 20/02/2026
/listar 10/02/26 - 20/02/2026 Mercado, Uber
/listar 10/02/26 - 20/02/2026 sem lazer
```

Sem período, o comando usa o ciclo correspondente à data da mensagem. `MM/YY` seleciona o ciclo que começa no dia 15 daquele mês e termina no dia 14 do seguinte (por exemplo, `04/26` cobre 15/04/2026 a 14/05/2026). Datas explícitas aceitam ano com dois ou quatro dígitos e incluem os dois dias informados.

O filtro final pode incluir uma ou mais categorias separadas por vírgula ou excluí-las com `sem`. Os resultados mostram ID, categoria, valor, descrição e data, em ordem decrescente de data, seguidos do total. Use o ID para excluir uma compra.

## `/deletar` — remover uma compra

```text
/deletar <ID>
```

Exemplo:

```text
/deletar 42
```

O bot confirma os dados da compra removida ou informa quando o ID não foi encontrado.

## `/limite` — consultar ou definir limite por categoria

Consultar um limite:

```text
/limite <CATEGORIA>
```

Definir ou atualizar:

```text
/limite <CATEGORIA> <VALOR>
```

Exemplos:

```text
/limite Mercado
/limite Mercado 800
/limite Mercado 0
```

Um valor maior que zero define ou atualiza o limite. O valor `0` remove o limite. A consulta mostra o total gasto no ciclo atual e o percentual utilizado. Após registrar uma compra, o progresso do limite é incluído na resposta quando existe um limite para aquela categoria.

## `/start` e `/info`

`/start` envia uma mensagem inicial. `/info` apresenta a ajuda dos comandos implementados pelo bot.
