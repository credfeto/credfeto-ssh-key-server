# Configuration

The server is configured via `appsettings.json` or environment variables.

## Keys section

| Key | Type | Default | Description |
| --- | ---- | ------- | ----------- |
| `Keys:BasePath` | `string` | `/var/lib/ssh-key-server/keys` | Root directory for file system key storage |

Example `appsettings.json`:

```json
{
  "Keys": {
    "BasePath": "/var/lib/ssh-key-server/keys"
  }
}
```

## Storage

Keys are stored as JSON files at `{BasePath}/{host}/{username}.json`. The storage backend is abstracted behind `ISshKeyDataStore` to allow future migration to a database backend without changing the API.

## Docker

Run with a named volume:

```bash
docker run -p 8080:8080 \
  -v ssh-keys:/var/lib/ssh-key-server/keys \
  credfeto-keys-server
```

Or bind-mount a host directory:

```bash
docker run -p 8080:8080 \
  -v /srv/ssh-keys:/var/lib/ssh-key-server/keys \
  credfeto-keys-server
```
