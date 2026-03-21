using CCTaskScoring.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace CCTaskScoring.Infrastructure.Mcp;

/// <summary>
/// MCP 客户端（HTTP 方式调用 Claude AI）
/// 包含降级逻辑：MCP 不可用时返回默认值，不抛出异常。
/// </summary>
public class McpClient : IMcpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpClient> _logger;
    private readonly string _apiKey;
    private readonly string _model;

    /// <summary>初始化 MCP 客户端</summary>
    /// <param name="httpClient">HTTP 客户端（由 IHttpClientFactory 管理）</param>
    /// <param name="configuration">应用配置</param>
    /// <param name="logger">日志记录器</param>
    public McpClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<McpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiKey = configuration["Mcp:ApiKey"] ?? string.Empty;
        _model = configuration["Mcp:Model"] ?? "claude-3-5-haiku-20241022";

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 从任务描述提取需求列表
    /// 降级：MCP 不可用时返回空数组
    /// </summary>
    /// <param name="description">任务描述文本</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>需求列表</returns>
    public async Task<string[]> ExtractRequirementsAsync(string description, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return Array.Empty<string>();

        try
        {
            var prompt =
                "请从以下任务描述中提取需求列表，每行一个需求，只输出需求列表，不要其他内容：\n\n"
                + description;
            var response = await CallClaudeAsync(prompt, ct);
            return response
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP ExtractRequirements failed, returning empty");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 分析代码正确性
    /// 降级：MCP 不可用时返回 -1 表示不可用
    /// </summary>
    /// <param name="code">源代码</param>
    /// <param name="tests">测试代码</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>0~100 分数，-1 表示不可用</returns>
    public async Task<double> AnalyzeCodeCorrectnessAsync(
        string code, string tests, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return -1;

        try
        {
            var prompt =
                "请分析以下代码与测试的正确性，给出 0-100 的分数（只输出数字）：\n"
                + $"代码：\n{code}\n\n测试：\n{tests}";
            var response = await CallClaudeAsync(prompt, ct);
            return double.TryParse(response.Trim(), out var score)
                ? Math.Clamp(score, 0, 100)
                : -1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP AnalyzeCorrectness failed");
            return -1;
        }
    }

    /// <summary>
    /// 生成结构化经验教训
    /// 降级：MCP 不可用时返回通用教训
    /// </summary>
    /// <param name="context">失败上下文信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含问题、原因、建议的教训结果</returns>
    public async Task<LessonResult> GenerateLessonAsync(string context, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return new LessonResult(
                "代码质量或正确性问题",
                "未分析（MCP 未配置）",
                "配置 MCP客户端 后将自动生成改进建议");
        }

        try
        {
            var prompt = $$"""
                请根据以下任务失败上下文，生成结构化的经验教训。
                输出格式（严格 JSON）：
                {"problem":"问题描述","cause":"根本原因","suggestion":"改进建议"}

                上下文：{{context}}
                """;
            var response = await CallClaudeAsync(prompt, ct);
            var doc = JsonDocument.Parse(response);
            return new LessonResult(
                doc.RootElement.GetProperty("problem").GetString() ?? "未知问题",
                doc.RootElement.GetProperty("cause").GetString() ?? "未知原因",
                doc.RootElement.GetProperty("suggestion").GetString() ?? "暂无建议");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP GenerateLesson failed, using fallback");
            return new LessonResult(
                "代码存在质量问题",
                "自动分析失败",
                "请人工审查并添加改进建议");
        }
    }

    /// <summary>调用 Claude API（Anthropic Messages API）</summary>
    /// <param name="prompt">提示文本</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>AI 回复文本</returns>
    private async Task<string> CallClaudeAsync(string prompt, CancellationToken ct)
    {
        var request = new
        {
            model = _model,
            max_tokens = 1024,
            messages = new[] { new { role = "user", content = prompt } }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            "https://api.anthropic.com/v1/messages");
        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        return json?.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }
}
