using CCTaskScoring.Core.Models;

namespace CCTaskScoring.Core.Interfaces;

/// <summary>
/// 任务仓储接口
/// </summary>
public interface ITaskRepository
{
    /// <summary>根据 ID 获取任务（含评分）</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>任务实体，不存在返回 null</returns>
    Task<TaskEntity?> GetByIdAsync(string id);

    /// <summary>获取任务列表（分页）</summary>
    /// <param name="status">状态过滤（可选）</param>
    /// <param name="page">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <returns>任务集合</returns>
    Task<IEnumerable<TaskEntity>> GetAllAsync(string? status = null, int page = 1, int pageSize = 20);

    /// <summary>获取任务总数</summary>
    /// <param name="status">状态过滤（可选）</param>
    /// <returns>总数</returns>
    Task<int> GetCountAsync(string? status = null);

    /// <summary>检查任务是否存在</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(string id);

    /// <summary>添加任务</summary>
    /// <param name="task">任务实体</param>
    Task AddAsync(TaskEntity task);

    /// <summary>更新任务</summary>
    /// <param name="task">任务实体</param>
    Task UpdateAsync(TaskEntity task);

    /// <summary>保存变更</summary>
    Task SaveChangesAsync();
}

/// <summary>
/// 评分仓储接口
/// </summary>
public interface IScoreRepository
{
    /// <summary>根据任务 ID 获取评分</summary>
    /// <param name="taskId">任务 ID</param>
    /// <returns>评分实体</returns>
    Task<ScoreEntity?> GetByTaskIdAsync(string taskId);

    /// <summary>添加评分</summary>
    /// <param name="score">评分实体</param>
    Task AddAsync(ScoreEntity score);

    /// <summary>更新评分</summary>
    /// <param name="score">评分实体</param>
    Task UpdateAsync(ScoreEntity score);

    /// <summary>保存变更</summary>
    Task SaveChangesAsync();
}

/// <summary>
/// 奖惩仓储接口
/// </summary>
public interface IRewardRepository
{
    /// <summary>添加奖惩记录</summary>
    /// <param name="reward">奖惩实体</param>
    Task AddAsync(RewardPunishment reward);

    /// <summary>根据任务 ID 获取奖惩记录</summary>
    /// <param name="taskId">任务 ID</param>
    /// <returns>奖惩记录集合</returns>
    Task<IEnumerable<RewardPunishment>> GetByTaskIdAsync(string taskId);

    /// <summary>保存变更</summary>
    Task SaveChangesAsync();
}
