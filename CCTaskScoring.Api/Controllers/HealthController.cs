using Microsoft.AspNetCore.Mvc;

namespace CCTaskScoring.Api.Controllers;

/// <summary>
/// 健康检查控制器
/// </summary>
[ApiController]
public class HealthController : ControllerBase
{
    /// <summary>健康检查端点</summary>
    /// <returns>服务状态</returns>
    [HttpGet("/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() =>
        Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}
