# Arranger

A Blazor app for arranging events: scheduling, expenses and participant management.

## Usage

Prerequisites: a Linux server with Docker, and the `faemiyahsdk:latest` build image
available locally (the Dockerfile builds against it; Docker will not pull it from a registry).

Then:

1. Copy `infra/.env_sample` to `infra/.env` and set `CONNECTIONSTRING` to your PostgreSQL database.
2. From the repository root, run:

       ./refresh.sh

This builds the `arranger` image and starts it via `infra/docker-compose.yml`. The app listens on
`127.0.0.1:8549`. Data-protection keys persist in the `arranger-dpkeys` volume.

## Development

Run a local PostgreSQL with `docker compose -f infra/docker-compose.dev.yml up -d`, then start the
server with `dotnet run --project src/Arranger.Server`.

NOTE: This service is unsecured and expects no certificates. Put it behind an NGINX or Apache
virtual host with a reverse proxy to add TLS.
