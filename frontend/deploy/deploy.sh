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

printf 'IMAGE_NAME=%s\n' "$IMAGE_NAME" > "$job_dir/pending.env"

ssh -F "$job_dir/ssh.conf" deploy-vm 'umask 077; mkdir -p vmeste-frontend'
scp -F "$job_dir/ssh.conf" deploy/docker-compose.yml deploy/rollout.sh "$job_dir/pending.env" deploy-vm:vmeste-frontend/
ssh -F "$job_dir/ssh.conf" deploy-vm 'cd vmeste-frontend && sh rollout.sh' < "$job_dir/config.json"
