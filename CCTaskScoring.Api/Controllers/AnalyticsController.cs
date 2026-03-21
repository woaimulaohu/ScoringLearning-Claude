using CCTaskScoring.Core.Interfaces;
using CCTaskScoring.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CCTaskScoring.Api.Controllers;

/// <summary>
/// 数据分析控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly INeo4jService _neo4jService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        AppDbContext dbContext,
        INeo4jService neo4jService,
        ILogger<AnalyticsController> logger)
    {
        _dbContext = dbContext;
        _neo4jService = neo4jService;
        _logger = logger;
    }

    /// <summary>获取任务统计摘要</summary>
    /// <returns>总任务数、待审查数、平均分</returns>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        var total = await _dbContext.Tasks.CountAsync();
        var needsReview = await _dbContext.Tasks.CountAsync(t => t.Status == "needs_review");
        var averageScore = await _dbContext.Scores
            .Where(s => s.TotalScore > 0)
            .Select(s => (double?)s.TotalScore)
            .AverageAsync() ?? 0.0;

        return Ok(new
        {
            total,
            needsReview,
            averageScore = Math.Round(averageScore, 2)
        });
    }

    /// <summary>获取高频错误模式</summary>
    /// <param name="limit">最大返回数量</param>
    /// <returns>错误模式列表</returns>
    [HttpGet("error-patterns")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetErrorPatterns([FromQuery] int limit = 20)
    {
        try
        {
            // Neo4j 降级处理：不可用时返回空列表
            if (_neo4jService == null || !await _neo4jService.IsAvailableAsync())
            {
                _logger.LogWarning("Neo4j unavailable, returning empty error patterns");
                return Ok(Array.Empty<ErrorPatternDto>());
            }

            var patterns = await _neo4jService.GetTopErrorPatternsAsync(limit);
            return Ok(patterns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch error patterns from Neo4j, returning empty list");
            return Ok(Array.Empty<ErrorPatternDto>());
        }
    }
}
