#!/bin/sh
set -eu
umask 077

registry_config=$(mktemp -d "$PWD/.deploy.XXXXXX")
trap 'rm -rf "$registry_config"' EXIT
cat > "$registry_config/config.json"

if [ ! -e .env ]; then
    postgres_password=$(openssl rand -hex 32)
    jwt_signing_key=$(openssl rand -base64 32)
    printf 'POSTGRES_PASSWORD=%s\nJWT_SIGNING_KEY=%s\n' "$postgres_password" "$jwt_signing_key" > "$registry_config/app.env"
    mv "$registry_config/app.env" .env
fi

if ! grep -q '^MINIO_ROOT_PASSWORD=.' .env; then
    printf '\nMINIO_ROOT_PASSWORD=%s\n' "$(openssl rand -hex 32)" >> .env
fi

if ! grep -q '^GRAFANA_ADMIN_PASSWORD=.' .env; then
    grafana_password=$(openssl rand -hex 32)
    printf '\nGRAFANA_ADMIN_PASSWORD=%s\n' "$grafana_password" >> .env
fi

mkdir -p .secrets/sage
chmod 700 .secrets
chmod 711 .secrets/sage
chmod -R a+rX infra/monitoring

set -- docker --config "$registry_config" compose \
    --project-name "${COMPOSE_PROJECT_NAME:-vmeste-backend}" \
    --env-file .env --env-file pending.env \
    -f compose.yaml -f compose.production.yaml

if ! docker info >/dev/null 2>&1; then
    set -- sudo -n "$@"
fi

"$@" pull api prometheus grafana
"$@" build minio minio-init
"$@" up -d --no-build --wait --wait-timeout 120
mv pending.env release.env
