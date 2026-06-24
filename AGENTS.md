# Agent Notes

This checkout is a private, standalone fork with custom features. There is no upstream tie; treat it as private-only delivery work.

## Remotes And Push Safety

- `origin` points at the private repo `dannyj/danny-sonarr` for both fetch and push.
- There is no public `Sonarr/Sonarr` remote configured.
- A local pre-push hook still blocks pushes to the public Sonarr repo, as a safety net in case it is ever re-added.

Before pushing, prefer:

```bash
git push origin <branch>
```

## Private Image Delivery

Private images are published by GitHub Actions to GHCR:

- Registry: `ghcr.io/dannyj/sonarr-private`
- Workflow: `.github/workflows/private-docker-publish.yml`

Tags:

- feature branches: branch name, for example `feature-plex-watch-stats`
- branch commits: `sha-<shortsha>`
- `production`: also publishes `latest`

Example pull:

```bash
docker pull ghcr.io/dannyj/sonarr-private:feature-plex-watch-stats
```

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
image: ghcr.io/dannyj/sonarr-private:production
```

## Related Reference

See `PRIVATE_AUTOMATION.md` for the fuller private CI/CD setup and deploy secret documentation.
