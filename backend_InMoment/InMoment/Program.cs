using FluentValidation;
using InMoment.API.Common;
using InMoment.API.Realtime;
using InMoment.Application.Abstractions.Accounts;
using InMoment.Application.Abstractions.Communication;
using InMoment.Application.Abstractions.Persistence;
using InMoment.Application.Abstractions.Queries;
using InMoment.Application.Abstractions.Realtime;
using InMoment.Application.Abstractions.Security;
using InMoment.Application.Features.Auth.Register;
using InMoment.Application.Features.Notifications.Common;
using InMoment.Application.Features.Privacy.Common;
using InMoment.Application.Features.Reports.Common;
using InMoment.Application.Abstractions.Gifs;
using InMoment.Infrastructure.Accounts;
using InMoment.Infrastructure.Auth;
using InMoment.Infrastructure.Communication;
using InMoment.Infrastructure.DependencyInjection;
using InMoment.Infrastructure.Persistence;
using InMoment.Infrastructure.Persistence.Repositories;
using InMoment.Infrastructure.Queries;
using InMoment.Infrastructure.Notifications;
using InMoment.Infrastructure.Security;
using InMoment.Infrastructure.Gifs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Validate configuration early
var connectionString = builder.Configuration.GetConnectionString("Db");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'Db' is not configured.");
}

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
          ?? throw new InvalidOperationException("Jwt configuration section is missing.");

ValidateJwtOptions(jwt);
ValidateProductionConfiguration(builder.Configuration, builder.Environment, jwt);

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger + JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "InMoment.API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Db
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(connectionString);
});

builder.Services.AddSignalR();

// Options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<PasswordRecoveryOptions>(
    builder.Configuration.GetSection("PasswordRecovery"));
builder.Services.Configure<SystemNotificationOptions>(
    builder.Configuration.GetSection(SystemNotificationOptions.SectionName));
builder.Services.Configure<GiphyOptions>(
    builder.Configuration.GetSection(GiphyOptions.SectionName));

// Auth - JWT
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/groups") ||
                     path.StartsWithSegments("/hubs/users")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("app", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy
                .SetIsOriginAllowed(origin =>
                {
                    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        return false;

                    return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                           || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                           || uri.Host.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase)
                           || uri.Host.StartsWith("192.168.x.x", StringComparison.OrdinalIgnoreCase)
                           || uri.Host.Equals("10.0.2.2", StringComparison.OrdinalIgnoreCase);
                })
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();

            return;
        }

        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>();

        if (allowedOrigins is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must be configured for non-development environments.");
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services.AddStorage(builder.Configuration);

builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterHandler).Assembly);
});

// RATE LIMITING
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";

        int? retryAfterSeconds = null;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString();
        }

        var payload = retryAfterSeconds is null
            ? """
              {
                "title": "Слишком много запросов",
                "message": "Вы выполняете действия слишком часто. Попробуйте чуть позже.",
                "status": 429
              }
              """
            : $$"""
              {
                "title": "Слишком много запросов",
                "message": "Вы выполняете действия слишком часто. Попробуйте снова через {{retryAfterSeconds}} сек.",
                "status": 429,
                "retryAfterSeconds": {{retryAfterSeconds}}
              }
              """;

        await context.HttpContext.Response.WriteAsync(payload, ct);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var key = BuildRateLimitKey(httpContext);
        var bucket = ResolveBucket(httpContext);

        return bucket switch
        {
            RateLimitBucket.CommentWrite => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"comment-write:{key}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 15,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 3,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }),

            RateLimitBucket.ReactionWrite => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"reaction-write:{key}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 40,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }),

            RateLimitBucket.MediaWrite => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"media-write:{key}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(10),
                    SegmentsPerWindow = 5,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }),

            RateLimitBucket.PhotoDelete => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"photo-delete:{key}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 80,
                    Window = TimeSpan.FromMinutes(10),
                    SegmentsPerWindow = 5,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }),

            RateLimitBucket.InviteWrite => RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"invite-write:{key}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 15,
                    Window = TimeSpan.FromHours(1),
                    SegmentsPerWindow = 6,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                }),

            _ => RateLimitPartition.GetNoLimiter("no-limit")
        };
    });
});


builder.Services.AddModerationServices(builder.Configuration);
// Persistence
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IInvitationRepository, InvitationRepository>();
builder.Services.AddScoped<IPhotoRepository, PhotoRepository>();
builder.Services.AddScoped<IReactionRepository, ReactionRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationPreviewReader, NotificationPreviewReader>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IRefreshSessionRepository, RefreshSessionRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
builder.Services.AddScoped<IFriendRequestRepository, FriendRequestRepository>();
builder.Services.AddScoped<IFriendshipRepository, FriendshipRepository>();
builder.Services.AddScoped<IContactImportLogRepository, ContactImportLogRepository>();
builder.Services.AddScoped<IPrivacySettingsRepository, PrivacySettingsRepository>();
builder.Services.AddScoped<IBlockedUserRepository, BlockedUserRepository>();
builder.Services.AddScoped<PrivacyAccessEvaluator>();
builder.Services.AddScoped<IAccountDataManager, AccountDataManager>();
builder.Services.AddScoped<IGroupInviteCodeRepository, GroupInviteCodeRepository>();
builder.Services.AddScoped<ISavedPhotoRepository, SavedPhotoRepository>();
builder.Services.AddScoped<INotificationSettingsRepository, NotificationSettingsRepository>();
builder.Services.AddScoped<IDeviceTokenRepository, DeviceTokenRepository>();
builder.Services.AddScoped<IContactInviteRepository, ContactInviteRepository>();
builder.Services.AddScoped<ICommentReactionRepository, CommentReactionRepository>();
builder.Services.AddScoped<ReportTargetContextFactory>();
builder.Services.AddScoped<ReportDtoBuilders>();
builder.Services.AddScoped<ISystemNotificationStateRepository, SystemNotificationStateRepository>();
builder.Services.AddScoped<ISystemAnnouncementRepository, SystemAnnouncementRepository>();
builder.Services.AddScoped<SystemNotificationProcessor>();
builder.Services.AddHostedService<SystemNotificationHostedService>();

// Realtime
builder.Services.AddScoped<IGroupRealtime, SignalRGroupRealtime>();
builder.Services.AddScoped<INotificationRealtime, SignalRNotificationRealtime>();

// Security
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddHttpClient<IpWhoIsGeoIpResolver>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(3);
});

builder.Services.AddHttpClient<GiphyGifSearchService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<IGifSearchService, GiphyGifSearchService>();

builder.Services.AddScoped<IGeoIpResolver, IpWhoIsGeoIpResolver>();

// Communication
builder.Services.AddScoped<PasswordRecoveryLinkBuilder>();
builder.Services.AddScoped<SmtpTransportSettingsFactory>();

builder.Services.AddScoped<SmtpPasswordRecoverySender>(sp =>
    new SmtpPasswordRecoverySender(
        sp.GetRequiredService<PasswordRecoveryLinkBuilder>(),
        sp.GetRequiredService<SmtpTransportSettingsFactory>(),
        sp.GetRequiredService<ILogger<SmtpPasswordRecoverySender>>()));

builder.Services.AddScoped<DevPasswordRecoverySender>(sp =>
    new DevPasswordRecoverySender(
        sp.GetRequiredService<PasswordRecoveryLinkBuilder>(),
        sp.GetRequiredService<ILogger<DevPasswordRecoverySender>>()));

builder.Services.AddScoped<IPasswordRecoverySender>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PasswordRecoveryOptions>>().Value;
    var environment = sp.GetRequiredService<IHostEnvironment>();

    if (options.IsSmtpConfigured)
    {
        if (environment.IsDevelopment())
        {
            return new ResilientPasswordRecoverySender(
                sp.GetRequiredService<SmtpPasswordRecoverySender>(),
                sp.GetRequiredService<DevPasswordRecoverySender>(),
                sp.GetRequiredService<ILogger<ResilientPasswordRecoverySender>>());
        }

        return sp.GetRequiredService<SmtpPasswordRecoverySender>();
    }

    if (environment.IsDevelopment())
        return sp.GetRequiredService<DevPasswordRecoverySender>();

    throw new InvalidOperationException(
        "PasswordRecovery SMTP settings must be configured outside Development.");
});

builder.Services.AddScoped<SmtpAccountDeletionRequestSender>(sp =>
    new SmtpAccountDeletionRequestSender(
        sp.GetRequiredService<SmtpTransportSettingsFactory>(),
        sp.GetRequiredService<ILogger<SmtpAccountDeletionRequestSender>>()));

builder.Services.AddScoped<DevAccountDeletionRequestSender>(sp =>
    new DevAccountDeletionRequestSender(
        sp.GetRequiredService<ILogger<DevAccountDeletionRequestSender>>()));

builder.Services.AddScoped<IAccountDeletionRequestSender>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PasswordRecoveryOptions>>().Value;
    var environment = sp.GetRequiredService<IHostEnvironment>();

    if (options.IsSmtpConfigured)
    {
        if (environment.IsDevelopment())
        {
            return new ResilientAccountDeletionRequestSender(
                sp.GetRequiredService<SmtpAccountDeletionRequestSender>(),
                sp.GetRequiredService<DevAccountDeletionRequestSender>(),
                sp.GetRequiredService<ILogger<ResilientAccountDeletionRequestSender>>());
        }

        return sp.GetRequiredService<SmtpAccountDeletionRequestSender>();
    }

    if (environment.IsDevelopment())
        return sp.GetRequiredService<DevAccountDeletionRequestSender>();

    throw new InvalidOperationException(
        "PasswordRecovery SMTP settings must be configured outside Development.");
});

builder.Services.AddScoped<DevContactInviteSender>();
builder.Services.AddScoped<DisabledContactInviteSender>();

builder.Services.AddScoped<IContactInviteSender>(sp =>
{
    var environment = sp.GetRequiredService<IHostEnvironment>();

    if (environment.IsDevelopment())
        return sp.GetRequiredService<DevContactInviteSender>();

    return sp.GetRequiredService<DisabledContactInviteSender>();
});

builder.Services.Configure<FirebasePushOptions>(
    builder.Configuration.GetSection(FirebasePushOptions.SectionName));

builder.Services.Configure<SessionGeoOptions>(
    builder.Configuration.GetSection(SessionGeoOptions.SectionName));

builder.Services.AddSingleton<FirebaseAccessTokenProvider>();
builder.Services.AddHttpClient<FcmHttpV1PushSender>();

builder.Services.AddScoped<INotificationPushDeliveryService, NotificationPushDeliveryService>();
builder.Services.AddScoped<InMoment.Application.Features.SystemAnnouncements.Create.CreateSystemAnnouncementHandler>();
builder.Services.AddScoped<InMoment.Application.Features.SystemAnnouncements.Update.UpdateSystemAnnouncementHandler>();
builder.Services.AddScoped<InMoment.Application.Features.SystemAnnouncements.List.ListSystemAnnouncementsHandler>();
builder.Services.AddScoped<InMoment.Application.Features.SystemAnnouncements.Delete.DeleteSystemAnnouncementHandler>();

builder.Services.AddScoped<IPushSender>(sp =>
{
    var options = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<FirebasePushOptions>>().Value;

    if (options.Enabled)
    {
        return sp.GetRequiredService<FcmHttpV1PushSender>();
    }

    return ActivatorUtilities.CreateInstance<LoggingPushSender>(sp);
});

// Middleware
builder.Services.AddTransient<ExceptionMiddleware>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Headers.ContainsKey("Access-Control-Request-Private-Network"))
        {
            context.Response.Headers["Access-Control-Allow-Private-Network"] = "true";
        }

        await next();
    });
}

app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

//app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("app");
app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHub<GroupsHub>("/hubs/groups");
app.MapHub<UsersHub>("/hubs/users");

app.Run();

static void ValidateJwtOptions(JwtOptions jwt)
{
    if (string.IsNullOrWhiteSpace(jwt.Issuer))
        throw new InvalidOperationException("Jwt:Issuer is not configured.");

    if (string.IsNullOrWhiteSpace(jwt.Audience))
        throw new InvalidOperationException("Jwt:Audience is not configured.");

    if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        throw new InvalidOperationException("Jwt:SigningKey is not configured.");

    if (jwt.SigningKey.Length < 32)
        throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters long.");
}

static string BuildRateLimitKey(HttpContext httpContext)
{
    var userId =
        httpContext.User.FindFirst("sub")?.Value ??
        httpContext.User.FindFirst("nameid")?.Value ??
        httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    if (!string.IsNullOrWhiteSpace(userId))
        return $"user:{userId}";

    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
    return $"ip:{ip}";
}

static RateLimitBucket ResolveBucket(HttpContext httpContext)
{
    var path = httpContext.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
    var method = httpContext.Request.Method.ToUpperInvariant();

    if (method is not ("POST" or "PATCH" or "DELETE"))
        return RateLimitBucket.None;

    if (path.Contains("/comments"))
        return RateLimitBucket.CommentWrite;

    if (path.Contains("/reactions"))
        return RateLimitBucket.ReactionWrite;

    if (method == "DELETE" && path.Contains("/groups/") && path.Contains("/photos/"))
        return RateLimitBucket.PhotoDelete;

    if (path.Contains("/uploads/photos/presign") ||
        path.Contains("/uploads/profile-photo/presign") ||
        (method == "POST" && path.Contains("/groups/") && path.EndsWith("/photos")) ||
        (method == "PATCH" && path.Contains("/groups/") && path.Contains("/photos/")))
        return RateLimitBucket.MediaWrite;

    if (path.Contains("/invite") || path.Contains("/invitations"))
        return RateLimitBucket.InviteWrite;

    return RateLimitBucket.None;
}

static void ValidateProductionConfiguration(
    IConfiguration configuration,
    IHostEnvironment environment,
    JwtOptions jwt)
{
    if (environment.IsDevelopment())
        return;

    if (jwt.SigningKey.Length < 64)
        throw new InvalidOperationException(
            "Jwt:SigningKey must be at least 64 characters long outside Development.");

    if (jwt.SigningKey.Contains("replace", StringComparison.OrdinalIgnoreCase) ||
        jwt.SigningKey.Contains("development", StringComparison.OrdinalIgnoreCase) ||
        jwt.SigningKey.Contains("local", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey contains development-like value and cannot be used outside Development.");
    }

    var allowedOrigins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>();

    if (allowedOrigins is not { Length: > 0 })
        throw new InvalidOperationException(
            "Cors:AllowedOrigins must be configured outside Development.");

    foreach (var origin in allowedOrigins)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"Invalid CORS origin: {origin}");

        if (uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(
                $"CORS origin must use HTTPS outside Development: {origin}");

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Local CORS origin cannot be used outside Development: {origin}");
        }
    }

    var storage = configuration.GetSection("Storage");

    RequireNonEmpty(storage["Endpoint"], "Storage:Endpoint");
    RequireNonEmpty(storage["AccessKey"], "Storage:AccessKey");
    RequireNonEmpty(storage["SecretKey"], "Storage:SecretKey");
    RequireNonEmpty(storage["Bucket"], "Storage:Bucket");
    RequireNonEmpty(storage["PublicBaseUrl"], "Storage:PublicBaseUrl");

    if (!storage["Endpoint"]!.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "Storage:Endpoint must use HTTPS outside Development.");

    if (!storage["PublicBaseUrl"]!.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "Storage:PublicBaseUrl must use HTTPS outside Development.");

    var passwordRecovery = configuration.GetSection("PasswordRecovery");

    RequireNonEmpty(passwordRecovery["SmtpHost"], "PasswordRecovery:SmtpHost");
    RequireNonEmpty(passwordRecovery["SenderEmail"], "PasswordRecovery:SenderEmail");
    RequireNonEmpty(passwordRecovery["SenderName"], "PasswordRecovery:SenderName");

    var firebasePush = configuration.GetSection("FirebasePush");
    var firebaseEnabled = bool.TryParse(firebasePush["Enabled"], out var enabled) && enabled;

    if (firebaseEnabled)
    {
        RequireNonEmpty(firebasePush["ProjectId"], "FirebasePush:ProjectId");
        RequireNonEmpty(firebasePush["ServiceAccountJsonPath"], "FirebasePush:ServiceAccountJsonPath");
    }

    var allowedHosts = configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Trim() == "*")
    {
        throw new InvalidOperationException(
            "AllowedHosts must be restricted outside Development.");
    }

    var systemNotifications = configuration.GetSection("SystemNotifications");
    var devForceSystemMemories = bool.TryParse(
        systemNotifications["DevForceSystemMemories"],
        out var forceMemories) && forceMemories;

    if (devForceSystemMemories)
    {
        throw new InvalidOperationException(
            "SystemNotifications:DevForceSystemMemories must be false outside Development.");
    }
}

static void RequireNonEmpty(string? value, string key)
{
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException($"{key} must be configured.");
}

enum RateLimitBucket
{
    None = 0,
    CommentWrite = 1,
    ReactionWrite = 2,
    MediaWrite = 3,
    InviteWrite = 4,
    PhotoDelete = 5
}
public partial class Program { }