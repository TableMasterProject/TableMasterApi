using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using DbUp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Sentry;
using TableMasterApi.DAL;
using TableMasterApi.DAL.Handlers;
using TableMasterApi.DAL.Interfaces;
using TableMasterApi.Hubs;
using TableMasterApi.Middleware;
using TableMasterApi.Model;
using TableMasterApi.Service;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseSentry();

Dapper.SqlMapper.AddTypeHandler(new PostgresTimeSpanHandler());

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection est manquante. Utilisez les variables d'environnement ou User Secrets.");
}

builder.Configuration["ConfigPerso:ConnectionString"] = connectionString;

var configPerso = builder.Configuration.GetSection("ConfigPerso").Get<ConfigPerso>()
    ?? throw new InvalidOperationException("La section ConfigPerso est manquante.");

configPerso.ConnectionString = connectionString;

if (string.IsNullOrWhiteSpace(configPerso.SecretKey) || Encoding.UTF8.GetByteCount(configPerso.SecretKey) < 32)
{
    throw new InvalidOperationException("ConfigPerso:SecretKey doit contenir au moins 32 octets.");
}

if (string.IsNullOrWhiteSpace(configPerso.Issuer) || string.IsNullOrWhiteSpace(configPerso.Audience))
{
    throw new InvalidOperationException("ConfigPerso:Issuer et ConfigPerso:Audience sont obligatoires.");
}

builder.Services.Configure<ConfigPerso>(builder.Configuration.GetSection("ConfigPerso"));
builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection("Brevo"));
builder.Services.Configure<AppLinksOptions>(builder.Configuration.GetSection("AppLinks"));
builder.Services.AddSingleton(configPerso);
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<FcmService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IDbConnectionFactory, PostgresConnectionFactory>();
builder.Services.AddHttpClient<GoogleMapsService>();
builder.Services.AddScoped<IAppLinkService, AppLinkService>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddHttpClient<IEmailService, BrevoEmailService>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BrevoOptions>>().Value;
    var brevoBaseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://api.brevo.com/v3/" : options.BaseUrl;
    httpClient.BaseAddress = new Uri(brevoBaseUrl.EndsWith('/') ? brevoBaseUrl : brevoBaseUrl + "/");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        if (allowedOrigins.Length == 0 && builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            return;
        }

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("Cors:AllowedOrigins doit etre configure hors developpement.");
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AuthPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddScoped<IAuthDAL, AuthDAL>();
builder.Services.AddScoped<IUserDAL, UserDAL>();
builder.Services.AddScoped<IRestaurantDAL, RestaurantDAL>();
builder.Services.AddScoped<IMenuDAL, MenuDAL>();
builder.Services.AddScoped<ITableDAL, TableDAL>();
builder.Services.AddScoped<IRoomDAL, RoomDAL>();
builder.Services.AddScoped<IReservationDAL, ReservationDAL>();
builder.Services.AddScoped<IReviewDAL, ReviewDAL>();
builder.Services.AddScoped<IDailyActivityDAL, DailyActivityDAL>();
builder.Services.AddScoped<IClosedDayExceptionDAL, ClosedDayExceptionDAL>();
builder.Services.AddScoped<IDeviceTokenDAL, DeviceTokenDAL>();

builder.Services.AddSignalR();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = configPerso.Issuer,
        ValidAudience = configPerso.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configPerso.SecretKey)),
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "UserId"
    };
});

builder.Services.AddControllers();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("x-api-version"));
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    const string bearerScheme = "bearer";

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TableMaster API",
        Version = "v1"
    });

    c.AddSecurityDefinition(bearerScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = bearerScheme,
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    });

    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(bearerScheme, document)] = []
    });
});

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres");

if (builder.Configuration.GetValue("Features:RunMigrationsOnStartup", true))
{
    Console.WriteLine("Verification de la base de donnees...");
    EnsureDatabase.For.PostgresqlDatabase(connectionString);

    var upgrader = DeployChanges.To
        .PostgresqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(System.Reflection.Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build();

    var result = upgrader.PerformUpgrade();
    if (!result.Successful)
    {
        throw result.Error;
    }
}

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("Features:EnableSwagger", false))
{
    app.UseSwagger();

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TableMaster API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("ConfiguredOrigins");
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");
app.MapHub<ReservationHub>("/reservationHub");

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/monitoring/sentry-test", () =>
    {
        var sentryId = SentrySdk.CaptureMessage("Hello Sentry from TableMasterApi");
        return Results.Ok(new
        {
            message = "Sentry test event captured.",
            sentryId = sentryId.ToString()
        });
    });
}

app.MapControllers();

app.Run();
