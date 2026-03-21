using System.Text.Json;
using System.Text.Json.Serialization;

namespace CCTaskScoring.Api.Infrastructure;

/// <summary>
/// 确保 DateTime 序列化时带 Z 后缀（UTC），避免前端 new Date() 时区偏差 +8h
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dt = reader.GetDateTime();
        // 反序列化时统一标记为 UTC
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // 输出带 Z 后缀的 ISO 8601 UTC 字符串，如：2024-01-01T10:00:00Z
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }
}
