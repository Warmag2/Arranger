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

The compose file works with Docker and Podman. Podman needs version 5.3 or later, for the `host-gateway`
mapping of `host.docker.internal`.

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
