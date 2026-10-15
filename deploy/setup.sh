#!/usr/bin/env bash
# First-boot setup of the live server (Lean profile, docs/07 §1a): Ubuntu 24.04 LTS on Lightsail.
#
# Given to the instance as its launch script, so it runs once, as root. It prepares everything but does not
# start the site: once DNS points at the server (Caddy needs that for its certificate), run
#   sudo /opt/syncbingo/update.sh
# Safe to run again by hand (sudo bash setup.sh); each step checks whether it is already done.

# Lightsail puts its own lines in front of a launch script and runs the whole file with /bin/sh, ignoring the
# #! line above. So under sh, run the rest of this file (after the marker below) with bash instead.
if [ -z "${BASH_VERSION:-}" ]; then
  exec bash -c "$(sed '1,/^# Bash from here on\.$/d' "$0")"
fi
# Bash from here on.

set -euxo pipefail

site_domain=syncbingo.com
time_zone=America/Denver # the nightly update runs at 04:00 here
repo=https://raw.githubusercontent.com/icemanjr/SyncBingo/master

timedatectl set-timezone "$time_zone"

# 1 GB of swap on a 1 GB machine: a memory spike slows things down instead of killing a container.
if [[ ! -f /swapfile ]]; then
  fallocate -l 1G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

# Prefer RAM, use swap only under pressure; and let Valkey fork for its append-only rewrites (it warns otherwise).
cat > /etc/sysctl.d/90-syncbingo.conf <<'EOF'
vm.swappiness = 10
vm.overcommit_memory = 1
EOF
sysctl --system

# Docker Engine and the Compose plugin, from Docker's own repository (Ubuntu's packages lag behind).
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
# shellcheck source=/dev/null
codename=$(. /etc/os-release && echo "$VERSION_CODENAME")
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $codename stable" \
  > /etc/apt/sources.list.d/docker.list
apt-get update
DEBIAN_FRONTEND=noninteractive apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
systemctl enable --now docker

# The site: the update script, its settings and the nightly timer (docs/07 §1a).
install -d -m 0755 /opt/syncbingo
curl -fsSL "$repo/deploy/update.sh" -o /opt/syncbingo/update.sh
chmod 0755 /opt/syncbingo/update.sh
if [[ ! -f /opt/syncbingo/.env ]]; then
  echo "SITE_DOMAIN=$site_domain" > /opt/syncbingo/.env
fi
for unit in syncbingo-update.service syncbingo-update.timer; do
  curl -fsSL "$repo/deploy/$unit" -o "/etc/systemd/system/$unit"
done
systemctl daemon-reload
systemctl enable --now syncbingo-update.timer

echo "SyncBingo setup done. Once DNS points here, run: sudo /opt/syncbingo/update.sh"
