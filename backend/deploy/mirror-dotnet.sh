#!/bin/sh
set -eu
umask 077

mirror_dir=$1
report=$2
repository=${IMAGE_NAME%:*}
auth_file="$mirror_dir/config.json"

mirror_image() {
    name=$1
    upstream="docker://mcr.microsoft.com/dotnet/$name"
    destination="docker://$repository:dotnet-$name-10.0"
    manifest="$mirror_dir/$name.manifest"

    if ! GODEBUG=http2client=0 skopeo inspect --retry-times 2 --raw "$upstream:10.0-noble-amd64" > "$manifest"; then
        printf 'Microsoft Registry unavailable, using the existing mirror for %s\n' "$name"
        skopeo inspect --authfile "$auth_file" --raw "$destination" > "$manifest"
    fi

    digest=$(skopeo manifest-digest "$manifest")
    printf '%s@%s\n' "$repository" "$digest" > "$mirror_dir/$name.ref"

    if skopeo inspect --authfile "$auth_file" --raw "$destination" > "$mirror_dir/$name.current" 2>/dev/null \
        && [ "$(skopeo manifest-digest "$mirror_dir/$name.current")" = "$digest" ]; then
        printf 'Mirror already contains %s@%s\n' "$name" "$digest"
        return
    fi

    printf 'Downloading dotnet/%s@%s\n' "$name" "$digest"
    GODEBUG=http2client=0 skopeo copy --retry-times 3 --preserve-digests \
        "$upstream@$digest" "dir:$mirror_dir/$name"

    printf 'Publishing %s to %s\n' "$name" "$repository"
    skopeo copy --retry-times 3 --preserve-digests --dest-authfile "$auth_file" \
        "dir:$mirror_dir/$name" "$destination"
}

mirror_image sdk
mirror_image aspnet

printf 'KANIKO_ARGS=--build-arg SDK_IMAGE=%s --build-arg ASPNET_IMAGE=%s\n' \
    "$(cat "$mirror_dir/sdk.ref")" "$(cat "$mirror_dir/aspnet.ref")" > "$report"
