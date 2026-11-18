# credfeto-ssh-key-server

SSH key server built with .NET 10 and Kestrel. Stores and serves SSH public keys indexed by host and username, providing a public API compatible with `sshd`'s `AuthorizedKeysCommand`.

## Build Status

| Branch  | Status                                                                                                                                                                                                                                              |
|---------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| main    | [![Build: Pre-Release](https://github.com/credfeto/credfeto-ssh-key-server/actions/workflows/build-and-publish-pre-release.yml/badge.svg)](https://github.com/credfeto/credfeto-ssh-key-server/actions/workflows/build-and-publish-pre-release.yml) |
| release | [![Build: Release](https://github.com/credfeto/credfeto-ssh-key-server/actions/workflows/build-and-publish-release.yml/badge.svg)](https://github.com/credfeto/credfeto-ssh-key-server/actions/workflows/build-and-publish-release.yml)             |

## Documentation

- [API Reference](docs/api.md)
- [Configuration](docs/configuration.md)

## Container Self-Test

The server accepts a `--self-test` switch. When it is passed, the server:

- builds the web application and its dependency injection container without starting it, so no ports are bound and no external connections are opened;
- supplies placeholder values for required secrets (such as `Challenge:HmacSecret`), because none are available at image build time;
- loads its native dependencies by looking up a culture and computing a hash and an HMAC;
- exits with code `0` on success, or `1` if anything throws.

The `Dockerfile` runs it straight after the published output is copied in:

```dockerfile
RUN [ "/usr/src/app/Credfeto.Keys.Server", "--self-test" ]
```

The exec form is required because the chiseled base image has no shell. A binary that cannot run on the base image (for example, one built against a newer glibc than the image provides) then fails `docker build` instead of failing when the container starts. Keep this `RUN` line, before `ENTRYPOINT`, as the default for new services, and change the base image only to one that passes it.

## Admin CLI

The server binary also manages keys directly in the key store, without the HTTP challenge and signature flow. Use it to add the first key for a user, or to remove a key that has been lost.

```sh
Credfeto.Keys.Server list --host <host> --username <username>
Credfeto.Keys.Server add --host <host> --username <username> --key "<public key line>"
Credfeto.Keys.Server delete --host <host> --username <username> --key "<public key line>"
Credfeto.Keys.Server delete --host <host> --username <username> --fingerprint SHA256:<fingerprint>
```

- `--host` and `--username` are always required; there is no default host.
- `--key` takes a whole public key line, such as the contents of `~/.ssh/id_ed25519.pub`. Only `ssh-ed25519` and `sk-ssh-ed25519@openssh.com` keys are accepted.
- Adding a key that is already stored changes nothing and still succeeds.
- `delete --key` matches on the key type and key data only, so the comment does not need to match. Every stored copy of the key is removed.
- `delete --fingerprint` takes the SHA256 fingerprint printed by `ssh-keygen -lf`.

Each key is printed on its own line as `<fingerprint> <type> <comment>`, prefixed with `Added:`, `Deleted:` or `Already present, nothing changed:` for changes. Results go to standard output and errors to standard error. Only public key data is ever printed.

### Running it in the container

The command must run as the user that owns the key store, so the files it writes stay readable by the server. In the container image this is the server user (`1654:1654`), and the keys live in `/var/lib/ssh-key-server/keys` on the `keyserver-data` volume. Run the command inside the running container:

```sh
docker compose exec <service> /usr/src/app/Credfeto.Keys.Server add --host server1.example.com --username mark --key "ssh-ed25519 AAAA... mark@laptop"
docker compose exec <service> /usr/src/app/Credfeto.Keys.Server list --host server1.example.com --username mark
```

If the key store does not exist, or is not owned by the user running the command, the command refuses to touch it and exits with code `3`.

### Exit codes

| Code | Meaning                                                              |
|------|----------------------------------------------------------------------|
| `0`  | Success, including adding a key that is already stored               |
| `1`  | Invalid host, username, key or fingerprint, or no matching key found |
| `2`  | Usage error, such as a missing `--host` or an unknown option         |
| `3`  | Key store not owned by the current user, missing, unreadable or bad  |

## Changelog

View [changelog](CHANGELOG.md)

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
