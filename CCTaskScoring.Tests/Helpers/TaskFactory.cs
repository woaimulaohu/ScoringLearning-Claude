using CCTaskScoring.Core.DTOs;
using CCTaskScoring.Core.Models;

namespace CCTaskScoring.Tests.Helpers;

/// <summary>测试数据工厂</summary>
public static class TestDataFactory
{
    /// <summary>创建测试用 TaskEntity</summary>
    /// <param name="id">任务 ID（默认随机 GUID）</param>
    /// <param name="status">任务状态（默认 pending）</param>
    /// <param name="language">编程语言（默认 Python）</param>
    /// <returns>TaskEntity 实例</returns>
    public static TaskEntity CreateTask(
        string? id = null,
        string status = "pending",
        string language = "Python") => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Description = "编写一个计算斐波那契数列的函数",
        Language = language,
        CreatedAt = DateTime.UtcNow,
        Status = status,
        Artifacts = """{"code":"def fib(n):\n    if n<=1: return n\n    return fib(n-1)+fib(n-2)","tests":"def test_fib(): assert fib(5)==5"}""",
        Logs = "INFO: Task started\nINFO: Generating code\nINFO: Task completed",
        TestResults = """{"passed":5,"failed":0,"skipped":0}""",
        StaticAnalysis = """{"lintScore":8.5,"issues":[]}""",
        Metadata = """{"language":"Python","durationSec":90,"attempts":1}"""
    };

    /// <summary>创建测试用 TaskSubmitRequest</summary>
    /// <param name="taskId">任务 ID（默认随机 GUID）</param>
    /// <param name="passed">通过的测试数</param>
    /// <param name="failed">失败的测试数</param>
    /// <param name="lintScore">Lint 评分</param>
    /// <param name="durationSec">持续时间（秒）</param>
    /// <param name="attempts">尝试次数</param>
    /// <returns>TaskSubmitRequest 实例</returns>
    public static TaskSubmitRequest CreateSubmitRequest(
        string? taskId = null,
        int passed = 5,
        int failed = 0,
        double lintScore = 8.5,
        int durationSec = 90,
        int attempts = 1) => new()
    {
        TaskId = taskId ?? Guid.NewGuid().ToString(),
        Description = "编写一个计算斐波那契数列的函数",
        Artifacts = new ArtifactsDto
        {
            Code = "def fib(n):\n    if n<=1: return n\n    return fib(n-1)+fib(n-2)",
            Tests = "def test_fib(): assert fib(5)==5"
        },
        Logs = "INFO: Task started\nINFO: Generating code\nINFO: Task completed",
        TestResults = new TestResultsDto { Passed = passed, Failed = failed, Skipped = 0 },
        StaticAnalysis = new StaticAnalysisDto { LintScore = lintScore },
        Metadata = new TaskMetadataDto { Language = "Python", DurationSec = durationSec, Attempts = attempts }
    };

    /// <summary>创建测试用 ScoreEntity</summary>
    /// <param name="taskId">任务 ID</param>
    /// <param name="total">总分</param>
    /// <returns>ScoreEntity 实例</returns>
    public static ScoreEntity CreateScore(string taskId, double total = 85.0) => new()
    {
        TaskId = taskId,
        CompletionScore = 80,
        CorrectnessScore = 100,
        QualityScore = 85,
        EfficiencyScore = 80,
        UxScore = 80,
        TotalScore = total,
        AutoScored = true,
        CreatedAt = DateTime.UtcNow
    };
}
