# ──────────────────────────────────────
# CCTaskScoring API - 多阶段构建
# ──────────────────────────────────────

# ── Stage 1: Build ──
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 先复制 csproj 利用 Docker 层缓存
COPY CCTaskScoring.sln .
COPY CCTaskScoring.Api/CCTaskScoring.Api.csproj CCTaskScoring.Api/
COPY CCTaskScoring.Core/CCTaskScoring.Core.csproj CCTaskScoring.Core/
COPY CCTaskScoring.Infrastructure/CCTaskScoring.Infrastructure.csproj CCTaskScoring.Infrastructure/
RUN dotnet restore CCTaskScoring.sln

# 复制源码（不包含测试项目，减小构建上下文）
COPY CCTaskScoring.Api/ CCTaskScoring.Api/
COPY CCTaskScoring.Core/ CCTaskScoring.Core/
COPY CCTaskScoring.Infrastructure/ CCTaskScoring.Infrastructure/

# 发布 Release 版本
RUN dotnet publish CCTaskScoring.Api/CCTaskScoring.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 创建数据和日志目录
RUN mkdir -p /app/data /app/logs

# 从构建阶段复制发布输出
COPY --from=build /app/publish .

# 端口和环境配置
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CCTaskScoring.Api.dll"]
