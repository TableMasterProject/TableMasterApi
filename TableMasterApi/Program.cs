using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TableMasterApi.DAL;
using TableMasterApi.DAL.Handlers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Model;
using TableMasterApi.Service;
using TableMasterApi.Hubs;
using DbUp;
using DbUp.Postgresql;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

Dapper.SqlMapper.AddTypeHandler(new PostgresTimeSpanHandler());

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Configuration["ConfigPerso:ConnectionString"] = connectionString;
}
else
{
    throw new InvalidOperationException("La chaîne de connexion DefaultConnection est manquante.");
}

// Ajouter la configuration ConfigPerso
builder.Services.Configure<ConfigPerso>(builder.Configuration.GetSection("ConfigPerso"));

// Ajouter JwtService � l'injection de d�pendances
builder.Services.AddSingleton(sp => builder.Configuration.GetSection("ConfigPerso").Get<ConfigPerso>()!);
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<FcmService>();

// ========== Injection de Dépendances pour tous les DAL ==========
// Ajouter les services DAL avec leurs interfaces (Scoped = une nouvelle instance par requête)
builder.Services.AddScoped<IAuthDAL, AuthDAL>();
builder.Services.AddScoped<IUserDAL, UserDAL>();
builder.Services.AddScoped<IRestaurantDAL, RestaurantDAL>();
builder.Services.AddScoped<IMenuDAL, MenuDAL>();
builder.Services.AddScoped<ITableDAL, TableDAL>();
builder.Services.AddScoped<IReservationDAL, ReservationDAL>();
builder.Services.AddScoped<IReviewDAL, ReviewDAL>();
builder.Services.AddScoped<IDailyActivityDAL, DailyActivityDAL>();
builder.Services.AddScoped<IClosedDayExceptionDAL, ClosedDayExceptionDAL>();
builder.Services.AddScoped<IDeviceTokenDAL, DeviceTokenDAL>();
// ====================================================

// Ajout de SignalR
builder.Services.AddSignalR();

// Ajoutez les services d'authentification avec JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Configuration pour valider le token JWT
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["ConfigPerso:Issuer"],
        ValidAudience = builder.Configuration["ConfigPerso:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["ConfigPerso:SecretKey"] ?? string.Empty))
    };
});

builder.Services.AddControllers();

// Configuration du versionnage pour .NET 10
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("x-api-version")
    );
})
.AddMvc() // Indispensable pour les Controllers
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

Console.WriteLine("Vérification de la base de données...");
EnsureDatabase.For.PostgresqlDatabase(connectionString);

var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(System.Reflection.Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();
var result = upgrader.PerformUpgrade();

if (!result.Successful)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Erreur DbUp : {result.Error}");
    Console.ResetColor();
    throw result.Error;
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "TableMaster API", Version = "v1" });
    c.SwaggerDoc("v2", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "TableMaster API", Version = "v2" });

    // 1. IL MANQUAIT ÇA : Définir comment le token doit être envoyé
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Authentification JWT. Tapez 'Bearer' suivi d'un espace et de votre token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // 2. Appliquer la sécurité (Le cadenas)
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer" // Doit correspondre exactement à l'ID ci-dessus
                }
            },
            new List<string>()
        }
    });

    // Filtre pour séparer V1 et V2
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        // Si ton contrôleur n'a pas d'attribut [ApiVersion], on peut décider de l'afficher en v1 par défaut
        var versions = apiDesc.CustomAttributes().OfType<Asp.Versioning.ApiVersionAttribute>().SelectMany(attr => attr.Versions);
        if (!versions.Any()) return docName == "v1";

        return versions.Any(v => $"v{v.MajorVersion}" == docName);
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
// Ajouter le Hub SignalR pour les r�servations
app.MapHub<ReservationHub>("/reservationHub");

app.MapControllers();

app.Run();
