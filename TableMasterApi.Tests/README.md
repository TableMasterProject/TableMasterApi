# Guide des Tests Unitaires - TableMasterApi

## 📋 Vue d'ensemble

Ce projet contient des tests unitaires complets utilisant :
- **xUnit** : Framework de test
- **Moq** : Mocking des dépendances (Base de données)
- **FluentAssertions** : Assertions lisibles

## 🗂️ Structure du Projet

```
TableMasterApi.Tests/
├── Fixtures/
│   └── JwtConfigFixture.cs          # Configuration JWT et données de test
├── Services/
│   └── JwtServiceTests.cs           # Tests du service JWT
├── Controllers/
│   └── UserControllerTests.cs       # Tests du contrôleur utilisateur
└── TableMasterApi.Tests.csproj      # Fichier projet de test
```

## 🎯 Fonctionnalités Testées

### 1. **JwtService** (Authentification)

#### Tests Inclus :
- ✅ Extraction du token du header `Authorization`
- ✅ Génération de tokens JWT valides
- ✅ Inclusion des claims utilisateur dans le token
- ✅ Vérification de l'expiration (5 minutes)
- ✅ Extraction de l'UserId du token
- ✅ Gestion des tokens invalides
- ✅ Génération de refresh tokens
- ✅ Unicité des refresh tokens

**Exemple de test** :
```csharp
[Fact]
public void GenerateAccessToken_WithValidUserId_ShouldCreateValidJwt()
{
    // Arrange
    long userId = 123;
    
    // Act
    var token = _jwtService.GenerateAccessToken(userId);
    
    // Assert
    token.Should().NotBeNullOrEmpty();
    var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
    jwtToken.Issuer.Should().Be(_fixture.JwtConfig.Issuer);
}
```

---

### 2. **UserController** (Gestion des Utilisateurs)

#### Tests Inclus :
- ✅ Récupération d'un utilisateur par ID
- ✅ Gestion des utilisateurs inexistants (404)
- ✅ Gestion des erreurs de base de données (500)
- ✅ Création d'utilisateur
- ✅ Validation des données
- ✅ Intégration avec JWT

#### Mocking de la Base de Données :
```csharp
_mockUserDAL
    .Setup(dal => dal.GetUserById(userId))
    .ReturnsAsync(testUser);

_mockUserDAL
    .Setup(dal => dal.GetUserById(999))
    .ReturnsAsync((UserOut)null!);
```

**Exemple de test** :
```csharp
[Fact]
public async Task GetUserById_WithValidIdAndAuthorization_ShouldReturnOkWithUser()
{
    // La base de données est mockée - aucune connexion réelle n'est effectuée
    var result = await controller.GetUserById(userId);
    
    var okResult = result.Result as OkObjectResult;
    okResult.StatusCode.Should().Be(200);
}
```

---

## 🚀 Exécution des Tests

### Via la ligne de commande :

```bash
# Aller au répertoire du projet
cd /home/maxence/Dev/TableMaster/TableMasterApi

# Exécuter tous les tests
dotnet test

# Exécuter les tests avec verbosité
dotnet test --verbosity normal

# Exécuter un fichier de test spécifique
dotnet test TableMasterApi.Tests/Services/JwtServiceTests.cs

# Exécuter un test spécifique
dotnet test --filter "JwtServiceTests.GenerateAccessToken_WithValidUserId_ShouldCreateValidJwt"
```

### Via Visual Studio Code :

1. Ouvrir la palette de commandes : `Ctrl+Shift+P`
2. Taper : "Test: Run All Tests"
3. Ou cliquer sur "Run All Tests" en haut du fichier de test

### Via Visual Studio :

1. Ouvrir **Test Explorer** : `Ctrl+E, T`
2. Sélectionner les tests à exécuter
3. Cliquer sur "Run"

---

## 📊 Coverage des Tests

### JwtService (14 tests)
- 3 tests d'extraction du token
- 4 tests de génération d'access token
- 3 tests d'extraction d'UserId
- 3 tests de refresh token

### UserController (9 tests)
- 4 tests de GetUserById
- 2 tests de CreateUser
- 3 tests d'intégration avec JWT

**Total : 23 tests unitaires**

---

## 🔧 Patterns de Test Utilisés

### 1. **Arrange-Act-Assert (AAA)**
```csharp
[Fact]
public void TestMethod()
{
    // Arrange - Préparation
    var data = new TestData();
    
    // Act - Exécution
    var result = SomeMethod(data);
    
    // Assert - Vérification
    result.Should().Be(expectedValue);
}
```

### 2. **Theory with InlineData**
```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("invalid")]
public void TestMultipleCases(string input)
{
    // Même test exécuté avec différentes données
}
```

### 3. **Mock Setup**
```csharp
_mockUserDAL
    .Setup(dal => dal.GetUserById(It.IsAny<long>()))
    .ReturnsAsync(testUser);
```

### 4. **Verification**
```csharp
_mockUserDAL.Verify(dal => dal.GetUserById(userId), Times.Once);
```

---

## 🎓 Comment Ajouter Vos Propres Tests

### Étape 1 : Créer un fichier de test
```csharp
// TableMasterApi.Tests/Controllers/RestaurantControllerTests.cs
public class RestaurantControllerTests
{
    [Fact]
    public async Task MyTest()
    {
        // Arrange
        var mockDAL = new Mock<RestaurantDAL>();
        
        // Act
        
        // Assert
    }
}
```

### Étape 2 : Utiliser les Fixtures
```csharp
public class MyTests : IClassFixture<JwtConfigFixture>
{
    private readonly JwtConfigFixture _fixture;
    
    public MyTests(JwtConfigFixture fixture)
    {
        _fixture = fixture;
    }
}
```

### Étape 3 : Créer des mocks pour la DB
```csharp
var mockDAL = new Mock<SomeDAL>();
mockDAL.Setup(x => x.GetData()).ReturnsAsync(testData);
```

---

## 📝 Bonnes Pratiques

✅ **À Faire** :
- Un test = une seule responsabilité
- Noms de tests explicites (`GetUserById_WithValidId_ShouldReturnOk`)
- Utiliser les fixtures pour partager la configuration
- Vérifier les appels aux mocks
- Tester les cas d'erreur

❌ **À Éviter** :
- Tests qui dépendent les uns des autres
- Tests trop complexes (>20 lignes)
- Appels réels à la base de données
- Assertions vagues (`result.Should().NotBeNull()`)
- Secrets hardcodés dans les tests

---

## 🔗 Ressources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions](https://fluentassertions.com/)
- [Test-Driven Development](https://martinfowler.com/bliki/TestDrivenDevelopment.html)

---

## 🐛 Dépannage

### Les tests ne compilent pas

```bash
# Nettoyer et rebâtir
dotnet clean
dotnet build
```

### Les tests ne trouvent pas les références

```bash
# Vérifier que le chemin du ProjectReference est correct dans .csproj
dotnet restore
```

### Erreur : "Database connection failed"

C'est normal ! Les tests utilisent des mocks, pas la vraie DB. Vérifiez que :
- ✅ Le mock est correctement configuré
- ✅ `Setup()` est appelé avant l'utilisation du mock
- ✅ Vous utilisez `.ReturnsAsync()` pour les méthodes async

---

**Créé le:** 7 mai 2026
**Framework:** xUnit + Moq + FluentAssertions
