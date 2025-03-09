using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TableMasterApi.Model;
using TableMasterApi.Service;
using TableMasterApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Charger la configuration � partir du fichier appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// V�rifier si une variable d'environnement existe pour la connexion SQL
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    // �craser la valeur de ConfigPerso:ConnectionString avec celle de l'environnement
    builder.Configuration["ConfigPerso:ConnectionString"] = connectionString;
}

// Ajouter la configuration ConfigPerso
builder.Services.Configure<ConfigPerso>(builder.Configuration.GetSection("ConfigPerso"));

// Ajouter JwtService � l'injection de d�pendances
builder.Services.AddSingleton<ConfigPerso>();
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
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
// Ajouter le Hub SignalR pour les r�servations
app.MapHub<ReservationHub>("/reservationHub");

app.MapControllers();

app.Run();

