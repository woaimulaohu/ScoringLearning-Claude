using CCTaskScoring.Core.DTOs;
using CCTaskScoring.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CCTaskScoring.Infrastructure.Scoring;

/// <summary>
/// 五维评分引擎
/// 根据任务提交数据计算完成度、正确性、质量、效率、用户体验五个维度的评分，
/// 并按权重加权得到总分。
/// </summary>
public class ScoringEngine : IScoringEngine
{
    private readonly IMcpClient _mcpClient;
    private readonly ILogger<ScoringEngine> _logger;

    // 权重配置
    private const double CompletionWeight = 0.30;
    private const double CorrectnessWeight = 0.30;
    private const double QualityWeight = 0.20;
    private const double EfficiencyWeight = 0.10;
    private const double UxWeight = 0.10;

    // 效率基准（120秒1次attempt = 80分基准）
    private const int BaselineDurationSec = 120;
    private const int BaselineAttempts = 1;
    private const double BaselineEfficiencyScore = 80.0;

    /// <summary>初始化评分引擎</summary>
    /// <param name="mcpClient">MCP 客户端（用于 AI 辅助评估完成度）</param>
    /// <param name="logger">日志记录器</param>
    public ScoringEngine(IMcpClient mcpClient, ILogger<ScoringEngine> logger)
    {
        _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 对任务进行五维评分，返回加权总分
    /// totalScore = completion*0.3 + correctness*0.3 + quality*0.2 + efficiency*0.1 + ux*0.1
    /// </summary>
    /// <param name="request">任务提交请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>五维评分结果</returns>
    public async Task<ScoringResult> ScoreAsync(TaskSubmitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _logger.LogInformation("Scoring task {TaskId}", request.TaskId);

        var correctness = CalculateCorrectnessScore(request.TestResults);
        var quality = CalculateQualityScore(request.StaticAnalysis);
        var efficiency = CalculateEfficiencyScore(request.Metadata?.DurationSec, request.Metadata?.Attempts);
        var ux = CalculateUxScore(request.Logs, request.Artifacts);
        var completion = await CalculateCompletionScoreAsync(request, ct);

        var total = completion * CompletionWeight
                  + correctness * CorrectnessWeight
                  + quality * QualityWeight
                  + efficiency * EfficiencyWeight
                  + ux * UxWeight;

        _logger.LogInformation(
            "Task {TaskId} scored: completion={Completion:F1}, correctness={Correctness:F1}, " +
            "quality={Quality:F1}, efficiency={Efficiency:F1}, ux={Ux:F1}, total={Total:F1}",
            request.TaskId, completion, correctness, quality, efficiency, ux, total);

        return new ScoringResult(completion, correctness, quality, efficiency, ux, Math.Round(total, 2));
    }

    /// <summary>
    /// 代码正确性：基于测试通过率
    /// 公式：passed / (passed + failed + skipped) * 100
    /// </summary>
    /// <param name="testResults">测试结果数据</param>
    /// <returns>0~100 的正确性分数</returns>
    internal static double CalculateCorrectnessScore(TestResultsDto? testResults)
    {
        if (testResults == null)
            return 60.0; // 无测试数据时给默认分

        var total = testResults.Passed + testResults.Failed + testResults.Skipped;
        if (total == 0)
            return 60.0;

        return Math.Round((double)testResults.Passed / total * 100, 2);
    }

    /// <summary>
    /// 代码质量：基于静态分析 lintScore（满分10分，×10 换算为 0~100）
    /// </summary>
    /// <param name="staticAnalysis">静态分析数据</param>
    /// <returns>0~100 的质量分数</returns>
    internal static double CalculateQualityScore(StaticAnalysisDto? staticAnalysis)
    {
        if (staticAnalysis == null)
            return 60.0;

        return Math.Round(Math.Clamp(staticAnalysis.LintScore * 10.0, 0, 100), 2);
    }

    /// <summary>
    /// 效率：基于时长和尝试次数
    /// 基准：120s / 1次 = 80分
    /// 时长惩罚：每超过基准 60s，扣 5 分
    /// 尝试次数惩罚：每多1次尝试，扣 10 分
    /// </summary>
    /// <param name="durationSec">任务耗时（秒）</param>
    /// <param name="attempts">尝试次数</param>
    /// <returns>0~100 的效率分数</returns>
    internal static double CalculateEfficiencyScore(int? durationSec, int? attempts)
    {
        var dur = durationSec ?? BaselineDurationSec;
        var att = Math.Max(attempts ?? BaselineAttempts, 1);

        // 时长惩罚：每超过基准 60s，扣 5 分
        var durationPenalty = Math.Max(0, (dur - BaselineDurationSec) / 60.0 * 5.0);
        // 尝试次数惩罚：每多1次尝试，扣 10 分
        var attemptPenalty = (att - BaselineAttempts) * 10.0;

        var score = BaselineEfficiencyScore - durationPenalty - attemptPenalty;
        return Math.Round(Math.Clamp(score, 0, 100), 2);
    }

    /// <summary>
    /// 用户体验：分析日志清晰度和文档完整性
    /// 基础分 50，根据日志和产物质量加分
    /// </summary>
    /// <param name="logs">日志内容</param>
    /// <param name="artifacts">产出物</param>
    /// <returns>0~100 的用户体验分数</returns>
    internal static double CalculateUxScore(string? logs, ArtifactsDto? artifacts)
    {
        double score = 50.0; // 基础分

        // 日志不为空且有实质内容 +20
        if (!string.IsNullOrWhiteSpace(logs) && logs.Length > 10)
            score += 20;

        // 日志包含结构化标记 +10
        if (logs?.Contains('\n') == true
            || logs?.Contains("INFO") == true
            || logs?.Contains("ERROR") == true)
            score += 10;

        // 产出物包含测试代码 +10
        if (!string.IsNullOrWhiteSpace(artifacts?.Tests))
            score += 10;

        // 产出物包含代码 +10
        if (!string.IsNullOrWhiteSpace(artifacts?.Code))
            score += 10;

        return Math.Round(Math.Clamp(score, 0, 100), 2);
    }

    /// <summary>
    /// 任务完成度：尝试通过 MCP 提取需求并评估完成度，
    /// MCP 不可用时降级为基于规则的判断
    /// </summary>
    private async Task<double> CalculateCompletionScoreAsync(
        TaskSubmitRequest request, CancellationToken ct)
    {
        var hasCode = !string.IsNullOrWhiteSpace(request.Artifacts?.Code);
        var hasDescription = !string.IsNullOrWhiteSpace(request.Description);

        if (!hasDescription)
            return 0;
        if (!hasCode)
            return 30.0;

        try
        {
            // 尝试通过 MCP 提取需求并评估完成度
            var requirements = await _mcpClient.ExtractRequirementsAsync(request.Description, ct);
            if (requirements.Length == 0)
                return 70.0;

            // 简单规则：需求数量与代码行数的比例估算
            var codeLines = request.Artifacts?.Code?.Split('\n').Length ?? 0;
            var completionRatio = Math.Min(1.0, codeLines / (requirements.Length * 5.0));
            return Math.Round(40 + completionRatio * 60, 2); // 40~100 分
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "MCP unavailable for task {TaskId}, using fallback completion scoring",
                request.TaskId);
            // 降级：有代码就给 70 分
            return 70.0;
        }
    }
}
