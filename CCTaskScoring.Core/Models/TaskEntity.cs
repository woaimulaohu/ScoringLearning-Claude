namespace CCTaskScoring.Core.Models;

/// <summary>
/// 任务实体
/// </summary>
public class TaskEntity
{
    /// <summary>任务唯一标识</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>任务描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>编程语言</summary>
    public string? Language { get; set; }

    /// <summary>框架</summary>
    public string? Framework { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>完成时间</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>产物（JSON）</summary>
    public string? Artifacts { get; set; }

    /// <summary>日志</summary>
    public string? Logs { get; set; }

    /// <summary>测试结果（JSON）</summary>
    public string? TestResults { get; set; }

    /// <summary>静态分析结果（JSON）</summary>
    public string? StaticAnalysis { get; set; }

    /// <summary>元数据（JSON）</summary>
    public string? Metadata { get; set; }

    /// <summary>任务状态：pending, scoring, scored, reviewed</summary>
    public string Status { get; set; } = "pending";

    // 导航属性
    /// <summary>评分</summary>
    public ScoreEntity? Score { get; set; }
}
