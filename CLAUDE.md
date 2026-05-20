# TableMasterApi — Contexte Claude Code

Répondre en français. Limiter les changements strictement à la demande.

## Vue d'ensemble

API REST + temps réel pour la plateforme TableMaster (gestion de réservations de restaurants, salles, tables, menus, avis, notifications push).

- **Stack** : ASP.NET Core API, **net10.0**, C# nullable activé.
- **DB** : PostgreSQL via **Dapper** + **Npgsql**.
- **Migrations** : **DbUp PostgreSQL**, scripts SQL embarqués dans `TableMasterApi/sql-scripts/` (préfixés par numéro de séquence, idempotents quand possible).
- **Auth** : JWT Bearer, refresh tokens persistés.
- **Temps réel** : SignalR (`ReservationHub` mappé sur `/reservationHub`).
- **Notifications push** : Firebase Admin (FCM).
- **Versioning API** : `Asp.Versioning.Mvc`, version par défaut `1.0`, header `x-api-version`.
- **Doc** : Swagger en dev ou si `Features:EnableSwagger`.
- **Tests** : xUnit, Moq, FluentAssertions.

## Structure du repo

```
TableMasterApi.sln
TableMasterApi/                    API
├── Controllers/                   endpoints HTTP versionnés (Auth, Reservation, Restaurant, Room, Table, Menu, Review, User, DailyActivity, ClosedDayException, DeviceToken)
├── DAL/                           accès données Dapper
│   ├── Interfaces/                contrats mockables
│   └── Handlers/                  type handlers Dapper (ex: PostgresTimeSpanHandler)
├── Model/                         DTO, entités, configs
├── Service/                       JWT, FCM, Google Maps, DB connection, current user, health checks
├── Hubs/                          SignalR
├── Middleware/                    gestion transversale des erreurs
├── sql-scripts/                   migrations DbUp embarquées
├── Program.cs                     bootstrap + DI
├── Dockerfile
└── docker-compose.yml
TableMasterApi.Tests/              tests unitaires + intégration légère
```

## Commandes

Depuis la racine du repo :

```bash
dotnet restore
dotnet build TableMasterApi.sln
dotnet test
bash run-tests.sh
```

Depuis `TableMasterApi/` :

```bash
dotnet run
docker compose up --build -d
```

## Règles d'architecture

- Controllers **minces** : pas de logique métier ni d'accès DB direct.
- Accès données dans les **DAL**, logique réutilisable dans les **Services**.
- Controllers dépendent des **interfaces DAL**, jamais des classes concrètes.
- Dapper avec **paramètres nommés** uniquement, jamais de concaténation SQL.
- Réutiliser `IDbConnectionFactory` / `PostgresConnectionFactory`.
- `async`/`await` pour tout I/O (DB, services externes).

## Ajouter une feature API

1. Modèle dans `Model/`.
2. Interface DAL dans `DAL/Interfaces/` + implémentation Dapper dans `DAL/`.
3. Controller dans `Controllers/` (route `api/[controller]`, version API).
4. Enregistrer DAL en `AddScoped` dans `Program.cs`.
5. Migration SQL dans `sql-scripts/` avec préfixe numérique suivant la séquence.
6. Tests xUnit ciblés.
7. Si contrat exposé change : prévenir / mettre à jour le mobile.

## Auth & sécurité

- JWT centralisé dans `JwtService`.
- Utilisateur courant via `ICurrentUserService`.
- Refresh + persistance dans les DAL auth/user.
- Health : `/health`, `/ready`.
- **Ne jamais commiter** : `.env`, connection strings, user secrets, clé Firebase (`tablemaster-firebase.json`), secret JWT.
- Ne pas éditer `bin/`, `obj/`, `.vs/` ni sorties générées.

## Tests

- Lancer `dotnet test` avant de terminer si possible.
- Mocker via Moq, assertions via FluentAssertions.
- Ajouter des tests autour de : auth, JWT, refresh token, controllers, DAL, services modifiés.
- Pas de dépendance à un vrai service externe dans les tests.

## Conventions C#

- PascalCase : classes, propriétés, méthodes publiques.
- camelCase : variables locales, paramètres.
- Nullable activé : garder annotations + checks cohérents.
- Réponses HTTP explicites, alignées sur les controllers existants.
- Gérer les cas métier prévisibles ; laisser le middleware traiter l'inattendu.

## Voir aussi

- `AGENTS.md` — instructions étendues.
- `README.md`, `CI_CD.md`, `POSTGRESQL_API_TESTS.md`, `TESTS-QUICK-START.md`.
