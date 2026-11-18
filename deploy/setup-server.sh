#!/usr/bin/env bash
# One-off setup of a fresh Ubuntu 22.04/24.04 VPS for the Pinpoint API. Run as root:
#   sudo bash setup-server.sh <domain>
# <domain> is the public hostname whose DNS A record points at this server. With no domain of your own,
# use <server-ip>.sslip.io (e.g. 203.0.113.7.sslip.io): it resolves to that IP and Caddy can still get a certificate.
# Safe to re-run: existing secrets in /etc/pinpoint/pinpoint.env are kept.
set -euo pipefail

DOMAIN="${1:?usage: setup-server.sh <domain>}"
ENV_FILE=/etc/pinpoint/pinpoint.env

echo "==> Packages"
apt-get update -q
apt-get install -y -q postgresql libicu-dev debian-keyring debian-archive-keyring apt-transport-https curl gnupg ufw
if ! command -v caddy >/dev/null; then
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/gpg.key | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt > /etc/apt/sources.list.d/caddy-stable.list
  apt-get update -q && apt-get install -y -q caddy
fi

if ! swapon --show | grep -q .; then
  echo "==> 1 GB swap (a 1 GB server can run short during migrations or a burst of check-ins)"
  fallocate -l 1G /swapfile && chmod 600 /swapfile && mkswap /swapfile >/dev/null && swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "==> Firewall (SSH, HTTP, HTTPS only; the API itself is never exposed)"
ufw allow OpenSSH >/dev/null; ufw allow 80/tcp >/dev/null; ufw allow 443/tcp >/dev/null; ufw --force enable >/dev/null

echo "==> Service user and folders"
id pinpoint >/dev/null 2>&1 || useradd --system --home /opt/pinpoint --shell /usr/sbin/nologin pinpoint
mkdir -p /opt/pinpoint/api /opt/pinpoint/.aspnet /var/log/pinpoint /etc/pinpoint
chown -R pinpoint:pinpoint /opt/pinpoint /var/log/pinpoint

if [[ ! -f $ENV_FILE ]]; then
  echo "==> Database and secrets"
  DB_PASS=$(openssl rand -hex 24)
  sudo -u postgres psql -q -c "CREATE ROLE pinpoint LOGIN PASSWORD '$DB_PASS';"
  sudo -u postgres psql -q -c "CREATE DATABASE pinpoint OWNER pinpoint;"
  cat > $ENV_FILE <<CONF
ASPNETCORE_ENVIRONMENT=Production
# Loopback only: Caddy is the sole way in, so X-Forwarded-For can be trusted for per-IP rate limits.
ASPNETCORE_URLS=http://127.0.0.1:5080
LOG_DIRECTORY=/var/log/pinpoint
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=pinpoint;Username=pinpoint;Password=$DB_PASS
JwtSettings__Key=$(openssl rand -base64 48)
Attendance__LecturerInviteCode=$(openssl rand -hex 4)
DeepLink__PublicBaseUrl=https://$DOMAIN
Swagger__Enabled=true
Seed__MigrateOnStartup=true
Seed__Enabled=true
Seed__Profile=Dundee
Seed__DefaultPassword=${SEED_PASSWORD:-$(openssl rand -base64 12)}
Seed__AdminPassword=$(openssl rand -base64 18)
CONF
  chmod 640 $ENV_FILE; chown root:pinpoint $ENV_FILE
fi

echo "==> systemd unit and Caddy"
cp "$(dirname "$0")/pinpoint-api.service" /etc/systemd/system/pinpoint-api.service
systemctl daemon-reload
systemctl enable pinpoint-api >/dev/null
sed "s/{\$DOMAIN}/$DOMAIN/" "$(dirname "$0")/Caddyfile" > /etc/caddy/Caddyfile
systemctl reload caddy || systemctl restart caddy

echo
echo "Done. Next, from your Mac: ./deploy/publish.sh <user>@<server>"
echo "Admin login: admin@seatcharter.dev / $(grep ^Seed__AdminPassword $ENV_FILE | cut -d= -f2-)"
echo "Lecturer invite code: $(grep ^Attendance__LecturerInviteCode $ENV_FILE | cut -d= -f2-)"
