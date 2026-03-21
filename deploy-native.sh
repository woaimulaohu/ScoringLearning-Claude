#!/usr/bin/env bash
# ============================================================
# CCTaskScoring & LearningSystem — 常规部署脚本（含环境搭建）
# 支持系统：Ubuntu 22.04 / Debian 12 / CentOS 8+
# 用法：
#   bash deploy-native.sh           # 完整部署（含环境安装）
#   bash deploy-native.sh --update  # 仅更新应用代码
#   bash deploy-native.sh --status  # 查看服务状态
# ============================================================
set -euo pipefail

# ── 颜色输出 ──────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'
info()    { echo -e "${GREEN}[INFO]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}    $*"; }
error()   { echo -e "${RED}[ERROR]${NC}   $*" >&2; exit 1; }
section() { echo -e "\n${BLUE}══════ $* ══════${NC}"; }

# ── 全局变量 ──────────────────────────────────────────────
APP_DIR="$(cd "$(dirname "$0")" && pwd)"   # 项目根目录
APP_USER="${APP_USER:-www-data}"            # 运行用户
API_PORT="${API_PORT:-8080}"               # API 端口
DOTNET_VERSION="8.0"
NODE_VERSION="20"
NEO4J_VERSION="5"
NEO4J_PASSWORD="${NEO4J_PASSWORD:-password}"

# ── 检测包管理器 ──────────────────────────────────────────
detect_pkg_manager() {
  if command -v apt-get &>/dev/null; then
    PKG_MGR="apt"
  elif command -v dnf &>/dev/null; then
    PKG_MGR="dnf"
  elif command -v yum &>/dev/null; then
    PKG_MGR="yum"
  else
    error "不支持的包管理器，请手动安装依赖"
  fi
  info "包管理器: $PKG_MGR"
}

pkg_install() {
  case "$PKG_MGR" in
    apt) apt-get install -y "$@" ;;
    dnf|yum) $PKG_MGR install -y "$@" ;;
  esac
}

pkg_update() {
  case "$PKG_MGR" in
    apt) apt-get update -qq ;;
    dnf|yum) $PKG_MGR check-update -q || true ;;
  esac
}

# ── 1. 安装 .NET 8 SDK ────────────────────────────────────
install_dotnet() {
  if command -v dotnet &>/dev/null && dotnet --list-sdks | grep -q "^8\."; then
    info ".NET 8 已安装: $(dotnet --version)"
    return
  fi

  section "安装 .NET $DOTNET_VERSION SDK"
  case "$PKG_MGR" in
    apt)
      wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb \
           -O /tmp/packages-microsoft-prod.deb
      dpkg -i /tmp/packages-microsoft-prod.deb
      pkg_update
      pkg_install dotnet-sdk-${DOTNET_VERSION}
      ;;
    dnf|yum)
      rpm --import https://packages.microsoft.com/keys/microsoft.asc
      $PKG_MGR install -y dotnet-sdk-${DOTNET_VERSION}
      ;;
  esac
  info ".NET 版本: $(dotnet --version)"
}

# ── 2. 安装 Node.js ───────────────────────────────────────
install_node() {
  if command -v node &>/dev/null && node --version | grep -q "^v${NODE_VERSION}"; then
    info "Node.js 已安装: $(node --version)"
    return
  fi

  section "安装 Node.js $NODE_VERSION"
  curl -fsSL https://deb.nodesource.com/setup_${NODE_VERSION}.x | bash -
  pkg_install nodejs
  info "Node.js 版本: $(node --version)"
  info "npm 版本: $(npm --version)"
}

# ── 3. 安装 Neo4j ─────────────────────────────────────────
install_neo4j() {
  if systemctl is-active --quiet neo4j 2>/dev/null; then
    info "Neo4j 已在运行"
    return
  fi

  section "安装 Neo4j $NEO4J_VERSION"
  case "$PKG_MGR" in
    apt)
      wget -O - https://debian.neo4j.com/neotechnology.gpg.key | \
        gpg --dearmor -o /usr/share/keyrings/neo4j.gpg
      echo "deb [signed-by=/usr/share/keyrings/neo4j.gpg] https://debian.neo4j.com stable ${NEO4J_VERSION}" \
        > /etc/apt/sources.list.d/neo4j.list
      pkg_update
      pkg_install neo4j
      ;;
    dnf|yum)
      rpm --import https://debian.neo4j.com/neotechnology.gpg.key
      cat > /etc/yum.repos.d/neo4j.repo <<EOF
[neo4j]
name=Neo4j RPM Repository
baseurl=https://yum.neo4j.com/stable/${NEO4J_VERSION}
enabled=1
gpgcheck=1
EOF
      pkg_install neo4j
      ;;
  esac

  # 设置密码
  neo4j-admin dbms set-initial-password "$NEO4J_PASSWORD" 2>/dev/null || true
  systemctl enable neo4j
  systemctl start neo4j

  # 等待启动
  info "等待 Neo4j 就绪..."
  local i=0
  until cypher-shell -u neo4j -p "$NEO4J_PASSWORD" "RETURN 1" &>/dev/null || [[ $i -ge 30 ]]; do
    sleep 2; i=$((i+2)); echo -n "."
  done
  echo
  info "Neo4j 已启动"
}

# ── 4. 安装 Nginx ─────────────────────────────────────────
install_nginx() {
  if command -v nginx &>/dev/null; then
    info "Nginx 已安装: $(nginx -v 2>&1)"
    return
  fi

  section "安装 Nginx"
  pkg_install nginx
  systemctl enable nginx
}

# ── 5. 构建后端 ───────────────────────────────────────────
build_backend() {
  section "构建 .NET 后端"
  cd "$APP_DIR"

  # 写入生产环境配置
  cat > CCTaskScoring.Api/appsettings.Production.json <<EOF
{
  "ConnectionStrings": {
    "Default": "Data Source=${APP_DIR}/data/scoring.db"
  },
  "Neo4j": {
    "Uri": "bolt://localhost:7687",
    "User": "neo4j",
    "Password": "${NEO4J_PASSWORD}"
  },
  "Mcp": {
    "Endpoint": "https://api.anthropic.com",
    "ApiKey": "${MCP_API_KEY:-}",
    "TimeoutSeconds": 30,
    "MaxRetries": 3
  },
  "AllowedHosts": "*"
}
EOF

  dotnet publish CCTaskScoring.Api/CCTaskScoring.Api.csproj \
    -c Release \
    -o "$APP_DIR/publish/api" \
    --nologo -v q

  mkdir -p "$APP_DIR/data" "$APP_DIR/logs"
  chown -R "$APP_USER:$APP_USER" "$APP_DIR/data" "$APP_DIR/logs" "$APP_DIR/publish" 2>/dev/null || true
  info "后端发布完成 → $APP_DIR/publish/api"
}

# ── 6. 构建前端 ───────────────────────────────────────────
build_frontend() {
  if [[ ! -d "$APP_DIR/frontend" ]]; then
    warn "未找到 frontend 目录，跳过前端构建"
    return
  fi

  section "构建 Vue 前端"
  cd "$APP_DIR/frontend"
  npm ci
  npm run build
  info "前端构建完成 → $APP_DIR/frontend/dist"
}

# ── 7. 配置 systemd 服务 ──────────────────────────────────
setup_systemd() {
  section "配置 systemd 服务"
  cat > /etc/systemd/system/cctaskscoring.service <<EOF
[Unit]
Description=CCTaskScoring API Service
After=network.target neo4j.service
Requires=neo4j.service

[Service]
Type=notify
User=${APP_USER}
WorkingDirectory=${APP_DIR}/publish/api
ExecStart=/usr/bin/dotnet CCTaskScoring.Api.dll
Restart=on-failure
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=cctaskscoring
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:${API_PORT}
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
EOF

  systemctl daemon-reload
  systemctl enable cctaskscoring
  systemctl restart cctaskscoring
  info "systemd 服务已启动"
}

# ── 8. 配置 Nginx ─────────────────────────────────────────
setup_nginx() {
  section "配置 Nginx 反向代理"
  local NGINX_CONF="/etc/nginx/sites-available/cctaskscoring"

  cat > "$NGINX_CONF" <<EOF
server {
    listen 80;
    server_name _;

    # 前端静态文件
    location / {
        root ${APP_DIR}/frontend/dist;
        index index.html;
        try_files \$uri \$uri/ /index.html;
    }

    # API 反向代理
    location /api/ {
        proxy_pass         http://127.0.0.1:${API_PORT};
        proxy_http_version 1.1;
        proxy_set_header   Host              \$host;
        proxy_set_header   X-Real-IP         \$remote_addr;
        proxy_set_header   X-Forwarded-For   \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_read_timeout 60s;
    }
}
EOF

  # 启用站点（Debian/Ubuntu）
  if [[ -d /etc/nginx/sites-enabled ]]; then
    ln -sf "$NGINX_CONF" /etc/nginx/sites-enabled/cctaskscoring
    rm -f /etc/nginx/sites-enabled/default 2>/dev/null || true
  else
    # CentOS/RHEL：直接放入 conf.d
    cp "$NGINX_CONF" /etc/nginx/conf.d/cctaskscoring.conf
  fi

  nginx -t && systemctl reload nginx
  info "Nginx 配置完成"
}

# ── 仅更新应用（不重新装环境）────────────────────────────
update_app() {
  info "更新应用代码..."
  build_backend
  build_frontend
  systemctl restart cctaskscoring
  nginx -t && systemctl reload nginx
  info "✅ 更新完成"
}

# ── 查看状态 ──────────────────────────────────────────────
show_status() {
  echo
  section "服务状态"
  for svc in neo4j cctaskscoring nginx; do
    local state
    state=$(systemctl is-active "$svc" 2>/dev/null || echo "not-found")
    printf "  %-20s %s\n" "$svc" "$state"
  done

  echo
  section "端口监听"
  ss -tlnp | grep -E ":($API_PORT|80|7474|7474)" || true
}

# ── 主流程 ────────────────────────────────────────────────
main() {
  [[ "$EUID" -ne 0 ]] && error "请使用 sudo 或 root 用户运行"

  local action="${1:-deploy}"

  case "$action" in
    --update)
      detect_pkg_manager
      update_app
      show_status
      exit 0
      ;;
    --status)
      show_status
      exit 0
      ;;
    deploy|*)
      detect_pkg_manager
      pkg_update

      # 安装基础工具
      pkg_install curl wget gnupg apt-transport-https ca-certificates lsb-release 2>/dev/null || true

      install_dotnet
      install_node
      install_neo4j
      install_nginx
      build_backend
      build_frontend
      setup_systemd
      setup_nginx
      ;;
  esac

  show_status

  local IP
  IP=$(hostname -I | awk '{print $1}')
  echo
  info "✅ 部署完成"
  info "   前端 + API:      http://${IP}"
  info "   API 直连:        http://${IP}:${API_PORT}"
  info "   Neo4j Browser:   http://${IP}:7474"
  info "   查看日志:        journalctl -u cctaskscoring -f"
}

main "${1:-deploy}"
