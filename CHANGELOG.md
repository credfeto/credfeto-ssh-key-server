# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--
Please ADD ALL Changes to the UNRELEASED SECTION and not a specific release
-->

## [Unreleased]
### Security
- Removed private key (server.pfx) from repository — cert path and passphrase are now configurable via Https:CertificatePath and Https:CertificatePassword in appsettings.json; a dev-cert generation script is provided for local setup
### Added
- SSH key server with file system storage for per-host/user authorized public key management
- Signed challenge-response verification required for adding and deleting SSH keys
- Restriction to ed25519 and sk-ssh-ed25519 key types only
- Script (tools/generate-dev-cert.sh) to generate a self-signed TLS certificate for localhost and trust it on Arch Linux and Debian/Ubuntu-based systems
- Unhandled exceptions are now caught, logged with the request method/path, and returned as a JSON error body instead of an empty response with no diagnostic information
### Fixed
- Corrected broken cross-reference in github-workflows.instructions.md — anchor #visual-indicators updated to #output-helpers to match actual section name in shell-scripts.instructions.md
- shell.firewall.examples.md open_port_for_private_networks no longer calls firewall-cmd --reload internally; added explicit caller-reload rule to shell.firewall.instructions.md
- Fix data loss in FileSystemKeyDataStore when the key file is corrupt or unreadable (#16)
- Reject malformed base64 SSH key data with 400 Bad Request instead of an unhandled 500 error (#17)
- Avoid eagerly allocating a SemaphoreSlim on every AddKey/RemoveKey call in FileSystemKeyDataStore (#19)
- Challenge:HmacSecret is now validated at startup (non-empty, valid base64) instead of silently failing every challenge-related request with an empty 500 response
- Challenge and key responses now serialize as camelCase JSON, matching the documented API contract instead of the C# property casing
- Challenge:HmacSecret and Keys:BasePath are now set via plain settable properties instead of init/required, so the Native AOT configuration binding source generator can actually assign them; previously it silently skipped both, leaving the app running with an empty secret and an empty base path in production regardless of what was configured
### Changed
- die() must output to stderr so error messages are not swallowed by stdout pipelines
- SDK - Updated DotNet SDK to 10.0.302
- Corrected docs/api.md to describe the challenge-response authentication flow
### Deprecated
### Removed
### Deployment Changes
<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->
## [0.0.0] - Project created