using CCTaskScoring.Core.DTOs;
using CCTaskScoring.Core.Interfaces;
using CCTaskScoring.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace CCTaskScoring.Api.Mcp;

/// <summary>
/// CCTaskScoring MCP 工具集 — 供 Claude Code 调用评分与学习系统
/// </summary>
[McpServerToolType]
public sealed class ScoringMcpTools
{
    private readonly ITaskRepository _taskRepo;
    private readonly IScoreRepository _scoreRepo;
    private readonly INeo4jService _neo4jService;
    private readonly System.Threading.Channels.Channel<string> _taskQueue;
    private readonly ILogger<ScoringMcpTools> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ScoringMcpTools(
        ITaskRepository taskRepo,
        IScoreRepository scoreRepo,
        INeo4jService neo4jService,
        System.Threading.Channels.Channel<string> taskQueue,
        ILogger<ScoringMcpTools> logger)
    {
        _taskRepo = taskRepo;
        _scoreRepo = scoreRepo;
        _neo4jService = neo4jService;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    // ── 任务管理 ───────────────────────────────────────────────

    [McpServerTool(Name = "submit_task")]
    [Description("提交 AI 编程任务进行自动评分。任务将异步处理，处理完成后状态从 pending 变为 scored。")]
    public async Task<string> SubmitTaskAsync(
        [Description("全局唯一任务 ID（字符串，由调用方生成）")] string taskId,
        [Description("任务描述，如\"编写快速排序函数\"")] string description,
        [Description("编程语言，如 Python、C#、JavaScript")] string? language = null,
        [Description("生成的代码（字符串）")] string? code = null,
        [Description("测试代码（字符串）")] string? tests = null,
        [Description("运行日志")] string? logs = null,
        [Description("通过的测试数量")] int passed = 0,
        [Description("失败的测试数量")] int failed = 0,
        [Description("Lint 评分（0-10）")] double lintScore = 0,
        [Description("任务执行耗时（秒）")] int durationSec = 0,
        [Description("尝试次数")] int attempts = 1)
    {
        if (await _taskRepo.ExistsAsync(taskId))
            return JsonSerializer.Serialize(new { error = "Task already exists", taskId }, _jsonOpts);

        var request = new TaskSubmitRequest
        {
            TaskId = taskId,
            Description = description,
            Artifacts = (code != null || tests != null) ? new ArtifactsDto { Code = code, Tests = tests } : null,
            Logs = logs,
            TestResults = new TestResultsDto { Passed = passed, Failed = failed, Skipped = 0 },
            StaticAnalysis = new StaticAnalysisDto { LintScore = lintScore },
            Metadata = new TaskMetadataDto { Language = language, DurationSec = durationSec, Attempts = attempts }
        };

        var task = new TaskEntity
        {
            Id = taskId,
            Description = description,
            Language = language,
            CreatedAt = DateTime.UtcNow,
            Artifacts = request.Artifacts != null ? JsonSerializer.Serialize(request.Artifacts) : null,
            Logs = logs,
            TestResults = JsonSerializer.Serialize(request.TestResults),
            StaticAnalysis = JsonSerializer.Serialize(request.StaticAnalysis),
            Metadata = JsonSerializer.Serialize(request.Metadata),
            Status = "pending"
        };

        await _taskRepo.AddAsync(task);
        await _taskRepo.SaveChangesAsync();
        await _taskQueue.Writer.WriteAsync(taskId);

        _logger.LogInformation("[MCP] Task {TaskId} submitted via MCP tool", taskId);
        return JsonSerializer.Serialize(new { taskId, status = "pending", message = "任务已提交，后台正在自动评分" }, _jsonOpts);
    }

    [McpServerTool(Name = "get_task")]
    [Description("查询任务详情和评分结果。状态说明：pending=等待评分，scored=自动评分完成，reviewed=人工审查完成，needs_review=需要人工审查。")]
    public async Task<string> GetTaskAsync(
        [Description("要查询的任务 ID")] string taskId)
    {
        var task = await _taskRepo.GetByIdAsync(taskId);
        if (task == null)
            return JsonSerializer.Serialize(new { error = $"Task '{taskId}' not found" }, _jsonOpts);

        return JsonSerializer.Serialize(MapToSummary(task), _jsonOpts);
    }

    [McpServerTool(Name = "list_tasks")]
    [Description("分页列出任务，支持按状态过滤。可用状态：pending、scored、reviewed、needs_review。")]
    public async Task<string> ListTasksAsync(
        [Description("状态过滤（留空返回所有）")] string? status = null,
        [Description("页码，从 1 开始")] int page = 1,
        [Description("每页数量，最大 50")] int pageSize = 10)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var tasks = await _taskRepo.GetAllAsync(status, page, pageSize);
        var total = await _taskRepo.GetCountAsync(status);

        var result = new
        {
            total,
            page,
            pageSize,
            data = tasks.Select(MapToSummary)
        };

        return JsonSerializer.Serialize(result, _jsonOpts);
    }

    [McpServerTool(Name = "review_task")]
    [Description("人工审查员修正任务评分。所有评分字段均为 0-100 的整数，未提供的字段保持原值不变。五维权重：完成度 30%、正确性 30%、质量 20%、效率 10%、UX 10%。")]
    public async Task<string> ReviewTaskAsync(
        [Description("要审查的任务 ID")] string taskId,
        [Description("完成度评分（0-100）")] int? completionScore = null,
        [Description("正确性评分（0-100）")] int? correctnessScore = null,
        [Description("代码质量评分（0-100）")] int? qualityScore = null,
        [Description("效率评分（0-100）")] int? efficiencyScore = null,
        [Description("用户体验评分（0-100）")] int? uxScore = null,
        [Description("审查员评语")] string? comments = null)
    {
        var task = await _taskRepo.GetByIdAsync(taskId);
        if (task == null)
            return JsonSerializer.Serialize(new { error = $"Task '{taskId}' not found" }, _jsonOpts);

        var score = task.Score;
        var isNewScore = score == null;
        if (score == null)
        {
            score = new ScoreEntity { TaskId = taskId };
            await _scoreRepo.AddAsync(score);
        }

        if (completionScore.HasValue) score.CompletionScore = completionScore.Value;
        if (correctnessScore.HasValue) score.CorrectnessScore = correctnessScore.Value;
        if (qualityScore.HasValue) score.QualityScore = qualityScore.Value;
        if (efficiencyScore.HasValue) score.EfficiencyScore = efficiencyScore.Value;
        if (uxScore.HasValue) score.UxScore = uxScore.Value;
        if (comments != null) score.ReviewerComments = comments;
        score.AutoScored = false;

        score.TotalScore = score.CompletionScore * 0.3
            + score.CorrectnessScore * 0.3
            + score.QualityScore * 0.2
            + score.EfficiencyScore * 0.1
            + score.UxScore * 0.1;

        task.Status = "reviewed";
        await _taskRepo.UpdateAsync(task);
        if (!isNewScore)
            await _scoreRepo.UpdateAsync(score);
        await _taskRepo.SaveChangesAsync();

        _logger.LogInformation("[MCP] Task {TaskId} reviewed via MCP tool, totalScore={Score}", taskId, score.TotalScore);
        return JsonSerializer.Serialize(MapToSummary(task), _jsonOpts);
    }

    // ── 数据分析 ───────────────────────────────────────────────

    [McpServerTool(Name = "get_analytics_summary")]
    [Description("获取系统统计摘要：任务总数、待审查数量、平均分。")]
    public async Task<string> GetAnalyticsSummaryAsync()
    {
        var total = await _taskRepo.GetCountAsync(null);
        var needsReview = await _taskRepo.GetCountAsync("needs_review");
        var scored = await _taskRepo.GetCountAsync("scored");
        var reviewed = await _taskRepo.GetCountAsync("reviewed");

        // 计算平均分（有评分的任务）
        var allTasks = await _taskRepo.GetAllAsync(null, 1, int.MaxValue);
        var scoredTasks = allTasks.Where(t => t.Score != null).ToList();
        var avgScore = scoredTasks.Count > 0 ? scoredTasks.Average(t => t.Score!.TotalScore) : 0;

        return JsonSerializer.Serialize(new
        {
            total,
            pending = await _taskRepo.GetCountAsync("pending"),
            scored,
            reviewed,
            needsReview,
            averageScore = Math.Round(avgScore, 2)
        }, _jsonOpts);
    }

    [McpServerTool(Name = "get_error_patterns")]
    [Description("获取高频错误模式（来自 Neo4j 知识图谱）。Neo4j 不可用时返回空列表。")]
    public async Task<string> GetErrorPatternsAsync(
        [Description("返回数量限制，默认 10")] int limit = 10)
    {
        try
        {
            if (_neo4jService == null || !await _neo4jService.IsAvailableAsync())
                return JsonSerializer.Serialize(Array.Empty<object>(), _jsonOpts);

            var patterns = await _neo4jService.GetTopErrorPatternsAsync(limit);
            return JsonSerializer.Serialize(patterns, _jsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MCP] Failed to fetch error patterns");
            return JsonSerializer.Serialize(Array.Empty<object>(), _jsonOpts);
        }
    }

    [McpServerTool(Name = "get_lessons")]
    [Description("获取与指定编程语言相关的历史教训（来自 Neo4j 知识图谱）。Neo4j 不可用时返回空列表。")]
    public async Task<string> GetLessonsAsync(
        [Description("编程语言，如 Python、C#、JavaScript")] string? language = null,
        [Description("任务类型（可选）")] string? taskType = null)
    {
        try
        {
            if (_neo4jService == null || !await _neo4jService.IsAvailableAsync())
                return JsonSerializer.Serialize(Array.Empty<object>(), _jsonOpts);

            var lessons = await _neo4jService.GetRelatedLessonsAsync(language, taskType);
            return JsonSerializer.Serialize(lessons, _jsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MCP] Failed to fetch lessons");
            return JsonSerializer.Serialize(Array.Empty<object>(), _jsonOpts);
        }
    }

    // ── 私有辅助方法 ───────────────────────────────────────────

    private static object MapToSummary(TaskEntity t) => new
    {
        id = t.Id,
        description = t.Description,
        language = t.Language,
        status = t.Status,
        createdAt = t.CreatedAt,
        completedAt = t.CompletedAt,
        score = t.Score == null ? null : (object)new
        {
            completionScore = t.Score.CompletionScore,
            correctnessScore = t.Score.CorrectnessScore,
            qualityScore = t.Score.QualityScore,
            efficiencyScore = t.Score.EfficiencyScore,
            uxScore = t.Score.UxScore,
            totalScore = t.Score.TotalScore,
            autoScored = t.Score.AutoScored,
            reviewerComments = t.Score.ReviewerComments
        }
    };
}
