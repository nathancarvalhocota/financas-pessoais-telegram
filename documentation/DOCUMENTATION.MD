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

### 2026-02-28 - /compra now returns specific invalid category feedback
Scope:
- Improved `/compra` validation feedback when category does not exist.
- Added support for Telegram command mention format (`/compra@BotName`).

Modified:
- `src/FinanceBot.Application/UseCases/TelegramCommandRouter.cs`
- `tests/FinanceBot.IntegrationTests/Api/TelegramWebhookEndpointTests.cs`

Added:
- `tests/FinanceBot.UnitTests/Application/TelegramCommandRouterTests.cs`

Implemented behavior:
- `/compra` now returns:
- `Categoria inexistente: <categoria>. Categorias disponiveis: ...`
- Categories list is now built dynamically from `Categoria` enum display mapping.
- `/compra@BotName` is accepted with the same parsing rules as `/compra`.

Decisions:
- Specific validation messages reduce user friction in Telegram flows and avoid generic guidance.
- Supporting command mention format improves compatibility with group chats.

Validation:
- `dotnet test FinanceBot.sln` executed after changes.

### 2026-02-28 - Database connection string now resolved from user-secrets/config sources
Scope:
- Updated runtime and EF design-time database configuration to remove hardcoded fallback connection strings.
- Enabled EF CLI to read `ConnectionStrings:DefaultConnection` from user-secrets in Infrastructure project.

Modified:
- `src/FinanceBot.Infrastructure/Persistence/FinanceBotDbContextFactory.cs`
- `src/FinanceBot.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `src/FinanceBot.Infrastructure/FinanceBot.Infrastructure.csproj`
- `tests/FinanceBot.UnitTests/Infrastructure/FinanceBotDbContextFactoryTests.cs`

Added:
- `tests/FinanceBot.UnitTests/Infrastructure/InfrastructureServiceCollectionExtensionsTests.cs`

Implemented behavior:
- `FinanceBotDbContextFactory` now resolves connection string in this order:
- `ConnectionStrings__DefaultConnection` environment variable.
- User-secrets (`ConnectionStrings:DefaultConnection`).
- Runtime DI now requires `ConnectionStrings:DefaultConnection` and throws a clear error when missing.
- Removed runtime fallback to hardcoded local Postgres credentials.
- Added Infrastructure `UserSecretsId` and `Microsoft.Extensions.Configuration.UserSecrets` package.

Decisions:
- Avoiding hardcoded credentials reduces accidental secret leaks and prevents ambiguous source-of-truth behavior.
- EF design-time and runtime now follow the same configuration contract (`ConnectionStrings:DefaultConnection`).

Validation:
- `dotnet test FinanceBot.sln` executed after changes.

### 2026-02-23 - EF design-time DbContext factory for migrations/update
Scope:
- Fixed `dotnet ef database update --project src/FinanceBot.Infrastructure` design-time creation.

Added:
- `src/FinanceBot.Infrastructure/Persistence/FinanceBotDbContextFactory.cs`
- `tests/FinanceBot.UnitTests/Infrastructure/FinanceBotDbContextFactoryTests.cs`

Implemented behavior:
- Added `IDesignTimeDbContextFactory<FinanceBotDbContext>` implementation.
- Factory now creates `FinanceBotDbContext` for EF CLI commands without API host bootstrapping.
- Supports `ConnectionStrings__DefaultConnection` environment variable.
- Uses local default Postgres connection string when environment variable is absent.

Decisions:
- EF CLI design-time operations require explicit context construction when startup host is not used.
- A dedicated factory avoids command failures and keeps migrations/update independent from runtime boot.

Validation:
- `dotnet build FinanceBot.sln` completed successfully.
- `dotnet test FinanceBot.sln` completed successfully.

### 2026-02-23 - Command services for /compra, /listar, /deletar
Scope:
- Implemented command routing and database-backed services for purchase lifecycle commands.
- Updated webhook flow to delegate command parsing/execution to Application layer.
- Added integration tests for all command flows.

Added:
- `src/FinanceBot.Application/Abstractions/ICompraRepository.cs`
- `src/FinanceBot.Application/Abstractions/ITelegramCommandRouter.cs`
- `src/FinanceBot.Application/UseCases/TelegramCommandRouter.cs`
- `src/FinanceBot.Infrastructure/Repositories/CompraRepository.cs`

Modified:
- `src/FinanceBot.Api/Program.cs`
- `src/FinanceBot.Api/Endpoints/TelegramWebhookEndpoint.cs`
- `src/FinanceBot.Api/Endpoints/Contracts/TelegramWebhookRequest.cs`
- `src/FinanceBot.Api/appsettings.json`
- `src/FinanceBot.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `tests/FinanceBot.IntegrationTests/Api/TelegramWebhookEndpointTests.cs`
- `tests/FinanceBot.IntegrationTests/FinanceBot.IntegrationTests.csproj`

Implemented behavior:
- Added command router service with handlers:
- `/compra`: validates input, parses value/category, persists expense, and returns confirmation.
- `/listar`: validates `MM/YY`, lists month expenses ordered by date, and returns total.
- `/deletar`: validates ID, removes existing expense, and returns removal summary.
- Added category normalization to parse labels with accents/special characters.
- Added Telegram `message.date` support in webhook payload contract.
- Webhook now delegates business behavior to `ITelegramCommandRouter`.
- Added EF repository implementation for `Compra` operations.
- Added database context registration and repository wiring in Infrastructure DI.

Tests added/updated:
- Integration tests now validate:
- `/compra` creates DB record and returns confirmation message.
- `/listar` returns seeded month records and correct total.
- `/deletar` removes DB record and returns summary.
- Existing webhook tests for `/start`, empty text, and missing `ChatId` remain green.
- Added `Microsoft.EntityFrameworkCore.InMemory` for deterministic integration tests.

Decisions:
- Parsing/validation/business response generation belongs in Application service,
- while persistence belongs in Infrastructure repository.
- In-memory EF provider is used only in tests
- to validate command behavior without external DB dependency.
- Webhook remains thin and resilient, focused on I/O + orchestration.

Validation:
- `dotnet build FinanceBot.sln` completed successfully with 0 errors and 0 warnings.
- `dotnet test FinanceBot.sln` completed successfully:
- Unit tests passed: 3/3.
- Integration tests passed: 6/6.

### 2026-02-23 - Webhook and Telegram messaging automated tests
Scope:
- Added automated tests for webhook receive flow and Telegram send flow.

Added:
- `tests/FinanceBot.IntegrationTests/Api/TelegramWebhookEndpointTests.cs`
- `tests/FinanceBot.UnitTests/Infrastructure/TelegramMessageSenderTests.cs`

Modified:
- `tests/FinanceBot.UnitTests/FinanceBot.UnitTests.csproj`

Implemented behavior:
- Integration tests validate webhook endpoint behavior:
- receives `/start` and sends a reply with configured chat id.
- ignores empty text and still returns `200 OK`.
- returns `200 OK` and does not send message when `Telegram:ChatId` is missing.
- Unit tests validate Telegram sender behavior:
- builds correct `sendMessage` endpoint URL and JSON payload.
- skips outbound HTTP call when token is empty.
- does not throw exceptions when Telegram API returns error status.

Decisions:
- Integration tests use `WebApplicationFactory<Program>` and service replacement
- to isolate sender side effects.
- Unit tests use custom `HttpMessageHandler` for deterministic transport assertions.
- The test setup keeps webhook semantics aligned with production
- (`200 OK` for invalid or incomplete updates).

Validation:
- `dotnet test FinanceBot.sln` completed successfully.
- Unit tests passed: 3/3.
- Integration tests passed: 3/3.

### 2026-02-23 - EF Core package version alignment
Scope:
- Aligned Entity Framework Core package versions in Infrastructure to remove assembly conflicts.

Modified:
- `src/FinanceBot.Infrastructure/FinanceBot.Infrastructure.csproj`

Implemented behavior:
- Changed `Microsoft.EntityFrameworkCore` from `8.0.14` to `8.0.4`.
- Changed `Microsoft.EntityFrameworkCore.Design` from `8.0.14` to `8.0.4`.
- Kept `Npgsql.EntityFrameworkCore.PostgreSQL` in `8.0.4` for compatibility.

Decisions:
- Mixed EF package versions generate binding warnings and can lead to runtime inconsistencies.
- Version alignment keeps build output clean and deterministic.

Validation:
- `dotnet build FinanceBot.sln` completed successfully with 0 errors and 0 warnings.

### 2026-02-23 - Initial database model objects (Compra + Categoria)
Scope:
- Added first domain entity and enum for persistence model.
- Added EF Core DbContext and mapping for `Compra`.

Added:
- `src/FinanceBot.Domain/Entities/Compra.cs`
- `src/FinanceBot.Domain/Enums/Categoria.cs`
- `src/FinanceBot.Infrastructure/Persistence/FinanceBotDbContext.cs`

Implemented behavior:
- Created entity `Compra` with attributes:
- `double Valor`
- `string Descricao`
- `Categoria Categoria`
- `DateTime Data`
- Added identity field `Id` for persistence key.
- Created enum `Categoria` with all requested category options.
- Added EF Core mapping for table `compras` with required fields and string conversion for enum.

Decisions:
- Entity and enum were placed in Domain to keep business model independent from infrastructure.
- DbContext and relational mapping were placed in Infrastructure to respect separation of concerns.
- Enum is persisted as string to keep database values readable and avoid ordinal-coupling issues.

Validation:
- `dotnet build FinanceBot.sln` completed successfully with 0 errors and 0 warnings.

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
- Supporting subfolders for endpoints, DI, abstractions, use cases, persistence,
- repositories, and telegram.

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
dotnet user-secrets set "Telegram:BotToken" "SEU_BOT_TOKEN" `
  --project c:\dev\financas-pessoais\src\FinanceBot.Api
dotnet user-secrets set "Telegram:ChatId" "SEU_CHAT_ID" `
  --project c:\dev\financas-pessoais\src\FinanceBot.Api
dotnet user-secrets list `
  --project c:\dev\financas-pessoais\src\FinanceBot.Api
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
$webhookUrl = "https://<SEU_DOMINIO_NGROK>/telegram/webhook"
curl.exe "https://api.telegram.org/bot<SEU_BOT_TOKEN>/setWebhook?url=$webhookUrl"
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
