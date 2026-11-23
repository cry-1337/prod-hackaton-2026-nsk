#!/bin/sh
set -eu
umask 077

: "${DEPLOY_VM:?Set DEPLOY_VM}" "${SSH_PRIVATE_KEY:?Set SSH_PRIVATE_KEY}"
: "${IMAGE_NAME:?Set IMAGE_NAME}"

job_dir=$(mktemp -d)
trap 'rm -rf "$job_dir"' EXIT

identity_file="$job_dir/identity"
if [ -f "$SSH_PRIVATE_KEY" ]; then
    awk '{ sub(/\r$/, ""); print }' < "$SSH_PRIVATE_KEY" > "$identity_file"
else
    printf '%s\n' "$SSH_PRIVATE_KEY" | tr -d '\r' > "$identity_file"
fi
unset SSH_PRIVATE_KEY

if ! ssh-keygen -y -P '' -f "$identity_file" >/dev/null 2>&1; then
    printf '%s\n' 'SSH_PRIVATE_KEY must contain a valid deployment key or point to its file.' >&2
    exit 1
fi

sh deploy/registry-auth.sh "$job_dir"

cat > "$job_dir/ssh.conf" <<EOF
Host deploy-vm
    HostName $DEPLOY_VM
    User ${DEPLOY_SSH_USER:-ubuntu}
    IdentityFile "$identity_file"
    UserKnownHostsFile "${SSH_KNOWN_HOSTS:-$PWD/deploy/known_hosts}"
    StrictHostKeyChecking yes
    BatchMode yes
    IdentitiesOnly yes
EOF

monitoring_files=$(sha256sum infra/monitoring/prometheus/*.yml infra/monitoring/prometheus/start.sh infra/monitoring/grafana/*.yml infra/monitoring/grafana/dashboards/*.json)
monitoring_hash=$(printf '%s\n' "$monitoring_files" | sha256sum)
caddy_hash=$(sha256sum deploy/compose.production.yaml)
printf 'IMAGE_NAME=%s\nAPP_PORT=%s\nMONITORING_CONFIG_HASH=%s\nCADDY_CONFIG_HASH=%s\n' \
    "$IMAGE_NAME" "${APP_PORT:-8080}" "${monitoring_hash%% *}" "${caddy_hash%% *}" > "$job_dir/pending.env"

ssh -F "$job_dir/ssh.conf" deploy-vm 'umask 077; mkdir -p vmeste-backend/infra'
if [ -n "${SAGE_API_KEY:-}" ] && [ "${SAGE_ENABLED:-true}" = true ]; then
    if [ -f "$SAGE_API_KEY" ]; then
        sage_token=$(sed 's/\r$//' < "$SAGE_API_KEY")
    else
        sage_token=$SAGE_API_KEY
    fi
    unset SAGE_API_KEY
    case "$sage_token" in
        ''|*[!A-Za-z0-9._~+/=-]*) echo 'SAGE_API_KEY must contain one bearer token.' >&2; exit 1 ;;
    esac
    printf '%s' "$sage_token" > "$job_dir/sage-key"
    unset sage_token
    ssh -F "$job_dir/ssh.conf" deploy-vm 'set -eu
        cd vmeste-backend
        umask 077
        mkdir -p .secrets/sage
        chmod 700 .secrets
        chmod 711 .secrets/sage
        tmp=$(mktemp .secrets/pending.XXXXXXXX)
        trap "rm -f -- $tmp" EXIT
        cat > "$tmp"
        if ! sudo -n cmp -s "$tmp" .secrets/sage/api-key; then
            chmod 400 "$tmp"
            sudo -n chown 65534:65534 "$tmp"
            mv "$tmp" .secrets/sage/api-key
        fi' < "$job_dir/sage-key"
    printf 'SAGE_ENABLED=true\nSAGE_API_KEY_FILE=/run/vmeste-sage/api-key\n' >> "$job_dir/pending.env"
elif [ "${SAGE_ENABLED:-}" = false ]; then
    printf 'SAGE_ENABLED=false\n' >> "$job_dir/pending.env"
fi

scp -F "$job_dir/ssh.conf" compose.yaml deploy/compose.production.yaml deploy/rollout.sh "$job_dir/pending.env" deploy-vm:vmeste-backend/
scp -r -F "$job_dir/ssh.conf" infra/minio infra/monitoring deploy-vm:vmeste-backend/infra/
ssh -F "$job_dir/ssh.conf" deploy-vm 'cd vmeste-backend && sh rollout.sh' < "$job_dir/config.json"
