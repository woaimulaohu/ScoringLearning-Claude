using CCTaskScoring.Core.DTOs;
using CCTaskScoring.Core.Interfaces;
using CCTaskScoring.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CCTaskScoring.Api.Controllers;

/// <summary>
/// 任务管理控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskRepository _taskRepo;
    private readonly IScoreRepository _scoreRepo;
    private readonly INeo4jService _neo4jService;
    private readonly System.Threading.Channels.Channel<string> _taskQueue;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        ITaskRepository taskRepo,
        IScoreRepository scoreRepo,
        INeo4jService neo4jService,
        System.Threading.Channels.Channel<string> taskQueue,
        ILogger<TasksController> logger)
    {
        _taskRepo = taskRepo;
        _scoreRepo = scoreRepo;
        _neo4jService = neo4jService;
        _taskQueue = taskQueue;
        _logger = logger;
    }

    /// <summary>提交任务（幂等，202 Accepted）</summary>
    /// <param name="request">任务提交请求</param>
    /// <returns>任务 ID 和状态</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitTask([FromBody] TaskSubmitRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // 幂等性检查
        if (await _taskRepo.ExistsAsync(request.TaskId))
        {
            _logger.LogWarning("Duplicate task submission: {TaskId}", request.TaskId);
            return Conflict(new { message = "Task already exists", taskId = request.TaskId });
        }

        var task = new TaskEntity
        {
            Id = request.TaskId,
            Description = request.Description,
            Language = request.Metadata?.Language,
            CreatedAt = request.Timestamp,
            Artifacts = request.Artifacts != null ? JsonSerializer.Serialize(request.Artifacts) : null,
            Logs = request.Logs,
            TestResults = request.TestResults != null ? JsonSerializer.Serialize(request.TestResults) : null,
            StaticAnalysis = request.StaticAnalysis != null ? JsonSerializer.Serialize(request.StaticAnalysis) : null,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
            Status = "pending"
        };

        await _taskRepo.AddAsync(task);
        await _taskRepo.SaveChangesAsync();

        // 加入后台处理队列
        await _taskQueue.Writer.WriteAsync(request.TaskId);

        _logger.LogInformation("Task {TaskId} submitted and queued for scoring", request.TaskId);
        return Accepted(new { taskId = request.TaskId, status = "pending" });
    }

    /// <summary>获取任务列表（分页）</summary>
    /// <param name="status">状态过滤</param>
    /// <param name="page">页码</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>分页任务列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var tasks = await _taskRepo.GetAllAsync(status, page, pageSize);
        var total = await _taskRepo.GetCountAsync(status);
        var result = tasks.Select(MapToResponse);
        return Ok(new { data = result, total, page, pageSize });
    }

    /// <summary>获取任务详情</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>任务详情</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTask(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null)
            return NotFound(new { message = $"Task {id} not found" });

        return Ok(MapToResponse(task));
    }

    /// <summary>审查员修正评分</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="request">审查请求</param>
    /// <returns>更新后的任务详情</returns>
    [HttpPut("{id}/review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewTask(string id, [FromBody] ReviewRequest request)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null)
            return NotFound(new { message = $"Task {id} not found" });

        var score = task.Score;
        var isNewScore = score == null;
        if (score == null)
        {
            score = new ScoreEntity { TaskId = id };
            await _scoreRepo.AddAsync(score);
        }

        if (request.CompletionScore.HasValue) score.CompletionScore = request.CompletionScore.Value;
        if (request.CorrectnessScore.HasValue) score.CorrectnessScore = request.CorrectnessScore.Value;
        if (request.QualityScore.HasValue) score.QualityScore = request.QualityScore.Value;
        if (request.EfficiencyScore.HasValue) score.EfficiencyScore = request.EfficiencyScore.Value;
        if (request.UxScore.HasValue) score.UxScore = request.UxScore.Value;
        if (request.ReviewerComments != null) score.ReviewerComments = request.ReviewerComments;
        score.AutoScored = false;

        // 加权总分：完成度 30% + 正确性 30% + 质量 20% + 效率 10% + UX 10%
        score.TotalScore = score.CompletionScore * 0.3
            + score.CorrectnessScore * 0.3
            + score.QualityScore * 0.2
            + score.EfficiencyScore * 0.1
            + score.UxScore * 0.1;

        task.Status = "reviewed";
        await _taskRepo.UpdateAsync(task);
        // 仅当 score 是已存在记录时才调用 Update；新建的 score 已通过 AddAsync 标记为 Added
        if (!isNewScore)
            await _scoreRepo.UpdateAsync(score);
        await _taskRepo.SaveChangesAsync();

        return Ok(MapToResponse(task));
    }

    /// <summary>获取任务关联的教训</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>教训列表</returns>
    [HttpGet("{id}/lessons")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLessons(string id)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null)
            return NotFound(new { message = $"Task {id} not found" });

        try
        {
            // Neo4j 降级处理
            if (_neo4jService == null || !await _neo4jService.IsAvailableAsync())
            {
                _logger.LogWarning("Neo4j unavailable, returning empty lessons for task {TaskId}", id);
                return Ok(Array.Empty<LessonDto>());
            }

            var lessons = await _neo4jService.GetRelatedLessonsAsync(task.Language, null);
            return Ok(lessons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch lessons for task {TaskId}, returning empty list", id);
            return Ok(Array.Empty<LessonDto>());
        }
    }

    /// <summary>为任务创建教训</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="request">教训创建请求</param>
    /// <returns>创建的教训</returns>
    [HttpPost("{id}/lessons")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateLesson(string id, [FromBody] CreateLessonRequest request)
    {
        var task = await _taskRepo.GetByIdAsync(id);
        if (task == null)
            return NotFound(new { message = $"Task {id} not found" });

        try
        {
            // Neo4j 降级处理：不可用时返回 503
            if (_neo4jService == null || !await _neo4jService.IsAvailableAsync())
            {
                _logger.LogWarning("Neo4j unavailable, cannot create lesson for task {TaskId}", id);
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Neo4j service unavailable" });
            }

            var lessonId = await _neo4jService.CreateLessonAsync(
                id, request.Problem, request.Problem, request.Cause, request.Suggestion);

            var lesson = new LessonDto(
                lessonId, request.Problem, request.Cause, request.Suggestion, DateTime.UtcNow);

            _logger.LogInformation("Lesson {LessonId} created for task {TaskId}", lessonId, id);
            return StatusCode(StatusCodes.Status201Created, lesson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create lesson for task {TaskId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Failed to create lesson" });
        }
    }

    private static TaskResponse MapToResponse(TaskEntity t) => new()
    {
        Id = t.Id,
        Description = t.Description,
        Language = t.Language,
        Framework = t.Framework,
        Status = t.Status,
        CreatedAt = t.CreatedAt,
        CompletedAt = t.CompletedAt,
        Artifacts = t.Artifacts,
        Logs = t.Logs,
        TestResults = t.TestResults,
        StaticAnalysis = t.StaticAnalysis,
        Metadata = t.Metadata,
        Score = t.Score == null ? null : new ScoreResponse
        {
            Id = t.Score.Id,
            CompletionScore = t.Score.CompletionScore,
            CorrectnessScore = t.Score.CorrectnessScore,
            QualityScore = t.Score.QualityScore,
            EfficiencyScore = t.Score.EfficiencyScore,
            UxScore = t.Score.UxScore,
            TotalScore = t.Score.TotalScore,
            AutoScored = t.Score.AutoScored,
            ReviewerComments = t.Score.ReviewerComments
        }
    };
}
