# Backend (.NET)

ASP.NET Core Web API on the latest stable .NET, Entity Framework Core, SQL Server.
Clean Architecture. CQRS only where it is justified (see below).

## Commands
Run from `back-end/`.
- Build: `dotnet build JeepneyFareCalculator.slnx`
- Test all: `dotnet test`
- Test one: `dotnet test --filter "FullyQualifiedName~<Name>"`
- Run API: `dotnet run --project src/JeepneyFareCalculator.Api --launch-profile https` (https://localhost:7227)
- Add migration: `dotnet ef migrations add <Name> -p src/JeepneyFareCalculator.Infrastructure -s src/JeepneyFareCalculator.Api`
- Apply migrations: `dotnet ef database update -p src/JeepneyFareCalculator.Infrastructure -s src/JeepneyFareCalculator.Api`
- Format: `dotnet format`

## Structure
- `JeepneyFareCalculator.slnx`: the solution.
- `src/JeepneyFareCalculator.Domain`: entities, value objects, domain rules. No dependencies on other projects.
- `src/JeepneyFareCalculator.Application`: use cases, DTOs, validators, interfaces (e.g. repositories, external services). `Distances/IDistanceEstimator` is the seam to the routing provider.
- `src/JeepneyFareCalculator.Infrastructure`: EF Core DbContext (`Persistence/AppDbContext`), repositories, external clients. Implements Application interfaces. Registers itself through `AddInfrastructure()`.
- `src/JeepneyFareCalculator.Api`: controllers (or minimal API endpoints), DI setup, middleware. No business logic.
- `tests/JeepneyFareCalculator.Api.Tests`: API integration tests. `Infrastructure/ApiFactory` hosts the API in memory; `Infrastructure/FakeDistanceEstimator` replaces the routing provider.

Dependency rule: Api -> Application -> Domain. Infrastructure -> Application.
Api also references Infrastructure, only to register it at start-up (composition root).
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
- The connection string is `ConnectionStrings:DefaultConnection` in user-secrets (Api project). First-time setup, from `back-end/`:
  `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=JeepneyFareCalculator;Trusted_Connection=True;TrustServerCertificate=True" --project src/JeepneyFareCalculator.Api`
  then apply migrations to create the database. `GET /api/health` returns 503 until the database exists.
- The one Administrator signs in with `Administrator:Username` and `Administrator:PasswordHash` from user-secrets (Api project). Until both are set, `POST /api/admin/login` returns 500. First-time setup, or to change the password, from `back-end/`:
  1. `dotnet run --project src/JeepneyFareCalculator.Api --no-launch-profile -- hash-password`, then type the password twice (it isn't shown). It prints a hash like `PBKDF2-SHA256.600000.<salt>.<key>`.
  2. `dotnet user-secrets set "Administrator:Username" "<username>" --project src/JeepneyFareCalculator.Api`
  3. `dotnet user-secrets set "Administrator:PasswordHash" "<hash from step 1>" --project src/JeepneyFareCalculator.Api`
  Only the hash is stored, never the password. Restart the API after changing them.
- Fare calculation estimates distances with OpenRouteService, using `OpenRouteService:ApiKey` from user-secrets (Api project). Until it is set, `POST /api/fares/calculate` returns 500 for a valid request. Get a free key at openrouteservice.org, then from `back-end/`:
  `dotnet user-secrets set "OpenRouteService:ApiKey" "<key>" --project src/JeepneyFareCalculator.Api`
- Admin sessions are kept in memory (`Api/Authentication/InMemoryTicketStore`), so restarting the API signs the Administrator out. A session ends after 2 hours without a request.
- Admin endpoints need `[Authorize]` on their controller. Integration tests sign in with `factory.CreateSignedInClientAsync()`, which goes through the real login endpoint over https (the cookie is Secure).
- Integration tests need LocalDB (`(localdb)\MSSQLLocalDB`). Each test run creates a database named `JeepneyFareCalculator_Tests_<guid>` and drops it at the end. If a run is killed, delete leftovers in SQL Server Object Explorer.
- Integration test classes must have `[Collection(ApiCollection.Name)]` so they share one `ApiFactory` and one database. Tests in that collection run one at a time. The one exception is `NoActiveFareMatrixTests`, which needs a database where nothing was ever uploaded, so it has its own `ApiFactory` (`IClassFixture`) and must never upload a fare matrix. It is in `NoActiveFareMatrixCollection`, which runs on its own so two hosts never run at once.
- The `FakeDistanceEstimator` is shared too: a test that depends on the estimated distance must set `factory.DistanceEstimator.NextEstimate` itself, never rely on what an earlier test left there.
- Tests run in the `Testing` environment, so user-secrets and `appsettings.Development.json` are not loaded.
- No CORS: in development the Angular dev server proxies `/api` to https://localhost:7227, so the browser sees one origin.
