using CCTaskScoring.Infrastructure.Mcp;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CCTaskScoring.Tests.Unit;

/// <summary>MCP 客户端单元测试（降级场景）</summary>
public class McpClientTests
{
    /// <summary>创建带有指定配置的 McpClient</summary>
    /// <param name="apiKey">API 密钥（空表示 MCP 不可用）</param>
    /// <param name="httpClient">自定义 HttpClient（可选）</param>
    /// <returns>McpClient 实例</returns>
    private static McpClient CreateClient(string apiKey = "", HttpClient? httpClient = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mcp:ApiKey"] = apiKey,
                ["Mcp:Model"] = "test-model"
            })
            .Build();
        var logger = new Mock<ILogger<McpClient>>().Object;
        return new McpClient(httpClient ?? new HttpClient(), config, logger);
    }

    // ── ExtractRequirements 降级测试 ──

    [Fact]
    public async Task ExtractRequirements_NoApiKey_ReturnsFallbackEmptyArray()
    {
        var client = CreateClient(apiKey: "");

        var result = await client.ExtractRequirementsAsync("编写一个排序函数");

        result.Should().BeEmpty();
    }

    // ── GenerateLesson 降级测试 ──

    [Fact]
    public async Task GenerateLesson_NoApiKey_ReturnsFallbackLesson()
    {
        var client = CreateClient(apiKey: "");

        var result = await client.GenerateLessonAsync("任务失败上下文");

        result.Should().NotBeNull();
        result.Problem.Should().NotBeEmpty();
        result.Cause.Should().NotBeEmpty();
        result.Suggestion.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateLesson_NoApiKey_ReturnsFallbackWithExpectedContent()
    {
        var client = CreateClient(apiKey: "");

        var result = await client.GenerateLessonAsync("代码质量太差");

        result.Problem.Should().Contain("质量");
        result.Cause.Should().Contain("未");
        result.Suggestion.Should().Contain("MCP");
    }

    // ── AnalyzeCodeCorrectness 降级测试 ──

    [Fact]
    public async Task AnalyzeCodeCorrectness_NoApiKey_ReturnsMinus1()
    {
        var client = CreateClient(apiKey: "");

        var result = await client.AnalyzeCodeCorrectnessAsync("def foo(): pass", "def test_foo(): pass");

        result.Should().Be(-1.0);
    }
}
