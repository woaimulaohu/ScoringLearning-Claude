using CCTaskScoring.Core.DTOs;
using CCTaskScoring.Core.Interfaces;
using CCTaskScoring.Core.Models;
using System.Text.Json;

namespace CCTaskScoring.Api.Services;

/// <summary>
/// 后台评分任务处理服务
/// 从队列取出任务 → 五维评分 → 生成教训（低分）→ 写入 Neo4j → 执行奖惩 → 更新状态
/// </summary>
public class ScoringBackgroundService : BackgroundService
{
    private readonly System.Threading.Channels.Channel<string> _taskQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScoringBackgroundService> _logger;

    /// <summary>初始化后台评分服务</summary>
    /// <param name="taskQueue">任务评分队列</param>
    /// <param name="serviceProvider">DI 容器（用于创建 Scope）</param>
    /// <param name="logger">日志记录器</param>
    public ScoringBackgroundService(
        System.Threading.Channels.Channel<string> taskQueue,
        IServiceProvider serviceProvider,
        ILogger<ScoringBackgroundService> logger)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>后台持续消费评分队列</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scoring background service started");

        await foreach (var taskId in _taskQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessTaskAsync(taskId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing task: {TaskId}", taskId);
            }
        }

        _logger.LogInformation("Scoring background service stopped");
    }

    /// <summary>处理单个任务的完整评分流程</summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="ct">取消令牌</param>
    private async Task ProcessTaskAsync(string taskId, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var scoreRepo = scope.ServiceProvider.GetRequiredService<IScoreRepository>();
        var scoringEngine = scope.ServiceProvider.GetRequiredService<IScoringEngine>();
        var rewardService = scope.ServiceProvider.GetRequiredService<IRewardService>();
        var neo4jService = scope.ServiceProvider.GetRequiredService<INeo4jService>();
        var mcpClient = scope.ServiceProvider.GetRequiredService<IMcpClient>();

        var task = await taskRepo.GetByIdAsync(taskId);
        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for scoring", taskId);
            return;
        }

        _logger.LogInformation("Starting scoring pipeline for task {TaskId}", taskId);

        // 标记为评分中
        task.Status = "scoring";
        await taskRepo.UpdateAsync(task);
        await taskRepo.SaveChangesAsync();

        // 1. 反序列化请求数据
        var request = BuildSubmitRequest(task);

        // 2. 五维评分
        var result = await scoringEngine.ScoreAsync(request, ct);

        // 3. 存储评分
        var score = new ScoreEntity
        {
            TaskId = taskId,
            CompletionScore = result.CompletionScore,
            CorrectnessScore = result.CorrectnessScore,
            QualityScore = result.QualityScore,
            EfficiencyScore = result.EfficiencyScore,
            UxScore = result.UxScore,
            TotalScore = result.TotalScore,
            AutoScored = true
        };
        await scoreRepo.AddAsync(score);
        await scoreRepo.SaveChangesAsync();

        // 4. 低分任务触发经验教训生成
        if (result.TotalScore < 60 || result.CorrectnessScore < 60)
        {
            await TryGenerateLessonAsync(task, result, neo4jService, mcpClient, ct);
        }

        // 5. 执行奖惩
        await rewardService.ApplyAsync(taskId, result.TotalScore, ct);

        // 6. 更新任务状态
        task.Status = result.TotalScore < 40 ? "needs_review" : "scored";
        task.CompletedAt = DateTime.UtcNow;
        await taskRepo.UpdateAsync(task);
        await taskRepo.SaveChangesAsync();

        _logger.LogInformation(
            "Task {TaskId} scoring complete. Total={Total:F1}, Status={Status}",
            taskId, result.TotalScore, task.Status);
    }

    /// <summary>将 TaskEntity 中的 JSON 字段反序列化为 TaskSubmitRequest</summary>
    /// <param name="task">任务实体</param>
    /// <returns>任务提交请求 DTO</returns>
    private static TaskSubmitRequest BuildSubmitRequest(TaskEntity task)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return new TaskSubmitRequest
        {
            TaskId = task.Id,
            Description = task.Description,
            Artifacts = DeserializeOrDefault<ArtifactsDto>(task.Artifacts, jsonOptions),
            Logs = task.Logs,
            TestResults = DeserializeOrDefault<TestResultsDto>(task.TestResults, jsonOptions),
            StaticAnalysis = DeserializeOrDefault<StaticAnalysisDto>(task.StaticAnalysis, jsonOptions),
            Metadata = DeserializeOrDefault<TaskMetadataDto>(task.Metadata, jsonOptions)
        };
    }

    /// <summary>安全反序列化 JSON 字符串，失败时返回 null</summary>
    private static T? DeserializeOrDefault<T>(string? json, JsonSerializerOptions options) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 尝试生成经验教训并写入 Neo4j
    /// Neo4j 或 MCP 不可用时静默跳过
    /// </summary>
    private async Task TryGenerateLessonAsync(
        TaskEntity task, ScoringResult result,
        INeo4jService neo4jService, IMcpClient mcpClient,
        CancellationToken ct)
    {
        try
        {
            if (!await neo4jService.IsAvailableAsync())
            {
                _logger.LogWarning("Neo4j unavailable, skipping lesson generation for task {TaskId}", task.Id);
                return;
            }

            await neo4jService.CreateOrMergeTaskNodeAsync(
                task.Id, task.Description, task.Language);

            var context =
                $"任务：{task.Description}\n" +
                $"总分：{result.TotalScore:F1}\n" +
                $"正确性：{result.CorrectnessScore:F1}\n" +
                $"质量：{result.QualityScore:F1}";

            var lesson = await mcpClient.GenerateLessonAsync(context, ct);

            var errorName = result.CorrectnessScore < 60
                ? $"CorrectnessIssue_{task.Language ?? "General"}"
                : $"QualityIssue_{task.Language ?? "General"}";

            await neo4jService.CreateLessonAsync(
                task.Id, errorName,
                lesson.Problem, lesson.Cause, lesson.Suggestion);

            _logger.LogInformation("Lesson generated for task {TaskId}", task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to generate lesson for task {TaskId}, skipping", task.Id);
        }
    }
}
