#!/bin/sh
set -eu

attempt=0
until mc alias set local http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 30 ]; then
        exit 1
    fi
    sleep 2
done

mc mb --ignore-existing local/catalog
mc anonymous set-json /seed/public-products.json local/catalog
for image in /seed/products/*.webp; do
    mc cp --attr 'Content-Type=image/webp' "$image" "local/catalog/products/${image##*/}"
done
