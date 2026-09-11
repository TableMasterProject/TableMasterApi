using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using DbUp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

builder.WebHost.UseSentry(options =>
{
    options.SendDefaultPii = false;
    options.SetBeforeSend(SentrySanitizer.Sanitize);
});

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
builder.Services.AddScoped<IAvailabilityNotifier, AvailabilityNotifier>();
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
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
builder.Services.AddReservationServices(
    builder.Configuration.GetValue(
        "Features:RunNotificationWorker",
        !builder.Environment.IsEnvironment("Testing")));

builder.Services.AddSignalR(options =>
{
    // Les valeurs par defaut (15 s / 30 s) ne laissent que deux pings de marge :
    // trop juste pour un reseau mobile ou une application qui sort de veille.
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Browser WebSocket APIs send the bearer token in the query string.
            if (context.Request.Path.StartsWithSegments("/reservationHub") &&
                !context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Token = context.Request.Query["access_token"];
            }
            return Task.CompletedTask;
        }
    };
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

builder.Services.AddControllers(options => options.Filters.Add<ApiErrorResultFilter>());

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
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

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
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    var error = response.StatusCode switch
    {
        401 => "Authentification requise.",
        403 => "Accès refusé.",
        404 => "Ressource introuvable.",
        429 => "Trop de requêtes. Réessayez plus tard.",
        _ => "Requête impossible."
    };
    await response.WriteAsJsonAsync(new { error, traceId = context.HttpContext.TraceIdentifier });
});
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});
app.MapHub<ReservationHub>("/reservationHub", options => options.CloseOnAuthenticationExpiration = true);

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

public partial class Program
{
}

internal static class SentrySanitizer
{
    private static readonly System.Text.RegularExpressions.Regex AccessToken = new(
        @"(?i)(^|[?&])access_token=[^&#\s]*",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static SentryEvent Sanitize(SentryEvent sentryEvent)
    {
        if (sentryEvent.Request is { } request)
        {
            request.Url = Redact(request.Url);
            request.QueryString = Redact(request.QueryString);
        }
        return sentryEvent;
    }

    private static string? Redact(string? value) => value is null
        ? null
        : AccessToken.Replace(value, "$1access_token=[Filtered]");
}
