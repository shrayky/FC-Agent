#!/usr/bin/env bash
# Готовит на Linux каталоги, в которые агент пишет логи и конфигурацию.
set -euo pipefail

for dir in /var/log/fc /var/lib/fc; do
    sudo mkdir -p "$dir"
    sudo chown -R "$(id -u):$(id -g)" "$dir"
done
