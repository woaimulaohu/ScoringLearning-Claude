using CCTaskScoring.Core.DTOs;
using CCTaskScoring.Core.Interfaces;
using CCTaskScoring.Infrastructure.Data;
using CCTaskScoring.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Serilog;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CCTaskScoring.Tests.Integration;

/// <summary>任务 API 集成测试</summary>
public class TaskApiIntegrationTests : IClassFixture<TaskApiIntegrationTests.CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TaskApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// 自定义 WebApplicationFactory，替换数据库和外部服务为测试替身
    /// </summary>
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // 重置 Serilog 全局 Logger，避免"logger already frozen"错误
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            // 清空所有日志提供者，防止 Serilog 二次初始化冲突
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.ConfigureServices(services =>
            {
                // 移除原始 AppDbContext 注册（包括 DbContext 本身和其 Options）
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(AppDbContext));
                if (dbContextDescriptor != null)
                    services.Remove(dbContextDescriptor);

                var dbOptionsDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (dbOptionsDescriptor != null)
                    services.Remove(dbOptionsDescriptor);

                // 移除原始 Neo4j 服务（它在构造函数中连接 Neo4j）
                var neo4jDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(INeo4jService));
                if (neo4jDescriptor != null)
                    services.Remove(neo4jDescriptor);

                // 移除原始 MCP 客户端（HttpClient 工厂方式注册）
                var mcpDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IMcpClient));
                if (mcpDescriptor != null)
                    services.Remove(mcpDescriptor);

                // 替换为内存数据库（固定名称，确保所有请求共享同一数据库）
                var dbName = "IntegrationTestDb_" + Guid.NewGuid();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                // Mock Neo4j 服务
                var mockNeo4j = new Mock<INeo4jService>();
                mockNeo4j.Setup(m => m.IsAvailableAsync()).ReturnsAsync(false);
                services.AddSingleton<INeo4jService>(mockNeo4j.Object);

                // Mock MCP 客户端
                var mockMcp = new Mock<IMcpClient>();
                mockMcp.Setup(m => m.ExtractRequirementsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(Array.Empty<string>());
                mockMcp.Setup(m => m.GenerateLessonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new LessonResult("问题", "原因", "建议"));
                mockMcp.Setup(m => m.AnalyzeCodeCorrectnessAsync(
                           It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(-1.0);
                services.AddSingleton<IMcpClient>(mockMcp.Object);

                // 移除后台评分服务，避免与集成测试的 Review 操作产生并发冲突
                // （后台服务会并行修改任务状态，干扰测试断言）
                var bgDescriptor = services.SingleOrDefault(
                    d => d.ImplementationType == typeof(CCTaskScoring.Api.Services.ScoringBackgroundService));
                if (bgDescriptor != null)
                    services.Remove(bgDescriptor);

                // InMemory 数据库无需手动 EnsureCreated，
                // 去掉额外的 BuildServiceProvider() 调用，避免创建独立 IInMemoryDatabaseRoot
                // 导致与 Host ServiceProvider 的 InMemory DB 数据不共享
            });
        }
    }

    // ── 健康检查 ──

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }

    // ── 提交任务 ──

    [Fact]
    public async Task SubmitTask_ValidRequest_Returns202()
    {
        var request = TestDataFactory.CreateSubmitRequest();

        var response = await _client.PostAsJsonAsync("/api/v1/tasks", request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("taskId").GetString().Should().Be(request.TaskId);
        body.GetProperty("status").GetString().Should().Be("pending");
    }

    [Fact]
    public async Task SubmitTask_DuplicateTaskId_Returns409()
    {
        var taskId = "duplicate-id-" + Guid.NewGuid();
        var request = TestDataFactory.CreateSubmitRequest(taskId);

        // 第一次提交
        var response1 = await _client.PostAsJsonAsync("/api/v1/tasks", request);
        response1.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // 重复提交
        var response2 = await _client.PostAsJsonAsync("/api/v1/tasks", request);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── 获取任务列表 ──

    [Fact]
    public async Task GetTasks_ReturnsPagedResult()
    {
        // 先提交几个任务
        for (int i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync("/api/v1/tasks", TestDataFactory.CreateSubmitRequest());
        }

        var response = await _client.GetAsync("/api/v1/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
    }

    // ── 获取任务详情 ──

    [Fact]
    public async Task GetTask_NonExistingId_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/tasks/non-existing-task-id");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTask_ExistingId_ReturnsTask()
    {
        var taskId = "get-test-" + Guid.NewGuid();
        var request = TestDataFactory.CreateSubmitRequest(taskId);
        await _client.PostAsJsonAsync("/api/v1/tasks", request);

        var response = await _client.GetAsync($"/api/v1/tasks/{taskId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetString().Should().Be(taskId);
        body.GetProperty("status").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("description").GetString().Should().NotBeNullOrEmpty();
    }

    // ── 审查任务 ──

    [Fact]
    public async Task ReviewTask_ValidRequest_Returns200()
    {
        var taskId = "review-test-" + Guid.NewGuid();
        var submitRequest = TestDataFactory.CreateSubmitRequest(taskId);
        await _client.PostAsJsonAsync("/api/v1/tasks", submitRequest);

        var reviewRequest = new ReviewRequest
        {
            CompletionScore = 90,
            CorrectnessScore = 95,
            QualityScore = 88,
            EfficiencyScore = 85,
            UxScore = 80,
            ReviewerComments = "代码质量优秀，逻辑清晰"
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/tasks/{taskId}/review", reviewRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("reviewed");
        body.GetProperty("score").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ReviewTask_NonExistingTask_Returns404()
    {
        var reviewRequest = new ReviewRequest
        {
            CompletionScore = 90,
            ReviewerComments = "测试"
        };

        var response = await _client.PutAsJsonAsync(
            "/api/v1/tasks/non-existing-review-id/review", reviewRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReviewTask_PartialScores_OnlyUpdatesProvided()
    {
        var taskId = "partial-review-" + Guid.NewGuid();
        await _client.PostAsJsonAsync("/api/v1/tasks", TestDataFactory.CreateSubmitRequest(taskId));

        // 仅提供部分评分
        var reviewRequest = new ReviewRequest
        {
            CompletionScore = 95,
            ReviewerComments = "仅更新完成度"
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/tasks/{taskId}/review", reviewRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── 数据分析 ──

    [Fact]
    public async Task GetSummary_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/analytics/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        body.GetProperty("needsReview").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetErrorPatterns_Neo4jUnavailable_ReturnsEmptyList()
    {
        var response = await _client.GetAsync("/api/v1/analytics/error-patterns");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetArrayLength().Should().Be(0);
    }

    // ── 教训（Lessons） ──

    [Fact]
    public async Task GetLessons_Neo4jUnavailable_ReturnsEmptyList()
    {
        // 先提交一个任务
        var taskId = "lessons-get-" + Guid.NewGuid();
        await _client.PostAsJsonAsync("/api/v1/tasks", TestDataFactory.CreateSubmitRequest(taskId));

        var response = await _client.GetAsync($"/api/v1/tasks/{taskId}/lessons");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Neo4j 不可用时降级返回空数组
        body.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetLessons_NonExistingTask_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/tasks/non-existing-lesson-task/lessons");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateLesson_Neo4jUnavailable_Returns503()
    {
        // 先提交一个任务
        var taskId = "lessons-post-" + Guid.NewGuid();
        await _client.PostAsJsonAsync("/api/v1/tasks", TestDataFactory.CreateSubmitRequest(taskId));

        var lessonRequest = new
        {
            problem = "循环引用导致栈溢出",
            cause = "递归函数缺少终止条件",
            suggestion = "添加递归深度检查或改为迭代实现"
        };

        var response = await _client.PostAsJsonAsync($"/api/v1/tasks/{taskId}/lessons", lessonRequest);

        // Neo4j 不可用时返回 503
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
