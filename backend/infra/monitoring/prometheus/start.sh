#!/bin/sh
set -eu

config=/etc/prometheus/prometheus.yml
if [ "${SAGE_ENABLED:-false}" = true ]; then
    umask 077
    config_dir=$(mktemp -d)
    cp "$config" /etc/prometheus/alerts.yml "$config_dir/"
    if [ -n "${SAGE_API_KEY_FILE:-}" ]; then
        if [ ! -r "$SAGE_API_KEY_FILE" ] || [ ! -s "$SAGE_API_KEY_FILE" ]; then
            echo 'Cannot read Sage token file.' >&2
            exit 1
        fi
        ln -s "$SAGE_API_KEY_FILE" "$config_dir/sage-api-key"
    else
        : "${SAGE_API_KEY:?Set SAGE_API_KEY to enable Sage Remote Write}"
        printf '%s' "$SAGE_API_KEY" > "$config_dir/sage-api-key"
    fi
    unset SAGE_API_KEY

    escape_replacement() {
        printf '%s' "$1" | sed -e "s/'/''/g" -e 's/[&|\]/\&/g'
    }

    config="$config_dir/prometheus.yml"
    sed \
        -e "s|__SAGE_REMOTE_WRITE_URL__|$(escape_replacement "$SAGE_REMOTE_WRITE_URL")|g" \
        -e "s|__SAGE_GROUP__|$(escape_replacement "$SAGE_GROUP")|g" \
        -e "s|__SAGE_SYSTEM__|$(escape_replacement "$SAGE_SYSTEM")|g" \
        -e "s|__SAGE_ENVIRONMENT__|$(escape_replacement "$SAGE_ENVIRONMENT")|g" \
        -e "s|__SAGE_DATACENTER__|$(escape_replacement "$SAGE_DATACENTER")|g" \
        /etc/prometheus/prometheus-sage.yml >> "$config"
fi

exec /bin/prometheus --config.file="$config" --storage.tsdb.path=/prometheus
