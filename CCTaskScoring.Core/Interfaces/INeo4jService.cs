namespace CCTaskScoring.Core.Interfaces;

/// <summary>经验教训 DTO</summary>
public record LessonDto(
    string Id,
    string Problem,
    string Cause,
    string Suggestion,
    DateTime CreatedAt);

/// <summary>错误模式 DTO</summary>
public record ErrorPatternDto(
    string Name,
    int Frequency,
    string? Description,
    string? Severity);

/// <summary>
/// Neo4j 图数据库服务接口
/// </summary>
public interface INeo4jService
{
    /// <summary>检查 Neo4j 是否可用</summary>
    /// <returns>是否可连接</returns>
    Task<bool> IsAvailableAsync();

    /// <summary>创建或更新任务节点</summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="description">任务描述</param>
    /// <param name="language">编程语言</param>
    Task CreateOrMergeTaskNodeAsync(string taskId, string description, string? language);

    /// <summary>创建或合并错误模式节点</summary>
    /// <param name="errorName">错误名称</param>
    /// <param name="description">错误描述</param>
    /// <param name="severity">严重程度</param>
    Task CreateOrMergeErrorPatternAsync(string errorName, string? description, string? severity);

    /// <summary>创建教训节点并建立关系</summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="errorName">错误名称</param>
    /// <param name="problem">问题描述</param>
    /// <param name="cause">根本原因</param>
    /// <param name="suggestion">改进建议</param>
    /// <returns>教训节点 ID</returns>
    Task<string> CreateLessonAsync(
        string taskId, string errorName,
        string problem, string cause, string suggestion);

    /// <summary>检索相关教训</summary>
    /// <param name="language">编程语言过滤</param>
    /// <param name="taskType">任务类型过滤</param>
    /// <param name="limit">最大返回数</param>
    /// <returns>教训列表</returns>
    Task<IEnumerable<LessonDto>> GetRelatedLessonsAsync(
        string? language, string? taskType, int limit = 5);

    /// <summary>获取高频错误模式</summary>
    /// <param name="limit">最大返回数</param>
    /// <returns>错误模式列表</returns>
    Task<IEnumerable<ErrorPatternDto>> GetTopErrorPatternsAsync(int limit = 20);

    /// <summary>为节点创建标签</summary>
    /// <param name="nodeName">节点名称</param>
    /// <param name="nodeType">节点类型（Task / ErrorPattern / Lesson）</param>
    /// <param name="tagName">标签名称</param>
    Task CreateTagAsync(string nodeName, string nodeType, string tagName);
}
