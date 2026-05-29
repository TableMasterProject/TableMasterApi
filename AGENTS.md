# TableMasterApi - Instructions Agent

Ces instructions s'appliquent au projet `TableMasterApi/`. Repondre a l'utilisateur en francais et garder les changements strictement limites a la demande.

## Projet

- Solution : `TableMasterApi.sln`.
- API : `TableMasterApi/TableMasterApi.csproj`.
- Tests : `TableMasterApi.Tests/TableMasterApi.Tests.csproj`.
- Runtime : ASP.NET Core API ciblant `net10.0`.
- Base de donnees : PostgreSQL.
- Acces donnees : Dapper avec `Npgsql`.
- Migrations : DbUp PostgreSQL avec scripts embarques dans `TableMasterApi/sql-scripts/`.
- Auth : JWT Bearer.
- Realtime : SignalR.
- Notifications : Firebase Admin.
- Monitoring : Sentry ASP.NET Core.
- Tests : xUnit, Moq, FluentAssertions.

## Commandes

Executer depuis ce dossier :

```bash
dotnet restore
dotnet build TableMasterApi.sln
dotnet test
bash run-tests.sh
```

Executer l'API depuis `TableMasterApi/` :

```bash
dotnet run
docker compose up --build -d
```

## Architecture

- `TableMasterApi/Controllers/` : endpoints HTTP versionnes.
- `TableMasterApi/DAL/` : implementations Dapper.
- `TableMasterApi/DAL/Interfaces/` : contrats mockables pour les DAL.
- `TableMasterApi/DAL/Handlers/` : type handlers Dapper, notamment PostgreSQL.
- `TableMasterApi/Model/` : DTO, entites et modeles de configuration.
- `TableMasterApi/Service/` : JWT, FCM, Google Maps, connexion DB, utilisateur courant, health checks.
- `TableMasterApi/Hubs/` : hubs SignalR.
- `TableMasterApi/Middleware/` : gestion transversale des erreurs.
- `TableMasterApi/sql-scripts/` : migrations DbUp, numerotees et idempotentes autant que possible.
- `TableMasterApi.Tests/` : tests unitaires et integration legere.

Les controllers doivent rester minces. Placer les acces base dans les DAL et la logique reutilisable dans les services.

## Dependency Injection

- Enregistrer les DAL dans `TableMasterApi/Program.cs` avec `AddScoped`.
- Enregistrer les services partages selon le cycle de vie existant (`AddSingleton`, `AddScoped`, `AddHttpClient`).
- Les controllers dependent des interfaces, pas des classes DAL concretes.
- Pour une nouvelle feature API, ajouter : modele, interface DAL, implementation DAL, controller, registration DI, migration si necessaire, tests cibles.

## API, Auth et Contrats

- Routes controllers : convention `api/[controller]` avec version API.
- Version API par defaut : `1.0`.
- Header accepte : `x-api-version`.
- JWT centralise dans `JwtService`.
- Utilisateur courant via `ICurrentUserService`.
- Refresh tokens et persistance auth dans les DAL auth/user.
- SignalR reservations : `ReservationHub` mappe sur `/reservationHub`.
- Health checks : `/health` et `/ready`.
- Sentry API : config via section `Sentry` (`Sentry:Dsn` en User Secrets local, `Sentry__Dsn` en env prod), jamais de DSN en dur.
- Test Sentry API : endpoint `GET /api/monitoring/sentry-test` disponible uniquement en environnement `Development`.
- Swagger actif en developpement ou si `Features:EnableSwagger` est active.

## Base de Donnees

- Utiliser Dapper avec parametres nommes, jamais de concatenation SQL avec entree utilisateur.
- Reutiliser `IDbConnectionFactory` / `PostgresConnectionFactory`.
- Les migrations vont dans `TableMasterApi/sql-scripts/` avec un prefixe numerique suivant la sequence existante.
- Si le schema change, mettre a jour les modeles, DAL, tests et le mobile si le contrat API expose change.
- Tenir compte de `PostgresTimeSpanHandler` pour les conversions temporelles existantes.

## Conventions C#

- PascalCase pour classes, proprietes, methodes publiques.
- camelCase pour variables locales et parametres.
- Nullable active : garder les annotations et checks coherents.
- Utiliser `async`/`await` pour DB et services externes.
- Preferer des reponses HTTP explicites et coherentes avec les controllers existants.
- Ne pas masquer les erreurs attendues : gerer les cas metier previsibles et laisser le middleware traiter l'inattendu.

## Tests

- Lancer `dotnet test` avant de terminer si possible.
- Utiliser Moq pour les dependances de controllers/services.
- Utiliser FluentAssertions pour des assertions lisibles.
- Ajouter des tests cibles pour auth, JWT, refresh-token, controllers, DAL et services modifies.
- Eviter les tests dependants d'un vrai service externe.

## Securite

- Ne pas afficher ni commiter `.env`, connection strings locales, user secrets, cles Firebase ou secrets JWT.
- Ne pas afficher ni commiter le DSN Sentry reel ; utiliser User Secrets en local et variables d'environnement en deploiement.
- Ne pas editer `bin/`, `obj/`, `.vs/` ou sorties generees sauf demande explicite.
- Garder les valeurs sensibles dans la configuration locale ou les secrets, jamais dans le code.
