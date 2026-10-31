# Manuel de déploiement — TableMasterApi

> Bloc 2 — C2.4.1 du RNCP39583. Manuel destiné aux opérations.

Cette procédure couvre **un déploiement complet** de l'API .NET sur un serveur Linux conteneurisé (Oracle Cloud Free Tier ARM, mais transposable à tout hôte Docker).

---

## 1. Pré-requis

### Sur la machine de build (local ou CI)

| Outil | Version mini | Vérification |
| --- | --- | --- |
| .NET SDK | **10.0.x** | `dotnet --version` |
| Docker Engine | 24.x | `docker version` |
| Docker Compose | v2 (plugin) | `docker compose version` |
| Git | 2.40+ | `git --version` |

### Sur le serveur cible

- Linux x86_64 ou ARM64 avec Docker + Docker Compose installés.
- **Nginx Proxy Manager** (avec Let's Encrypt) déjà configuré pour terminer le TLS et joindre l'API sur le réseau Docker externe `tablemaster-proxy`.
- Un utilisateur non-root pour exécuter Docker (`usermod -aG docker $USER`).
- Volumes persistants pour : PostgreSQL data, clé Firebase, logs.

### Secrets attendus (jamais dans Git)

| Variable | Source | Description |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | env serveur | DSN PostgreSQL : `Host=…;Port=5432;Database=…;Username=…;Password=…` |
| `ConfigPerso__SecretKey` | env serveur | Secret JWT d'au moins 32 octets |
| `ConfigPerso__Issuer` / `ConfigPerso__Audience` | env serveur | Identifiants JWT |
| `tablemaster-firebase.json` | volume monté | Clé service-account Firebase Admin (FCM) |
| `GoogleMaps__ApiKey` | env serveur | Clé API Maps |
| `Sentry__Dsn` | env serveur / User Secrets local | DSN Sentry API, jamais commité |

---

## 2. Procédure de déploiement standard

### 2.1. Préparer la release localement (ou via CI)

```bash
git checkout Prod
git pull --ff-only
dotnet restore TableMasterApi.sln
dotnet build TableMasterApi.sln --configuration Release --no-restore
dotnet test --no-build --configuration Release       # rouge → on n'avance pas
```

Le pipeline GitHub Actions (`.github/workflows/dotnet.yml`) fait la même chose automatiquement à chaque push sur `Prod` et publie l'image vers GHCR :
`ghcr.io/<owner>/tablemaster-api:<sha>` + `latest`.

### 2.2. Pousser l'image (manuel si besoin)

```bash
docker build -t ghcr.io/<owner>/tablemaster-api:$(git rev-parse --short HEAD) \
             -t ghcr.io/<owner>/tablemaster-api:latest \
             -f TableMasterApi/Dockerfile .
docker push ghcr.io/<owner>/tablemaster-api:latest
```

### 2.3. Déployer sur le serveur

```bash
ssh ubuntu@141.253.120.94
cd /opt/tablemaster/prod/TableMasterApi
export FIREBASE_SERVICE_ACCOUNT_HOST_FILE=../tablemaster-firebase.json
export CORS_ALLOWED_ORIGIN=https://client.tablemaster.maxencecoeur.fr
export APP_LINKS_BASE_URL=https://client.tablemaster.maxencecoeur.fr
API_IMAGE_TAG=<sha-image> APP_VERSION=<version> \
  SENTRY_RELEASE=<release-sentry> ./ops/deploy-api.sh
```

Le workflow de production fournit `FIREBASE_SERVICE_ACCOUNT_HOST_FILE`,
`CORS_ALLOWED_ORIGIN` et `APP_LINKS_BASE_URL` à chaque déploiement. Pour une
exécution manuelle, conserver ces trois `export` afin de reprendre la clé
Firebase à la racine du dépôt et de ne pas réutiliser d'anciennes URL du `.env`.

Le Compose de production est `TableMasterApi/docker-compose.yml`. Il connecte
`api` à `postgres-network` et à `tablemaster-proxy`, mais pas PostgreSQL au
réseau du proxy. Créer `tablemaster-proxy` sur Oracle avant le déploiement et
y connecter durablement Nginx Proxy Manager (voir `ops/README.md`).

Le proxy host NPM de `api.tablemaster.maxencecoeur.fr` doit utiliser le schéma
`http`, l'hôte `TableMasterApi-prod`, le port interne `8080`, le support WebSocket
et un certificat HTTPS public. Le proxy ne doit pas journaliser la query string
`access_token` de `/reservationHub`.

Le Compose réellement déployé est `TableMasterApi/docker-compose.yml`. Par
défaut, il monte la clé Firebase depuis son propre répertoire (utile en local).
Sur Oracle, `FIREBASE_SERVICE_ACCOUNT_HOST_FILE=../tablemaster-firebase.json`
monte le fichier déjà présent à la racine du dépôt, en lecture seule.

`ops/nginx-api.reference.conf` documente le comportement attendu d'un proxy
Nginx classique ; il n'est pas déployé automatiquement par NPM. Après
configuration de NPM, ouvrir une connexion Web SignalR puis vérifier qu'aucune
occurrence de `access_token` n'est présente dans ses logs récents.

### 2.4. Appliquer les migrations BDD

Les migrations DbUp **s'exécutent automatiquement** au démarrage de l'API (scripts embarqués dans `TableMasterApi/sql-scripts/`). Aucune action manuelle requise.

> Pour une exécution manuelle (debug) : lancer `dotnet run` localement contre la BDD cible avec la connection string. DbUp affichera dans la console les scripts appliqués.

Pour la livraison sessions/temps réel, préparer le build Flutter coordonné avant la bascule. Les migrations `009`, `010` et `011` sont additives : déployer les migrations et l'API, vérifier le worker d'outbox, puis publier Flutter. Ne pas supprimer les nouvelles tables lors d'un rollback applicatif et ne pas réouvrir le hub anonyme.

### 2.5. Vérification post-déploiement

```bash
# Liveness
curl -fsS https://api.tablemaster.maxencecoeur.fr/health  # → {"status":"Healthy"}

# Readiness (vérifie DB + Firebase)
curl -fsS https://api.tablemaster.maxencecoeur.fr/ready

# Version exposée (si Swagger activé)
curl -fsS https://api.tablemaster.maxencecoeur.fr/swagger/v1/swagger.json | jq '.info.version'

# Endpoint métier de smoke
curl -fsS -H "x-api-version: 1.0" https://api.tablemaster.maxencecoeur.fr/api/Restaurant
```

Tous les checks doivent être OK avant de considérer le déploiement validé.

En environnement `Development`, l'endpoint `GET /api/monitoring/sentry-test` envoie un message de test dans Sentry. En production, vérifier Sentry via le dashboard après un déploiement ou une erreur contrôlée côté staging.

---

## 3. Rollback

`ops/deploy-api.sh` revient automatiquement à l'image précédente si les
contrôles post-déploiement échouent. Pour un rollback manuel, utiliser le SHA
complet de l'image stable et les scripts du dépôt :

```bash
ssh ubuntu@141.253.120.94
cd /opt/tablemaster/prod/TableMasterApi
export FIREBASE_SERVICE_ACCOUNT_HOST_FILE=../tablemaster-firebase.json
export CORS_ALLOWED_ORIGIN=https://client.tablemaster.maxencecoeur.fr
export APP_LINKS_BASE_URL=https://client.tablemaster.maxencecoeur.fr
API_IMAGE_TAG=<sha-precedent> APP_VERSION=<version-precedente> \
  SENTRY_RELEASE=<release-precedente> ./ops/deploy-api.sh
```

> Les migrations DbUp sont conçues idempotentes mais **non réversibles**. Un rollback applicatif ne rollback pas le schéma BDD. Si un script destructif est passé, restaurer depuis la sauvegarde (cf. §5).

---

## 4. Premier déploiement (cold start)

Si l'environnement n'existe pas encore :

1. Créer `/opt/tablemaster/prod/TableMasterApi/.env` ou
   `/opt/tablemaster/prod/TableMasterApi/TableMasterApi/.env` à partir du
   template `TableMasterApi/.env.example`.
2. Déposer la clé Firebase dans
   `/opt/tablemaster/prod/TableMasterApi/tablemaster-firebase.json`
   (`chmod 400`) ; elle reste hors Git.
3. Créer le réseau Docker `tablemaster-proxy`, puis démarrer PostgreSQL depuis
   la racine du dépôt :
   ```bash
   docker network create tablemaster-proxy
   docker compose --env-file .env -f TableMasterApi/docker-compose.yml up -d postgres
   docker compose --env-file .env -f TableMasterApi/docker-compose.yml exec postgres pg_isready
   ```
   Adapter `--env-file` vers `TableMasterApi/.env` si c'est l'emplacement choisi.
4. Exporter `FIREBASE_SERVICE_ACCOUNT_HOST_FILE=../tablemaster-firebase.json`,
   puis démarrer l'API avec `ops/deploy-api.sh` : les migrations s'exécutent automatiquement.
5. Vérifier `/ready`.

---

## 5. Sauvegardes BDD

Sauvegarde quotidienne automatique via cron :

```cron
0 3 * * * docker compose exec -T postgres pg_dump -U $POSTGRES_USER $POSTGRES_DB | gzip > /opt/backups/tablemaster-$(date +\%F).sql.gz
```

Tests de restauration **mensuels** (procédure dans `docs/POSTGRESQL_API_TESTS.md`).

---

## 6. Logs et diagnostic

| Source | Commande |
| --- | --- |
| API runtime | `docker compose logs -f api --tail=200` |
| Logs Nginx (front) | `journalctl -u nginx -f` |
| Erreurs applicatives | Dashboard Sentry API |
| Métriques santé | `curl /health` et `/ready` |

---

## 7. Commandes de diagnostic rapide

```bash
# État des conteneurs
docker compose ps

# Inspecter l'image courante
docker inspect ghcr.io/<owner>/tablemaster-api:latest | jq '.[0].Config.Env'

# Mémoire / CPU
docker stats --no-stream

# Lister les migrations BDD appliquées
docker compose exec postgres psql -U $POSTGRES_USER -d $POSTGRES_DB \
  -c "SELECT * FROM SchemaVersions ORDER BY Id;"
```

---

## 8. Voir aussi

- `README.md` — installation locale.
- `CI_CD.md` — pipeline GitHub Actions détaillé.
- `SECURITY.md` — modèle de menaces et mesures OWASP.
- `docs/MISE-A-JOUR.md` — procédure de montée de version (à venir).
- `docs/SECURITE_SESSIONS_RESERVATIONS.md` — contrats d'erreur, SignalR, dates, sessions et reprise de l'outbox.
