using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TableMasterApi.Model;
using TableMasterApi.Service;
using TableMasterApi.Hubs;
using DbUp;
using DbUp.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Charger la configuration � partir du fichier appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Configuration["ConfigPerso:ConnectionString"] = connectionString;
}

// Ajouter la configuration ConfigPerso
builder.Services.Configure<ConfigPerso>(builder.Configuration.GetSection("ConfigPerso"));

// Ajouter JwtService � l'injection de d�pendances
builder.Services.AddSingleton(sp => builder.Configuration.GetSection("ConfigPerso").Get<ConfigPerso>());
builder.Services.AddSingleton<JwtService>();

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["ConfigPerso:SecretKey"]))
    };
});

builder.Services.AddControllers();

// Configuration du versionnage pour .NET 8
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
EnsureDatabase.For.SqlDatabase(connectionString);

var upgrader = DeployChanges.To
    .SqlDatabase(connectionString)
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
builder.Services.AddSwaggerGen();

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
