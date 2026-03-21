namespace CCTaskScoring.Core.Models;

/// <summary>
/// 评分实体
/// </summary>
public class ScoreEntity
{
    /// <summary>评分 ID</summary>
    public int Id { get; set; }

    /// <summary>关联任务 ID</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>完成度评分（0-100）</summary>
    public double CompletionScore { get; set; }

    /// <summary>正确性评分（0-100）</summary>
    public double CorrectnessScore { get; set; }

    /// <summary>代码质量评分（0-100）</summary>
    public double QualityScore { get; set; }

    /// <summary>效率评分（0-100）</summary>
    public double EfficiencyScore { get; set; }

    /// <summary>用户体验评分（0-100）</summary>
    public double UxScore { get; set; }

    /// <summary>加权总分</summary>
    public double TotalScore { get; set; }

    /// <summary>是否为自动评分</summary>
    public bool AutoScored { get; set; } = true;

    /// <summary>审查员评语</summary>
    public string? ReviewerComments { get; set; }

    /// <summary>评分创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    /// <summary>关联任务</summary>
    public TaskEntity? Task { get; set; }
}
