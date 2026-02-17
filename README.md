# FinanceBot (Telegram) – Controle de Gastos por Comandos

## Contexto e Objetivo

Este projeto é um **bot do Telegram** com backend em **C#/.NET** e persistência em **PostgreSQL**, criado para controle financeiro pessoal (uso individual) com **alta assertividade de categorização**.

A motivação principal é que **extratos/faturas (merchant)** não capturam o **propósito real** de uma compra. Ex.: no mesmo supermercado é possível comprar "compras do mês" (Mercado) e "bebidas para festa" (Lazer). Para obter análises confiáveis, o sistema prioriza **entrada manual explícita** via comandos.

O Telegram é usado como "front-end" (CLI):
- O usuário digita comandos predefinidos (sugeridos ao digitar `/`).
- O Telegram envia o texto para o webhook.
- A API valida, interpreta, persiste e responde com feedback.

> Importante: o Telegram **não valida parâmetros**; toda validação e tratamento de erro acontecem na API.

---

## Como o programa funciona (alto nível)

1. **Usuário envia um comando** no chat do bot (ex.: `/compra ...`).
2. O Telegram envia um **Update** (JSON) para o **Webhook HTTPS** configurado.
3. A API:
   - identifica o comando
   - parseia argumentos
   - valida regras de negócio
   - grava no Postgres
   - retorna uma mensagem de sucesso/erro para o chat
4. Para análises completas, a API consulta o banco, agrega dados e responde com o resultado. Futuramente será incluída análise de IA local.

---

## Requisitos e Stack

- **.NET 8+**
- **ASP.NET Core Minimal API** (ou Controllers, se preferir)
- **Telegram Bot API** (via biblioteca `Telegram.Bot` ou HTTP direto)
- **PostgreSQL**

---

## Comandos atuais (MVP)

### 1) `/compra` – Registrar nova compra
Registra uma nova despesa do mês com base na data/hora da mensagem.

**Formato:**
```
/compra <VALOR>, <DESCRICAO>, <CATEGORIA>
```

**Exemplos:**
```
/compra 58,90, Almoço da semana, Mercado
/compra 42.00, Bebidas aniversário, Lazer/Festa
/compra 20, Hamburguer, Restaurante/Lanches
```

**Regras:**
- `<VALOR>` obrigatório; parse flexível para `,` ou `.` (armazenar como `numeric(12,2)`).
- `<DESCRICAO>` obrigatório (string curta).
- `<CATEGORIA>` obrigatória; deve existir na tabela `categories`.
- Data da compra:
  - usar **timestamp do Telegram** (`message.date`) convertido para o fuso configurado (ex.: `America/Sao_Paulo`)
  - armazenar no banco em UTC (`timestamptz`)

**Resposta esperada (sucesso):**
- confirmação do registro com resumo do que foi salvo.

**Resposta esperada (erro):**
- mensagem objetiva com "uso correto" + exemplos.

---

### 2) `/listar` – Listar compras de um mês
Lista as compras de um mês específico informado como `MM/YY`.

**Formato:**
```
/listar <MES/ANO>
```

**Exemplos:**
```
/listar 09/26
/listar 01/27
```

**Regras:**
- `MES/ANO` obrigatório no formato `MM/YY`.
- Se o usuário não informar parâmetro, assume o mês atual.
- Retorno ordenado por data (mais recente primeiro).
- Cada item exibe o `id` da compra (necessário para `/deletar`).

**Resposta esperada:**
- lista com `id`, `data`, `valor`, `descricao`, `categoria`
- total do mês ao final.

---

### 3) `/deletar` – Remover uma compra
Remove uma compra registrada por engano, informando o `id` exibido no `/listar`.

**Formato:**
```
/deletar <ID>
```

**Exemplos:**
```
/deletar 42
```

**Regras:**
- `<ID>` obrigatório; deve corresponder a uma compra existente.
- Responde com confirmação dos dados deletados (valor, descrição, categoria) para evitar remoções acidentais.

**Resposta esperada (sucesso):**
- confirmação com resumo da compra removida.

**Resposta esperada (erro):**
- mensagem informando que o ID não foi encontrado.

---

### 4) `/analise` – Análise completa do mês
Gera agregações e insights do mês informado como `MM/YY`. Os números são sempre calculados ao vivo a partir das compras — sem cache.

**Formato:**
```
/analise <MES/ANO>
```

**Exemplos:**
```
/analise 01/27
```

**Saídas esperadas (determinísticas):**
- total do mês
- total por categoria (R$ e %)
- maiores gastos (top N)
- comparação com mês anterior (R$ e %), se houver dados

**IA (futuro):**
- o sistema pode gerar um texto narrativo a partir das métricas já calculadas
- princípio: **números sempre determinísticos**; IA só redige e sugere insights
- sugestões para o mês seguinte (ex.: "Limite os gastos em lazer para no máximo R$ 200...")
- **não bloquear o webhook**: operações de IA devem rodar em background e a resposta ser enviada via `sendMessage` assíncrono

---

## Dados e Persistência (PostgreSQL)

### Entidades

#### `categorias`
- `id` (PK, SERIAL)
- `nome` (`varchar(100)`, UNIQUE)

**Valores pré-populados:**
Educação, Lazer / Festa, Restaurante / Lanche, Uber, Mercado, Moto, Compras, Outros, Estética, Limpeza, Saúde e Farmácia.

#### `compras`
- `id` (PK, SERIAL)
- `data` (`timestamptz`) — data/hora do Telegram, armazenada em UTC
- `valor` (`numeric(12,2)`)
- `descricao` (`varchar(255)`)
- `categoria_id` (`int`, FK → `categorias.id`)

```sql
CREATE TABLE categorias (
    id   SERIAL PRIMARY KEY,
    nome VARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE compras (
    id          SERIAL PRIMARY KEY,
    data  TIMESTAMPTZ NOT NULL,
    valor       NUMERIC(12,2) NOT NULL,
    descricao   VARCHAR(255) NOT NULL,
    categoria_id INT NOT NULL REFERENCES categorias(id)
);
```

> **Nota:** análises são sempre geradas sob demanda via queries de agregação. Não há tabela de cache de análises — os dados brutos em `compras` são a única fonte de verdade.

---

## Webhook do Telegram

### Configuração do bot (BotFather)
- Criar bot via `@BotFather` → obter `BOT_TOKEN`.
- Definir comandos via `/setcommands`:
  ```
  compra  - Registrar nova compra (VALOR, DESCRICAO, CATEGORIA)
  listar  - Listar compras do mês (MM/YY → 09/26)
  deletar - Remover uma compra (ID)
  analise - Análise completa dos gastos do mês (MM/YY → 01/27)
  ```

### Configurar webhook
```
https://api.telegram.org/bot<BOT_TOKEN>/setWebhook?url=https://<SEU_DOMINIO>/telegram/webhook
```

Endpoint sugerido: `POST /telegram/webhook`

### Tratamento de Updates
A API deve lidar com:
- `message.text` contendo comandos (`/compra`, `/listar`, `/deletar`, `/analise`)
- extrair `chat.id` para responder
- usar `message.date` como timestamp de referência

**Regra de performance:**
- responder `200 OK` rapidamente
- operações longas (ex.: IA) devem rodar fora do caminho crítico (background)

---

## Regras de Negócio e Validação

- **Valor:**
  - aceitar `,` e `.` como separador decimal
  - rejeitar valores negativos em `/compra` (créditos podem ser um comando futuro `/credito`)
- **Categoria:**
  - deve existir na tabela `categories`
  - ignorar acentos, diferenças entre maiúsculo/minúsculo e caracteres especiais (ex.: `ç`)
  - em caso de erro, responder com a lista de categorias válidas
- **Data:**
  - usar `message.date` (UTC) e converter para o fuso configurado para calcular mês de referência
- **Consistência:**
  - sempre armazenar `timestamptz` em UTC

---

## Estrutura de Projeto (sugestão)

```
/
├─ README.md
├─ src/
│  ├─ FinanceBot.Api/               # Webhook + endpoints + DI
│  ├─ FinanceBot.Domain/            # Entidades, enums, regras
│  ├─ FinanceBot.Application/       # Use cases: registrar/listar/deletar/analisar
│  └─ FinanceBot.Infrastructure/    # EF Core, repos, Postgres, Telegram client
├─ tests/
└─ docker-compose.yml               # Postgres + app (opcional)
```

---

## Fluxos (exemplos)

### Registrar compra
1. Usuário: `/compra 58,90, Almoço da semana, Mercado`
2. API:
   - parse: `valor = 58.90`, `descricao = Almoço da semana`, `categoria = Mercado`
   - timestamp: `message.date`
   - insert em `purchases`
3. Bot responde: `✅ Compra registrada: R$ 58,90 – Mercado`

### Listar
1. Usuário: `/listar 09/26`
2. API:
   - converte `09/26` para intervalo `[2026-09-01, 2026-09-30)`
   - query em `purchases` com JOIN em `categories`
3. Bot responde com lista (incluindo `id`) + total.

### Deletar
1. Usuário: `/deletar 42`
2. API:
   - busca `purchases` onde `id = 42`
   - deleta e retorna resumo da compra removida
3. Bot responde: `🗑️ Compra removida: R$ 58,90 – Almoço da semana (Mercado)`

### Análise
1. Usuário: `/analise 01/27`
2. API:
   - agrega `purchases` do mês por categoria
   - compara com mês anterior (queries ao vivo)
   - (futuro) envia métricas para IA gerar narrativa em background
3. Bot responde com resumo estruturado.

---

## Roadmap sugerido

1. Fundação: solution, docker-compose, entidades do Domain, DbContext + migrations
2. Webhook funcional: `POST /telegram/webhook`, roteador de comandos, teste com ngrok
3. `/compra` completo: parse + validação + insert
4. `/listar` com total e exibição de IDs
5. `/deletar` com confirmação
6. `/analise` com agregações por categoria e comparação com mês anterior
7. (Futuro) texto narrativo via IA local / Ollama em background

---

## Regras práticas finais

- **Telegram é UI "burra"**: tudo que importa (parse, validação, regras) fica na API.
- **Armazene em Postgres (relacional)**: consultas e agregações mensais são naturais em SQL.
- **Use timestamp do Telegram** para data/hora; armazene em UTC e converta para o fuso do sistema nos relatórios.
- **Não bloqueie o webhook** com trabalho pesado (IA/relatórios complexos). Se precisar, mova para background.
- **Análises são sempre ao vivo**: os números vêm de queries sobre `purchases` — nunca de cache.
