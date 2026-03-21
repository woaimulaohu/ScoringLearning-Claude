using CCTaskScoring.Core.DTOs;
using CCTaskScoring.Core.Interfaces;
using CCTaskScoring.Infrastructure.Scoring;
using CCTaskScoring.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CCTaskScoring.Tests.Unit;

/// <summary>评分引擎单元测试</summary>
public class ScoringEngineTests
{
    private readonly Mock<IMcpClient> _mockMcp;
    private readonly Mock<ILogger<ScoringEngine>> _mockLogger;
    private readonly ScoringEngine _engine;

    public ScoringEngineTests()
    {
        _mockMcp = new Mock<IMcpClient>();
        _mockLogger = new Mock<ILogger<ScoringEngine>>();
        _mockMcp.Setup(m => m.ExtractRequirementsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<string>());
        _engine = new ScoringEngine(_mockMcp.Object, _mockLogger.Object);
    }

    // ── 正确性评分 ──

    [Fact]
    public void CalculateCorrectnessScore_AllPassed_Returns100()
    {
        var result = ScoringEngine.CalculateCorrectnessScore(
            new TestResultsDto { Passed = 10, Failed = 0, Skipped = 0 });
        result.Should().Be(100.0);
    }

    [Fact]
    public void CalculateCorrectnessScore_HalfPassed_Returns50()
    {
        var result = ScoringEngine.CalculateCorrectnessScore(
            new TestResultsDto { Passed = 5, Failed = 5, Skipped = 0 });
        result.Should().Be(50.0);
    }

    [Fact]
    public void CalculateCorrectnessScore_NoTests_ReturnsDefault60()
    {
        var result = ScoringEngine.CalculateCorrectnessScore(null);
        result.Should().Be(60.0);
    }

    [Fact]
    public void CalculateCorrectnessScore_ZeroTotal_ReturnsDefault60()
    {
        var result = ScoringEngine.CalculateCorrectnessScore(
            new TestResultsDto { Passed = 0, Failed = 0, Skipped = 0 });
        result.Should().Be(60.0);
    }

    [Theory]
    [InlineData(7, 3, 0, 70.0)]
    [InlineData(1, 9, 0, 10.0)]
    [InlineData(8, 1, 1, 80.0)]
    public void CalculateCorrectnessScore_VariousPassRates_ReturnsCorrect(
        int passed, int failed, int skipped, double expected)
    {
        var result = ScoringEngine.CalculateCorrectnessScore(
            new TestResultsDto { Passed = passed, Failed = failed, Skipped = skipped });
        result.Should().Be(expected);
    }

    // ── 质量评分 ──

    [Theory]
    [InlineData(8.5, 85.0)]
    [InlineData(10.0, 100.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(5.0, 50.0)]
    public void CalculateQualityScore_LintScore_ReturnsCorrect(double lintScore, double expected)
    {
        var result = ScoringEngine.CalculateQualityScore(
            new StaticAnalysisDto { LintScore = lintScore });
        result.Should().Be(expected);
    }

    [Fact]
    public void CalculateQualityScore_NoStaticAnalysis_ReturnsDefault60()
    {
        var result = ScoringEngine.CalculateQualityScore(null);
        result.Should().Be(60.0);
    }

    [Fact]
    public void CalculateQualityScore_LintScoreAbove10_ClampedTo100()
    {
        var result = ScoringEngine.CalculateQualityScore(
            new StaticAnalysisDto { LintScore = 15.0 });
        result.Should().Be(100.0);
    }

    [Fact]
    public void CalculateQualityScore_NegativeLintScore_ClampedTo0()
    {
        var result = ScoringEngine.CalculateQualityScore(
            new StaticAnalysisDto { LintScore = -5.0 });
        result.Should().Be(0.0);
    }

    // ── 效率评分 ──

    [Fact]
    public void CalculateEfficiencyScore_BaselineDurationAndAttempts_Returns80()
    {
        var result = ScoringEngine.CalculateEfficiencyScore(120, 1);
        result.Should().Be(80.0);
    }

    [Fact]
    public void CalculateEfficiencyScore_LongDuration_ReturnsLowerScore()
    {
        // 480s = 120(基准) + 360(超出), 360/60*5 = 30分罚分
        // 80 - 30 = 50
        var result = ScoringEngine.CalculateEfficiencyScore(480, 1);
        result.Should().Be(50.0);
    }

    [Fact]
    public void CalculateEfficiencyScore_MultipleAttempts_ReturnsLowerScore()
    {
        // 3次尝试, (3-1)*10 = 20分罚分
        // 80 - 0(时长罚分) - 20 = 60
        var result = ScoringEngine.CalculateEfficiencyScore(120, 3);
        result.Should().Be(60.0);
    }

    [Fact]
    public void CalculateEfficiencyScore_ExtremeValues_NotNegative()
    {
        var result = ScoringEngine.CalculateEfficiencyScore(99999, 100);
        result.Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public void CalculateEfficiencyScore_NullValues_ReturnsBaseline()
    {
        var result = ScoringEngine.CalculateEfficiencyScore(null, null);
        result.Should().Be(80.0);
    }

    [Fact]
    public void CalculateEfficiencyScore_ShortDuration_Returns80()
    {
        // 低于基准时间不加分，仍然是80
        var result = ScoringEngine.CalculateEfficiencyScore(60, 1);
        result.Should().Be(80.0);
    }

    // ── UX 评分 ──

    [Fact]
    public void CalculateUxScore_WithLogsAndArtifacts_ReturnsHighScore()
    {
        // 基础50 + 日志内容20 + 结构化日志10 + 测试代码10 + 代码10 = 100
        var result = ScoringEngine.CalculateUxScore(
            "INFO: started\nINFO: completed",
            new ArtifactsDto { Code = "def foo(): pass", Tests = "def test_foo(): pass" });
        result.Should().Be(100.0);
    }

    [Fact]
    public void CalculateUxScore_NoLogsNoArtifacts_ReturnsBaseline50()
    {
        var result = ScoringEngine.CalculateUxScore(null, null);
        result.Should().Be(50.0);
    }

    [Fact]
    public void CalculateUxScore_OnlyLogs_Returns80()
    {
        // 基础50 + 日志内容20 + 结构化日志10 = 80
        var result = ScoringEngine.CalculateUxScore(
            "INFO: Task started\nINFO: Task completed", null);
        result.Should().Be(80.0);
    }

    [Fact]
    public void CalculateUxScore_ShortLogs_Returns50()
    {
        // 日志长度 <= 10，不加分
        var result = ScoringEngine.CalculateUxScore("short", null);
        result.Should().Be(50.0);
    }

    [Fact]
    public void CalculateUxScore_OnlyCode_Returns60()
    {
        // 基础50 + 代码10 = 60
        var result = ScoringEngine.CalculateUxScore(
            null, new ArtifactsDto { Code = "def foo(): pass" });
        result.Should().Be(60.0);
    }

    [Fact]
    public void CalculateUxScore_OnlyTests_Returns60()
    {
        // 基础50 + 测试代码10 = 60
        var result = ScoringEngine.CalculateUxScore(
            null, new ArtifactsDto { Tests = "def test_foo(): pass" });
        result.Should().Be(60.0);
    }

    // ── 总分权重验证 ──

    [Fact]
    public async Task ScoreAsync_WeightedTotalIsCorrect()
    {
        var request = TestDataFactory.CreateSubmitRequest(
            passed: 10, lintScore: 10.0, durationSec: 120, attempts: 1);
        var result = await _engine.ScoreAsync(request);

        // 总分 = 完成度*0.3 + 正确性*0.3 + 质量*0.2 + 效率*0.1 + UX*0.1
        var expected = result.CompletionScore * 0.3
                     + result.CorrectnessScore * 0.3
                     + result.QualityScore * 0.2
                     + result.EfficiencyScore * 0.1
                     + result.UxScore * 0.1;
        result.TotalScore.Should().BeApproximately(Math.Round(expected, 2), 0.01);
    }

    [Fact]
    public async Task ScoreAsync_PerfectTask_ScoreIsHigh()
    {
        var request = TestDataFactory.CreateSubmitRequest(
            passed: 10, failed: 0, lintScore: 10.0, durationSec: 60, attempts: 1);
        var result = await _engine.ScoreAsync(request);
        result.TotalScore.Should().BeGreaterThan(75.0);
    }

    [Fact]
    public async Task ScoreAsync_FailedTask_ScoreIsLow()
    {
        var request = TestDataFactory.CreateSubmitRequest(
            passed: 0, failed: 10, lintScore: 2.0, durationSec: 600, attempts: 5);
        var result = await _engine.ScoreAsync(request);
        result.TotalScore.Should().BeLessThan(60.0);
    }

    [Fact]
    public async Task ScoreAsync_McpReturnsRequirements_CompletionScoreCalculated()
    {
        // MCP 返回3个需求，代码有3行 => completionRatio = min(1.0, 3/(3*5)) = 0.2
        // completionScore = 40 + 0.2 * 60 = 52
        _mockMcp.Setup(m => m.ExtractRequirementsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "需求1", "需求2", "需求3" });

        var request = TestDataFactory.CreateSubmitRequest();
        var result = await _engine.ScoreAsync(request);

        result.CompletionScore.Should().BeGreaterThanOrEqualTo(40.0);
        result.CompletionScore.Should().BeLessThanOrEqualTo(100.0);
    }

    [Fact]
    public async Task ScoreAsync_McpUnavailable_FallbackCompletionScore70()
    {
        // MCP 返回空数组 → 降级到 70 分
        _mockMcp.Setup(m => m.ExtractRequirementsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<string>());

        var request = TestDataFactory.CreateSubmitRequest();
        var result = await _engine.ScoreAsync(request);

        result.CompletionScore.Should().Be(70.0);
    }

    [Fact]
    public async Task ScoreAsync_McpThrowsException_FallbackCompletionScore70()
    {
        _mockMcp.Setup(m => m.ExtractRequirementsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("MCP unavailable"));

        var request = TestDataFactory.CreateSubmitRequest();
        var result = await _engine.ScoreAsync(request);

        result.CompletionScore.Should().Be(70.0);
    }

    [Fact]
    public async Task ScoreAsync_NullRequest_ThrowsArgumentNullException()
    {
        var act = async () => await _engine.ScoreAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ScoreAsync_NoDescription_CompletionScoreIs0()
    {
        var request = TestDataFactory.CreateSubmitRequest();
        request.Description = "";

        var result = await _engine.ScoreAsync(request);
        result.CompletionScore.Should().Be(0.0);
    }

    [Fact]
    public async Task ScoreAsync_NoCode_CompletionScoreIs30()
    {
        var request = TestDataFactory.CreateSubmitRequest();
        request.Artifacts = null;

        var result = await _engine.ScoreAsync(request);
        result.CompletionScore.Should().Be(30.0);
    }
}
