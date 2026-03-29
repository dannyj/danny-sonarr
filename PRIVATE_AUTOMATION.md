# Private Automation

This repo is configured for a private fork workflow:

- Public pushes to `Sonarr/Sonarr` are blocked locally.
- Docker images are published from the private repo to GHCR.
- Upstream Sonarr changes are merged into a PR instead of going straight onto your deploy branch.

## Branches

Recommended branch layout:

- `private-main`: deployable private branch
- `feature/*`: custom feature work
- `automation/upstream-sync-*`: bot-created upstream merge branches

## GitHub Workflows

- `.github/workflows/private-docker-publish.yml`
  - Validates Docker builds on pull requests to `private-main`
  - Builds `linux/amd64` and `linux/arm64`
  - Publishes `ghcr.io/<owner>/sonarr-private`
  - Tags include branch name and commit SHA
  - Publishes `latest` from `private-main`
  - Optionally deploys on your server after publishing when deploy secrets are configured

- `.github/workflows/private-upstream-sync.yml`
  - Fetches `Sonarr/Sonarr`
  - Merges the selected upstream branch into a reusable sync branch from `private-main`
  - Creates or updates a PR for review
  - Fails if the merge needs manual conflict resolution

## Server Compose

Use GHCR on the server instead of local builds:

```yaml
services:
  sonarr:
    image: ghcr.io/<owner>/sonarr-private:private-main
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
2. Create `private-main` in the private repo if it does not already exist.
3. Merge your feature branch into `private-main`.
4. Enable GitHub Actions in the private repo.
5. On your server, log in to GHCR and switch Compose to the published image.
6. If you want auto-deploy, add the deploy secrets listed above.

## Optional Hardening

- Set the private repo default branch to `private-main`.
- Add branch protection on `private-main`.
- Restrict Actions to the private repo.
- Keep `origin` fetch-only and use `private` for all pushes.
