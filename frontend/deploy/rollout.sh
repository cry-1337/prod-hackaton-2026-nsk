#!/bin/sh
set -eu
umask 077

registry_config=$(mktemp -d)
trap 'rm -rf "$registry_config"' EXIT
cat > "$registry_config/config.json"

set -- docker --config "$registry_config" compose --project-name vmeste-frontend -f docker-compose.yml
if ! docker info >/dev/null 2>&1; then
    set -- sudo -n "$@"
fi

"$@" --env-file pending.env pull frontend
if "$@" --env-file pending.env up -d --no-build --wait --wait-timeout 75; then
    mv pending.env release.env
else
    if [ -f release.env ]; then
        "$@" --env-file release.env up -d --no-build --wait --wait-timeout 75
    fi
    exit 1
fi
