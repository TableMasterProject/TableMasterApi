# 🔧 Instructions Copilot - TableMasterApi

**Dernière mise à jour:** 7 mai 2026  
**Version API:** v1, v2 (Asp.Versioning)  
**Langage:** C# (.NET 10 / ASP.NET Core 8)

---

## 📋 Table des matières

1. [Stack Technologique](#stack-technologique)
2. [Architecture & Patterns](#architecture--patterns)
3. [Conventions de Code](#conventions-de-code)
4. [Structure des Dossiers](#structure-des-dossiers)
5. [Instructions de Réponse](#instructions-de-réponse)
6. [Composants Clés](#composants-clés)
7. [Tests & Mocking](#tests--mocking)

---

## 📚 Stack Technologique

### Framework & Runtimes
- **Framework:** ASP.NET Core 8 (SDK: .NET 10)
- **Runtime:** .NET 10.0
- **Nullable:** Activé (`<Nullable>enable</Nullable>`)
- **ImplicitUsings:** Activé

### Frameworks et Bibliothèques Clés

| Technologie | Version | Rôle |
|-------------|---------|------|
| **Asp.Versioning.Mvc** | 8.1.1 | Gestion du versionnage d'API (v1, v2) |
| **Dapper** | 2.1.35 | Micro-ORM pour SQL Server |
| **DbUp.SqlServer** | 7.2.0 | Migrations SQL Server |
| **Microsoft.AspNetCore.Authentication.JwtBearer** | 8.0.12 | Authentification JWT |
| **Microsoft.AspNetCore.SignalR** | 1.2.0 | Communication en temps réel |
| **Swashbuckle.AspNetCore** | 7.2.0 | Swagger/OpenAPI |
| **Microsoft.Data.SqlClient** | 6.1.4 | Driver SQL Server |
| **FirebaseAdmin** | 3.5.0 | Cloud Messaging (FCM) |
| **System.IdentityModel.Tokens.Jwt** | 8.3.1 | Token JWT |

### Base de Données
- **SQL Server 2025** (latest)
- **Migrations:** DbUp (fichiers .sql dans `sql-scripts/`)
- **ORM:** Dapper (requêtes SQL brutes avec mapping)

### Authentification & Sécurité
- **JWT Bearer** pour l'authentification
- **SHA256** pour le hashing des mots de passe et refresh tokens
- **SignalR** pour les notifications en temps réel (ReservationHub)
- **Firebase Cloud Messaging** pour les notifications push

### Tests
- **xUnit** 2.6.6 - Framework de test
- **Moq** 4.20.70 - Mocking (interfaces mockables)
- **FluentAssertions** 6.12.0 - Assertions fluides

---

## 🏗️ Architecture & Patterns

### Architectural Pattern: Layered Architecture avec Dependency Injection

```
Controllers (HTTP Entry Points)
    ↓
Services (Business Logic)
    ↓
DAL/Interfaces (Data Access - Mockable)
    ↓
Database (SQL Server)
```

### Design Patterns Utilisés

#### 1. **Dependency Injection (DI)**
- Container IoC dans `Program.cs`
- **Scope:** 
  - `AddSingleton` : `JwtService`, `FcmService`, `ConfigPerso`
  - `AddScoped` : Tous les DAL (une instance par requête HTTP)
- **Bénéfice:** Testabilité, découplage, injection de mocks

```csharp
builder.Services.AddScoped<IUserDAL, UserDAL>();
builder.Services.AddScoped<IAuthDAL, AuthDAL>();
// ... etc
```

#### 2. **Interface-Based DAL (Data Access Layer)**
- Chaque DAL implémente une interface (`IUserDAL`, `IRestaurantDAL`, etc.)
- Permet le mocking complet dans les tests unitaires
- Controllers injectent l'interface, pas l'implémentation

```csharp
public class UserController : ControllerBase
{
    private readonly IUserDAL _userDAL;
    private readonly IAuthDAL _authDAL;
    
    public UserController(IUserDAL userDAL, IAuthDAL authDAL, JwtService jwtService)
    {
        _userDAL = userDAL;
        _authDAL = authDAL;
    }
}
```

#### 3. **JWT Token Management**
- Génération: `JwtService.GenerateAccessToken(userId)` → 5 min d'expiration
- Refresh: `JwtService.GenerateRefreshToken()` → 64 bytes Base64 aléatoire
- Validation: Middleware ASP.NET Core automatique
- Extraction: `_jwtService.ExtractUserIdFromToken(token)`

#### 4. **FCM Integration**
- Service singleton: `FcmService`
- Firebase Admin SDK pour les notifications push
- Configuration via `tablemaster-firebase.json`

#### 5. **SignalR for Real-time**
- Hub: `ReservationHub` pour les mises à jour de réservations
- Groupes: `RESTAURANT_GROUP_PREFIX`, `USER_GROUP_PREFIX`
- Méthode broadcast: `SendAsync(methodName, data)`

---

## 📝 Conventions de Code

### Nommage

#### Classes
```csharp
// Controllers
public class UserController : ControllerBase { }
public class RestaurantController : ControllerBase { }

// DAL Classes
public class UserDAL : IUserDAL { }
public class RestaurantDAL : IRestaurantDAL { }

// Services
public class JwtService { }
public class FcmService { }

// Models (Input/Output)
public class UserIn { }          // DTO d'entrée
public class UserOut : UserIn { } // DTO de sortie (hérite de Input)
public class LoginUserIn { }
public class LoginUserOut { }
```

#### Interfaces
```csharp
// PascalCase avec préfixe 'I'
public interface IUserDAL { }
public interface IAuthDAL { }
public interface IRestaurantDAL { }
```

#### Propriétés
```csharp
// PascalCase
public string Email { get; set; }
public long UserId { get; set; }
public DateTime CreatedAt { get; set; }
```

#### Variables Locales & Paramètres
```csharp
// camelCase
var userId = 123;
var userEmail = "user@example.com";
long? refreshTokenId = await userDal.GetUserIdByRefreshToken(hash);
```

#### Methods
```csharp
// PascalCase, verbes explicites
public async Task<UserOut?> GetUserById(long id)
public async Task<bool> SaveRefreshToken(long userId, string hashedToken, DateTime expiry)
public async Task<LoginUserOut> Login(LoginUserIn loginUser)
public string GenerateAccessToken(long userId)
public bool VerifyPassword(string hashedPassword, string plainPassword)
```

### Routing & HTTP

#### Route Convention
```csharp
[ApiVersion("1.0")]
[Route("api/[controller]")]      // /api/user, /api/restaurant
[ApiController]
public class UserController : ControllerBase
{
    [HttpPost]                   // POST /api/user
    public async Task<ActionResult<LoginUserOut>> AddUser([FromBody] UserIn user)
    
    [HttpGet("{id}")]            // GET /api/user/123
    [Authorize]
    public async Task<ActionResult<UserOut>> GetUserById(long id)
    
    [HttpPut]                    // PUT /api/user
    [Route("Password")]          // PUT /api/user/Password
    public async Task<ActionResult<bool>> PutPassword([FromBody] PasswordEntity passwordEntity)
}
```

#### Version API
- URL-based: `/api/v1/user`, `/api/v2/user`
- Header-based: `x-api-version: 1.0`

#### Status Codes
```csharp
// Succès
return Ok(user);                 // 200 OK

// Créé
return Created(uri, resource);   // 201 Created

// Bad Request
return BadRequest("Message");    // 400

// Non trouvé
return NotFound("Message");      // 404

// Unauthorized / Forbidden
return Unauthorized();           // 401
return StatusCode(403, "Message"); // 403 Forbidden

// Server Error
return StatusCode(500, e.Message); // 500 Internal Server Error
```

### Async/Await Pattern
```csharp
// Toutes les opérations DB sont async
public async Task<UserOut?> GetUserById(long id)
{
    using (var connection = new SqlConnection(_config.ConnectionString))
    {
        var user = await connection.QuerySingleOrDefaultAsync<UserOut>(query, new { Id = id });
        return user;
    }
}
```

### Try-Catch Pattern
```csharp
try
{
    // Logique métier
    var user = await _userDAL.GetUserById(id);
    if (user == null) return NotFound();
    
    return Ok(user);
}
catch (SqlException e)
{
    if (e.Number == 2627) // Violation de contrainte unique
        return StatusCode(403, "L'Email existe deja");
    return StatusCode(500, e.Message);
}
catch (Exception e)
{
    return StatusCode(500, e.Message);
}
```

---

## 📂 Structure des Dossiers

```
TableMasterApi/
│
├── Controllers/                   # Entry points HTTP
│   ├── AuthController.cs         # POST /api/auth (Login, Refresh)
│   ├── UserController.cs         # User management
│   ├── RestaurantController.cs   # Restaurant CRUD
│   ├── MenuController.cs         # Menu management
│   ├── TableController.cs        # Table management
│   ├── ReservationController.cs  # Reservation handling
│   ├── ReviewController.cs       # Review system
│   ├── DailyActivityController.cs # Operating hours
│   ├── ClosedDayExceptionController.cs # Holiday/Closed days
│   └── DeviceTokenController.cs  # Push notification tokens
│
├── DAL/                           # Data Access Layer
│   ├── Interfaces/               # Contrats testables
│   │   ├── IAuthDAL.cs          # Password verification
│   │   ├── IUserDAL.cs          # User CRUD + Refresh tokens
│   │   ├── IRestaurantDAL.cs    # Restaurant management
│   │   ├── IMenuDAL.cs          # Menu CRUD
│   │   ├── ITableDAL.cs         # Table management
│   │   ├── IReservationDAL.cs   # Reservation handling
│   │   ├── IReviewDAL.cs        # Review CRUD
│   │   ├── IDailyActivityDAL.cs # Operating hours
│   │   ├── IClosedDayExceptionDAL.cs # Holidays
│   │   └── IDeviceTokenDAL.cs   # Device tokens
│   ├── AuthDAL.cs               # Implémentation
│   ├── UserDAL.cs
│   ├── RestaurantDAL.cs
│   ├── MenuDAL.cs
│   ├── TableDAL.cs
│   ├── ReservationDAL.cs
│   ├── ReviewDAL.cs
│   ├── DailyActivityDAL.cs
│   ├── ClosedDayExceptionDAL.cs
│   └── DeviceTokenDAL.cs
│
├── Service/                       # Business logic services
│   ├── JwtService.cs            # Token generation & extraction
│   ├── FcmService.cs            # Firebase Cloud Messaging
│   └── GoogleMapsService.cs     # Geolocation services
│
├── Model/                         # DTOs et entities
│   ├── User.cs                  # UserIn, UserOut
│   ├── Restaurant.cs            # RestaurantIn, RestaurantOut
│   ├── LoginUser.cs             # LoginUserIn, LoginUserOut
│   ├── Menu.cs                  # MenuIn, MenuOut
│   ├── TableEntity.cs           # TableEntityIn, TableEntityOut
│   ├── Reservation.cs           # ReservationIn, ReservationOut
│   ├── Review.cs                # ReviewIn, ReviewOut
│   ├── DailyActivity.cs         # DailyActivityIn/Out
│   ├── ClosedDayException.cs    # ClosedDayExceptionIn/Out
│   ├── PasswordEntity.cs        # OldPassword, NewPassword
│   ├── SearchRestaurant.cs      # Filtres de recherche
│   └── ConfigPerso.cs           # Configuration DB/JWT
│
├── Hubs/                         # SignalR Hubs
│   └── ReservationHub.cs        # Real-time reservation updates
│
├── Database/                      # SQL Server project
│   ├── Database.sqlproj
│   └── Table/                   # SQL Server tables
│
├── Program.cs                    # Configuration DI + Middleware
├── Dockerfile                    # Conteneurisation
├── docker-compose.yml            # Orchestre Docker
├── TableMasterApi.csproj        # Manifest NuGet
├── appsettings.json             # Config production
├── appsettings.Development.json # Config développement
└── sql-scripts/                 # Migrations DbUp
```

---

## 📢 Instructions de Réponse

### Langue & Tone
- **Toujours répondre en français** (technique et concis)
- **Tone:** Professionnel, direct, sans verbosité
- **Exemples de code:** Avec commentaires en français

### Style de Réponse

#### Pour les bugs/erreurs
```
❌ **Problème:** Description concise du bug
🔍 **Cause:** Analyse rapide
✅ **Solution:** Code corrigé
📝 **Explication:** 2-3 lignes max
```

#### Pour les nouvelles fonctionnalités
```
🎯 **Objectif:** Ce qu'on ajoute
📊 **Architecture:** Schéma ASCII si pertinent
💻 **Implémentation:** Code avec changements minimaux
✔️ **Tests:** Si applicable, tests unitaires correspondants
```

#### Pour les questions architecturales
```
🏗️ **Pattern recommandé:** Justifier le choix
📋 **Étapes d'implémentation:** Lister clairement
⚠️ **Pièges à éviter:** Avertissements pertinents
```

### Ordre de Priorité
1. Tests unitaires → toujours inclure des tests Moq si c'est un DAL/Service
2. Interfaces → tout ce qui est mockable doit avoir une interface
3. DI → tout service doit être injectable via Program.cs
4. Documentation → commentaires de code en français

---

## 🔑 Composants Clés

### JwtService
**Responsabilités:**
- Génération tokens d'accès (5 min)
- Génération refresh tokens (30 jours)
- Extraction UserId du token
- Hashing SHA256 des tokens

**Injection:**
```csharp
builder.Services.AddSingleton<JwtService>();

// Dans un contrôleur
public AuthController(IUserDAL userDAL, IAuthDAL authDAL, JwtService jwtService)
{
    _jwtService = jwtService;
}
```

### ConfigPerso
**Contient:** Secrets, URLs, connexions DB

```csharp
public class ConfigPerso
{
    public string ConnectionString { get; set; }
    public string SecretKey { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
    // Autres configs...
}
```

**Fichiers de config:**
- `appsettings.json` → Variables de production
- `appsettings.Development.json` → Développement local
- `.env` → Secrets sensibles (git-ignored)

### DAL Pattern
Chaque DAL suit ce pattern:

```csharp
public class UserDAL : IUserDAL
{
    private readonly ConfigPerso _config;
    
    public UserDAL(ConfigPerso config)
    {
        _config = config;
    }
    
    public async Task<UserOut?> GetUserById(long id)
    {
        using (var connection = new SqlConnection(_config.ConnectionString))
        {
            var query = "SELECT * FROM [User] WHERE Id = @Id";
            var user = await connection.QuerySingleOrDefaultAsync<UserOut>(query, new { Id = id });
            return user;
        }
    }
}
```

### Validation d'Autorisation
```csharp
[Authorize]  // Middleware JWT automatique
public async Task<ActionResult<UserOut>> GetUserById(long id)
{
    var token = _jwtService.ExtractTokenFromAuthorization(
        HttpContext.Request.Headers["Authorization"]);
    var idUserToken = _jwtService.ExtractUserIdFromToken(token);
    
    // Vérifier que l'utilisateur accède à ses propres données
    if (id != idUserToken)
        return Unauthorized();
    
    // ...
}
```

---

## 🧪 Tests & Mocking

### Framework
- **xUnit** pour les tests
- **Moq** pour les mocks d'interfaces
- **FluentAssertions** pour les vérifications lisibles

### Fixtures Réutilisables
```csharp
// Classe fixture pour JWT
public class JwtConfigFixture
{
    public ConfigPerso JwtConfig { get; }
    public IOptions<ConfigPerso> JwtConfigOptions { get; }
    
    public JwtConfigFixture()
    {
        JwtConfig = new ConfigPerso { /* ... */ };
        JwtConfigOptions = Options.Create(JwtConfig);
    }
}

// Utilisation
public class AuthControllerTests : IClassFixture<JwtConfigFixture>
{
    private readonly JwtService _jwtService;
    
    public AuthControllerTests(JwtConfigFixture fixture)
    {
        _jwtService = new JwtService(fixture.JwtConfigOptions);
    }
}
```

### Exemple Test avec Moq
```csharp
[Fact]
public async Task Login_ShouldReturnOk_WhenCredentialsAreValid()
{
    // Arrange
    var userDal = new Mock<IUserDAL>();
    var authDal = new Mock<IAuthDAL>();
    
    var user = new UserOut { Id = 1, Email = "user@example.com" };
    
    userDal.Setup(x => x.GetUserByEmail("user@example.com"))
        .ReturnsAsync(user);
    authDal.Setup(x => x.VerifyPassword(user.Password, "password"))
        .Returns(true);
    
    var controller = new AuthController(userDal.Object, authDal.Object, _jwtService);
    
    // Act
    var result = await controller.Login(new LoginUserIn 
    { 
        Email = "user@example.com", 
        Password = "password" 
    });
    
    // Assert
    var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
    ok.Value.Should().BeOfType<LoginUserOut>();
    
    userDal.Verify(x => x.GetUserByEmail("user@example.com"), Times.Once);
}
```

### Commandes
```bash
# Lancer les tests
dotnet test

# Avec rapport de couverture
dotnet test /p:CollectCoverageMetrics=true

# Tests spécifiques
dotnet test --filter "FullyQualifiedName~AuthControllerTests"
```

---

## 🚀 Déploiement & Docker

### Build
```bash
dotnet build -c Release
```

### Docker
```dockerfile
# Dockerfile fourni
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app
COPY . .
RUN dotnet build -c Release
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "TableMasterApi.dll"]
```

### Docker Compose
```bash
docker-compose up
```

---

## ⚠️ Pièges Courants à Éviter

1. **❌ Instantiation directe de DAL** → Utiliser l'interface injectée
2. **❌ Oublier `[Authorize]`** → Toutes les routes protégées doivent être décorées
3. **❌ Mélanger async/sync** → Tout doit être `async Task`
4. **❌ Ignorer les interfaces** → Créer une interface AVANT l'implémentation pour la testabilité
5. **❌ Hardcoder les strings de connexion** → Toujours via `ConfigPerso`
6. **❌ Ne pas valider les entrées** → Toujours check `null` et `BadRequest`

---

## 📞 Contacts & Ressources

- **Documentation .NET:** https://docs.microsoft.com/dotnet
- **Dapper:** https://github.com/DapperLib/Dapper
- **SignalR:** https://docs.microsoft.com/aspnet/core/signalr
- **Firebase Admin:** https://firebase.google.com/docs/admin/setup

---

**Generated for GitHub Copilot** | Mis à jour régulièrement
