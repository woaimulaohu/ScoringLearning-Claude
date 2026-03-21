namespace CCTaskScoring.Core.DTOs;

/// <summary>
/// 任务响应 DTO
/// </summary>
public class TaskResponse
{
    /// <summary>任务 ID</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>任务描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>编程语言</summary>
    public string? Language { get; set; }

    /// <summary>框架</summary>
    public string? Framework { get; set; }

    /// <summary>任务状态</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>完成时间</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>产物（JSON 字符串）</summary>
    public string? Artifacts { get; set; }

    /// <summary>日志</summary>
    public string? Logs { get; set; }

    /// <summary>测试结果（JSON 字符串）</summary>
    public string? TestResults { get; set; }

    /// <summary>静态分析结果（JSON 字符串）</summary>
    public string? StaticAnalysis { get; set; }

    /// <summary>元数据（JSON 字符串）</summary>
    public string? Metadata { get; set; }

    /// <summary>评分信息</summary>
    public ScoreResponse? Score { get; set; }
}

/// <summary>
/// 评分响应 DTO
/// </summary>
public class ScoreResponse
{
    /// <summary>评分 ID</summary>
    public int Id { get; set; }

    /// <summary>完成度评分</summary>
    public double CompletionScore { get; set; }

    /// <summary>正确性评分</summary>
    public double CorrectnessScore { get; set; }

    /// <summary>代码质量评分</summary>
    public double QualityScore { get; set; }

    /// <summary>效率评分</summary>
    public double EfficiencyScore { get; set; }

    /// <summary>用户体验评分</summary>
    public double UxScore { get; set; }

    /// <summary>加权总分</summary>
    public double TotalScore { get; set; }

    /// <summary>是否为自动评分</summary>
    public bool AutoScored { get; set; }

    /// <summary>审查员评语</summary>
    public string? ReviewerComments { get; set; }
}

/// <summary>
/// 人工审查请求 DTO
/// </summary>
public class ReviewRequest
{
    /// <summary>完成度评分（可选覆盖）</summary>
    public double? CompletionScore { get; set; }

    /// <summary>正确性评分（可选覆盖）</summary>
    public double? CorrectnessScore { get; set; }

    /// <summary>代码质量评分（可选覆盖）</summary>
    public double? QualityScore { get; set; }

    /// <summary>效率评分（可选覆盖）</summary>
    public double? EfficiencyScore { get; set; }

    /// <summary>用户体验评分（可选覆盖）</summary>
    public double? UxScore { get; set; }

    /// <summary>审查员评语</summary>
    public string? ReviewerComments { get; set; }
}

/// <summary>
/// 创建教训请求 DTO
/// </summary>
public class CreateLessonRequest
{
    /// <summary>问题描述</summary>
    public string Problem { get; set; } = string.Empty;

    /// <summary>原因分析</summary>
    public string Cause { get; set; } = string.Empty;

    /// <summary>改进建议</summary>
    public string Suggestion { get; set; } = string.Empty;
}
