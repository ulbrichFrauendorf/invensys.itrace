# Production Deployment With GitHub Actions

This is a root-first walkthrough for deploying one or more sites from GitHub Actions to an Ubuntu server.

Assumptions:

- You are logged into the Ubuntu server as `root`.
- Docker is already installed.
- All sites are deployed by one Linux user: `github-actions`.
- Each repository gets its own GitHub deploy key.
- GitHub Actions may reuse the same server SSH key and secret values for every site on this server.
- Each site lives under `/home/github-actions/sites/<site-name>`.

The guide uses `itrace` as the example site name. Replace these values per site:

```bash
APP_NAME="itrace"
GITHUB_REPO="ulbrichFrauendorf/invensys.itrace"
APP_PATH="/home/github-actions/sites/${APP_NAME}"
WEB_PORT="8082"
BRANCH="main"
```

## 1. Create The Shared Linux User

Run as `root` on the Ubuntu server.

```bash
id github-actions >/dev/null 2>&1 || adduser --disabled-password --gecos "" github-actions
usermod -aG docker github-actions

mkdir -p /home/github-actions/sites
chown -R github-actions:github-actions /home/github-actions
chmod 755 /home/github-actions
```

Verify Docker access:

```bash
sudo -u github-actions docker ps
```

If Docker permission is denied, log out of the server and log back in, then retry.

## 2. Create The Shared Server SSH Key

Run as `root` on the Ubuntu server.

This key is used by GitHub Actions to SSH into the server as `github-actions`.

```bash
mkdir -p /root/deploy-keys
chmod 700 /root/deploy-keys

ssh-keygen -t ed25519 \
  -C "github-actions-server-deploy" \
  -f /root/deploy-keys/github-actions-server-deploy
```

Press Enter for no passphrase unless you specifically want one.

Install the public key for the `github-actions` user:

```bash
mkdir -p /home/github-actions/.ssh
chmod 700 /home/github-actions/.ssh

cat /root/deploy-keys/github-actions-server-deploy.pub >> /home/github-actions/.ssh/authorized_keys

chown -R github-actions:github-actions /home/github-actions/.ssh
chmod 600 /home/github-actions/.ssh/authorized_keys
```

Print the private key. This exact output becomes the GitHub secret `DEPLOY_SSH_KEY`.

```bash
cat /root/deploy-keys/github-actions-server-deploy
```

These GitHub Actions secrets can be reused by every repository that deploys to this server:

```text
DEPLOY_SSH_HOST        your-server-host-or-ip
DEPLOY_SSH_PORT        22
DEPLOY_SSH_USER        github-actions
DEPLOY_SSH_KEY         the full private key printed above
DEPLOY_SSH_PASSPHRASE  leave empty if no passphrase was used
```

## 3. Create One GitHub Deploy Key For The Site

Run as `root` on the Ubuntu server.

Each repository must have its own deploy key because GitHub does not allow the same deploy key to be attached to multiple repositories.

```bash
APP_NAME="itrace"
GITHUB_REPO="ulbrichFrauendorf/invensys.itrace"

sudo -u github-actions mkdir -p /home/github-actions/.ssh
sudo -u github-actions chmod 700 /home/github-actions/.ssh

sudo -u github-actions ssh-keygen -t ed25519 \
  -C "${GITHUB_REPO}-${APP_NAME}-deploy-key" \
  -f /home/github-actions/.ssh/${APP_NAME}_github_deploy_key \
  -N ""

cat >> /home/github-actions/.ssh/config <<EOF

Host github.com-${APP_NAME}
  HostName github.com
  User git
  IdentityFile /home/github-actions/.ssh/${APP_NAME}_github_deploy_key
  IdentitiesOnly yes
EOF

ssh-keyscan github.com >> /home/github-actions/.ssh/known_hosts

chown -R github-actions:github-actions /home/github-actions/.ssh
chmod 700 /home/github-actions/.ssh
chmod 600 /home/github-actions/.ssh/config
chmod 600 /home/github-actions/.ssh/known_hosts

cat /home/github-actions/.ssh/${APP_NAME}_github_deploy_key.pub
```

Copy the public key printed by the last command.

In GitHub, open the repository:

```text
Settings -> Deploy keys -> Add deploy key
```

Use:

```text
Title: production ubuntu deploy key
Key: paste the public key
Allow write access: off
```

## 4. Clone The Site

Run as `root` on the Ubuntu server.

```bash
APP_NAME="itrace"
GITHUB_REPO="ulbrichFrauendorf/invensys.itrace"
APP_PATH="/home/github-actions/sites/${APP_NAME}"
BRANCH="main"

mkdir -p /home/github-actions/sites
chown github-actions:github-actions /home/github-actions/sites

if [ ! -d "${APP_PATH}/.git" ]; then
  sudo -u github-actions git clone "git@github.com-${APP_NAME}:${GITHUB_REPO}.git" "${APP_PATH}"
fi

sudo -u github-actions git -C "${APP_PATH}" fetch --all --tags --prune
sudo -u github-actions git -C "${APP_PATH}" checkout "${BRANCH}"
sudo -u github-actions git -C "${APP_PATH}" pull origin "${BRANCH}"
```

## 5. Add GitHub Repository Secrets

In each GitHub repository, open:

```text
Settings -> Secrets and variables -> Actions -> New repository secret
```

Add the shared server SSH secrets:

```text
DEPLOY_SSH_HOST
DEPLOY_SSH_PORT
DEPLOY_SSH_USER
DEPLOY_SSH_KEY
DEPLOY_SSH_PASSPHRASE
```

Add site-specific secrets:

```text
DEPLOY_APP_NAME    itrace
DEPLOY_APP_PATH    /home/github-actions/sites/itrace
DEPLOY_WEB_PORT    8082
```

Add the SQL password for the existing SQL Server:

```text
MSSQL_SA_PASSWORD
```

For this repository, production is app-only by default. It expects SQL Server to already exist and be reachable from the app container through the Ubuntu host on port `1433`.

`docker-compose.production.yml` builds this connection string automatically:

```text
Server=host.docker.internal,1433;Database=invensys.itrace;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;MultipleActiveResultSets=true
```

If SQL Server is not published on the Ubuntu host port `1433`, update `docker-compose.production.yml` before deploying.

The SQL service in `docker-compose.production.yml` is behind the `local-sql` profile. It will not start during the normal deployment. To intentionally create a dedicated SQL container for this app, deploy with:

```bash
docker compose --profile local-sql -f docker-compose.production.yml up -d --remove-orphans
```

For your preferred setup, do not use the `local-sql` profile.

## 6. Add The Repository Deploy Script

Create `scripts/deploy-prod.sh` in the repository.

```bash
#!/usr/bin/env bash
set -euo pipefail

COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.production.yml}"

if docker compose version >/dev/null 2>&1; then
  docker compose -f "$COMPOSE_FILE" pull --ignore-pull-failures
  docker compose -f "$COMPOSE_FILE" build --pull
  docker compose -f "$COMPOSE_FILE" up -d --remove-orphans
  docker compose -f "$COMPOSE_FILE" ps
else
  docker-compose -f "$COMPOSE_FILE" pull --ignore-pull-failures
  docker-compose -f "$COMPOSE_FILE" build --pull
  docker-compose -f "$COMPOSE_FILE" up -d --remove-orphans
  docker-compose -f "$COMPOSE_FILE" ps
fi
```

Make it executable and commit it:

```bash
git add scripts/deploy-prod.sh
git update-index --chmod=+x scripts/deploy-prod.sh
git commit -m "Add production deploy script"
git push
```

## 7. Add The GitHub Actions Workflow

Create `.github/workflows/deploy-prod.yml` in the repository.

```yaml
name: Deploy Production

on:
  push:
    tags:
      - "v[0-9]+.[0-9]+.[0-9]+"
  workflow_dispatch:
    inputs:
      ref:
        description: "Branch, tag, or commit to deploy"
        required: false
        default: "main"

concurrency:
  group: production-deploy-${{ github.repository }}
  cancel-in-progress: true

jobs:
  deploy:
    runs-on: ubuntu-latest
    permissions:
      contents: read

    steps:
      - name: Select deployment ref
        id: deploy_ref
        shell: bash
        run: |
          set -euo pipefail
          if [ "${{ github.event_name }}" = "workflow_dispatch" ]; then
            echo "REF=${{ inputs.ref }}" >> "$GITHUB_OUTPUT"
            echo "IMAGE_TAG=${{ inputs.ref }}" >> "$GITHUB_OUTPUT"
          else
            echo "REF=${GITHUB_REF_NAME}" >> "$GITHUB_OUTPUT"
            echo "IMAGE_TAG=${GITHUB_REF_NAME}" >> "$GITHUB_OUTPUT"
          fi

      - name: Test SSH port
        env:
          SSH_HOST: ${{ secrets.DEPLOY_SSH_HOST }}
          SSH_PORT: ${{ secrets.DEPLOY_SSH_PORT }}
        shell: bash
        run: |
          set -euo pipefail
          nc -vz -w 10 "$SSH_HOST" "$SSH_PORT"

      - name: Deploy on server
        uses: appleboy/ssh-action@v1.2.0
        env:
          DEPLOY_REF: ${{ steps.deploy_ref.outputs.REF }}
          IMAGE_TAG: ${{ steps.deploy_ref.outputs.IMAGE_TAG }}
          WEB_PORT: ${{ secrets.DEPLOY_WEB_PORT }}
          MSSQL_SA_PASSWORD: ${{ secrets.MSSQL_SA_PASSWORD }}
          APP_NAME: ${{ secrets.DEPLOY_APP_NAME }}
          APP_PATH: ${{ secrets.DEPLOY_APP_PATH }}
        with:
          host: ${{ secrets.DEPLOY_SSH_HOST }}
          username: ${{ secrets.DEPLOY_SSH_USER }}
          key: ${{ secrets.DEPLOY_SSH_KEY }}
          passphrase: ${{ secrets.DEPLOY_SSH_PASSPHRASE }}
          port: ${{ secrets.DEPLOY_SSH_PORT }}
          script_stop: true
          timeout: 120s
          command_timeout: 60m
          envs: DEPLOY_REF,IMAGE_TAG,WEB_PORT,MSSQL_SA_PASSWORD,APP_NAME,APP_PATH
          script: |
            set -euo pipefail

            REPOSITORY="${{ github.repository }}"

            if [ "$(whoami)" != "github-actions" ]; then
              echo "This workflow must SSH as github-actions."
              exit 1
            fi

            if [ -z "$APP_NAME" ] || [ -z "$APP_PATH" ]; then
              echo "DEPLOY_APP_NAME and DEPLOY_APP_PATH are required."
              exit 1
            fi

            path_prefix="/home/github-actions/sites/"
            if [ "${APP_PATH#"$path_prefix"}" = "$APP_PATH" ]; then
              echo "DEPLOY_APP_PATH must be under /home/github-actions/sites/."
              exit 1
            fi

            if ! printf '%s' "$APP_NAME" | grep -Eq '^[A-Za-z0-9._-]+$'; then
              echo "DEPLOY_APP_NAME may only contain letters, numbers, dot, underscore, and dash."
              exit 1
            fi

            mkdir -p "$APP_PATH"
            cd "$APP_PATH"

            if [ ! -d ".git" ]; then
              git clone "git@github.com-${APP_NAME}:${REPOSITORY}.git" .
            fi

            git fetch --all --tags --prune
            git checkout -f "$DEPLOY_REF"

            umask 077
            {
              echo "ASPNETCORE_ENVIRONMENT=Production"
              echo "WEB_PORT=${WEB_PORT}"
              echo "IMAGE_TAG=${IMAGE_TAG}"
              echo "MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}"
            } > .env

            chmod +x scripts/deploy-prod.sh
            ./scripts/deploy-prod.sh
```

Commit it:

```bash
git add .github/workflows/deploy-prod.yml
git commit -m "Add production deployment workflow"
git push
```

## 8. Deploy A Release

Create and push a version tag:

```bash
git checkout main
git pull
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will deploy the tag.

Manual deployment is also available:

```text
GitHub -> Actions -> Deploy Production -> Run workflow
```

Use `main`, a tag like `v1.0.0`, or a commit SHA as the `ref`.

## 9. Verify The Site

Run as `root` on the Ubuntu server.

```bash
APP_NAME="itrace"
APP_PATH="/home/github-actions/sites/${APP_NAME}"

sudo -u github-actions docker compose -f "${APP_PATH}/docker-compose.production.yml" ps
sudo -u github-actions docker compose -f "${APP_PATH}/docker-compose.production.yml" logs --tail=100 app
```

Check the exposed port:

```bash
curl -I http://localhost:8082
```

## 10. Add Another Site

Repeat these sections for each additional site:

```text
3. Create One GitHub Deploy Key For The Site
4. Clone The Site
5. Add GitHub Repository Secrets
8. Deploy A Release
9. Verify The Site
```

Reuse these GitHub Actions secrets across sites on the same server:

```text
DEPLOY_SSH_HOST
DEPLOY_SSH_PORT
DEPLOY_SSH_USER
DEPLOY_SSH_KEY
DEPLOY_SSH_PASSPHRASE
```

Use unique values for each site:

```text
DEPLOY_APP_NAME
DEPLOY_APP_PATH
DEPLOY_WEB_PORT
GitHub deploy key
Application secrets
```

## 11. Common Checks

Test GitHub Actions SSH access from your workstation:

```bash
ssh -i ./github-actions-server-deploy github-actions@your-server-host-or-ip
```

Test repository clone access on the server as `root`:

```bash
APP_NAME="itrace"
sudo -u github-actions ssh -T "git@github.com-${APP_NAME}"
```

Check Docker access:

```bash
sudo -u github-actions docker ps
```

Check ownership:

```bash
ls -ld /home/github-actions /home/github-actions/.ssh /home/github-actions/sites
```

Expected owner for `/home/github-actions/.ssh` and `/home/github-actions/sites` is `github-actions:github-actions`.
