using CCTaskScoring.Core.Interfaces;
using CCTaskScoring.Core.Models;
using CCTaskScoring.Infrastructure.Rewards;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CCTaskScoring.Tests.Unit;

/// <summary>奖惩服务单元测试</summary>
public class RewardServiceTests
{
    // ── DetermineActions 静态方法测试 ──

    [Theory]
    [InlineData(95.0)]
    [InlineData(90.0)]
    [InlineData(100.0)]
    public void DetermineActions_ExcellentScore_ReturnsPositiveActions(double score)
    {
        var actions = RewardService.DetermineActions(score).ToList();

        actions.Should().HaveCount(3);
        actions.Should().Contain(a => a.ActionType == "INCREASE_PRIORITY");
        actions.Should().Contain(a => a.ActionType == "INCREASE_QUOTA");
        actions.Should().Contain(a => a.ActionType == "MARK_TRUSTED");
    }

    [Theory]
    [InlineData(89.0)]
    [InlineData(75.0)]
    public void DetermineActions_GoodScore_ReturnsNoActions(double score)
    {
        var actions = RewardService.DetermineActions(score).ToList();
        actions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(74.0)]
    [InlineData(60.0)]
    public void DetermineActions_PassScore_ReturnsNoActions(double score)
    {
        var actions = RewardService.DetermineActions(score).ToList();
        actions.Should().BeEmpty();
    }

    [Theory]
    [InlineData(59.0)]
    [InlineData(40.0)]
    public void DetermineActions_NeedsImprovementScore_ReturnsWarning(double score)
    {
        var actions = RewardService.DetermineActions(score).ToList();

        actions.Should().HaveCount(1);
        actions.Should().Contain(a => a.ActionType == "SYSTEM_WARNING");
    }

    [Theory]
    [InlineData(39.0)]
    [InlineData(0.0)]
    [InlineData(10.0)]
    public void DetermineActions_FailScore_ReturnsCriticalActions(double score)
    {
        var actions = RewardService.DetermineActions(score).ToList();

        actions.Should().HaveCount(3);
        actions.Should().Contain(a => a.ActionType == "FORCE_REVIEW");
        actions.Should().Contain(a => a.ActionType == "RESTRICT_OPERATIONS");
        actions.Should().Contain(a => a.ActionType == "ADD_TO_LESSON_LIBRARY");
    }

    // ── ApplyAsync 集成测试（Mock 仓储） ──

    [Fact]
    public async Task ApplyAsync_HighScore_SavesRewardRecords()
    {
        var mockRepo = new Mock<IRewardRepository>();
        var mockLogger = new Mock<ILogger<RewardService>>();
        var service = new RewardService(mockRepo.Object, mockLogger.Object);

        var result = await service.ApplyAsync("task-123", 95.0);

        result.Should().HaveCount(3);
        mockRepo.Verify(r => r.AddAsync(It.IsAny<RewardPunishment>()), Times.Exactly(3));
        mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_PassScore_SavesNoRecords()
    {
        var mockRepo = new Mock<IRewardRepository>();
        var mockLogger = new Mock<ILogger<RewardService>>();
        var service = new RewardService(mockRepo.Object, mockLogger.Object);

        var result = await service.ApplyAsync("task-123", 80.0);

        result.Should().BeEmpty();
        mockRepo.Verify(r => r.AddAsync(It.IsAny<RewardPunishment>()), Times.Never);
        mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_WarningScore_SavesOneRecord()
    {
        var mockRepo = new Mock<IRewardRepository>();
        var mockLogger = new Mock<ILogger<RewardService>>();
        var service = new RewardService(mockRepo.Object, mockLogger.Object);

        var result = await service.ApplyAsync("task-456", 45.0);

        result.Should().HaveCount(1);
        mockRepo.Verify(r => r.AddAsync(It.Is<RewardPunishment>(
            rp => rp.ActionType == "SYSTEM_WARNING" && rp.TaskId == "task-456")),
            Times.Once);
        mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_FailScore_SavesCriticalRecords()
    {
        var mockRepo = new Mock<IRewardRepository>();
        var mockLogger = new Mock<ILogger<RewardService>>();
        var service = new RewardService(mockRepo.Object, mockLogger.Object);

        var result = await service.ApplyAsync("task-789", 30.0);

        result.Should().HaveCount(3);
        mockRepo.Verify(r => r.AddAsync(It.Is<RewardPunishment>(
            rp => rp.ActionType == "FORCE_REVIEW")), Times.Once);
        mockRepo.Verify(r => r.AddAsync(It.Is<RewardPunishment>(
            rp => rp.ActionType == "RESTRICT_OPERATIONS")), Times.Once);
        mockRepo.Verify(r => r.AddAsync(It.Is<RewardPunishment>(
            rp => rp.ActionType == "ADD_TO_LESSON_LIBRARY")), Times.Once);
        mockRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_RecordHasCorrectTaskId()
    {
        var mockRepo = new Mock<IRewardRepository>();
        var mockLogger = new Mock<ILogger<RewardService>>();
        var service = new RewardService(mockRepo.Object, mockLogger.Object);
        var capturedRecords = new List<RewardPunishment>();

        mockRepo.Setup(r => r.AddAsync(It.IsAny<RewardPunishment>()))
                .Callback<RewardPunishment>(rp => capturedRecords.Add(rp));

        await service.ApplyAsync("my-task-id", 95.0);

        capturedRecords.Should().OnlyContain(r => r.TaskId == "my-task-id");
        capturedRecords.Should().OnlyContain(r => r.AppliedAt <= DateTime.UtcNow);
        capturedRecords.Should().OnlyContain(r => r.Expiry.HasValue);
    }
}
