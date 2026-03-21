using CCTaskScoring.Infrastructure.Data;
using CCTaskScoring.Infrastructure.Data.Repositories;
using CCTaskScoring.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CCTaskScoring.Tests.Unit;

/// <summary>任务仓储单元测试（使用内存数据库）</summary>
public class TaskRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TaskRepository _repository;

    public TaskRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _repository = new TaskRepository(_context);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTask_ReturnsTask()
    {
        var task = TestDataFactory.CreateTask("test-id-1");
        await _repository.AddAsync(task);
        await _repository.SaveChangesAsync();

        var result = await _repository.GetByIdAsync("test-id-1");

        result.Should().NotBeNull();
        result!.Id.Should().Be("test-id-1");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingTask_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync("non-existing-id");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ExistingTask_ReturnsTrue()
    {
        var task = TestDataFactory.CreateTask("exists-id");
        await _repository.AddAsync(task);
        await _repository.SaveChangesAsync();

        var result = await _repository.ExistsAsync("exists-id");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingTask_ReturnsFalse()
    {
        var result = await _repository.ExistsAsync("not-exists");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_NoFilter_ReturnsAllTasks()
    {
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "pending"));
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "scored"));
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "needs_review"));
        await _repository.SaveChangesAsync();

        var allTasks = await _repository.GetAllAsync();
        allTasks.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_ReturnsFilteredTasks()
    {
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "pending"));
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "scored"));
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "needs_review"));
        await _repository.SaveChangesAsync();

        var pendingTasks = await _repository.GetAllAsync(status: "pending");
        pendingTasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_Pagination_ReturnsCorrectPage()
    {
        // 创建5个任务
        for (int i = 0; i < 5; i++)
        {
            await _repository.AddAsync(TestDataFactory.CreateTask());
        }
        await _repository.SaveChangesAsync();

        var page1 = await _repository.GetAllAsync(page: 1, pageSize: 2);
        var page2 = await _repository.GetAllAsync(page: 2, pageSize: 2);
        var page3 = await _repository.GetAllAsync(page: 3, pageSize: 2);

        page1.Should().HaveCount(2);
        page2.Should().HaveCount(2);
        page3.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCountAsync_NoFilter_ReturnsTotalCount()
    {
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "pending"));
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "scored"));
        await _repository.SaveChangesAsync();

        var count = await _repository.GetCountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task GetCountAsync_WithStatusFilter_ReturnsFilteredCount()
    {
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "pending"));
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "pending"));
        await _repository.AddAsync(TestDataFactory.CreateTask(status: "scored"));
        await _repository.SaveChangesAsync();

        var pendingCount = await _repository.GetCountAsync("pending");
        var scoredCount = await _repository.GetCountAsync("scored");

        pendingCount.Should().Be(2);
        scoredCount.Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_NewTask_CanBeRetrieved()
    {
        var task = TestDataFactory.CreateTask("new-task");
        await _repository.AddAsync(task);
        await _repository.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync("new-task");
        retrieved.Should().NotBeNull();
        retrieved!.Description.Should().Be(task.Description);
        retrieved.Language.Should().Be(task.Language);
        retrieved.Status.Should().Be(task.Status);
    }

    [Fact]
    public async Task UpdateAsync_ExistingTask_UpdatesFields()
    {
        var task = TestDataFactory.CreateTask("update-test");
        await _repository.AddAsync(task);
        await _repository.SaveChangesAsync();

        task.Status = "scored";
        task.CompletedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(task);
        await _repository.SaveChangesAsync();

        var updated = await _repository.GetByIdAsync("update-test");
        updated!.Status.Should().Be("scored");
        updated.CompletedAt.Should().NotBeNull();
    }

    public void Dispose() => _context.Dispose();
}
