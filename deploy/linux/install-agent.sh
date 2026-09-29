#!/usr/bin/env bash
set -euo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Ejecute con sudo." >&2
  exit 1
fi
if [[ $# -ne 2 ]]; then
  echo "Uso: sudo ./install-agent.sh /ruta/netwatch-agent /ruta/agent.json" >&2
  exit 1
fi

binary=$(realpath "$1")
configuration=$(realpath "$2")
id -u netwatch >/dev/null 2>&1 || useradd --system --home-dir /nonexistent --shell /usr/sbin/nologin netwatch
install -d -o root -g root -m 0755 /opt/netwatch-agent
install -d -o root -g netwatch -m 0750 /etc/netwatch
install -o root -g root -m 0755 "$binary" /opt/netwatch-agent/netwatch-agent
install -o root -g netwatch -m 0640 "$configuration" /etc/netwatch/agent.json
install -o root -g root -m 0644 "$(dirname "$0")/netwatch-agent.service" /etc/systemd/system/netwatch-agent.service
systemctl daemon-reload
systemctl enable --now netwatch-agent.service
systemctl --no-pager status netwatch-agent.service
