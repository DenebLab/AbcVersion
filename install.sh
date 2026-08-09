#!/bin/sh
# Installer for the abcversion native binary.
#
#   curl -sSL https://raw.githubusercontent.com/deneblab/abcversion/production/install.sh | sh
#
# Environment:
#   ABCVERSION_VERSION      version to install, e.g. 1.2.15 (default: latest release)
#   ABCVERSION_INSTALL_DIR  where to put the binary (default: $HOME/.local/bin)
#   ABCVERSION_BASE_URL     release base URL (default: GitHub releases; override for mirrors/tests)
#
# The whole script lives in main(), called on the very last line, so that a download
# truncated midway cannot execute a partial program.

set -eu

REPO_URL="https://github.com/deneblab/abcversion"

log() { printf '%s\n' "$*"; }
err() { printf 'install.sh: %s\n' "$*" >&2; }

die() {
    err "$*"
    exit 1
}

# Native AOT links against glibc. On musl (Alpine) the binary downloads happily and then
# fails to exec with a confusing loader error, so refuse up front with a useful message.
detect_libc() {
    if [ -n "$(find /lib /usr/lib -maxdepth 1 -name 'ld-musl-*' -print -quit 2>/dev/null)" ]; then
        echo musl
        return
    fi
    if command -v ldd >/dev/null 2>&1 && ldd --version 2>&1 | grep -qi musl; then
        echo musl
        return
    fi
    echo gnu
}

detect_rid() {
    os=$(uname -s)
    arch=$(uname -m)

    case "$os" in
        Linux)
            [ "$(detect_libc)" = musl ] && die \
"musl-based systems (Alpine) are not supported: the binary is linked against glibc.
Use a glibc image such as debian-slim, or 'dotnet tool install --global Deneblab.AbcVersionCmd'."
            case "$arch" in
                x86_64 | amd64) echo linux-x64 ;;
                *) die "unsupported Linux architecture '$arch'. Only x86_64 is published. See $REPO_URL/releases" ;;
            esac
            ;;
        Darwin)
            case "$arch" in
                arm64 | aarch64) echo osx-arm64 ;;
                x86_64)
                    die \
"Intel macOS (osx-x64) is not published - only Apple Silicon (osx-arm64).
Use 'dotnet tool install --global Deneblab.AbcVersionCmd' instead."
                    ;;
                *) die "unsupported macOS architecture '$arch'" ;;
            esac
            ;;
        *)
            die "unsupported operating system '$os'. Supported: Linux x86_64, macOS arm64.
For Windows use install.ps1; see $REPO_URL"
            ;;
    esac
}

fetch() {
    # fetch <url> <destination>; must fail on HTTP errors rather than saving an error page.
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL "$1" -o "$2"
    elif command -v wget >/dev/null 2>&1; then
        wget -q "$1" -O "$2"
    else
        die "neither curl nor wget is available"
    fi
}

verify_checksum() {
    # verify_checksum <directory> <filename>; the .sha256 records a bare filename,
    # so verification runs from inside that directory.
    dir=$1
    name=$2
    if command -v sha256sum >/dev/null 2>&1; then
        (cd "$dir" && sha256sum -c "$name.sha256" >/dev/null 2>&1)
    elif command -v shasum >/dev/null 2>&1; then
        (cd "$dir" && shasum -a 256 -c "$name.sha256" >/dev/null 2>&1)
    else
        die "no sha256 tool found (need sha256sum or shasum); cannot verify the download"
    fi
}

main() {
    version=${ABCVERSION_VERSION:-}
    install_dir=${ABCVERSION_INSTALL_DIR:-"$HOME/.local/bin"}
    base_url=${ABCVERSION_BASE_URL:-"$REPO_URL/releases"}

    rid=$(detect_rid)
    asset="abcversion-$rid"

    if [ -n "$version" ]; then
        version=${version#v}
        url="$base_url/download/v$version/$asset"
        label="v$version"
    else
        url="$base_url/latest/download/$asset"
        label="the latest release"
    fi

    log "Installing abcversion ($rid) from $label"

    tmp=$(mktemp -d)
    # shellcheck disable=SC2064  # $tmp must expand now, not when the trap fires
    trap "rm -rf '$tmp'" EXIT INT TERM

    fetch "$url" "$tmp/$asset" ||
        die "download failed: $url
If you pinned a version, check it exists at $REPO_URL/releases"

    fetch "$url.sha256" "$tmp/$asset.sha256" ||
        die "no checksum published for this release ($url.sha256).
Releases before checksums were introduced cannot be verified; install manually if you accept that."

    verify_checksum "$tmp" "$asset" ||
        die "checksum mismatch - the download is corrupt or has been tampered with. Nothing was installed."

    chmod +x "$tmp/$asset"

    mkdir -p "$install_dir" || die "cannot create '$install_dir'. Set ABCVERSION_INSTALL_DIR to a writable path."
    [ -w "$install_dir" ] || die "'$install_dir' is not writable. Set ABCVERSION_INSTALL_DIR to a writable path."

    # Move into place only after verification, so a failure never leaves a half-installed binary.
    mv -f "$tmp/$asset" "$install_dir/abcversion"

    log "Installed $("$install_dir/abcversion" --version 2>/dev/null || echo 'abcversion') to $install_dir/abcversion"

    case ":$PATH:" in
        *":$install_dir:"*) ;;
        *)
            log ""
            log "$install_dir is not on your PATH. Add it with:"
            log "    export PATH=\"$install_dir:\$PATH\""
            ;;
    esac
}

main "$@"
