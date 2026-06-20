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

## 4. Add GitHub Repository Secrets

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

## 5. Deploy A Release

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

## 6. Add Host Nginx And SSL

Run as `root` on the Ubuntu server.

Set the public domain for this site:

```bash
DOMAIN="itrace.invensys.web.za"
WEB_PORT="8082"
```

Make sure the domain already points to this server before issuing the certificate.

Install nginx and Certbot if they are not already installed:

```bash
apt update
apt install -y nginx certbot python3-certbot-nginx
```

Create the nginx site:

```bash
cat > "/etc/nginx/sites-available/${DOMAIN}" <<EOF
server {
    listen 80;
    server_name ${DOMAIN};

    location / {
        proxy_pass http://127.0.0.1:${WEB_PORT};
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
EOF
```

Enable the site:

```bash
ln -s "/etc/nginx/sites-available/${DOMAIN}" "/etc/nginx/sites-enabled/${DOMAIN}"
nginx -t
systemctl reload nginx
```

Issue and install the TLS certificate:

```bash
certbot --nginx -d "${DOMAIN}"
```

After Certbot updates nginx, verify the public site:

```bash
curl -I "https://${DOMAIN}/"
curl -I "https://${DOMAIN}/health"
```

## 7. Verify The Site

Check the exposed port:

```bash
curl -I http://localhost:8082
```
