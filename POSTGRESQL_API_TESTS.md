# Recap des tests API PostgreSQL

Ce document resume les tests effectues apres la migration de l'API de SQL Server vers PostgreSQL.

## Environnement teste

- API ASP.NET Core `net10.0`
- Base PostgreSQL via Docker Compose
- Provider ADO.NET : `Npgsql`
- Migrations automatiques : DbUp avec `dbup-postgresql`
- Acces data : Dapper

La base PostgreSQL utilisee pendant les tests etait celle du service Docker `postgres`.
Les secrets locaux, le contenu du `.env` et le fichier Firebase n'ont pas ete affiches ni documentes.

## Demarrage valide

Les points suivants ont ete verifies :

- Le conteneur PostgreSQL demarre et passe en etat `healthy`.
- Docker Compose est valide avec `docker compose config --quiet`.
- L'API demarre avec une connection string PostgreSQL.
- DbUp se lance au demarrage de l'API.
- DbUp detecte les scripts deja executes et ne rejoue pas les migrations inutilement.

Les scripts DbUp PostgreSQL valides sont :

- `TableMasterApi/sql-scripts/001_InitialSchema.sql`
- `TableMasterApi/sql-scripts/002_UpdateReservationStatus.sql`
- `TableMasterApi/sql-scripts/003_AddDeviceTokens.sql`

## Scenarios HTTP testes

Un scenario complet a ete execute en HTTP contre l'API locale, avec creation d'un compte de test, recuperation d'un token JWT, puis appels authentifies.

Routes d'authentification et utilisateur :

- `POST /api/User` : creation de compte
- `POST /api/Auth` : login
- `POST /api/Auth/refresh` : renouvellement du refresh token
- `GET /api/User/{id}` : lecture utilisateur
- `PUT /api/User` : mise a jour utilisateur
- `PUT /api/User/Password` : changement de mot de passe
- `DELETE /api/User` : suppression de compte

Routes restaurant :

- `POST /api/Restaurant` : creation restaurant
- `GET /api/Restaurant/{id}` : lecture restaurant
- `GET /api/Restaurant` : recherche restaurants avec pagination et distance
- `PUT /api/Restaurant/{id}` : modification restaurant
- `DELETE /api/Restaurant/{id}` : suppression restaurant

Routes tables :

- `POST /api/Table/Restaurant/{id}` : creation table
- `GET /api/Table/Restaurant/{id}` : lecture tables d'un restaurant
- `PUT /api/Table/{id}` : modification table
- `POST /api/Table/Restaurant/{id}/Bulk` : remplacement des tables
- `DELETE /api/Table/{id}` : suppression table

Routes menu :

- `POST /api/Menu` : creation menu
- `GET /api/Menu/restaurant/{restaurantId}` : lecture menus d'un restaurant
- `DELETE /api/Menu/{id}` : suppression menu

Routes horaires :

- `POST /api/DailyActivity` : creation horaire
- `GET /api/DailyActivity/restaurant/{restaurantId}` : lecture horaires
- `PUT /api/DailyActivity/{id}` : modification horaire
- `DELETE /api/DailyActivity/{id}` : suppression horaire

Routes jours de fermeture :

- `POST /api/ClosedDayException` : creation fermeture
- `GET /api/ClosedDayException/restaurant/{restaurantId}` : lecture fermetures
- `PUT /api/ClosedDayException/{id}` : modification fermeture
- `DELETE /api/ClosedDayException/{id}` : suppression fermeture

Routes avis :

- `POST /api/Review` : creation avis
- `GET /api/Review/Restaurant/{id}` : lecture avis restaurant
- `GET /api/Review/My` : lecture de mes avis
- `PUT /api/Review/{id}` : modification avis
- `DELETE /api/Review/{id}` : suppression avis

Routes reservations :

- `POST /api/Reservation` : creation reservation
- `GET /api/Reservation` : lecture reservations
- `GET /api/Reservation/My` : lecture de mes reservations
- `PUT /api/Reservation/{id}/Status` : changement de statut
- `DELETE /api/Reservation/{id}` : suppression reservation

Routes device tokens :

- `POST /api/DeviceToken` : enregistrement token appareil
- `DELETE /api/DeviceToken` : suppression token appareil

## Bug trouve et corrige

Pendant le test de `POST /api/DailyActivity`, l'API retournait une erreur 500.

Cause :

- PostgreSQL renvoie les colonnes SQL `TIME` via `Npgsql` sous forme de `TimeOnly`.
- Les modeles de l'API utilisent `TimeSpan` pour `StartTime` et `EndTime`.
- Dapper ne convertissait pas automatiquement `TimeOnly` vers `TimeSpan`.

Correction ajoutee :

- Ajout de `TableMasterApi/DAL/Handlers/PostgresTimeSpanHandler.cs`
- Enregistrement du handler Dapper dans `Program.cs`

Ce handler convertit :

- `TimeOnly` vers `TimeSpan` en lecture
- `TimeSpan` vers parametre SQL en ecriture

Apres correction, les routes `DailyActivity` passent.

## Nettoyage des donnees de test

Les donnees creees par les scenarios de test ont ete ciblees avec des prefixes dedies :

- emails `codex.route.*@example.com`
- emails `codex.password.*@example.com`
- restaurants `Codex Test Restaurant ...`
- device tokens `test-device-*`

Les donnees de test `codex.route.*` ont ete nettoyees apres la passe complete.
Le compte temporaire utilise pour tester le changement de mot de passe a ete supprime via `DELETE /api/User`.

## Commandes de verification executees

```bash
docker compose config --quiet
dotnet build TableMasterApi.sln --no-restore
dotnet test --no-build
```

Resultat final :

- Build OK
- Tests xUnit OK : 31/31
- Scenario HTTP complet OK
- Aucune trace restante de `SqlConnection`, `SqlException`, `DbUp.SqlServer`, `SqlDatabase`, `OUTPUT INSERTED`, `GETDATE()` ou syntaxe SQL Server critique dans `TableMasterApi/`

## Notes

- Les warnings de nullabilite existants ne bloquent pas la migration PostgreSQL.
- Le package `Microsoft.AspNetCore.SignalR` remonte un warning NuGet indiquant qu'il est probablement inutile, mais ce point n'a pas ete modifie car il est hors scope de la migration.
- Les tests ont valide le comportement avec une base PostgreSQL locale Docker. Ils ne constituent pas encore une suite automatisee versionnee dans `TableMasterApi.Tests`.
