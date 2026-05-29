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
- Reverse-proxy **Nginx** (avec Let's Encrypt) déjà configuré pour terminer le TLS et proxifier vers le port API.
- Un utilisateur non-root pour exécuter Docker (`usermod -aG docker $USER`).
- Volumes persistants pour : PostgreSQL data, clé Firebase, logs.

### Secrets attendus (jamais dans Git)

| Variable | Source | Description |
| --- | --- | --- |
| `ConnectionStrings__Postgres` | env serveur | DSN PostgreSQL : `Host=…;Port=5432;Database=…;Username=…;Password=…` |
| `Jwt__Key` | env serveur | Secret JWT (64 chars min) |
| `Jwt__Issuer` / `Jwt__Audience` | env serveur | Identifiants JWT |
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
ssh deploy@api.tablemaster.lmpe.ovh
cd /opt/tablemaster
docker compose pull api          # tire la nouvelle image
docker compose up -d api         # remplace le conteneur en place
docker compose ps                # vérifie que api est "healthy"
```

Le compose attendu (extrait) :

```yaml
services:
  api:
    image: ghcr.io/<owner>/tablemaster-api:latest
    restart: unless-stopped
    env_file: /opt/tablemaster/.env.prod
    volumes:
      - /opt/tablemaster/secrets/tablemaster-firebase.json:/app/tablemaster-firebase.json:ro
    ports:
      - "127.0.0.1:5080:8080"     # exposé seulement en local, Nginx proxy devant
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/ready"]
      interval: 30s
      timeout: 5s
      retries: 3
```

### 2.4. Appliquer les migrations BDD

Les migrations DbUp **s'exécutent automatiquement** au démarrage de l'API (scripts embarqués dans `TableMasterApi/sql-scripts/`). Aucune action manuelle requise.

> Pour une exécution manuelle (debug) : lancer `dotnet run` localement contre la BDD cible avec la connection string. DbUp affichera dans la console les scripts appliqués.

### 2.5. Vérification post-déploiement

```bash
# Liveness
curl -fsS https://api.tablemaster.lmpe.ovh/health  # → {"status":"Healthy"}

# Readiness (vérifie DB + Firebase)
curl -fsS https://api.tablemaster.lmpe.ovh/ready

# Version exposée (si Swagger activé)
curl -fsS https://api.tablemaster.lmpe.ovh/swagger/v1/swagger.json | jq '.info.version'

# Endpoint métier de smoke
curl -fsS -H "x-api-version: 1.0" https://api.tablemaster.lmpe.ovh/api/Restaurant
```

Tous les checks doivent être OK avant de considérer le déploiement validé.

En environnement `Development`, l'endpoint `GET /api/monitoring/sentry-test` envoie un message de test dans Sentry. En production, vérifier Sentry via le dashboard après un déploiement ou une erreur contrôlée côté staging.

---

## 3. Rollback

L'image précédente reste taggée par son `sha` court dans GHCR. Pour revenir en arrière :

```bash
ssh deploy@api.tablemaster.lmpe.ovh
cd /opt/tablemaster
# Identifier le sha de la version stable précédente :
docker compose images api
# Forcer le tag :
sed -i 's|tablemaster-api:.*|tablemaster-api:<sha-precedent>|' docker-compose.yml
docker compose pull api
docker compose up -d api
```

> Les migrations DbUp sont conçues idempotentes mais **non réversibles**. Un rollback applicatif ne rollback pas le schéma BDD. Si un script destructif est passé, restaurer depuis la sauvegarde (cf. §5).

---

## 4. Premier déploiement (cold start)

Si l'environnement n'existe pas encore :

1. Créer le `/opt/tablemaster/.env.prod` à partir du template `.env.example`.
2. Déposer la clé Firebase : `/opt/tablemaster/secrets/tablemaster-firebase.json` (`chmod 400`).
3. Démarrer PostgreSQL :
   ```bash
   docker compose up -d postgres
   docker compose exec postgres pg_isready
   ```
4. Démarrer l'API : les migrations s'exécutent automatiquement.
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
