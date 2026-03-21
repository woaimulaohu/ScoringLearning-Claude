using CCTaskScoring.Core.Interfaces;
using CCTaskScoring.Core.Models;
using Microsoft.Extensions.Logging;

namespace CCTaskScoring.Infrastructure.Rewards;

/// <summary>
/// 基于总分的奖惩服务
/// 根据评分结果自动执行奖惩动作，并持久化记录。
/// </summary>
public class RewardService : IRewardService
{
    private readonly IRewardRepository _rewardRepo;
    private readonly ILogger<RewardService> _logger;

    /// <summary>初始化奖惩服务</summary>
    /// <param name="rewardRepo">奖惩仓储</param>
    /// <param name="logger">日志记录器</param>
    public RewardService(IRewardRepository rewardRepo, ILogger<RewardService> logger)
    {
        _rewardRepo = rewardRepo ?? throw new ArgumentNullException(nameof(rewardRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 根据总分决定并记录奖惩动作
    /// 90-100：提升优先级、增加配额、标记可信执行
    /// 75-89 / 60-74：无奖惩
    /// 40-59：系统警告
    /// 0-39：强制人工复核、限制操作、记录教训
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="totalScore">总分</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>执行的奖惩动作列表</returns>
    public async Task<IEnumerable<RewardAction>> ApplyAsync(
        string taskId, double totalScore, CancellationToken ct = default)
    {
        var actions = DetermineActions(totalScore);
        var defaultExpiry = DateTime.UtcNow.AddDays(30);

        foreach (var action in actions)
        {
            var record = new RewardPunishment
            {
                TaskId = taskId,
                ActionType = action.ActionType,
                Reason = action.Reason,
                AppliedAt = DateTime.UtcNow,
                Expiry = action.Expiry ?? defaultExpiry
            };
            await _rewardRepo.AddAsync(record);
            _logger.LogInformation(
                "Reward/Punishment applied to task {TaskId}: {ActionType} - {Reason}",
                taskId, action.ActionType, action.Reason);
        }

        await _rewardRepo.SaveChangesAsync();
        return actions;
    }

    /// <summary>
    /// 根据总分区间决定奖惩动作
    /// </summary>
    /// <param name="totalScore">总分</param>
    /// <returns>奖惩动作列表</returns>
    internal static IEnumerable<RewardAction> DetermineActions(double totalScore) => totalScore switch
    {
        >= 90 => new[]
        {
            new RewardAction("INCREASE_PRIORITY", "优秀表现：任务优先级提升"),
            new RewardAction("INCREASE_QUOTA", "优秀表现：并发请求配额增加"),
            new RewardAction("MARK_TRUSTED", "优秀表现：标记为可信执行")
        },
        >= 75 => Array.Empty<RewardAction>(), // 良好：无奖惩
        >= 60 => Array.Empty<RewardAction>(), // 合格：无奖惩
        >= 40 => new[]
        {
            new RewardAction("SYSTEM_WARNING", "待改进：已触发系统警告"),
        },
        _ => new[]
        {
            new RewardAction("FORCE_REVIEW", "严重失败：强制人工复核"),
            new RewardAction("RESTRICT_OPERATIONS", "严重失败：限制高风险操作权限",
                DateTime.UtcNow.AddDays(7)),
            new RewardAction("ADD_TO_LESSON_LIBRARY", "严重失败：记录至经验教训库")
        }
    };
}
