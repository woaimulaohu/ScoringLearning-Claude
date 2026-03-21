using System.ComponentModel.DataAnnotations;

namespace CCTaskScoring.Core.DTOs;

/// <summary>
/// 任务提交请求 DTO
/// </summary>
public class TaskSubmitRequest
{
    /// <summary>任务唯一标识</summary>
    [Required]
    public string TaskId { get; set; } = string.Empty;

    /// <summary>提交时间戳</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>任务描述</summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>产物信息</summary>
    public ArtifactsDto? Artifacts { get; set; }

    /// <summary>日志信息</summary>
    public string? Logs { get; set; }

    /// <summary>测试结果</summary>
    public TestResultsDto? TestResults { get; set; }

    /// <summary>静态分析结果</summary>
    public StaticAnalysisDto? StaticAnalysis { get; set; }

    /// <summary>任务元数据</summary>
    public TaskMetadataDto? Metadata { get; set; }
}

/// <summary>
/// 产物 DTO
/// </summary>
public class ArtifactsDto
{
    /// <summary>代码内容</summary>
    public string? Code { get; set; }

    /// <summary>测试代码内容</summary>
    public string? Tests { get; set; }
}

/// <summary>
/// 测试结果 DTO
/// </summary>
public class TestResultsDto
{
    /// <summary>通过数</summary>
    public int Passed { get; set; }

    /// <summary>失败数</summary>
    public int Failed { get; set; }

    /// <summary>跳过数</summary>
    public int Skipped { get; set; }
}

/// <summary>
/// 静态分析 DTO
/// </summary>
public class StaticAnalysisDto
{
    /// <summary>Lint 评分</summary>
    public double LintScore { get; set; }

    /// <summary>问题列表</summary>
    public object[]? Issues { get; set; }
}

/// <summary>
/// 任务元数据 DTO
/// </summary>
public class TaskMetadataDto
{
    /// <summary>编程语言</summary>
    public string? Language { get; set; }

    /// <summary>持续时间（秒）</summary>
    public int DurationSec { get; set; }

    /// <summary>尝试次数</summary>
    public int Attempts { get; set; }
}
