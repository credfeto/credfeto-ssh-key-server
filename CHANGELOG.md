# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--
Please ADD ALL Changes to the UNRELEASED SECTION and not a specific release
-->

## [Unreleased]
### Security
### Added
- SSH key server with file system storage for per-host/user authorized public key management
- Signed challenge-response verification required for adding and deleting SSH keys
- Restriction to ed25519 and sk-ssh-ed25519 key types only
### Fixed
- Corrected broken cross-reference in github-workflows.instructions.md — anchor #visual-indicators updated to #output-helpers to match actual section name in shell-scripts.instructions.md
- shell.firewall.examples.md open_port_for_private_networks no longer calls firewall-cmd --reload internally; added explicit caller-reload rule to shell.firewall.instructions.md
### Changed
- die() must output to stderr so error messages are not swallowed by stdout pipelines
### Deprecated
### Removed
### Deployment Changes
<!--
Releases that have at least been deployed to staging, BUT NOT necessarily released to live.  Changes should be moved from [Unreleased] into here as they are merged into the appropriate release branch
-->
## [0.0.0] - Project created