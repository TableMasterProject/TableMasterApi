# 🚀 CI/CD - TableMaster API

## GitHub Actions Workflow

Ce repo utilise GitHub Actions pour tester et compiler l'API automatiquement.

### 📋 Workflow: `.github/workflows/dotnet.yml`

**Déclenché:**
- ✅ Chaque `push` sur `main` ou `develop`
- ✅ Chaque `pull_request` vers `main` ou `develop`

**Actions exécutées:**
1. 🔧 Setup .NET 10.0
2. 📦 Restaure les dépendances NuGet
3. 🔨 Build en Release
4. 🧪 Exécute les 31 tests unitaires (xUnit)
5. 📊 Upload les résultats de test
6. 📦 Publie l'API compilée
7. 💾 Upload les artefacts

**Durée:** ~2-3 minutes

---

## 📊 Test Results

```
✅ 31 / 31 tests passing
- JwtServiceTests: 14 tests (tokens, refresh, extraction)
- AuthControllerTests: 4 tests (login, refresh)
- AuthenticationIntegrationTests: 7 tests (full auth flow)
- UserControllerTests: 3 tests (register, password, delete)
- RestaurantControllerTests: 3 tests (CRUD)
```

---

## 📦 Artefacts

Après chaque run, les artefacts sont disponibles dans **Actions → [Run] → Artifacts:**

### `api-build/`
```
TableMasterApi/
├── TableMasterApi.dll
├── TableMasterApi.runtimeconfig.json
├── appsettings.json
├── appsettings.Development.json
├── Dapper.dll
├── FirebaseAdmin.dll
└── ... (toutes les dépendances)
```

**Utilisation:**
```bash
# Télécharger l'API compilée
# Déployer sur serveur:
dotnet TableMasterApi.dll --urls "http://0.0.0.0:5000"
```

### `test-results/`
```
test-results.trx
```
Rapport XUnit détaillé des tests.

---

## 🔍 View Logs

1. Aller à **Actions** sur GitHub
2. Cliquer sur le workflow qui vous intéresse
3. Cliquer sur le run
4. Voir les logs complets de chaque step

---

## 🛠️ Troubleshooting

### ❌ Workflow ne s'exécute pas

**Vérifier:**
- ✅ Workflow activé: **Actions → ... → Enable workflows**
- ✅ Branches `main` et `develop` existent
- ✅ Fichier `.github/workflows/dotnet.yml` existe

### ❌ Tests échouent

**Relancer localement:**
```bash
dotnet test TableMasterApi.Tests/
```

**Vérifier les logs:**
```bash
dotnet test --verbosity detailed
```

### ❌ Build échoue

**Relancer localement:**
```bash
dotnet build -c Release
```

**Vérifier les dépendances:**
```bash
dotnet restore
```

---

## 🚀 Déploiement

Pour déployer automatiquement après les tests, ajouter un step au workflow:

```yaml
- name: Deploy to Server
  if: success()
  run: |
    scp -r ./publish/* user@server:/app/tablemaster-api
    ssh user@server 'systemctl restart tablemaster-api'
  env:
    DEPLOY_KEY: ${{ secrets.DEPLOY_KEY }}
```

Ajouter le secret **DEPLOY_KEY** dans **Settings → Secrets and variables → Actions**

## 🔎 Monitoring Sentry

L'API utilise `Sentry.AspNetCore` pour remonter les exceptions et traces HTTP. Le DSN ne doit pas être commité :

- local : `dotnet user-secrets set "Sentry:Dsn" "<dsn-api>"`
- production : variable d'environnement `Sentry__Dsn`
- test dev : appeler `GET /api/monitoring/sentry-test`

---

## 📚 Ressources

- [GitHub Actions Docs](https://docs.github.com/en/actions)
- [DotNet Setup Action](https://github.com/actions/setup-dotnet)
- [Test Results Report](https://github.com/dorny/test-reporter)

---

**Mis à jour:** 7 mai 2026
