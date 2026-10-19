#!/usr/bin/env bash
# Deploys the Full profile (docs/07 §2–3) for a short demo, uploads the web app and checks it answers.
# The live site (Lean) is not touched: Full is a separate copy on CloudFront's own address.
#
#   infra/scripts/full-up.sh                          then test it, then: infra/scripts/full-down.sh
#   ALARM_EMAIL=you@example.com infra/scripts/full-up.sh     (also emails the alarms)
#
# It costs about $0.08 an hour (about $2 a day) until full-down.sh removes it (docs/10 §5c).
# CDK still asks before it creates IAM roles or opens security groups; answer y.

# shellcheck source-path=SCRIPTDIR source=common.sh
source "$(dirname "$0")/common.sh"

need aws npm npx dotnet python3 curl
account=$(signed_in_account)

say "Deploying SyncBingo Full ($stage) to account $account in $AWS_REGION"
echo "This creates billed resources: about \$0.08 an hour until infra/scripts/full-down.sh runs."
read -r -p "Go ahead? [y/N] " answer
[[ $answer == [yY]* ]] || fail "Stopped; nothing was created."

say "1/5 CDK bootstrap (once per account and Region; a no-op after that)"
cdk bootstrap "aws://$account/$AWS_REGION"

say "2/5 Deploying the stacks (CloudFront takes 5–15 minutes of this)"
mkdir -p "$infra/cdk.out"
date -u +%Y-%m-%dT%H:%M:%SZ > "$started"
context=()
if [[ -n ${ALARM_EMAIL:-} ]]; then
  context=(-c "alarmEmail=$ALARM_EMAIL")
fi
cdk deploy --all "${context[@]}" --outputs-file "$outputs"

bucket=$(output Web BucketName)
distribution=$(output Web DistributionId)
site=$(output Web SiteUrl)

say "3/5 Building the web app (the Full build shows the Serious Bingo teaser, ADR 0011)"
(cd "$repo/web" && npm ci --no-audit --no-fund && VITE_SERIOUS_BINGO_TEASER=true npm run build)

say "4/5 Uploading it to s3://$bucket"
# Hashed assets never change, so they cache for a year; pages must be revalidated (as Caddy does).
aws s3 sync "$repo/web/dist" "s3://$bucket" --delete --exclude index.html \
  --cache-control "public, max-age=31536000, immutable"
aws s3 cp "$repo/web/dist/index.html" "s3://$bucket/index.html" --cache-control "no-cache"
aws cloudfront create-invalidation --distribution-id "$distribution" --paths "/index.html" > /dev/null

say "5/5 Waiting for the site and the API to answer through CloudFront"
for _ in $(seq 1 60); do
  page=$(curl -s -o /dev/null -w '%{http_code}' "$site/" || true)
  # A made-up game: the API itself answers with a problem document, so the whole path works.
  api=$(curl -s -o /dev/null -w '%{http_code} %{content_type}' "$site/api/sessions/aaaaaaaaaaaaaaaa/join-info" || true)
  if [[ $page == 200 && $api == *problem+json* ]]; then
    break
  fi
  sleep 10
done
[[ $page == 200 && $api == *problem+json* ]] \
  || fail "The site didn't answer in 10 minutes (page: $page, API: $api). Check the ECS service's tasks, then run full-down.sh."

say "Full is up: $site"
cat << NEXT

Try it:
  open $site                    host a game on a laptop, join from a phone
  E2E_BASE_URL=$site npx --prefix e2e playwright test        (from e2e/, with Playwright installed)
  k6 run -e BASE=$site -e GAMES=3 load/bingo.js
  Failover: during a game, stop one task in the ECS console (cluster in SyncBingo-$stage-App);
            the players reconnect to the other task and the game carries on.
  Dashboard: CloudWatch > Dashboards > SyncBingo-$stage

When done, the same day:  infra/scripts/full-down.sh
NEXT
