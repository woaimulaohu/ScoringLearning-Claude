namespace CCTaskScoring.Core.Interfaces;

/// <summary>奖惩动作</summary>
public record RewardAction(string ActionType, string Reason, DateTime? Expiry = null);

/// <summary>
/// 奖惩服务接口
/// </summary>
public interface IRewardService
{
    /// <summary>根据总分执行奖惩</summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="totalScore">总分</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>执行的奖惩动作列表</returns>
    Task<IEnumerable<RewardAction>> ApplyAsync(
        string taskId, double totalScore, CancellationToken ct = default);
}
