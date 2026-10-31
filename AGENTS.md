# TableMasterApi - Instructions Agent

Ces instructions s'appliquent au projet `TableMasterApi/`. Repondre a l'utilisateur en francais et garder les changements strictement limites a la demande.

## Workflow Obligatoire

- Avant toute action, lire tous les `AGENTS.md` applicables, verifier `git status --short` et preserver les changements utilisateur existants.
- Identifier uniquement les skills directement utiles a la tache et lire leur `SKILL.md` avant d'agir. Ne pas charger de skills sans rapport avec la demande.
- Pour un bug non trivial, une regression ou une cause inconnue, utiliser `superpowers:systematic-debugging` avant toute correction. Une faute de frappe ou une correction evidente ne requiert pas ce workflow complet.
- Pour une fonctionnalite ou une correction de comportement testable, utiliser `superpowers:test-driven-development` et commencer par le plus petit test pertinent.
- Avant toute affirmation de reussite, utiliser `superpowers:verification-before-completion` avec la plus petite preuve fraiche couvrant reellement le changement.
- Quand `graphify-out/graph.json` existe, interroger Graphify avant l'exploration large. Utiliser ensuite `rg` pour rechercher le contenu et `fd` pour trouver les fichiers.
- Le skill `playwright-cli` est reserve aux parcours navigateur ou Swagger explicitement concernes ; il ne remplace jamais les tests xUnit.
- Ne jamais commiter, pousser, publier, supprimer ou ecraser des fichiers sans demande explicite.

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

- Toute logique ajoutee ou modifiee doit avoir des tests xUnit cibles couvrant le cas nominal, les erreurs attendues et les regressions pertinentes.
- Pour un bug, le test de regression doit echouer avant la correction puis reussir apres celle-ci.
- Utiliser Moq pour les dependances de controllers/services.
- Utiliser FluentAssertions pour des assertions lisibles.
- Ajouter des tests cibles pour auth, JWT, refresh-token, controllers, DAL et services modifies.
- Eviter les tests dependants d'un vrai service externe.
- Executer d'abord les tests cibles correspondant au comportement modifie.
- Executer la suite complete `dotnet test` et `dotnet build TableMasterApi.sln --no-restore` seulement pour un changement transverse, un contrat API, l'authentification, la securite, l'infrastructure, une release ou a la demande explicite de l'utilisateur.
- Executer `git diff --check` avant de conclure.
- Il est interdit d'annoncer que le travail est termine si un test, le build ou une verification obligatoire echoue.
- Si l'environnement empeche reellement une verification, indiquer la commande tentee, l'erreur exacte et le perimetre restant non verifie. Ne jamais presenter ce cas comme une reussite.

## Securite

- Ne pas afficher ni commiter `.env`, connection strings locales, user secrets, cles Firebase ou secrets JWT.
- Ne pas afficher ni commiter le DSN Sentry reel ; utiliser User Secrets en local et variables d'environnement en deploiement.
- Ne pas editer `bin/`, `obj/`, `.vs/` ou sorties generees sauf demande explicite.
- Garder les valeurs sensibles dans la configuration locale ou les secrets, jamais dans le code.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- When graphify-out/graph.json exists and the question is architectural, cross-file or has an unknown location, run `graphify query "<question>"` before broad search. For a known file, symbol or small local change, direct `rg`, `fd` and source reads are allowed. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After structural or cross-file code changes, run `graphify update .` to keep the graph current. Skip it for documentation, configuration and small local text changes.
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
