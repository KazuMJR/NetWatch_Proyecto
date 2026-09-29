#!/usr/bin/env bash
set -euo pipefail

if [[ $EUID -ne 0 ]]; then
  echo "Ejecute: sudo $0 NOMBRE_INTERFAZ" >&2
  exit 1
fi

if [[ $# -ne 1 ]]; then
  echo "Uso: sudo $0 NOMBRE_INTERFAZ" >&2
  echo "Identifique la interfaz host-only con: ip -br address" >&2
  exit 1
fi

interface=$1
connection=netwatch-hostonly

if ! command -v nmcli >/dev/null 2>&1; then
  echo "NetworkManager/nmcli no está instalado. Este script está preparado para Xubuntu Desktop." >&2
  exit 1
fi

if ! ip link show "$interface" >/dev/null 2>&1; then
  echo "La interfaz '$interface' no existe." >&2
  exit 1
fi

if nmcli -t -f NAME connection show | grep -Fxq "$connection"; then
  nmcli connection modify "$connection" \
    connection.interface-name "$interface" \
    ipv4.method manual \
    ipv4.addresses 192.168.56.20/24 \
    ipv4.gateway "" \
    ipv4.dns "" \
    ipv4.never-default yes \
    ipv6.method disabled
else
  nmcli connection add \
    type ethernet \
    ifname "$interface" \
    con-name "$connection" \
    ipv4.method manual \
    ipv4.addresses 192.168.56.20/24 \
    ipv4.never-default yes \
    ipv6.method disabled
fi

nmcli connection up "$connection"
echo "Interfaz '$interface' configurada con 192.168.56.20/24 sin gateway ni DNS."
ip -br address show "$interface"

