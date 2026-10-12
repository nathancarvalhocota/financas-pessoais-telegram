# Configuração, desenvolvimento local e Railway

Este guia mostra como configurar e executar a API FinanceBot localmente e no Railway, registrar o webhook e diagnosticar falhas de entrega.

As instruções refletem o código atual. Os exemplos usam marcadores; mantenha valores reais nos User Secrets, nas variáveis do Railway ou em um gerenciador de segredos. Para a primeira publicação da versão que exige o secret, siga a ordem de ativação abaixo.

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
dotnet user-secrets set "Telegram:WebhookSecret" "<WEBHOOK_SECRET>" --project src/FinanceBot.Api
dotnet user-secrets set "Telegram:AllowedChatIds:0" "SEU_CHAT_ID" --project src/FinanceBot.Api
```

Para permitir mais de um chat, configure índices adicionais: `Telegram:AllowedChatIds:1`, `Telegram:AllowedChatIds:2` e assim por diante. O webhook ignora mensagens de chats que não estejam nessa lista.

O token é obtido com o `@BotFather`. O chat ID deve ser o identificador numérico do chat autorizado. Para conferir os secrets cadastrados, use `dotnet user-secrets list --project src/FinanceBot.Api`; não compartilhe a saída, pois ela contém credenciais.

A aplicação exige `ConnectionStrings:DefaultConnection`, `Telegram:BotToken` não vazio, `Telegram:WebhookSecret` válido e pelo menos um `Telegram:AllowedChatIds`. O secret deve ser diferente do token e conter de 1 a 256 letras ASCII, dígitos, `_` ou `-`. Veja a geração e entrada protegida adiante. A validação identifica as chaves sem imprimir valores e ocorre antes de acessar o banco; não consulta o Telegram. O PostgreSQL precisa estar acessível para concluir a inicialização.

A configuração usa os provedores padrão do ASP.NET Core: User Secrets apenas em Development e variáveis de ambiente com precedência sobre eles. Em produção, use as variáveis do serviço; não há carregamento adicional de User Secrets.

## Executar a API

Na raiz do repositório:

```powershell
dotnet run --project src/FinanceBot.Api
```

Durante cada inicialização, a API verifica as migrations quando o banco é relacional e aplica apenas as pendentes. As migrations já registradas no banco não são reaplicadas. Se o PostgreSQL responder `57P03` durante esse passo, a API faz até quatro tentativas, esperando 2, 4 e 8 segundos entre elas. São até 14 segundos de espera, além do tempo das operações. Outros erros falham imediatamente; se as tentativas se esgotarem, a API registra a falha e não termina de iniciar. O retry se limita ao startup e não exige novas variáveis ou secrets.

O endpoint de saúde local fica em `http://localhost:5000/health`. Ele retorna `ok` quando a API está atendendo, mas não testa a conexão com o banco a cada chamada.

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

O caminho final precisa ser `/telegram/webhook`. Use o exemplo PowerShell abaixo tanto para o túnel local quanto para o domínio público do Railway. Informe apenas a URL base HTTPS, sem esse caminho.

Informe o mesmo WebhookSecret configurado na API de destino. O token do bot e o WebhookSecret são credenciais distintas. Na ativação inicial em produção, registre o secret enquanto a versão antiga ainda está em execução, seguindo o roteiro abaixo.

As entradas protegidas evitam inserir credenciais como literais no histórico de comandos ou em URLs no navegador. Execute em uma sessão privada, sem registrar transcrições; os valores existirão em memória durante as chamadas.

```powershell
$telegramBotToken = [System.Net.NetworkCredential]::new(
    '', (Read-Host 'Token do bot' -AsSecureString)).Password
$telegramWebhookSecret = [System.Net.NetworkCredential]::new(
    '', (Read-Host 'WebhookSecret configurado na API' -AsSecureString)).Password
$financeBotPublicUrl = (Read-Host 'URL HTTPS publica, sem /telegram/webhook').Trim().TrimEnd('/')

try {
    if ($telegramWebhookSecret -cnotmatch '\A[A-Za-z0-9_-]{1,256}\z') {
        throw 'WebhookSecret invalido.'
    }
    $webhookParameters = @{
        url = "$financeBotPublicUrl/telegram/webhook"
        secret_token = $telegramWebhookSecret
    }

    $webhookRegistration = Invoke-RestMethod -Method Post `
        -Uri "https://api.telegram.org/bot$telegramBotToken/setWebhook" `
        -Body $webhookParameters -ErrorAction Stop

    if (-not $webhookRegistration.ok -or -not $webhookRegistration.result) {
        throw 'Registro recusado.'
    }

    $webhookInfo = Invoke-RestMethod -Method Get `
        -Uri "https://api.telegram.org/bot$telegramBotToken/getWebhookInfo" `
        -ErrorAction Stop

    if (-not $webhookInfo.ok) {
        throw 'Consulta recusada.'
    }

    $webhookInfo.result | Select-Object `
        url, pending_update_count, last_error_date, last_error_message
}
catch {
    Write-Warning 'Falha ao registrar ou consultar o webhook. Confira URL, credenciais e rede; a excecao bruta pode conter o token.'
}
finally {
    Remove-Variable telegramBotToken, telegramWebhookSecret, webhookParameters `
        -ErrorAction SilentlyContinue
}
```

Confirme que `url` aponta para o ambiente desejado. `pending_update_count` informa a fila; `last_error_date` e `last_error_message` podem se referir a uma falha anterior. Envie `/start` de um chat autorizado e confira a resposta e os logs da API. Para consultar novamente sem alterar o registro, execute apenas a chamada a `getWebhookInfo` com o token disponível na sessão.

`getWebhookInfo` não revela o secret registrado. A correspondência é verificada pela entrega de uma mensagem. Um redeploy com a mesma URL e secret não exige novo registro; mudar a URL ou o secret exige atualizar `setWebhook`. O exemplo mantém as atualizações pendentes; `drop_pending_updates=true` as descartaria. [Telegram: setWebhook e getWebhookInfo](https://core.telegram.org/bots/api#setwebhook).

**Um bot tem um webhook ativo.** Registrar um túnel local com o bot de produção direciona as próximas entregas para esse túnel. Prefira um bot separado para desenvolvimento ou restaure explicitamente a URL e, quando utilizado, o secret de produção ao terminar. [Telegram: recebimento de atualizações](https://core.telegram.org/bots/api#getting-updates).

## Configurar o Railway

### Variáveis e publicação

Na primeira ativação da versão que exige o header, siga o roteiro de ativação inicial adiante: aplique as variáveis na versão antiga, registre o secret no Telegram e só então publique a versão estrita. Os passos gerais abaixo também servem para atualizações posteriores.

1. Abra o projeto no Railway e selecione o ambiente e o serviço da **API**.
2. Em **Variables**, cadastre ou confira as chaves abaixo. Os dois sublinhados representam a hierarquia de configuração do .NET.
3. Revise as alterações pendentes e a versão que será publicada; aplique o deployment para que as variáveis alteradas tenham efeito. [Railway: variáveis](https://docs.railway.com/variables).
4. Confira os logs de inicialização e acesse `https://<DOMINIO_PUBLICO>/health`.
5. Registre o domínio público no Telegram pelo procedimento acima e teste `/start` de um chat autorizado.

| Variável no serviço da API | Uso |
|---|---|
| `Telegram__BotToken` | Token obtido com o BotFather, sem o prefixo `bot` |
| `Telegram__AllowedChatIds__0` | Primeiro chat autorizado |
| `Telegram__AllowedChatIds__1` | Segundo chat, se necessário; continue os índices para outros |
| `ConnectionStrings__DefaultConnection` | Conexão PostgreSQL compatível com Npgsql |
| `Telegram__WebhookSecret` | Secret obrigatório; deve corresponder ao `secret_token` registrado no Telegram |

Preserve a conexão existente se ela já funciona. Um exemplo de formato Npgsql é `Host=<HOST>;Port=<PORTA>;Database=<BANCO>;Username=<USUARIO>;Password=<SENHA>`. Não substitua automaticamente essa configuração por uma URL de conexão de outro formato.

O Dockerfile atual faz a API escutar na porta **8080**; confira se o domínio do serviço encaminha para essa porta. O healthcheck definido em `railway.toml` usa `/health`. Não é necessário registrar o webhook novamente a cada publicação da API se a URL continuar igual.

### Ativação inicial de WebhookSecret

A API exige `Telegram:WebhookSecret` e verifica o header `X-Telegram-Bot-Api-Secret-Token` antes de executar comandos ou enviar respostas. Header ausente, incorreto ou com múltiplos valores recebe HTTP 401, sem operação financeira ou envio. Updates autenticados ignorados e chats não autorizados recebem HTTP 200 sem operação ou envio. Este roteiro é operacional e deve ser executado ao publicar a versão; não foi executado durante a implementação.

Escolha um secret próprio aleatório e guarde-o no gerenciador de segredos. A lista de chats autorizados continua independente da autenticação. O parâmetro `secret_token` aceita de 1 a 256 caracteres, usando apenas `A-Z`, `a-z`, `0-9`, `_` e `-`. [Telegram: secret_token](https://core.telegram.org/bots/api#setwebhook).

#### Gerar e guardar o secret

O WebhookSecret é criado por você; não é um novo token fornecido pelo BotFather. Em uma sessão PowerShell privada, gere 32 bytes aleatórios e converta-os em 64 caracteres hexadecimais, compatíveis com o Telegram:

```powershell
$webhookSecretBytes = New-Object byte[] 32
$webhookSecretGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $webhookSecretGenerator.GetBytes($webhookSecretBytes)
    $webhookSecret = -join ($webhookSecretBytes | ForEach-Object { $_.ToString('x2') })
}
finally {
    $webhookSecretGenerator.Dispose()
}
Set-Clipboard -Value $webhookSecret
```

O comando copia o valor para a área de transferência sem imprimi-lo. Guarde-o no gerenciador de segredos e cole-o na variável `Telegram__WebhookSecret` do serviço da API no Railway. No registro PowerShell do webhook, informe esse mesmo valor na entrada protegida para enviá-lo como `secret_token`. Não gere outro valor entre essas duas etapas. Não compartilhe nem sincronize a área de transferência enquanto ela contiver o secret.

Para usar esse valor também nos User Secrets locais, na mesma sessão:

```powershell
dotnet user-secrets set "Telegram:WebhookSecret" $webhookSecret --project src/FinanceBot.Api
```

Prefira um secret diferente para cada bot/ambiente. Ao terminar de guardar e cadastrar o valor, limpe a área de transferência e as variáveis da sessão:

```powershell
Set-Clipboard -Value ''
Remove-Variable webhookSecret, webhookSecretBytes, webhookSecretGenerator -ErrorAction SilentlyContinue
```

Gerar e guardar o valor não ativa a autenticação; a publicação e o registro precisam seguir a ordem abaixo.

#### Publicar a versão com autenticação

Para evitar que a API passe a exigir o header antes de o Telegram enviá-lo:

1. Confirme a URL HTTPS de produção, o ambiente, o serviço da API e a versão em execução.
2. Cadastre `Telegram__WebhookSecret` e confira token e chats permitidos. Aplique as variáveis **ainda na versão que não exige o header**. Antes de aplicar alterações pendentes, confira se o deployment incluirá também código novo.
3. Execute o registro PowerShell com a mesma URL de produção e o secret cadastrado. A versão antiga ignora o header adicional enviado pelo Telegram. Confirme sucesso e teste `/start`.
4. Publique a versão que exige o header, com as variáveis já aplicadas. Acompanhe os logs de inicialização.
5. Consulte `getWebhookInfo`, compare eventuais erros com o horário da publicação e teste `/start` novamente.
6. Confira que uma chamada direta ao webhook sem o secret recebe HTTP 401, sem executar comando nem enviar resposta. Esse comportamento também está coberto por testes automatizados.

Se a versão que exige o header já estiver publicada, confira e aplique as opções necessárias e registre no Telegram o mesmo secret para restabelecer as entregas. Preserve a fila de atualizações; o bot ainda não tem proteção contra execução duplicada de um mesmo update.

Para desenvolvimento local, acrescente `Telegram:WebhookSecret` aos User Secrets da API e registre o túnel com esse valor. Exemplo com marcador:

```powershell
dotnet user-secrets set "Telegram:WebhookSecret" "<WEBHOOK_SECRET>" --project src/FinanceBot.Api
```

Substitua o marcador apenas na máquina local; não salve credenciais reais no guia, em scripts versionados ou em histórico compartilhado.

### Consultar logs

Em `Production`, `appsettings.Production.json` configura `Microsoft.EntityFrameworkCore.Database.Command` em `Warning`: consultas SQL executadas com sucesso deixam de gerar logs informativos, mas avisos e erros dessa categoria continuam sendo registrados. Os logs de webhook e inicialização do banco permanecem em `Information`. Em desenvolvimento, o nível padrão continua exibindo as consultas. Variáveis de ambiente podem sobrescrever esse filtro, por exemplo `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command`; não é necessário cadastrar essa variável no Railway para aplicar o padrão versionado.

No Railway, abra o serviço da **API**, selecione o deployment e consulte os logs de execução. **Build Logs** mostram a compilação; os logs do serviço **PostgreSQL** mostram o banco. Para pesquisar serviços e períodos, use **Observability / Log Explorer**. O Railway captura a saída padrão e a saída de erro dos serviços. [Railway: logs](https://docs.railway.com/observability/logs).

Os logs usam `ILogger` e a saída de console padrão. `TelegramWebhookCompleted` registra o identificador HTTP existente (`RequestId`), duração monotônica em milissegundos (`DurationMs`), etapa (`Stage`), resultado (`Outcome`) e comando reconhecido (`Command`), quando aplicável. Os resultados são `ignored` para update sem conteúdo/chat válido ou chat não autorizado, `rejected` para autenticação recusada, `processed` para execução concluída e `failed` para falha inesperada de execução. O comando é limitado aos seis nomes implementados; texto, argumentos e comandos desconhecidos não são registrados. `processed` não comprova entrega: o sender pode registrar um erro HTTP e retornar normalmente, ou lançar uma falha de transporte capturada pelo endpoint, mantendo HTTP 200 depois da operação.

Os nomes acima identificam eventos estruturados; o console padrão pode mostrar apenas o número do evento. Para localizar o processamento no Railway, busque a categoria `TelegramWebhook` com `[1001]` ou o texto `Telegram webhook processing completed`. Para o banco, busque `[1100]`, `[1101]`, `[1102]` e `[1103]`. O evento de retry contém o texto `Database temporarily unavailable`.

O startup registra `DatabaseInitializationStarted`, seguido de `DatabaseInitializationCompleted` ou `DatabaseInitializationFailed`. `Stage` identifica `resolve`, `inspect` ou `migrate`; a conclusão informa `completed` para provedor relacional e `skipped` para não relacional. A falha informa somente tipo de exceção e SQLSTATE PostgreSQL quando disponível, além da duração, sem connection string ou mensagem bruta. A validação das opções ocorre antes dessa etapa. Em PostgreSQL, `MigrateAsync` verifica e aplica apenas migrations pendentes.

Para `57P03` durante migrations, as três primeiras falhas registram `DatabaseInitializationRetrying` (`1103`) em Information, com tentativa, máximo de quatro tentativas e espera em segundos. Cada nova tentativa usa outro contexto; as esperas são canceláveis. Recuperação dentro desse limite termina com `1101`, sem o evento de falha `1102`. A quarta falha, ou qualquer outro erro, gera `1102` e é propagada; nesse caso o runtime ainda pode imprimir a exceção não tratada. A duração final inclui as esperas. Mensagens de inicialização, recuperação e conexões recusadas emitidas pelo próprio PostgreSQL podem continuar aparecendo.

O HttpClient do Telegram tem os loggers automáticos removidos, inclusive em Trace, pois a URI contém o token. Falhas de envio registram etapa, tipo de exceção e status quando disponível, sem URI, mensagem de exceção, corpo bruto, secret ou texto financeiro. Não habilite logs sensíveis do EF. Uma falha de envio após concluir o comando mantém HTTP 200; consulte os dados antes de reenviar comandos financeiros.

Mensagens de inicialização do PostgreSQL, isoladamente, não identificam a causa do reinício. Correlacione os horários com os deployments e eventos do serviço antes de atribuí-las a cold start. A mensagem de que o diretório já contém um banco indica reutilização dos dados existentes.

## Diagnóstico

- **`sendMessage` responde 404:** confira se o token está correto e foi salvo sem o prefixo `bot`.
- **Webhook retorna 502 ou 503:** confirme que a API está rodando na porta 5000, que o túnel aponta para essa porta e que a URL registrada ainda está atual.
- **O bot ignora mensagens:** confira se o chat de origem está incluído em `Telegram:AllowedChatIds` e se o perfil está em `Development`.
- **Telegram entrega a mesma atualização novamente:** confira `getWebhookInfo` e os logs. O Telegram pode tentar novamente quando não recebe uma confirmação de sucesso a tempo; falhas de roteamento ou persistência também devem ser investigadas.
- **API não inicia no Railway:** confira as chaves indicadas pela validação, as variáveis no serviço e ambiente corretos, se o deployment das alterações foi aplicado e os logs de conexão/migrations.
- **HTTP 401 no webhook:** confira se a versão ativa da API e o registro no Telegram usam o mesmo secret e se há um único valor no header. A lista de chats permitidos continua sendo uma verificação separada.
- **Despesa registrada, mas sem resposta:** confira falhas de `sendMessage` nos logs da API; consulte `/listar` antes de repetir `/compra`, para evitar duplicação.
- **PostgreSQL responde `57P03`:** confira `1103` para as tentativas e `1101` para recuperação bem-sucedida, correlacionando com `ready to accept connections` no banco. Se houver `1102` após quatro tentativas, o banco não ficou disponível dentro do limite; confira os eventos e o estado do serviço PostgreSQL no Railway. O retry da API não impede reinícios do banco.

Ao compartilhar diagnóstico, remova tokens, secrets, strings de conexão e dados pessoais. Exceções de chamadas ao Telegram podem conter o token na URL.
