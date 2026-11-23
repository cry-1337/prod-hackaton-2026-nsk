FROM alpine:3.22 AS server
ARG TARGETARCH
ARG MINIO_RELEASE=RELEASE.2025-09-07T16-13-09Z
RUN set -eu; \
    asset="minio.linux-${TARGETARCH}.${MINIO_RELEASE}"; \
    base="https://github.com/minio/minio/releases/download/${MINIO_RELEASE}"; \
    cd /tmp; \
    wget -q "${base}/${asset}"; \
    wget -q "${base}/${asset}.sha256sum"; \
    checksum="$(cut -d ' ' -f 1 "${asset}.sha256sum")"; \
    printf '%s  %s\n' "$checksum" "$asset" | sha256sum -c -; \
    mv "${asset}" /usr/local/bin/minio; \
    chmod +x /usr/local/bin/minio; \
    rm "${asset}.sha256sum"
ENTRYPOINT ["minio"]

FROM alpine:3.22 AS init
ARG TARGETARCH
ARG MC_RELEASE=RELEASE.2025-08-13T08-35-41Z
RUN set -eu; \
    asset="mc.linux-${TARGETARCH}.${MC_RELEASE}"; \
    base="https://github.com/minio/mc/releases/download/${MC_RELEASE}"; \
    cd /tmp; \
    wget -q "${base}/${asset}"; \
    wget -q "${base}/${asset}.sha256sum"; \
    checksum="$(cut -d ' ' -f 1 "${asset}.sha256sum")"; \
    printf '%s  %s\n' "$checksum" "$asset" | sha256sum -c -; \
    mv "${asset}" /usr/local/bin/mc; \
    chmod +x /usr/local/bin/mc; \
    rm "${asset}.sha256sum"
COPY init.sh public-products.json /seed/
COPY products/ /seed/products/
ENTRYPOINT ["/bin/sh", "/seed/init.sh"]
