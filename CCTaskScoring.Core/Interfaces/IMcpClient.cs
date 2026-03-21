namespace CCTaskScoring.Core.Interfaces;

/// <summary>AI 生成的教训结果</summary>
public record LessonResult(string Problem, string Cause, string Suggestion);

/// <summary>
/// MCP（模型上下文协议）客户端接口
/// </summary>
public interface IMcpClient
{
    /// <summary>从任务描述提取需求列表</summary>
    /// <param name="description">任务描述文本</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>需求列表</returns>
    Task<string[]> ExtractRequirementsAsync(string description, CancellationToken ct = default);

    /// <summary>分析代码逻辑正确性</summary>
    /// <param name="code">源代码</param>
    /// <param name="tests">测试代码</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>0~100 的正确性分数，-1 表示不可用</returns>
    Task<double> AnalyzeCodeCorrectnessAsync(string code, string tests, CancellationToken ct = default);

    /// <summary>生成结构化经验教训</summary>
    /// <param name="context">失败上下文信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包含问题、原因、建议的教训结果</returns>
    Task<LessonResult> GenerateLessonAsync(string context, CancellationToken ct = default);
}
