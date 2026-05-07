# TableMasterApi Agent Instructions

These instructions apply to `TableMasterApi/`.

## Project

- Solution: `TableMasterApi.sln`
- API project: `TableMasterApi/TableMasterApi.csproj`
- Test project: `TableMasterApi.Tests/TableMasterApi.Tests.csproj`
- Database project: `Database/Database.sqlproj`
- Main runtime: ASP.NET Core API targeting `net10.0`
- Database: SQL Server
- Data access: Dapper
- Migrations: DbUp embedded scripts from `TableMasterApi/sql-scripts/`
- Tests: xUnit, Moq, FluentAssertions

Answer the user in French and keep changes tightly scoped.

## Commands

Run from this directory:

```bash
dotnet restore
dotnet build TableMasterApi.sln
dotnet test
bash run-tests.sh
```

Run the API from `TableMasterApi/`:

```bash
dotnet run
docker compose up --build -d
```

## Architecture

- `TableMasterApi/Controllers/`: HTTP controllers.
- `TableMasterApi/DAL/`: Dapper data access implementations.
- `TableMasterApi/DAL/Interfaces/`: mockable DAL contracts.
- `TableMasterApi/Model/`: DTOs and entities.
- `TableMasterApi/Service/`: services such as JWT, FCM, and Google Maps.
- `TableMasterApi/Hubs/`: SignalR hubs.
- `TableMasterApi/sql-scripts/`: DbUp migrations.
- `Database/Table/`: SQL table definitions.
- `TableMasterApi.Tests/`: unit and integration tests.

Controllers should stay thin. Put database work in DAL classes and reusable logic in services.

## Dependency Injection

- Register DAL interfaces in `TableMasterApi/Program.cs` with `AddScoped`.
- Register shared stateless services consistently with the existing singleton pattern.
- Controllers should depend on interfaces, not concrete DAL classes.
- When adding a DAL, add its interface, implementation, registration, and tests where appropriate.

## C# Conventions

- Use PascalCase for classes, public methods, and properties.
- Use camelCase for local variables and parameters.
- Nullable is enabled, so keep nullability annotations meaningful.
- Use `async`/`await` for database and external service calls.
- Use Dapper parameter binding. Never build SQL by concatenating user input.
- Preserve existing route conventions: `api/[controller]`, `ApiVersion`, and `x-api-version`.
- Handle expected SQL errors explicitly when the surrounding code already does so.

## Auth, Realtime, Notifications

- JWT behavior is centralized in `JwtService`.
- Refresh token persistence is handled through user/auth DAL code.
- SignalR reservation updates go through `ReservationHub`.
- Firebase push notifications go through `FcmService` and require a local Firebase service account file. Do not expose or commit private Firebase credentials.

## Testing

- Run `dotnet test` before finishing changes when feasible.
- Use Moq for DAL/service dependencies in controller tests.
- Use FluentAssertions for readable assertions.
- Reuse fixtures in `TableMasterApi.Tests/Fixtures/` for JWT/config setup.
- Add focused tests for auth, JWT, refresh-token, controller, and service changes.

## Safety

- Do not commit or display `.env`, local connection strings, Firebase private keys, or user secrets.
- Avoid editing generated folders such as `bin/`, `obj/`, and Rider metadata unless the task explicitly requires it.
