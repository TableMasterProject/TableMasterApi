using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TableMasterApi.Model;
using TableMasterApi.Service;
using TableMasterApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Charger la configuration à partir du fichier appsettings.json
builder.Services.Configure<ConfigPerso>(builder.Configuration.GetSection("ConfigPerso"));

// Ajouter JwtService à l'injection de dépendances
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
// Ajouter le Hub SignalR pour les réservations
app.MapHub<ReservationHub>("/reservationHub");

app.MapControllers();

app.Run();
