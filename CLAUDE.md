# Backend (.NET)

ASP.NET Core Web API on the latest stable .NET, Entity Framework Core, SQL Server.
Clean Architecture. CQRS only where it is justified (see below).

## Commands
Run from `backend/`.
- Build: `dotnet build <Solution>.sln`
- Test all: `dotnet test`
- Test one: `dotnet test --filter "FullyQualifiedName~<Name>"`
- Run API: `dotnet run --project src/<Project>.Api`
- Add migration: `dotnet ef migrations add <Name> -p src/<Project>.Infrastructure -s src/<Project>.Api`
- Apply migrations: `dotnet ef database update -p src/<Project>.Infrastructure -s src/<Project>.Api`
- Format: `dotnet format`

## Structure
- `src/<Project>.Domain`: entities, value objects, domain rules. No dependencies on other projects.
- `src/<Project>.Application`: use cases, DTOs, validators, interfaces (e.g. repositories, external services).
- `src/<Project>.Infrastructure`: EF Core DbContext, repositories, external clients. Implements Application interfaces.
- `src/<Project>.Api`: controllers (or minimal API endpoints), DI setup, middleware. No business logic.
- `tests/`: mirrors `src/`.

Dependency rule: Api -> Application -> Domain. Infrastructure -> Application.
Domain never references any other project. Never reference Infrastructure from Application or Domain.

## CQRS: only if needed
- Default: one use-case class or service method per feature in Application. Keep it simple.
- Do NOT add MediatR or a command/query pipeline by default.
- Introduce CQRS for a feature only when it clearly pays off, e.g. read models that differ a lot from write models, heavy read queries that need their own optimized path, or cross-cutting behaviors that benefit from a pipeline.
- If CQRS seems justified, explain why and ask before introducing it or any new library for it.
- Once a feature uses it, keep commands (change state) and queries (read-only) strictly separate.

## Conventions
- Controllers are thin: validate input, call the Application layer, return a result. No business logic or EF code in controllers.
- Never return EF entities from the API. Map to DTOs.
- Validate input in Application (e.g. FluentValidation), not in controllers.
- Use `async`/`await` with `CancellationToken` end to end. Never `.Result` or `.Wait()`.
- Constructor injection only. No static state or service locator.
- Reads: use `AsNoTracking()`. Avoid N+1 queries; project to DTOs with `Select` where possible.
- Errors: use a consistent error response (ProblemDetails). Don't swallow exceptions.
- Logging: `ILogger<T>` with structured logging. Never log secrets or personal data.
- Naming: `PascalCase` types/members, `_camelCase` private fields, `Async` suffix on async methods, `I` prefix on interfaces.
- Tests: xUnit, `Method_Scenario_ExpectedResult` naming. Primary seam is API integration tests (`WebApplicationFactory`, real LocalDB, only `IDistanceEstimator` faked). Add Domain unit tests only when a rule is too fiddly to cover through the API.

## Database
- Never edit generated migrations by hand. Add a new migration instead.
- Ask before changing the schema or dropping columns.
- Connection strings and secrets come from user-secrets or Key Vault, never from committed files.

## Gotchas
- <e.g. integration tests need a local SQL Server or Docker container running>
- <e.g. `appsettings.Development.json` is gitignored; copy from `appsettings.Sample.json`>
- <e.g. CORS must allow `http://localhost:4200` in Development>
