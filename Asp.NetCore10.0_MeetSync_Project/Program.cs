using System.Threading.RateLimiting;
using Asp.NetCore10._0_MeetSync_Project.Filters;
using Asp.NetCore10._0_MeetSync_Project.Hubs;
using Asp.NetCore10._0_MeetSync_Project.Middlewares;
using FluentValidation;
using MeetSync.Application.Common.Options;
using MeetSync.Application.Interfaces;
using MeetSync.Application.Validators;
using MeetSync.Infrastructure.Persistence;
using MeetSync.Infrastructure.Services;
using MeetSync.Infrastructure.Services.State;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog Structured Logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/meetsync-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

// Strongly-typed options configuration
var meetSyncSettingsSection = builder.Configuration.GetSection(MeetSyncSettings.SectionName);
builder.Services.Configure<MeetSyncSettings>(meetSyncSettingsSection);
var meetSyncSettings = meetSyncSettingsSection.Get<MeetSyncSettings>() ?? new MeetSyncSettings();

// Global Exception Handling & ProblemDetails (.NET 10 standard)
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// FluentValidation registration
builder.Services.AddValidatorsFromAssemblyContaining<CreateRoomRequestValidator>();

// Core framework services & Global Validation Filter
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ValidationFilter>();
});

// Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MeetSync Pro API",
        Version = "v1",
        Description = "Real-time video conferencing and meeting management platform API built with .NET 10, SignalR, and WebRTC."
    });
});

// Strict CORS Policy configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("MeetSyncCorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Native Rate Limiting Policies
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    options.AddTokenBucketLimiter("SignalRPolicy", opt =>
    {
        opt.TokenLimit = 60;
        opt.QueueLimit = 10;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        opt.TokensPerPeriod = 10;
        opt.AutoReplenishment = true;
    });
});

builder.Services.AddDbContext<MeetSyncDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Application & Infrastructure Services DI Registration
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddScoped<IMeetingRecordingService, MeetingRecordingService>();
builder.Services.AddScoped<ITranscriptionService, TranscriptionService>();
builder.Services.AddScoped<IAiSummaryService, AiSummaryService>();

// Configure Meeting State Store (InMemory vs Redis)
if (meetSyncSettings.UseRedisStateStore)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = meetSyncSettings.RedisConnectionString;
        options.InstanceName = "MeetSyncCache:";
    });
    builder.Services.AddSingleton<IMeetingStateStore, RedisMeetingStateStore>();
}
else
{
    builder.Services.AddSingleton<IMeetingStateStore, InMemoryMeetingStateStore>();
}

// SignalR & Redis Backplane Configuration
var signalRBuilder = builder.Services.AddSignalR();
if (meetSyncSettings.UseRedisStateStore && !string.IsNullOrWhiteSpace(meetSyncSettings.RedisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(meetSyncSettings.RedisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("MeetSyncSignalR");
    });
}

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(meetSyncSettings.SessionTimeoutMinutes);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Custom Security Headers Middleware
app.UseSecurityHeaders();

// Global Exception Middleware
app.UseExceptionHandler();

// Serilog Request Logging
app.UseSerilogRequestLogging();

// OpenAPI / Swagger Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "MeetSync Pro API v1"));
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors("MeetSyncCorsPolicy");
app.UseRateLimiter();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<MeetingHub>("/meetingHub").RequireRateLimiting("SignalRPolicy");

try
{
    Log.Information("Starting MeetSync Pro Application Host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
