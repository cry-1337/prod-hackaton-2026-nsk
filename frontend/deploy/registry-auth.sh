#!/bin/sh
set -eu
umask 077

job_dir=$1
: "${SPIRIT_DOCKER_SA_KEY:?Set SPIRIT_DOCKER_SA_KEY}"

curl -fsSL --retry 3 https://spirit.t-education.com/cli/tedu/14.1.3/linux/dp -o "$job_dir/dp"
printf '%s  %s\n' f740fd005d7f51723e0d85bcd7582a35dd8d24de51da522bfbbfd3dcb7333cb9 "$job_dir/dp" | sha256sum -c -
chmod 700 "$job_dir/dp"
"$job_dir/dp" auth service-acc --key-file "$SPIRIT_DOCKER_SA_KEY"
"$job_dir/dp" auth configure-kaniko --config-path "$job_dir/config.json"
