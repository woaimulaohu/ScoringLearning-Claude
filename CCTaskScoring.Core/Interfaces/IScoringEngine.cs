using CCTaskScoring.Core.DTOs;

namespace CCTaskScoring.Core.Interfaces;

/// <summary>评分结果（五维 + 总分）</summary>
public record ScoringResult(
    double CompletionScore,
    double CorrectnessScore,
    double QualityScore,
    double EfficiencyScore,
    double UxScore,
    double TotalScore);

/// <summary>
/// 评分引擎接口
/// </summary>
public interface IScoringEngine
{
    /// <summary>对任务进行五维评分</summary>
    /// <param name="request">任务提交请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>五维评分结果</returns>
    Task<ScoringResult> ScoreAsync(TaskSubmitRequest request, CancellationToken ct = default);
}
