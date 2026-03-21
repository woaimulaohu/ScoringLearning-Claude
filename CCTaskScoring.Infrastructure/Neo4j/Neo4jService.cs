using CCTaskScoring.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace CCTaskScoring.Infrastructure.Neo4j;

/// <summary>
/// Neo4j 图数据库服务
/// 管理任务节点、错误模式节点、教训节点及其之间的关系。
/// 使用 IDriver（线程安全）作为单例，Session 通过 using 确保及时释放。
/// </summary>
public class Neo4jService : INeo4jService, IDisposable
{
    private readonly IDriver _driver;
    private readonly ILogger<Neo4jService> _logger;
    private bool _disposed;

    /// <summary>初始化 Neo4j 服务</summary>
    /// <param name="configuration">应用配置（需包含 Neo4j:Uri, Neo4j:User, Neo4j:Password）</param>
    /// <param name="logger">日志记录器</param>
    public Neo4jService(IConfiguration configuration, ILogger<Neo4jService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var uri = configuration["Neo4j:Uri"] ?? "bolt://localhost:7687";
        var user = configuration["Neo4j:User"] ?? "neo4j";
        var password = configuration["Neo4j:Password"] ?? "password";

        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        _logger.LogInformation("Neo4j driver initialized for {Uri}", uri);
    }

    /// <summary>检查 Neo4j 连接是否可用</summary>
    /// <returns>是否可连接</returns>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _driver.VerifyConnectivityAsync().WaitAsync(cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Neo4j is not available");
            return false;
        }
    }

    /// <summary>创建或更新任务节点（MERGE 语义）</summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="description">任务描述</param>
    /// <param name="language">编程语言</param>
    public async Task CreateOrMergeTaskNodeAsync(string taskId, string description, string? language)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "MERGE (t:Task {id: $id}) " +
                "ON CREATE SET t.description = $desc, t.language = $lang, t.created_at = datetime() " +
                "ON MATCH SET t.description = $desc, t.language = $lang",
                new { id = taskId, desc = description, lang = language ?? "unknown" });
        });
        _logger.LogDebug("Task node created/merged: {TaskId}", taskId);
    }

    /// <summary>创建或合并错误模式节点（累计频率）</summary>
    /// <param name="errorName">错误名称</param>
    /// <param name="description">错误描述</param>
    /// <param name="severity">严重程度</param>
    public async Task CreateOrMergeErrorPatternAsync(string errorName, string? description, string? severity)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "MERGE (e:ErrorPattern {name: $name}) " +
                "ON CREATE SET e.frequency = 1, e.description = $desc, e.severity = $sev, e.created_at = datetime() " +
                "ON MATCH SET e.frequency = e.frequency + 1",
                new { name = errorName, desc = description, sev = severity });
        });
        _logger.LogDebug("ErrorPattern node created/merged: {ErrorName}", errorName);
    }

    /// <summary>
    /// 创建教训节点并建立关系
    /// Task -[:HAS_ERROR]-> ErrorPattern -[:LEADS_TO_LESSON]-> Lesson
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="errorName">错误名称</param>
    /// <param name="problem">问题描述</param>
    /// <param name="cause">根本原因</param>
    /// <param name="suggestion">改进建议</param>
    /// <returns>教训节点 ID</returns>
    public async Task<string> CreateLessonAsync(
        string taskId, string errorName,
        string problem, string cause, string suggestion)
    {
        var lessonId = Guid.NewGuid().ToString();
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            // 创建或合并错误模式节点（累计频率）
            await tx.RunAsync(
                "MERGE (e:ErrorPattern {name: $name}) " +
                "ON CREATE SET e.frequency = 1, e.created_at = datetime() " +
                "ON MATCH SET e.frequency = e.frequency + 1",
                new { name = errorName });

            // 创建教训节点
            await tx.RunAsync(
                "CREATE (l:Lesson {id: $id, problem: $problem, cause: $cause, " +
                "suggestion: $suggestion, created_at: datetime()})",
                new { id = lessonId, problem, cause, suggestion });

            // 建立关系
            await tx.RunAsync(
                "MATCH (t:Task {id: $taskId}), (e:ErrorPattern {name: $errorName}), " +
                "(l:Lesson {id: $lessonId}) " +
                "CREATE (t)-[:HAS_ERROR]->(e) " +
                "CREATE (e)-[:LEADS_TO_LESSON]->(l)",
                new { taskId, errorName, lessonId });
        });

        _logger.LogInformation(
            "Lesson {LessonId} created for task {TaskId} with error {ErrorName}",
            lessonId, taskId, errorName);
        return lessonId;
    }

    /// <summary>检索与语言/类型相关的教训（最新优先）</summary>
    /// <param name="language">编程语言过滤</param>
    /// <param name="taskType">任务类型过滤</param>
    /// <param name="limit">最大返回数</param>
    /// <returns>教训列表</returns>
    public async Task<IEnumerable<LessonDto>> GetRelatedLessonsAsync(
        string? language, string? taskType, int limit = 5)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (e:ErrorPattern)<-[:HAS_ERROR]-(:Task) " +
                "WHERE ($lang IS NULL OR e.name CONTAINS $lang OR e.name CONTAINS $type) " +
                "MATCH (e)-[:LEADS_TO_LESSON]->(l:Lesson) " +
                "RETURN l.id AS id, l.problem AS problem, l.cause AS cause, " +
                "       l.suggestion AS suggestion, l.created_at AS createdAt " +
                "ORDER BY l.created_at DESC LIMIT $limit",
                new { lang = language, type = taskType ?? "", limit });
            return await cursor.ToListAsync();
        });

        return result.Select(r => new LessonDto(
            r["id"].As<string>(),
            r["problem"].As<string>(),
            r["cause"].As<string>(),
            r["suggestion"].As<string>(),
            r["createdAt"].As<DateTimeOffset>().DateTime
        ));
    }

    /// <summary>获取高频错误模式（按频率降序）</summary>
    /// <param name="limit">最大返回数</param>
    /// <returns>错误模式列表</returns>
    public async Task<IEnumerable<ErrorPatternDto>> GetTopErrorPatternsAsync(int limit = 20)
    {
        await using var session = _driver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (e:ErrorPattern) " +
                "RETURN e.name AS name, e.frequency AS frequency, " +
                "       e.description AS description, e.severity AS severity " +
                "ORDER BY e.frequency DESC LIMIT $limit",
                new { limit });
            return await cursor.ToListAsync();
        });

        return result.Select(r => new ErrorPatternDto(
            r["name"].As<string>(),
            r["frequency"].As<int>(),
            r["description"]?.As<string>(),
            r["severity"]?.As<string>()
        ));
    }

    /// <summary>为节点创建标签</summary>
    /// <param name="nodeName">节点名称</param>
    /// <param name="nodeType">节点类型（Task / ErrorPattern / Lesson）</param>
    /// <param name="tagName">标签名称</param>
    public async Task CreateTagAsync(string nodeName, string nodeType, string tagName)
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            // 创建 Tag 节点并建立 TAGGED_WITH 关系
            var identifierField = nodeType switch
            {
                "Task" => "id",
                "ErrorPattern" => "name",
                "Lesson" => "id",
                _ => "id"
            };

            await tx.RunAsync(
                $"MERGE (tag:Tag {{name: $tagName}}) " +
                $"WITH tag " +
                $"MATCH (n:{nodeType} {{{identifierField}: $nodeName}}) " +
                $"MERGE (n)-[:TAGGED_WITH]->(tag)",
                new { tagName, nodeName });
        });
        _logger.LogDebug("Tag '{TagName}' added to {NodeType} '{NodeName}'",
            tagName, nodeType, nodeName);
    }

    /// <summary>初始化 Neo4j 约束和索引</summary>
    /// <returns>异步任务</returns>
    public async Task InitializeSchemaAsync()
    {
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            // 唯一约束
            await tx.RunAsync("CREATE CONSTRAINT task_id IF NOT EXISTS FOR (t:Task) REQUIRE t.id IS UNIQUE");
            await tx.RunAsync("CREATE CONSTRAINT lesson_id IF NOT EXISTS FOR (l:Lesson) REQUIRE l.id IS UNIQUE");
            await tx.RunAsync("CREATE CONSTRAINT error_name IF NOT EXISTS FOR (e:ErrorPattern) REQUIRE e.name IS UNIQUE");
            await tx.RunAsync("CREATE CONSTRAINT tag_name IF NOT EXISTS FOR (tag:Tag) REQUIRE tag.name IS UNIQUE");

            // 索引
            await tx.RunAsync("CREATE INDEX task_language IF NOT EXISTS FOR (t:Task) ON (t.language)");
            await tx.RunAsync("CREATE INDEX error_frequency IF NOT EXISTS FOR (e:ErrorPattern) ON (e.frequency)");
            await tx.RunAsync("CREATE INDEX lesson_created IF NOT EXISTS FOR (l:Lesson) ON (l.created_at)");
        });
        _logger.LogInformation("Neo4j schema initialized (constraints + indexes)");
    }

    /// <summary>释放 Neo4j 驱动资源</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _driver?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
