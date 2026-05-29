# Server Logs API (token-authed, pipe-friendly)

Exposes the RustDedicated container logs over HTTP so they can be pulled from a CLI and
piped into tools like **Claude Code** — without a browser session.

## Endpoints

| Method & path | Response | Notes |
| --- | --- | --- |
| `GET /api/server-logs.txt` | `text/plain` (raw) | Pipe target. Default `tail=500`. |
| `GET /api/server-logs` | JSON `{ logs }` | Used by the dashboard poll too. Default `tail=200`. |

Query params (both): `tail` = number of trailing lines (capped at **5000**),
`since` = unix-seconds cutoff for incremental pulls. Lines carry Docker timestamps.

Defined in `web/src/index.ts`; logs are read from the `rust-server` container via
`getServerLogs()` in `web/src/docker.ts`.

## Auth

Both endpoints authorize via **either**:

1. A valid browser **session cookie** (the dashboard's own login), or
2. The static **API token** in `LOGS_API_TOKEN` (env, default empty = disabled).

Supply the token any of three ways (header preferred — query strings leak into proxy/access
logs): `Authorization: Bearer <token>`, `X-API-Token: <token>`, or `?token=<token>`.

Comparison is constant-time (`web/src/auth.ts`). Unauthorized → HTTP `401`. No other
`/api/*` route accepts the token; it is scoped to logs only.

### Setup

```sh
# Generate a token and put it in .env (picked up via compose `env_file`)
openssl rand -hex 32
# .env →  LOGS_API_TOKEN=<the value>
```

Redeploy the `web-admin` service for it to take effect.

## Usage

```sh
# Raw logs straight into a one-shot Claude Code prompt
curl -fsS -H "Authorization: Bearer $LOGS_API_TOKEN" \
  "https://admin.example.com/api/server-logs.txt?tail=1000" \
  | claude -p "Diagnose any errors or crashes in these Rust server logs"

# Just view them
curl -fsS -H "Authorization: Bearer $LOGS_API_TOKEN" \
  "https://admin.example.com/api/server-logs.txt?tail=500"
```

Handy shell alias:

```sh
alias rustlogs='curl -fsS -H "Authorization: Bearer $LOGS_API_TOKEN" \
  "https://admin.example.com/api/server-logs.txt?tail=1000"'
# then:  rustlogs | claude -p "what went wrong?"
```

## Security notes

- Token is **off by default**; logs stay session-only until you set `LOGS_API_TOKEN`.
- Server logs can contain IPs, Steam IDs, and RCON output — treat the token as a secret
  and prefer TLS (the public deploy is HTTPS via Dokploy).
- The token grants **read-only log access**, nothing else.
