# Shared by full-up.sh and full-down.sh: settings and small helpers. Sourced, not run.
# shellcheck shell=bash disable=SC2034  # the variables are used by the scripts that source this

set -euo pipefail

# The project's only Region (docs/10 §3), the stage the demo uses, and the CDK CLI CI uses.
export AWS_REGION=us-east-2
export AWS_DEFAULT_REGION=$AWS_REGION
export CDK_DEFAULT_REGION=$AWS_REGION
stage=dev
cdk_version=2.1145.0

# The .NET SDK's own installer puts it in ~/.dotnet, which a script's PATH may lack.
if [[ -x $HOME/.dotnet/dotnet ]]; then
  PATH=$HOME/.dotnet:$PATH
fi

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
infra=$repo/infra
outputs=$infra/cdk.out/full-outputs.json
started=$infra/cdk.out/full-started-at

say() { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
fail() { printf '\033[31mError: %s\033[0m\n' "$*" >&2; exit 1; }

cdk() { (cd "$infra" && npx --yes "aws-cdk@$cdk_version" "$@" -c profile=full -c stage="$stage"); }

need() {
  for tool in "$@"; do
    command -v "$tool" > /dev/null || fail "$tool is not installed."
  done
}

# Signed in, and to which account. aws login sessions last 12 hours.
signed_in_account() {
  aws sts get-caller-identity --query Account --output text 2> /dev/null \
    || fail "Not signed in to AWS. Run: aws login --region $AWS_REGION"
}

# One output of a deployed stack, from the file cdk deploy wrote.
output() {
  local stack=$1 key=$2
  python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))[sys.argv[2]][sys.argv[3]])' \
    "$outputs" "SyncBingo-$stage-$stack" "$key"
}
