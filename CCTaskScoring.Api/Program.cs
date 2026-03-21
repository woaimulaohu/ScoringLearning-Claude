using System.Threading.Channels;
using CCTaskScoring.Api.Infrastructure;
using CCTaskScoring.Api.Mcp;
using CCTaskScoring.Api.Services;
using CCTaskScoring.Core.Interfaces;
using CCTaskScoring.Infrastructure.Data;
using CCTaskScoring.Infrastructure.Data.Repositories;
using CCTaskScoring.Infrastructure.Mcp;
using CCTaskScoring.Infrastructure.Neo4j;
using CCTaskScoring.Infrastructure.Rewards;
using CCTaskScoring.Infrastructure.Scoring;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Serilog;

// ── Serilog 早期初始化（测试环境跳过，避免 WebApplicationFactory 重入时 logger frozen 问题） ──
var isTestEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing";
if (!isTestEnvironment)
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateBootstrapLogger();
}

try
{
    Log.Information("Starting CCTaskScoring API...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog 配置（从 appsettings.json 读取，测试环境跳过避免 frozen 冲突） ──
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());
    }

    // ── 控制器 + JSON camelCase 序列化 ──
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            // 强制 DateTime 序列化带 Z 后缀，避免前端解析时区偏差（+8h）
            opts.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        });

    // ── Swagger ──
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ── SQLite + EF Core ──
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

    // ── Channel（无界容量，后台评分队列） ──
    builder.Services.AddSingleton(Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true }));

    // ── 仓储注入 ──
    builder.Services.AddScoped<ITaskRepository, TaskRepository>();
    builder.Services.AddScoped<IScoreRepository, ScoreRepository>();
    builder.Services.AddScoped<IRewardRepository, RewardRepository>();

    // ── 评分引擎 ──
    builder.Services.AddScoped<IScoringEngine, ScoringEngine>();

    // ── 奖惩服务 ──
    builder.Services.AddScoped<IRewardService, RewardService>();

    // ── Neo4j 服务（单例，IDriver 线程安全） ──
    builder.Services.AddSingleton<INeo4jService, Neo4jService>();

    // ── MCP 客户端（HttpClient + Polly 重试） ──
    builder.Services.AddHttpClient<IMcpClient, McpClient>()
        .SetHandlerLifetime(TimeSpan.FromMinutes(5))
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

    // ── 后台服务 ──
    builder.Services.AddHostedService<ScoringBackgroundService>();

    // ── MCP Server（Streamable HTTP，供 Claude Code 发现并调用） ──
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .WithTools<ScoringMcpTools>();

    // ── CORS（开发环境允许所有来源） ──
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevCors", policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
    });

    var app = builder.Build();

    // ── EF Core 自动迁移（测试环境跳过，InMemory 不支持 Migrate） ──
    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Database migrated successfully");
    }

    // ── 中间件管道 ──
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "CCTaskScoring API V1");
            c.RoutePrefix = "swagger";
        });
    }

    if (!app.Environment.IsEnvironment("Testing"))
        app.UseSerilogRequestLogging();

    app.UseCors("DevCors");

    app.MapControllers();

    // ── MCP 端点（供 Claude Code 通过 Streamable HTTP 协议调用） ──
    // 必须在 MapFallbackToFile 之前注册，否则 SPA fallback 会拦截 /mcp 请求
    app.MapMcp("/mcp");

    // 非测试环境才启用静态文件和 SPA fallback（测试环境没有 wwwroot）
    if (!app.Environment.IsEnvironment("Testing"))
    {
        app.UseStaticFiles(); // wwwroot 的 Vue 前端
        app.MapFallbackToFile("index.html"); // SPA fallback：非 API/MCP 路由返回 index.html
    }

    Log.Information("CCTaskScoring API started on {Urls}", string.Join(", ", app.Urls));
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw; // 确保 WebApplicationFactory 能感知到异常（不吞掉）
}
finally
{
    if (!isTestEnvironment)
        await Log.CloseAndFlushAsync();
}

// 供测试项目通过 WebApplicationFactory<Program> 访问
public partial class Program { }
