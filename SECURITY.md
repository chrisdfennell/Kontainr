# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| dev     | Yes       |
| < dev   | No        |

## Reporting a Vulnerability

If you discover a security vulnerability in Kontainr, please report it responsibly:

1. **Do not** open a public issue
2. Open a private vulnerability report via [GitHub Security Advisories](https://github.com/chrisdfennell/Kontainr/security/advisories/new)
3. Or contact the maintainer directly

## Threat Model

Kontainr talks to the Docker socket. Anyone who can use Kontainr can start privileged
containers, bind-mount host paths, and open a shell inside any container — which is to say
**access to Kontainr is equivalent to root on the Docker host**. Every control below exists
to keep that access to the people who are supposed to have it.

Kontainr has a single shared credential and no user model, so it cannot tell two operators
apart. Treat it as a tool for a small, trusted group.

## Security Considerations

- **Authentication is required.** Kontainr refuses to start unless `Auth__Username` and
  `Auth__Password` are set. To run without authentication — appropriate only when something
  in front of Kontainr already authenticates requests — set `Auth__Disabled=true`, which
  logs a warning on every start.
- **Basic Auth has no rate limiting.** Credentials are also sent on every request. Put
  Kontainr behind a reverse proxy that terminates TLS (Traefik, Caddy, nginx) and use a long
  password. Kontainr will warn at startup about short or well-known passwords.
- **Stored credentials.** SSH, registry and git passwords are encrypted with ASP.NET Data
  Protection. The key ring lives in `/app/data/keys` (override with `KONTAINR_KEYS`) and must
  be persisted, or those credentials become unrecoverable.
- **Encrypt the key ring.** By default the key ring sits in the same volume as the secrets it
  protects, so read access to that volume — a backup, a stray bind mount — yields plaintext
  for every stored credential. Set `KONTAINR_KEY_PASSPHRASE` to encrypt the key ring at rest
  with AES-256-GCM under a PBKDF2-derived key. Keep a copy of the passphrase somewhere other
  than the data volume; losing it makes the stored credentials unrecoverable.
- **Remote Docker over TCP.** Docker's TCP socket has no authentication of its own. Kontainr
  refuses to connect to a non-loopback TCP endpoint unless TLS is configured (point it at a
  directory containing `ca.pem`, `cert.pem` and `key.pem`, as `DOCKER_CERT_PATH` does) or the
  connection is explicitly marked as plaintext-allowed. Server certificates are validated
  against the supplied CA, not the machine trust store.
- **SSH host keys are not verified.** Kontainr does not yet pin or check the host key when
  connecting to an SSH host or SSH-tunnelled Docker host, so a first connection is open to a
  machine-in-the-middle that would capture the password. Connect over networks you trust.
- **Git stack credentials are visible to Docker.** Deploying a private git stack passes the
  clone URL, including embedded credentials, to a short-lived container. That value is
  readable via `docker inspect` by anyone with Docker access on that host.
- **The audit log has no actor.** It records what happened and when, but not who did it or
  from where, and it is a plain JSON file capped at 500 entries in the data volume.
- **Container isolation.** The published image runs as UID 10001. Reaching the Docker socket
  still requires membership of a group that can read it — pass the host's docker group with
  `--group-add "$(getent group docker | cut -d: -f3)"`.

## Reducing the Blast Radius

- Do not expose Kontainr's port to an untrusted network. Bind it to a private interface or
  keep it behind a VPN.
- Terminate TLS in front of it. Basic Auth over plain HTTP sends the password in the clear.
- Set `KONTAINR_KEY_PASSPHRASE`, and store it outside the data volume.
- Keep the data volume off shared or world-readable storage.
