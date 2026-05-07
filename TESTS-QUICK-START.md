# 🎯 Guide Rapide - Tests Unitaires

## ✅ Status
- **Tests créés** : 21 ✅
- **Tests réussis** : 21/21 ✅ 
- **Temps d'exécution** : ~71ms
- **Framework** : xUnit + Moq + FluentAssertions

---

## 🚀 Exécution Rapide

### Option 1 : Command Simple
```bash
cd /home/maxence/Dev/TableMaster/TableMasterApi
dotnet test
```

### Option 2 : Avec le Script
```bash
bash run-tests.sh
```

### Option 3 : Depuis VS Code
1. Ouvrir la palette : `Ctrl+Shift+P`
2. Taper : `Test: Run All Tests`

---

## 📊 Résultats

```
✅ TableMasterApi.Tests.Services.JwtServiceTests (14 tests)
   ├─ ExtractTokenFromAuthorization_WithValidBearerToken_ShouldReturnToken
   ├─ GenerateAccessToken_WithValidUserId_ShouldCreateValidJwt
   ├─ GenerateAccessToken_ShouldIncludeUserIdInClaims
   ├─ GenerateAccessToken_ShouldHaveExpirationInFuture
   ├─ ExtractUserIdFromToken_WithValidToken_ShouldReturnCorrectUserId
   ├─ ExtractUserIdFromToken_WithInvalidToken_ShouldThrowException
   ├─ ExtractUserIdFromToken_WithMultipleUsers_ShouldExtractCorrectId
   ├─ GenerateRefreshToken_ShouldReturnValidBase64String
   ├─ GenerateRefreshToken_ShouldGenerateDifferentTokensEachTime
   ├─ GenerateRefreshToken_ShouldHaveSufficientLength
   └─ ... et 4 autres

✅ TableMasterApi.Tests.Controllers.AuthenticationIntegrationTests (7 tests)
   ├─ AuthenticationFlow_GenerateTokenAndExtractUserId_ShouldMaintainIntegrity
   ├─ AuthenticationFlow_MultipleRequests_EachTokenIsUnique
   ├─ TokenRefresh_GeneratedTokensAreUniqueAndSecure
   ├─ JwtCompliance_TokenContainsRequiredClaims
   ├─ JwtCompliance_TokenHasCorrectSignatureAlgorithm
   ├─ TokenSecurity_ExpiredTokenShouldBeIgnored
   └─ LifecycleDemonstration_UserLoginToTokenValidation_CompleteFlow

Réussi!  - Total : 21 tests, Durée : 71 ms
```

---

## 📁 Structure des Fichiers

```
TableMasterApi.Tests/
├── Fixtures/
│   └── JwtConfigFixture.cs           ← Configuration et données de test
├── Services/
│   └── JwtServiceTests.cs            ← Tests JWT (14 tests)
├── Controllers/
│   └── AuthenticationIntegrationTests.cs  ← Tests intégration (7 tests)
├── GlobalUsings.cs                   ← Imports globaux
├── xunit.runner.json                 ← Config xUnit
├── TableMasterApi.Tests.csproj       ← Fichier projet
├── README.md                         ← Documentation détaillée
├── SUMMARY.md                        ← Résumé complet
└── run-tests.sh                      ← Script de lancement
```

---

## 🔍 Tests Clés

### JWT Service - Génération de Token
```csharp
[Fact]
public void GenerateAccessToken_WithValidUserId_ShouldCreateValidJwt()
{
    // Crée un token valide avec :
    // ✓ Signature HMAC SHA256
    // ✓ Claims utilisateur
    // ✓ Expiration en 5 minutes
    // ✓ Issuer/Audience configurés
}
```

### Authentification - Flux Complet
```csharp
[Fact]
public void AuthenticationFlow_GenerateTokenAndExtractUserId_ShouldMaintainIntegrity()
{
    // Simule :
    // 1. Génération du token
    // 2. Envoi dans Authorization header
    // 3. Extraction et validation du token
    // 4. Vérification que l'UserId est correct
}
```

---

## 💡 Utilisation pour Développeurs

### Ajouter un Nouveau Test
```bash
# 1. Créer le fichier test
# Services/MyServiceTests.cs

# 2. Écrire le test
[Fact]
public void MyTest()
{
    // Arrange
    // Act
    // Assert
}

# 3. Exécuter
dotnet test
```

### Déboguer un Test
```bash
# Lancer avec verbosité maximale
dotnet test --logger "console;verbosity=detailed"

# Lancer un test spécifique
dotnet test --filter "MyTest"
```

---

## 🎓 Patterns Utilisés

| Pattern | Exemple |
|---------|---------|
| **Fixtures** | `IClassFixture<JwtConfigFixture>` |
| **Theory** | `[Theory] [InlineData(...)]` |
| **AAA** | Arrange → Act → Assert |
| **Mocking** | `new Mock<Interface>()` |
| **Assertions Fluides** | `.Should().Be(expected)` |

---

## 🚨 Erreurs Courantes

### ❌ Les tests échouent avec "No Tests Found"
**Solution** : Vérifier que le fichier `.csproj` contient la bonne référence au projet principal
```xml
<ProjectReference Include="..\TableMasterApi\TableMasterApi.csproj" />
```

### ❌ Error: "Can't mock non-virtual members"
**Solution** : Créer une interface et utiliser l'injection de dépendance
```csharp
// Au lieu de Mock<UserDAL>
Mock<IUserDAL>
```

### ❌ Tests lents
**Solution** : Utiliser des fixtures pour partager l'état entre tests
```csharp
public class MyTests : IClassFixture<MyFixture>
```

---

## 📚 Ressources

- [xUnit Documentation](https://xunit.net/)
- [Moq (Mocking Library)](https://github.com/moq/moq4)
- [FluentAssertions](https://fluentassertions.com/)
- [Unit Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

---

## ✨ Points Clés

✅ **Pas de dépendance DB** — Utilise des mocks  
✅ **Tests indépendants** — Chacun peut s'exécuter seul  
✅ **Noms explicites** — Structure claire du test  
✅ **Réutilisable** — Fixtures pour partager la config  
✅ **Rapide** — 21 tests en 71ms  

---

**Prêt à écrire plus de tests ? Bonne chance ! 🚀**
