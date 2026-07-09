#!/bin/sh
# Generates a self-signed TLS certificate for localhost development.
# Trusts the certificate on Arch Linux and Debian/Ubuntu-based systems.
# Usage: generate-dev-cert.sh [output-path]
#   output-path defaults to server.pfx in the repository root.

die() {
    if [ -t 2 ]; then
        printf '\n\033[31m✗\033[0m %s\n' "$*" >&2
    else
        printf '\n✗ %s\n' "$*" >&2
    fi
    exit 1
}

success() {
    if [ -t 1 ]; then
        printf '\n\033[32m✓\033[0m %s\n' "$*"
    else
        printf '\n✓ %s\n' "$*"
    fi
}

info() {
    if [ -t 1 ]; then
        printf '\n\033[32m→\033[0m %s\n' "$*"
    else
        printf '\n→ %s\n' "$*"
    fi
}

# Returns true (0) when running inside a Claude Code Bash-tool session.
# Claude Code sets CLAUDECODE=1 in every shell it spawns via the Bash tool;
# that value is inherited by subprocesses (e.g. git hooks).
# Source: https://docs.anthropic.com/en/docs/claude-code/settings#environment-variables
is_ai_agent() {
    [ "${CLAUDECODE}" = "1" ]
}

if is_ai_agent; then
    die "This script must not be run by an AI agent"
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
OUT="${1:-${REPO_ROOT}/server.pfx}"

command -v openssl > /dev/null 2>&1 || die "openssl is required but not found in PATH"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "${WORK_DIR}"' 0

info "Generating private key..."
openssl genrsa -out "${WORK_DIR}/key.pem" 4096 2>/dev/null || die "Failed to generate private key"

info "Generating self-signed certificate for localhost..."
openssl req -new -x509 \
    -key "${WORK_DIR}/key.pem" \
    -out "${WORK_DIR}/cert.pem" \
    -days 365 \
    -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1,IP:::1" \
    2>/dev/null || die "Failed to generate certificate"

info "Exporting to PKCS#12 format..."
openssl pkcs12 -export \
    -out "${OUT}" \
    -inkey "${WORK_DIR}/key.pem" \
    -in "${WORK_DIR}/cert.pem" \
    -passout pass: \
    2>/dev/null || die "Failed to export PKCS#12 certificate"

success "Certificate written to: ${OUT}"

if [ -f /etc/arch-release ]; then
    info "Trusting certificate on Arch Linux..."
    if command -v trust > /dev/null 2>&1; then
        trust anchor --store "${WORK_DIR}/cert.pem" || die "Failed to trust certificate — try running with sudo"
        success "Certificate trusted on Arch Linux"
    else
        info "p11-kit not found — install p11-kit or trust the certificate manually"
    fi
elif [ -f /etc/debian_version ]; then
    info "Trusting certificate on Debian/Ubuntu..."
    sudo cp "${WORK_DIR}/cert.pem" /usr/local/share/ca-certificates/localhost-dev.crt || die "Failed to copy certificate"
    sudo update-ca-certificates || die "Failed to update CA certificates"
    success "Certificate trusted on Debian/Ubuntu"
else
    info "Automatic trust not supported on this OS — trust the certificate manually"
fi

success "Done. Add the following to appsettings-local.json to enable HTTPS:"
printf '{\n  "Https": {\n    "CertificatePath": "%s",\n    "CertificatePassword": ""\n  }\n}\n' "${OUT}"
