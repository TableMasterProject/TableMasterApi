# Sécurité — TableMasterApi

> Document de référence Bloc 2 — C2.2.3 (sécurisation du code source) du RNCP39583.
> Mapping des **mesures concrètes** dans le code de l'API face à l'**OWASP Top 10 (édition 2021)**.

## Synthèse

| Faille OWASP | Couverture | Localisation |
| --- | :---: | --- |
| A01 Broken Access Control | ✅ | `[Authorize]` + `ICurrentUserService` |
| A02 Cryptographic Failures | ✅ | TLS (Nginx), `BCrypt`/hash mot de passe, JWT signé |
| A03 Injection | ✅ | Dapper paramètres nommés, jamais de concat |
| A04 Insecure Design | ✅ | Architecture en couches, threat-model documenté |
| A05 Security Misconfiguration | ✅ | Secrets hors code, Docker minimal, headers Nginx |
| A06 Vulnerable Components | ⚠️ | Dependabot à activer (manuel pour l'instant) |
| A07 Identification & Auth Failures | ✅ | JWT court + refresh token rotation |
| A08 Software & Data Integrity | ✅ | Migrations DbUp signées en source, CI vérifiée |
| A09 Logging & Monitoring | ✅ | `ILogger` + Sentry ASP.NET Core |
| A10 Server-Side Request Forgery | ✅ | Aucun proxy d'URL utilisateur côté API |

---

## A01 — Broken Access Control

**Risque** : un utilisateur peut accéder à des ressources qui ne lui appartiennent pas.

**Mesures :**
- Tous les controllers sensibles décorés `[Authorize]`.
- L'identité courante est résolue via `ICurrentUserService` (claim `sub`) — jamais via paramètre client.
- Les méthodes "mes ressources" (`/Reservation/My`, etc.) filtrent côté DAL sur `current_user.Id`.
- Les opérations sur le restaurant vérifient que `restaurant.UserId == currentUser.Id` avant write/update/delete.

**À auditer périodiquement** : chaque nouveau controller doit prouver son contrôle d'accès dans la PR.

## A02 — Cryptographic Failures

**Risque** : transport en clair, secrets stockés faiblement.

**Mesures :**
- TLS terminé au reverse-proxy Nginx (Let's Encrypt) ; HSTS activé.
- Mots de passe utilisateurs hashés (jamais stockés en clair).
- JWT signés HS256 avec secret stocké en variable d'environnement (`Jwt:Key`) — jamais commité.
- Clé Firebase Admin (`tablemaster-firebase.json`) hors Git, montée en volume Docker.

**Contre-exemple à ne pas faire** : `appsettings.json` ne doit contenir aucun secret de production.

## A03 — Injection

**Risque** : injection SQL via paramètres utilisateur.

**Mesures :**
- 100 % des requêtes utilisent **Dapper avec paramètres nommés** :
  ```csharp
  await connection.QueryAsync<UserOut>(
      "SELECT * FROM Users WHERE Email = @Email",
      new { Email = email });
  ```
- **Aucune** concaténation de string avec entrée utilisateur dans les DAL (`TableMasterApi/DAL/`).
- Type handlers Dapper personnalisés (ex. `PostgresTimeSpanHandler`) maîtrisent les conversions sensibles.

## A04 — Insecure Design

**Mesures :**
- Architecture en couches : Controllers (HTTP) → Services (logique) → DAL (données).
- Controllers minces : aucun accès BDD ni logique métier complexe.
- DAL via interfaces (`DAL/Interfaces/`) — mockables, testables, substituables.
- Schéma BDD piloté par migrations versionnées (`sql-scripts/`).
- Threat-model documenté dans le dossier de cadrage Bloc 1.

## A05 — Security Misconfiguration

**Mesures :**
- Image Docker basée sur `mcr.microsoft.com/dotnet/aspnet` (officielle, minimale).
- `appsettings.Development.json` jamais déployé en prod (CI build en `Release`).
- Swagger activé uniquement en dev ou si `Features:EnableSwagger=true` — désactivé par défaut en prod.
- Reverse-proxy Nginx avec headers : `X-Content-Type-Options`, `Strict-Transport-Security`, `Referrer-Policy`.
- Health checks `/health` et `/ready` séparés des endpoints métier.

## A06 — Vulnerable and Outdated Components

**Mesures :**
- `dotnet list package --vulnerable` lancé avant chaque release.
- Versions NuGet pinnées dans le `.csproj`.

**À automatiser** : activer **Dependabot** ou **Renovate** sur le repo GitHub pour PR automatiques de mise à jour.

## A07 — Identification & Authentication Failures

**Mesures :**
- JWT à courte durée (~15 min) + refresh token long avec **rotation à chaque usage**.
- Refresh tokens persistés en BDD avec date d'expiration + `IsRevoked` (`AuthDAL`).
- Logout : révocation côté serveur (DeviceToken supprimé) + suppression côté client.
- `JwtService` centralise génération + validation — aucun fork dans les controllers.

**Améliorations à venir** : rate limiting sur `/Auth` (ASP.NET Rate Limiting middleware) pour mitiger le brute-force.

## A08 — Software & Data Integrity Failures

**Mesures :**
- Migrations DbUp embarquées comme **ressources** dans l'assembly (`<EmbeddedResource>`) → impossibles à modifier sans rebuild.
- Scripts SQL numérotés et **idempotents** quand possible.
- CI lance `dotnet test` avant tout build d'image Docker → pas d'image publiée si tests rouges.

## A09 — Security Logging & Monitoring Failures

**État actuel :**
- `ILogger<T>` injecté dans services et controllers sensibles (Auth, JWT, refresh).
- Logs structurés via le provider par défaut ASP.NET.
- Sentry ASP.NET Core est branché dans `Program.cs` pour collecter les exceptions et traces HTTP.
- Le DSN Sentry reste hors code : User Secrets en local (`Sentry:Dsn`) et variable d'environnement en prod (`Sentry__Dsn`).
- Un endpoint de vérification existe uniquement en développement : `GET /api/monitoring/sentry-test`.

**À compléter** :
- Ajouter un middleware de `correlationId` pour tracer une requête de bout en bout.
- Activer audit log sur opérations sensibles (login, refresh, logout, delete account).

## A10 — Server-Side Request Forgery

**État** : l'API ne consomme **pas d'URL fournie par l'utilisateur** côté serveur. Les seules sorties HTTP sont :
- Firebase Admin (FCM) — destination figée dans la lib.
- Google Maps (si utilisé côté serveur) — endpoint figé.

Aucune fonctionnalité de type "fetch URL", "import depuis lien", "webhook entrant" → faible surface SSRF.

---

## Audit & révision

| Quand | Action |
| --- | --- |
| Avant chaque release majeure | Lancer `dotnet list package --vulnerable --include-transitive` |
| À chaque PR | CI vérifie build + tests + scan image Docker (à brancher) |
| Trimestriel | Audit OWASP ZAP en mode automatisé contre l'API de staging |
| Annuel | Revue manuelle de ce document — corriger les points vieillis |

## Contact

Découverte d'une vulnérabilité → ouvrir un Security Advisory privé sur GitHub, **ne pas** ouvrir d'issue publique.
