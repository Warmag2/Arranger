# Arranger

A Blazor app for arranging events: scheduling, expenses and participant management.

## Usage

Prerequisites: a server with Docker, and the `faemiyahsdk:latest` build image available
locally (the Dockerfile builds against it; Docker will not pull it from a registry,
see https://github.com/Warmag2/Faemiyah.Library).

Then:

1. Copy `infra/.env_sample` to `infra/.env`.
2. Fill `ARRANGER_USER` and `ARRANGER_PASSWORD` as per your pregenerated PostgreSQL database.
3. From the repository root, run:

       ./refresh.sh

This builds the `arranger` image and starts it via `infra/docker-compose.yml`. The app listens on
`127.0.0.1:8549`. Data-protection keys persist in the `arranger-dpkeys` volume.

The compose file targets Podman 5.3 or later (for the `host-gateway` mapping of `host.docker.internal`).
Its `logging` block uses Podman's `k8s-file` driver to write `/var/log/containers/arranger.log`, which
Docker does not have. On Docker, replace the block with:

```yaml
    logging:
      driver: json-file
      options:
        max-size: 4mb
```

Docker then keeps the log under `/var/lib/docker/containers/<id>/`; read it with `docker logs arranger`.

### Removing everything

`infra/prune.sh` is meant for a machine that runs Docker only for Arranger, for example after trying it out.
**It deletes all unused images, containers, networks, build cache and volumes on the host, not just
Arranger's.** That includes the `arranger-dpkeys` volume (logging every user out) and any other stopped
service's data. Do not run it on a machine with anything else in Docker; remove Arranger's containers,
image and volume by hand instead.

## Development

Run a local PostgreSQL with `docker compose -f infra/docker-compose.dev.yml up -d`, then either
start the `Server` project in debug mode from MSVS or run the server with
`dotnet run --project src/Arranger.Server`.

NOTE: This service is unsecured and expects no certificates. Put it behind an NGINX or Apache
virtual host with a reverse proxy to add TLS.

### Reverse proxy

The proxy must:

- **Keep the original `Host` header.** The SignalR hub refuses browser connections whose `Origin` differs from
  `Host`, so without this the arrangement pages stay at "Connecting".
- **Forward WebSocket upgrades** for the live updates. Without them the client falls back to slower transports.
- **Send `X-Forwarded-Proto`,** so that the app knows the request came over HTTPS.

Apache, with `mod_proxy`, `mod_proxy_http`, `mod_proxy_wstunnel`, `mod_rewrite` and `mod_headers` enabled:

    <VirtualHost *:443>
        ServerName arranger.example.com
        # SSLEngine, SSLCertificateFile etc.

        RewriteEngine On
        RewriteCond %{HTTP:Upgrade} =websocket [NC]
        RewriteRule ^/?(.*) "ws://127.0.0.1:8549/$1" [P,L]

        ProxyPreserveHost On
        RequestHeader set X-Forwarded-Proto "https"
        ProxyPass        / http://127.0.0.1:8549/
        ProxyPassReverse / http://127.0.0.1:8549/
    </VirtualHost>

NGINX equivalent:

    location / {
        proxy_pass http://127.0.0.1:8549;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

The app trusts `X-Forwarded-For` and `X-Forwarded-Proto` only from loopback and the addresses in
`ReverseProxy:KnownProxies`. Apache adds `X-Forwarded-For` by itself. The published port reaches the container
from its network's gateway rather than from loopback, so `infra/docker-compose.yml` pins `arrangernet` to
`10.89.249.0/24` and trusts its gateway `10.89.249.1`. If that subnet clashes with another network on the host,
change both values together. To check the setup, fail a login and confirm that the log shows your own IP, not
the gateway's.
