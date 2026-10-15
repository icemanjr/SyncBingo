#!/usr/bin/env bash
# Updates the live site to the newest published build (docs/07 §1a). A systemd timer runs it off-hours;
# run it by hand for an urgent fix. Lives in /opt/syncbingo next to `.env` (SITE_DOMAIN=syncbingo.com).
#
# Both images are started by the commit they were built from, with that commit's Compose file, so the
# API, the web app and the Compose settings always match. If anything is missing, nothing is restarted.
set -euo pipefail
cd "$(dirname "$0")"

registry=ghcr.io/icemanjr
docker pull --quiet "$registry/syncbingo-api:latest" >/dev/null
revision=$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$registry/syncbingo-api:latest")
if [[ ! $revision =~ ^[0-9a-f]{40}$ ]]; then
  echo "The latest API image has no commit label ('$revision'); not updating." >&2
  exit 1
fi

current=$(cat .revision 2>/dev/null || true)
if [[ $revision == "$current" ]]; then
  echo "Already running $revision."
  exit 0
fi

curl --fail --silent --show-error --location --output docker-compose.lean.yml.new \
  "https://raw.githubusercontent.com/icemanjr/SyncBingo/$revision/docker-compose.lean.yml"

export IMAGE_TAG="sha-$revision"
compose() { docker compose --file docker-compose.lean.yml.new "$@"; }
compose pull --quiet
mv docker-compose.lean.yml.new docker-compose.lean.yml
# Only containers whose image or settings changed restart; players reconnect and resync (ADR 0002).
docker compose --file docker-compose.lean.yml up --detach --remove-orphans
echo "$revision" > .revision
docker image prune --force >/dev/null
echo "Now running $revision (was ${current:-nothing})."
