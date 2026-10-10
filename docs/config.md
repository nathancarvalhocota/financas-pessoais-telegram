# Configuração e desenvolvimento local

Este guia mostra como configurar e executar a API FinanceBot localmente e como expor o webhook do Telegram durante o desenvolvimento.

## Pré-requisitos

- .NET 8 SDK
- PostgreSQL
- Um bot criado com o `@BotFather` no Telegram
- `cloudflared` ou `ngrok` para expor a API local ao Telegram

O perfil local da API usa `http://localhost:5000` e `https://localhost:5001` e define os ambientes `Development` (`src/FinanceBot.Api/Properties/launchSettings.json`).

## Configurar credenciais

A API carrega User Secrets no ambiente de desenvolvimento. Configure os valores no projeto da API; não coloque credenciais em arquivos versionados.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=financebot;Username=postgres;Password=SUA_SENHA" --project src/FinanceBot.Api
dotnet user-secrets set "Telegram:BotToken" "SEU_TOKEN" --project src/FinanceBot.Api
dotnet user-secrets set "Telegram:AllowedChatIds:0" "SEU_CHAT_ID" --project src/FinanceBot.Api
```

Para permitir mais de um chat, configure índices adicionais: `Telegram:AllowedChatIds:1`, `Telegram:AllowedChatIds:2` e assim por diante. O webhook ignora mensagens de chats que não estejam nessa lista.

O token é obtido com o `@BotFather`. O chat ID deve ser o identificador numérico do chat autorizado. Para conferir os secrets cadastrados, use `dotnet user-secrets list --project src/FinanceBot.Api`; não compartilhe a saída, pois ela contém credenciais.

A aplicação exige `ConnectionStrings:DefaultConnection`. O PostgreSQL precisa estar acessível antes de iniciar a API.

## Executar a API

Na raiz do repositório:

```powershell
dotnet run --project src/FinanceBot.Api
```

Durante a inicialização, a API aplica migrations automaticamente quando o banco é relacional. O endpoint de saúde local fica em `http://localhost:5000/health`.

## Expor o webhook localmente

O Telegram precisa acessar uma URL pública HTTPS. Mantenha a API em execução e abra outro terminal.

Com Cloudflare Tunnel:

```powershell
cloudflared tunnel --url http://localhost:5000
```

Ou, com ngrok já instalado e autenticado:

```powershell
ngrok http 5000
```

Copie a URL HTTPS gerada pelo túnel. Túneis temporários podem gerar uma URL diferente ao reiniciar.

## Registrar e conferir o webhook

No BotFather, crie o bot e use `/setcommands` para exibir os comandos disponíveis na interface do Telegram. O menu corresponde ao router atual:

```text
start - Iniciar o bot
compra - Registrar uma despesa
listar - Consultar despesas
deletar - Remover uma despesa pelo ID
limite - Consultar ou definir limite por categoria
info - Exibir ajuda
```

Consulte [commands.md](commands.md) para formatos e exemplos.

Registre o webhook, substituindo os marcadores pelos valores locais:

```text
https://api.telegram.org/bot<SEU_TOKEN>/setWebhook?url=https://<SUA_URL_PUBLICA>/telegram/webhook
```

O caminho final precisa ser `/telegram/webhook`. Para conferir o registro e a fila de atualizações:

```text
https://api.telegram.org/bot<SEU_TOKEN>/getWebhookInfo
```

Envie `/start` ao bot e confira a resposta e os logs da API.

## Diagnóstico

- **`sendMessage` responde 404:** confira se o token está correto e foi salvo sem o prefixo `bot`.
- **Webhook retorna 502 ou 503:** confirme que a API está rodando na porta 5000, que o túnel aponta para essa porta e que a URL registrada ainda está atual.
- **O bot ignora mensagens:** confira se o chat de origem está incluído em `Telegram:AllowedChatIds` e se o perfil está em `Development`.
- **Telegram entrega a mesma atualização novamente:** confira `getWebhookInfo` e os logs. O Telegram pode tentar novamente quando não recebe uma confirmação de sucesso a tempo; falhas de roteamento ou persistência também devem ser investigadas.
