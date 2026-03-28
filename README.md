# 语言切换 | Language Switch
[中文](./README-zh.md) | [English](./README.md)

# 🤖 ClaudeCode Task Scoring & Learning System

A scoring and learning system for ClaudeCode. Encapsulates scoring capabilities as an **MCP server**, enabling ClaudeCode to submit tasks, query scores, and retrieve historical lessons via the standard MCP protocol, while providing a Vue 3 manual review interface.

---

## ✨ Feature Overview

- 🔧 **MCP Tools**: ClaudeCode can directly call tools such as `submit_task`, `get_task`, `get_lessons`
- 📊 **5-Dimensional Automatic Scoring**: Completeness (30%) + Correctness (30%) + Quality (20%) + Efficiency (10%) + UX (10%)
- 🧠 **Knowledge Graph**: Neo4j stores error patterns and lessons learned, supporting language-based retrieval
- 🔍 **Manual Review**: Vue 3 frontend with support for score correction and lesson management
- 📦 **Single-Process Deployment**: API + MCP Server + static frontend files are uniformly hosted by ASP.NET Core, exposed via a single port

---

## 🛠️ Technology Stack

| Layer | Technology |
|-------|------------|
| Backend | C# 12 / .NET 8 / ASP.NET Core 8 |
| MCP | ModelContextProtocol.AspNetCore 1.1.0 |
| Primary Database | SQLite (EF Core, for tasks/scores/rewards) |
| Graph Database | Neo4j 5.x (lessons learned knowledge graph) |
| Frontend | Vue 3 + TypeScript + Vite + Element Plus |
| Logging | Serilog (Console + Rolling File) |
| HTTP Resilience | Polly (Exponential Backoff Retry) |

---

## 📁 Project Structure

```
CCTaskScoring&LearningSystem/
├── CCTaskScoring.Api/          # Web Layer: Controllers, MCP Tools, Background Services
│   ├── Controllers/            # REST API Controllers
│   ├── Mcp/                    # MCP Tool Definitions (ScoringMcpTools)
│   ├── Services/               # ScoringBackgroundService (Background Scoring Queue)
│   └── wwwroot/                # Vue 3 Build Artifacts (Hosted by ASP.NET Core)
├── CCTaskScoring.Core/         # Domain Layer: Models, Interfaces, DTOs
├── CCTaskScoring.Infrastructure/ # Infrastructure Layer: EF Core, Neo4j, Scoring Engine
│   ├── Data/                   # AppDbContext, Repository Implementations
│   ├── Neo4j/                  # Neo4jService
│   ├── Scoring/                # ScoringEngine (5-Dimensional Scoring Logic)
│   └── Rewards/                # RewardService (Reward/Penalty Mechanism)
├── CCTaskScoring.Tests/        # Unit Tests + Integration Tests
├── frontend/                   # Vue 3 Frontend Source Code
├── neo4j/init/                 # Neo4j Initialization Cypher Scripts
├── docker-compose.yml
├── Dockerfile
├── deploy-docker.sh            # One-Click Docker Deployment Script
├── deploy-native.sh            # Native Deployment Script
└── .env.example                # Environment Variable Template
```

---

## 🚀 Quick Start

### 🐳 Option 1: Docker Deployment (Recommended)

**Prerequisites**: Docker Engine 20.10+, Docker Compose v2

```bash
# 1. Clone the project
git clone <repo-url>
cd CCTaskScoring&LearningSystem

# 2. Copy and configure environment variables
cp .env.example .env
# Edit .env to fill in Neo4j password and Anthropic API Key (optional)

# 3. One-click deployment
bash deploy-docker.sh
```

After deployment:

| Service | Address |
|---------|---------|
| 🌐 Frontend / API | http://localhost:8080 |
| 🔌 MCP Endpoint | http://localhost:8080/mcp |
| 📖 Swagger UI | http://localhost:8080/swagger |
| 🗄️ Neo4j Browser | http://localhost:7474 |

Update the service:
```bash
bash deploy-docker.sh --update
```

Stop the service:
```bash
bash deploy-docker.sh --down
```

---

### 💻 Option 2: Local Deployment

**Prerequisites**: .NET 8 SDK, Node.js 20+, Neo4j (optional)

```bash
# 1. Start the backend
cd CCTaskScoring.Api
dotnet run

# 2. Start the frontend development server (new terminal)
cd frontend
npm install
npm run dev
```

The backend listens on `http://localhost:5176` by default, and the frontend development server proxies API requests.

---

## ⚙️ Environment Variables

Copy `.env.example` to `.env` and modify as needed:

```env
# ASP.NET Core
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080

# SQLite Database Path
ConnectionStrings__Default=Data Source=data/scoring.db

# Neo4j (optional, lessons feature degrades to empty list if not configured)
NEO4J_URI=bolt://neo4j:7687
NEO4J_USER=neo4j
NEO4J_PASSWORD=your_password

# Anthropic API (optional, required for AI-assisted scoring)
MCP_API_KEY=sk-ant-your-api-key-here
MCP_ENDPOINT=https://api.anthropic.com
```

> 🔒 **Security Note**: `.env` is included in `.gitignore` - do not commit files containing real secrets to version control.

---

## 🔌 MCP Integration

Add the following configuration to Claude Code's `.mcp.json` to enable ClaudeCode to discover and call the scoring tools:

```json
{
  "mcpServers": {
    "scoring": {
      "type": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

### 🧰 Available MCP Tools

| Tool Name | Description |
|-----------|-------------|
| `submit_task` | 📥 Submit a task for automatic scoring (asynchronous processing) |
| `get_task` | 🔎 Query task details and scoring results |
| `list_tasks` | 📋 List tasks with pagination, supporting status filtering |
| `review_task` | ✏️ Manually correct 5-dimensional scores |
| `get_analytics_summary` | 📈 Get system statistics summary (total count, average score, etc.) |
| `get_error_patterns` | ⚠️ Get high-frequency error patterns (from Neo4j) |
| `get_lessons` | 💡 Get historical lessons learned (filtered by language) |

**Example: Submit a Task**

```
submit_task(
  taskId="uuid-xxx",
  description="Write a quicksort function",
  language="Python",
  code="def quicksort(arr): ...",
  passed=5,
  failed=0,
  lintScore=9.0,
  durationSec=120
)
```

---

## 📡 REST API

All endpoints are prefixed with `/api/v1`. The complete documentation can be viewed via Swagger UI (`/swagger`) in the development environment.

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/tasks` | 📥 Submit a task |
| `GET` | `/api/v1/tasks` | 📋 Get task list (pagination) |
| `GET` | `/api/v1/tasks/{id}` | 🔎 Get task details |
| `PUT` | `/api/v1/tasks/{id}/review` | ✏️ Manual review to correct scores |
| `GET` | `/api/v1/tasks/{id}/lessons` | 💡 Get lessons associated with a task |
| `POST` | `/api/v1/tasks/{id}/lessons` | ➕ Create lessons for a task |
| `GET` | `/api/v1/analytics/summary` | 📈 Get statistics summary |
| `GET` | `/health` | 💚 Health check |

---

## 📊 Scoring Rules

Task scoring is divided into five dimensions, with the total score calculated by weighted average:

| Dimension | Weight | Description |
|-----------|--------|-------------|
| ✅ Completeness | 30% | Whether all task requirements are implemented |
| 🎯 Correctness | 30% | Test pass rate, logical correctness |
| 🧹 Code Quality | 20% | Lint score, code standards compliance |
| ⚡ Efficiency | 10% | Execution time, number of attempts |
| 🎨 User Experience | 10% | Log clarity, documentation completeness |

**🏆 Reward/Penalty Mechanism**:

| Score Range | Status | Action |
|-------------|--------|--------|
| 90–100 | 🌟 Excellent | Increase task priority |
| 75–89 | 👍 Good | None |
| 60–74 | 🆗 Pass | None |
| 40–59 | ⚠️ Needs Improvement | System warning, recorded in logs |
| 0–39 | ❌ Failed | Mandatory manual review, written to lessons database |

---

## 🧪 Run Tests

```bash
dotnet test
```

Test projects are located in `CCTaskScoring.Tests/`, including unit tests (scoring engine, repositories, MCP client) and integration tests (API endpoints).

---

## 📝 Logging

Runtime logs are written to `logs/app-{date}.log`, with rolling retention for 30 days. In Docker deployment, the log directory is mounted to the host's `./logs`.

```bash
# Docker real-time logs
docker compose logs -f scoreservice
```

## 🎉 Demo Screenshots

<p align="center">
  <img src="frontend/public/演示1.png" width="80%" alt="Demo 1">
  <img src="frontend/public/演示2.png" width="80%" alt="Demo 2">
  <img src="frontend/public/演示3.png" width="80%" alt="Demo 3">
  <img src="frontend/public/演示4.png" width="80%" alt="Demo 4">
    <img src="frontend/public/演示4.png" width="80%" alt="Demo 5">
</p>
