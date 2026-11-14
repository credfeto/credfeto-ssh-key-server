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

## Changelog

View [changelog](CHANGELOG.md)

## Contributors

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
