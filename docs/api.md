# API Reference

The SSH key server exposes a REST API over HTTP.

## Endpoints

### GET /keys/{host}/{user}

Returns the authorised public SSH keys for `{user}` on `{host}` in `authorized_keys` format (plain text, one key per line).

This endpoint is intended to be called by `sshd`'s `AuthorizedKeysCommand`. Add the following to `/etc/ssh/sshd_config`:

```text
AuthorizedKeysCommand /usr/bin/curl -s https://keys.markridgwell.com/keys/%H/%u
AuthorizedKeysCommandUser nobody
```

**Response:** `200 OK` with `text/plain` body. Returns an empty body when no keys exist for the given host and user.

### POST /keys/{host}/{user}

Adds a public SSH key for `{user}` on `{host}`.

**Request body:** `text/plain` — a single raw authorized_keys line, e.g.:

```text
ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAI... mark@laptop
```

**Response:** `201 Created` with JSON body containing the assigned key ID:

```json
{ "keyId": "550e8400-e29b-41d4-a716-446655440000" }
```

Supported key types: `ssh-rsa`, `ssh-dss`, `ssh-ed25519`, `ecdsa-sha2-nistp256`, `ecdsa-sha2-nistp384`, `ecdsa-sha2-nistp521`, `sk-ssh-ed25519@openssh.com`, `sk-ecdsa-sha2-nistp256@openssh.com`.

### DELETE /keys/{host}/{user}/{keyId}

Removes the key with the given `{keyId}` (UUID returned by POST) for `{user}` on `{host}`.

**Response:** `204 No Content` if removed, `404 Not Found` if the key did not exist.

### GET /ping

Health check endpoint.

**Response:** `200 OK` with JSON body `{ "message": "Pong!" }`.

## Input Validation

- `{host}` must be a valid DNS hostname (letters, digits, hyphens, dots; max 253 characters).
- `{user}` must contain only letters, digits, underscores, or hyphens (max 32 characters).
- Key data must be valid base64-encoded content.

See also: [`src/Credfeto.Keys.Server/keys.http`](../src/Credfeto.Keys.Server/keys.http) for example HTTP requests.
