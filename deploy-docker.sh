#!/usr/bin/env bash
# ============================================================
# CCTaskScoring & LearningSystem — Docker 部署脚本
# 依赖：Docker Engine 20.10+、Docker Compose v2
# 用法：
#   bash deploy-docker.sh           # 完整部署
#   bash deploy-docker.sh --update  # 仅重新构建并重启服务
#   bash deploy-docker.sh --down    # 停止并删除容器
# ============================================================
set -euo pipefail

# ── 颜色输出 ──────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; NC='\033[0m'
info()  { echo -e "${GREEN}[INFO]${NC}  $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error() { echo -e "${RED}[ERROR]${NC} $*" >&2; exit 1; }

# ── 检查依赖 ──────────────────────────────────────────────
check_deps() {
  command -v docker   &>/dev/null || error "Docker 未安装，请先安装 Docker Engine"
  command -v docker compose &>/dev/null || \
  docker compose version &>/dev/null   || error "Docker Compose v2 未安装"
  info "Docker 版本: $(docker --version)"
  info "Compose 版本: $(docker compose version)"
}

# ── 初始化 .env ───────────────────────────────────────────
init_env() {
  if [[ ! -f .env ]]; then
    info "生成默认 .env 文件..."
    cat > .env <<'EOF'
# Neo4j 认证（格式：用户名/密码）
NEO4J_AUTH=neo4j/password
NEO4J_PASSWORD=password
NEO4J_URI=bolt://neo4j:7687

# 运行环境
ASPNETCORE_ENVIRONMENT=Production

# Anthropic API（可选，AI 评分功能需要）
MCP_API_KEY=
MCP_ENDPOINT=https://api.anthropic.com
EOF
    warn ".env 已生成，请根据实际情况修改后重新运行"
  fi
  # 导出环境变量
  set -a; source .env; set +a
}

# ── 创建必要目录 ──────────────────────────────────────────
init_dirs() {
  mkdir -p data logs neo4j/{data,logs,init}
  info "数据目录已初始化"
}

# ── 构建前端 ─────────────────────────────────────────────
build_frontend() {
  if [[ ! -d frontend ]]; then
    warn "未找到 frontend 目录，跳过前端构建"
    return
  fi
  info "构建前端..."
  docker run --rm \
    -v "$(pwd)/frontend:/app" \
    -w /app \
    node:20-alpine \
    sh -c "npm ci && npm run build"
  info "前端构建完成 → frontend/dist"
}

# ── 主流程 ────────────────────────────────────────────────
main() {
  local action="${1:-deploy}"

  case "$action" in
    --down)
      info "停止并删除容器..."
      docker compose down
      info "完成"
      exit 0
      ;;
    --update)
      check_deps
      init_env
      build_frontend
      info "重新构建并重启服务..."
      docker compose build scoreservice
      docker compose up -d scoreservice
      ;;
    *)
      check_deps
      init_env
      init_dirs
      build_frontend
      info "启动所有服务..."
      docker compose up -d --build
      ;;
  esac

  # 等待健康检查
  info "等待服务就绪（最多 120s）..."
  local elapsed=0
  until docker compose ps --format json | grep -q '"Health":"healthy"' || [[ $elapsed -ge 120 ]]; do
    sleep 5; elapsed=$((elapsed+5))
    echo -n "."
  done
  echo

  # 输出状态
  docker compose ps
  echo
  info "✅ 部署完成"
  info "   API:         http://$(hostname -I | awk '{print $1}'):8080"
  info "   Neo4j Browser: http://$(hostname -I | awk '{print $1}'):7474"
  info "   日志: docker compose logs -f"
}

main "${1:-}"
