# Testes

Execute os testes pela raiz do repositório:

```powershell
dotnet test FinanceBot.sln
```

Para executar apenas um dos projetos:

```powershell
dotnet test tests/FinanceBot.UnitTests/FinanceBot.UnitTests.csproj
dotnet test tests/FinanceBot.IntegrationTests/FinanceBot.IntegrationTests.csproj
```

Para compilar sem executar os testes:

```powershell
dotnet build FinanceBot.sln
```

## Projetos e escopo atual

- **FinanceBot.UnitTests:** cobre regras de categoria, ciclos, limites e limiares de alerta; também testa o roteamento de comandos, registro e exclusão em repositórios em memória, composição de Infrastructure e envio HTTP ao Telegram.
- **FinanceBot.IntegrationTests:** usa `WebApplicationFactory<Program>` para enviar updates ao endpoint real da aplicação. Os testes verificam respostas, autorização de chats, persistência de comandos e mensagens de saída capturadas por um `HttpMessageHandler` de teste.

## Limites dos testes de integração

Os testes de integração substituem o `FinanceBotDbContext` de produção pelo provedor EF Core InMemory. Eles não exercitam PostgreSQL, Npgsql ou migrations. Embora o projeto declare o pacote `Testcontainers.PostgreSql`, os testes atuais não criam um banco PostgreSQL com Testcontainers.

As chamadas de saída ao Telegram são simuladas; os testes não precisam de token real, rede externa nem chat do bot.
