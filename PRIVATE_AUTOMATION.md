# Private Automation

This repo is configured as a standalone private fork:

- Public pushes to `Sonarr/Sonarr` are blocked locally by a pre-push hook safety net.
- Docker images are published from the private repo to GHCR.
- There is no automated upstream sync; this fork no longer tracks `Sonarr/Sonarr`.

## Branches

Recommended branch layout:

- `production`: deployable private branch
- `feature/*`: custom feature work

## GitHub Workflows

- `.github/workflows/private-docker-publish.yml`
  - Validates Docker builds on pull requests to `production`
  - Builds `linux/amd64` and `linux/arm64`
  - Publishes `ghcr.io/<owner>/sonarr-private`
  - Tags include branch name and commit SHA
  - Publishes `latest` from `production`
  - Optionally deploys on your server after publishing when deploy secrets are configured

## Server Compose

Use GHCR on the server instead of local builds:

```yaml
services:
  sonarr:
    image: ghcr.io/<owner>/sonarr-private:production
    container_name: sonarr-private
    ports:
      - "8989:8989"
    volumes:
      - /path/to/config:/config
      - /path/to/tv:/tv
      - /path/to/downloads:/downloads
    restart: unless-stopped
```

If the package is private, authenticate Docker on the server with a GitHub token that can read packages.

## Deploy Secrets

To let GitHub deploy directly to your Docker server, add these repository secrets:

- `DEPLOY_HOST`
- `DEPLOY_USER`
- `DEPLOY_SSH_KEY`
- `DEPLOY_PATH`
- Optional: `DEPLOY_PORT`

`DEPLOY_PATH` should be the directory on the server that contains your `docker-compose.yml`.

## First-Time Setup

1. Push this branch to your private repo only.
2. Create `production` in the private repo if it does not already exist.
3. Merge your feature branch into `production`.
4. Enable GitHub Actions in the private repo.
5. On your server, log in to GHCR and switch Compose to the published image.
6. If you want auto-deploy, add the deploy secrets listed above.

## Optional Hardening

- Set the private repo default branch to `production`.
- Add branch protection on `production`.
- Restrict Actions to the private repo.
