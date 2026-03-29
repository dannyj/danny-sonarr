# Agent Notes

This checkout is a private Sonarr fork with custom features. Treat it as private-only delivery work.

## Remotes And Push Safety

- `origin` points at the public `Sonarr/Sonarr` repo for fetches only.
- Public pushes must never be used from this checkout.
- Pushes go to the `private` remote only.
- A local pre-push hook blocks pushes to the public Sonarr repo.

Before pushing, prefer:

```bash
git push private <branch>
```

## Private Image Delivery

Private images are published by GitHub Actions to GHCR:

- Registry: `ghcr.io/dannyj/sonarr-private`
- Workflow: `.github/workflows/private-docker-publish.yml`

Tags:

- feature branches: branch name, for example `feature-plex-watch-stats`
- branch commits: `sha-<shortsha>`
- `private-main`: also publishes `latest`

Example pull:

```bash
docker pull ghcr.io/dannyj/sonarr-private:feature-plex-watch-stats
```

## Upstream Sync

Upstream intake is handled in:

- `.github/workflows/private-upstream-sync.yml`

It merges from public `Sonarr/Sonarr` into a private sync branch and opens or updates a PR against `private-main`.

## Local Build And Test

Frontend-only validation:

```bash
corepack enable
yarn build --env production
```

Local container rebuild from this checkout:

```bash
docker compose build sonarr
docker compose up -d sonarr
```

The Dockerfile is written so GitHub Actions can build `linux/amd64` and `linux/arm64` images for GHCR.

## Runtime Data

Do not commit local runtime files under `data/config`.

Examples:

- `data/config/*.db`
- `data/config/*.db-shm`
- `data/config/*.db-wal`
- `data/config/logs/`
- `data/config/MediaCover/`
- `data/config/Sentry/`

Some of these files are still tracked historically in this repo. `.gitignore` will not hide tracked files by itself.

## Compose Deployment

Server-side Compose should use the GHCR image instead of local builds, for example:

```yaml
services:
  sonarr:
    image: ghcr.io/dannyj/sonarr-private:feature-plex-watch-stats
```

For stable deployment from the main private branch, prefer:

```yaml
image: ghcr.io/dannyj/sonarr-private:latest
```

or:

```yaml
image: ghcr.io/dannyj/sonarr-private:private-main
```

## Related Reference

See `PRIVATE_AUTOMATION.md` for the fuller private CI/CD setup and deploy secret documentation.
