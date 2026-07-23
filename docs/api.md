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

### GET /keys/{host}/{user}/add-challenge

Issues a challenge token that must be signed to prove ownership of the key being added.

**Response:** `200 OK` with JSON body:

```json
{
  "challenge": "<token>",
  "namespace": "ssh-key-server-v1",
  "validUntil": "<iso8601>"
}
```

The challenge is valid for 300 seconds.

### POST /keys/{host}/{user}

Adds a public SSH key for `{user}` on `{host}`, authenticated by a challenge-response signature.

**Request body:** `application/json`:

```json
{
  "key": "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAI... mark@laptop",
  "challenge": "<challenge token from GET /keys/{host}/{user}/add-challenge>",
  "signature": "-----BEGIN SSH SIGNATURE-----\n<base64>\n-----END SSH SIGNATURE-----"
}
```

- `key` is a raw `authorized_keys` line for the key being added.
- `challenge` is the token obtained from `GET /keys/{host}/{user}/add-challenge`.
- `signature` is a PEM-format SSH signature (`-----BEGIN SSH SIGNATURE-----` ... `-----END SSH SIGNATURE-----`), produced by signing the challenge token with the private key corresponding to `key`.

To produce the signature:

```sh
CHALLENGE="<challenge from add-challenge response>"
NAMESPACE="ssh-key-server-v1"
printf '%s' "$CHALLENGE" | ssh-keygen -Y sign -f ~/.ssh/id_ed25519 -n "$NAMESPACE" -
```

The `-----BEGIN SSH SIGNATURE-----` block printed by `ssh-keygen` is the `signature` value.

**Response:** `201 Created` with JSON body containing the assigned key ID:

```json
{ "keyId": "550e8400-e29b-41d4-a716-446655440000" }
```

`400 Bad Request` if the host/username are invalid, the challenge is invalid or expired, the key format is unsupported, or the signature does not verify.

Supported key types: `ssh-ed25519`, `sk-ssh-ed25519@openssh.com`.

### GET /keys/{host}/{user}/{keyId}/challenge

Issues a challenge token that must be signed to prove ownership of the key being deleted.

**Response:** `200 OK` with the same JSON shape as `GET /keys/{host}/{user}/add-challenge`. `404 Not Found` if `{keyId}` does not exist for `{user}` on `{host}`.

### DELETE /keys/{host}/{user}/{keyId}

Removes the key with the given `{keyId}` (UUID returned by POST) for `{user}` on `{host}`, authenticated by a challenge-response signature.

**Request body:** `application/json`:

```json
{
  "challenge": "<challenge token from GET /keys/{host}/{user}/{keyId}/challenge>",
  "signature": "-----BEGIN SSH SIGNATURE-----\n<base64>\n-----END SSH SIGNATURE-----"
}
```

The signature is produced the same way as for `POST`, signing the delete challenge token with the private key corresponding to the key being removed.

**Response:** `204 No Content` if removed, `404 Not Found` if the key did not exist, `400 Bad Request` if the challenge is invalid/expired or the signature does not verify.

### GET /ping

Health check endpoint.

**Response:** `200 OK` with JSON body `{ "message": "Pong!" }`.

## Full add-key flow

```text
1. GET /keys/{host}/{user}/add-challenge
   → { "challenge": "<token>", "namespace": "ssh-key-server-v1", "validUntil": "<iso8601>" }

2. Sign challenge (see "To produce the signature" under POST above)

3. POST /keys/{host}/{user}
   Content-Type: application/json
   { "key": "ssh-ed25519 AAAA... comment", "challenge": "$CHALLENGE", "signature": "$SIGNATURE" }
   → 201 { "keyId": "<uuid>" }
```

## Full delete-key flow

```text
1. GET /keys/{host}/{user}/{keyId}/challenge
   → { "challenge": "<token>", "namespace": "...", "validUntil": "..." }

2. Sign challenge (same as above)

3. DELETE /keys/{host}/{user}/{keyId}
   Content-Type: application/json
   { "challenge": "$CHALLENGE", "signature": "$SIGNATURE" }
   → 204 No Content (or 404 if not found)
```

## Input Validation

- `{host}` must be a valid DNS hostname (letters, digits, hyphens, dots; max 253 characters).
- `{user}` must contain only letters, digits, underscores, or hyphens (max 32 characters).
- Key data must be valid base64-encoded content.

See also: [`src/Credfeto.Keys.Server/keys.http`](../src/Credfeto.Keys.Server/keys.http) for example HTTP requests.
