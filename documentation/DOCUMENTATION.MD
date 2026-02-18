# FinanceBot Implementation Documentation

## Objective
Centralize all implemented changes in this repository.

## Update Policy (Mandatory)
For every new implementation from now on, this file must be updated with:
- Date
- Scope of the change
- Files added or modified
- Architectural decisions
- Validation steps performed

## Change Log

### 2026-02-18 - Documentation expanded with webhook runbook
Scope:
- Added consolidated implementation summary and operational guide for ngrok/webhook setup.

Modified:
- `documentation/DOCUMENTATION.MD`

Implemented behavior:
- Added "Implementation Summary (Current)" with final state of the webhook implementation.
- Added "Local Webhook Setup with ngrok" with step-by-step commands.
- Added "How to Configure Telegram Webhook" quick guide.
- Added troubleshooting section for common webhook/debug issues.

### 2026-02-18 - Single ChatId source and debug profile alignment
Scope:
- Simplified webhook chat id source to one key and aligned debug launch environment.

Added:
- `src/FinanceBot.Api/Properties/launchSettings.json`

Modified:
- `src/FinanceBot.Api/Endpoints/TelegramWebhookEndpoint.cs`
- `src/FinanceBot.Infrastructure/Telegram/TelegramOptions.cs`
- `src/FinanceBot.Api/appsettings.json`
- `.vscode/launch.json`

Implemented behavior:
- Webhook now uses only `Telegram:ChatId` as chat id source.
- Removed support for `Telegram:DefaultChatId`.
- Kept webhook acknowledgment as `200 OK` when chat id is missing to avoid retries/backoff.
- Added `DOTNET_ENVIRONMENT=Development` to VS Code debug profiles.
- Added API `launchSettings.json` with Development environment and fixed local URLs.

Decisions:
- A single configuration key reduces ambiguity and debugging errors.
- Explicit launch environment settings reduce mismatch between `dotnet run` and Run/Debug.

Validation:
- `dotnet build FinanceBot.sln` completed successfully with 0 errors and 0 warnings.

### 2026-02-18 - Webhook hardening for debug and retry safety
Scope:
- Fixed webhook retry loop behavior and improved chat id resolution for debug scenarios.

Modified:
- `src/FinanceBot.Api/Program.cs`
- `src/FinanceBot.Api/Endpoints/TelegramWebhookEndpoint.cs`
- `src/FinanceBot.Api/appsettings.json`
- `src/FinanceBot.Infrastructure/Telegram/TelegramOptions.cs`

Implemented behavior:
- Added explicit `AddUserSecrets<Program>` in API startup.
- Webhook now returns `200 OK` when chat id is missing, preventing Telegram retry loops.
- Added compatibility with both `Telegram:ChatId` and `Telegram:DefaultChatId`.
- Added fallback to `message.chat.id` when no configured chat id is available.
- Added informational logs for ignored updates/messages without text.

Decisions:
- Webhook endpoints should always acknowledge quickly to avoid retries/backoff storms.
- Configuration loading was made resilient for local debug launches that may skip expected profiles.
- Backward compatibility was kept for existing secrets key names.

Validation:
- `dotnet build FinanceBot.sln` completed successfully with 0 errors and 0 warnings.

### 2026-02-18 - VS Code launch profile for Development
Scope:
- Added VS Code debug configuration with `ASPNETCORE_ENVIRONMENT=Development`.

Added:
- `.vscode/launch.json`

Implemented behavior:
- Added `.NET` profile using `projectPath` for `FinanceBot.Api`.
- Added `CoreCLR` profile for direct DLL launch in `net8.0`.
- Both profiles force `ASPNETCORE_ENVIRONMENT=Development`.

Decisions:
- Added two profiles to support both C# Dev Kit (`dotnet`) and CoreCLR debugger modes.
- Kept working directory in `src/FinanceBot.Api` to ensure local config and secrets behavior.

### 2026-02-18 - Webhook now uses configured chat id
Scope:
- Changed Telegram webhook reply target to use configured chat id instead of `message.chat.id`.

Modified:
- `src/FinanceBot.Infrastructure/Telegram/TelegramOptions.cs`
- `src/FinanceBot.Api/Endpoints/TelegramWebhookEndpoint.cs`
- `src/FinanceBot.Api/appsettings.json`

Implemented behavior:
- Added `Telegram:ChatId` to options/configuration.
- Webhook now reads `TelegramOptions.ChatId` and sends replies to that configured chat.
- If `Telegram:ChatId` is missing/zero, webhook logs warning and still returns `200 OK`.

Decisions:
- Kept endpoint resilient by acknowledging updates even when configuration is incomplete.
- Used typed options to keep config centralized and avoid literal keys spread across logic.

### 2026-02-17 - Initial project structure and solution
Scope:
- Created the base solution and project layout following the architecture described in `README.md`.

Added:
- `FinanceBot.sln`
- `src/FinanceBot.Api/FinanceBot.Api.csproj`
- `src/FinanceBot.Application/FinanceBot.Application.csproj`
- `src/FinanceBot.Domain/FinanceBot.Domain.csproj`
- `src/FinanceBot.Infrastructure/FinanceBot.Infrastructure.csproj`
- `tests/FinanceBot.UnitTests/FinanceBot.UnitTests.csproj`
- `tests/FinanceBot.IntegrationTests/FinanceBot.IntegrationTests.csproj`

Created folder structure:
- `src/FinanceBot.Api`
- `src/FinanceBot.Application`
- `src/FinanceBot.Domain`
- `src/FinanceBot.Infrastructure`
- `tests/FinanceBot.UnitTests`
- `tests/FinanceBot.IntegrationTests`
- Supporting subfolders for endpoints, DI, abstractions, use cases, persistence, repositories, and telegram.

Decisions:
- Adopted layered architecture (`Api -> Application <- Infrastructure`, with `Domain` as core).
- Added project references to enforce separation of concerns.
- Added test projects (unit and integration) from the beginning.

Validation:
- `dotnet restore FinanceBot.sln` executed successfully.

### 2026-02-17 - Repository ignore rules
Scope:
- Added repository ignore patterns for C#/.NET development.

Added:
- `.gitignore`

Configured ignore categories:
- Build artifacts (`bin/`, `obj/`, `out/`, `artifacts/`)
- IDE/user files (`.vs/`, `.vscode/`, `.idea/`, `*.user`, `*.suo`, etc.)
- NuGet packages and caches
- Test results and coverage outputs
- Logs and OS-specific files
- Local environment/secrets files (`.env*`, `secrets.json`)
- Optional frontend/docker local artifacts (`node_modules/`, `docker-compose.override.yml`)

### 2026-02-17 - Telegram webhook MVP endpoint
Scope:
- Implemented a simple Telegram webhook flow in the API project.

Added:
- `src/FinanceBot.Api/Program.cs`
- `src/FinanceBot.Api/appsettings.json`
- `src/FinanceBot.Api/Endpoints/TelegramWebhookEndpoint.cs`
- `src/FinanceBot.Api/Endpoints/Contracts/TelegramWebhookRequest.cs`
- `src/FinanceBot.Application/Abstractions/ITelegramMessageSender.cs`
- `src/FinanceBot.Infrastructure/Telegram/TelegramOptions.cs`
- `src/FinanceBot.Infrastructure/Telegram/TelegramMessageSender.cs`
- `src/FinanceBot.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

Modified:
- `src/FinanceBot.Infrastructure/FinanceBot.Infrastructure.csproj`

Implemented behavior:
- Added `POST /telegram/webhook` endpoint.
- Added minimal command recognition for `/start`, `/compra`, `/listar`, `/deletar`, `/analise`.
- Added outbound integration to Telegram `sendMessage`.
- Added `Telegram:BotToken` configuration support.
- Added DI registration for Infrastructure services and typed HTTP client.
- Added simple health endpoint `GET /health`.
- Webhook returns `200 OK` even if reply send fails, with logging for resilience.

Decisions:
- Kept transport-specific logic in Infrastructure (`TelegramMessageSender`).
- Exposed sending contract through Application abstraction (`ITelegramMessageSender`) to respect DIP.
- Kept endpoint logic simple for MVP; command business rules will move to use cases in next steps.

Validation:
- `dotnet build FinanceBot.sln` completed successfully with 0 errors and 0 warnings.

## Current Status Summary
- Solution and layered architecture are in place.
- Telegram webhook communication is functional at MVP level.
- Domain/business command processing for purchases and analysis is not implemented yet.

## Implementation Summary (Current)
- API endpoint: `POST /telegram/webhook` in `src/FinanceBot.Api/Endpoints/TelegramWebhookEndpoint.cs`.
- Response strategy: endpoint always acknowledges with `200 OK` for malformed or incomplete updates.
- Command routing currently recognized: `/start`, `/compra`, `/listar`, `/deletar`, `/analise`.
- Telegram send implementation: `src/FinanceBot.Infrastructure/Telegram/TelegramMessageSender.cs`.
- Telegram options:
- `Telegram:BotToken`
- `Telegram:ChatId`
- Chat ID source is single and explicit: only `Telegram:ChatId`.
- API configuration bootstrap includes user secrets: `AddUserSecrets<Program>`.
- Debug configuration:
- VS Code: `.vscode/launch.json` with `ASPNETCORE_ENVIRONMENT=Development`.
- API profile: `src/FinanceBot.Api/Properties/launchSettings.json` with Development env.

## Local Webhook Setup with ngrok
Prerequisites:
- `ngrok` installed and authenticated.
- Telegram bot token created in `@BotFather`.
- API project running locally.

1. Configure user secrets in API project:
```powershell
dotnet user-secrets set "Telegram:BotToken" "SEU_BOT_TOKEN" --project c:\dev\financas-pessoais\src\FinanceBot.Api
dotnet user-secrets set "Telegram:ChatId" "SEU_CHAT_ID" --project c:\dev\financas-pessoais\src\FinanceBot.Api
dotnet user-secrets list --project c:\dev\financas-pessoais\src\FinanceBot.Api
```

2. Run API in Development:
```powershell
cd c:\dev\financas-pessoais\src\FinanceBot.Api
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run
```

3. Start ngrok using the same local endpoint:
- If API is on `http://localhost:5000`:
```powershell
ngrok http 5000
```
- If API is on `https://localhost:5001`:
```powershell
ngrok http https://localhost:5001
```

4. Register webhook in Telegram:
```powershell
curl.exe "https://api.telegram.org/bot<SEU_BOT_TOKEN>/setWebhook?url=https://<SEU_DOMINIO_NGROK>/telegram/webhook"
```

5. Validate webhook status:
```powershell
curl.exe "https://api.telegram.org/bot<SEU_BOT_TOKEN>/getWebhookInfo"
```

Expected:
- `"ok": true`
- `result.url` equals your current ngrok URL + `/telegram/webhook`
- `pending_update_count` close to `0`

## How to Configure Telegram Webhook (Quick)
1. Keep API running locally.
2. Expose local API with ngrok (HTTPS public URL).
3. Execute `setWebhook` with your public URL and `/telegram/webhook`.
4. Confirm with `getWebhookInfo`.
5. Send `/start` in Telegram and check API logs + bot response.

## Troubleshooting
- `Telegram sendMessage failed 404 Not Found`:
- Bot token is invalid or saved with `bot` prefix in secret.
- Correct format is `123456:ABC...` without leading `bot`.

- `Wrong response from webhook: 502/503`:
- ngrok cannot reach local API (wrong port, API down, or stale ngrok URL).
- Recheck local port, restart ngrok, and run `setWebhook` again.

- `ChatId` appears as zero in debug:
- wrong key name in secrets; must be `Telegram:ChatId`.
- debug environment not in Development or wrong startup profile.

- Same webhook hitting breakpoint many times:
- Telegram retries on non-2xx or timeout.
- keep webhook fast and return `200 OK`.

## Next implementation note
Every next change must append a new dated section in this file before closing the task.
