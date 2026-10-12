# Regras de domínio

Este documento reúne as regras financeiras compartilhadas pelos comandos. Formatos e exemplos de uso ficam em [`commands.md`](commands.md).

## Compra e categoria

Uma compra (`Compra`) registra valor, descrição, categoria e data. A API usa `message.date` como instante da compra; quando a data não vem no update, usa a hora UTC atual. O instante é persistido em UTC.

As categorias válidas são `Educacao`, `Lazer`, `Lanches`, `Uber`, `Mercado`, `Moto`, `Compras`, `Outros`, `Estetica`, `Limpeza` e `Saude`. O código normaliza o texto de entrada removendo acentos, espaços, pontuação e diferenças entre maiúsculas e minúsculas. Essa normalização não cria sinônimos para as categorias.

## Ciclo de faturamento e datas

O ciclo começa no dia 15 e termina no dia 14 do mês seguinte. A regra usa a data de negócio no fuso `America/Sao_Paulo`.

Em `/listar MM/YY`, `MM/YY` indica o mês em que o ciclo começa: `/listar 04/26` corresponde a 15/04/2026 até 14/05/2026. Sem período explícito, `/listar` usa a data da mensagem para escolher o ciclo. Em `/limite`, o ciclo atual é calculado a partir do relógio da aplicação.

Filtros com datas explícitas aceitam os dois dias informados. As consultas usam o intervalo UTC `[início, fim exclusivo)`: incluem compras com `Data >= início` e excluem aquelas com `Data >= fim exclusivo`. `DateTimeConverter` calcula o início do dia seguinte ao último dia no calendário de São Paulo e só então converte esse limite para UTC, respeitando mudanças históricas de offset. Não acrescenta um dia depois da conversão UTC.

No ciclo de 15/04 a 14/05, o fim exclusivo da consulta é o início de 15/05 em São Paulo. As datas apresentadas nos cabeçalhos e alertas continuam inclusivas; a ordenação decrescente e o fuso de exibição dos instantes das compras não mudam.

## Limites por categoria

Há no máximo um limite por categoria. Um valor positivo define ou atualiza o limite; `0` remove o existente. A consulta e o progresso após uma compra usam os gastos daquela categoria no ciclo corrente.

`LimiteCategoria.Criar` exige categoria definida no enum e valor positivo e finito; zero, negativos, `NaN` e infinitos retornam erro tipado de domínio. A entidade não tem construtor nem setters públicos. `AlterarValor` valida categoria e novo valor antes de alterar e retorna erro tipado em caso de falha, preservando o estado anterior. Materializar um registro legado não limpa ou transforma seus dados. O handler interpreta zero como remoção, sem criar uma entidade com esse valor; remover um limite inexistente preserva a confirmação. Ausência de limite em uma consulta é um estado normal.

O limite é ultrapassado quando o total gasto é maior que o limite. Atingir exatamente o valor definido ainda não conta como ultrapassagem.

`AvaliarProgresso(totalGasto)` é uma avaliação pura sobre o total fornecido, sem acesso a repositórios. Retorna categoria, limite, gasto, percentual inteiro e indicação de ultrapassagem. O percentual mantém `Math.Round(totalGasto / limite * 100)` com arredondamento de empate para o par. `/limite` e o registro de compra usam essa avaliação.

## Alertas de gasto

Após registrar uma compra, o código compara o total do ciclo atual com o total do ciclo anterior completo. Ele considera os limiares de 125%, 150% e 200%, e emite o nível mais alto cruzado por aquela compra. Sem gasto positivo no ciclo anterior, não há base para alerta.

Os alertas são calculados a partir dos totais antes e depois da compra. O sistema não persiste um estado de alertas já enviados.
