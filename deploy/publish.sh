#!/usr/bin/env bash
# Build the API on this Mac and ship it to the server. Usage: ./deploy/publish.sh <user>@<server>
# Self-contained build, so the server needs no .NET install. Run setup-server.sh on the server once first.
set -euo pipefail

TARGET="${1:?usage: publish.sh <user>@<server>}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/deploy/build"

case "$(ssh "$TARGET" uname -m)" in
  x86_64) RID=linux-x64 ;;
  aarch64) RID=linux-arm64 ;;
  *) echo "Unsupported server architecture"; exit 1 ;;
esac

echo "==> Building for $RID"
rm -rf "$OUT"
dotnet publish "$ROOT/api/SeatCharter.Api.csproj" -c Release -r "$RID" --self-contained -o "$OUT" -v quiet
rm -rf "$OUT/.env" "$OUT/logs"   # local secrets and logs never leave this machine

echo "==> Uploading"
ssh "$TARGET" "mkdir -p ~/pinpoint-upload"
rsync -az --delete "$OUT/" "$TARGET:~/pinpoint-upload/"

echo "==> Installing and restarting"
ssh "$TARGET" 'sudo rsync -a --delete ~/pinpoint-upload/ /opt/pinpoint/api/ && sudo chown -R pinpoint:pinpoint /opt/pinpoint && sudo systemctl restart pinpoint-api && for i in $(seq 1 30); do curl -fsS http://127.0.0.1:5080/health 2>/dev/null && echo " <- API healthy" && exit 0; sleep 1; done; echo "API not healthy after 30 s: sudo journalctl -u pinpoint-api -n 50"; exit 1'
