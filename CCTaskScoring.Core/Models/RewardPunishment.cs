namespace CCTaskScoring.Core.Models;

/// <summary>
/// 奖惩记录实体
/// </summary>
public class RewardPunishment
{
    /// <summary>记录 ID</summary>
    public int Id { get; set; }

    /// <summary>关联任务 ID</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>操作类型：reward 或 punishment</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>原因说明</summary>
    public string? Reason { get; set; }

    /// <summary>应用时间</summary>
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    /// <summary>过期时间</summary>
    public DateTime? Expiry { get; set; }

    // 导航属性
    /// <summary>关联任务</summary>
    public TaskEntity? Task { get; set; }
}
