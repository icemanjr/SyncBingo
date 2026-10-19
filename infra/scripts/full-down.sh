#!/usr/bin/env bash
# Removes the Full profile and checks nothing billable is left (docs/10 §6). Safe to run again.
# Keeps the CDK bootstrap stack (CDKToolkit): it costs cents and the next deploy needs it.

# shellcheck source-path=SCRIPTDIR source=common.sh
source "$(dirname "$0")/common.sh"

need aws npx dotnet python3
account=$(signed_in_account)

say "Removing SyncBingo Full ($stage) from account $account in $AWS_REGION"
if [[ -f $started ]]; then
  python3 - "$(cat "$started")" << 'HOURS'
import sys, datetime
start = datetime.datetime.fromisoformat(sys.argv[1].replace('Z', '+00:00'))
hours = (datetime.datetime.now(datetime.timezone.utc) - start).total_seconds() / 3600
print(f"It has been up for {hours:.1f} hours: about ${hours * 0.083:.2f} (docs/10 §5c).")
HOURS
fi

say "1/2 Destroying the stacks (CloudFront is disabled first; this takes several minutes)"
cdk destroy --all --force

say "2/2 Checking nothing is left"
left=0
# check <what> <command...>: the command lists what is left; an error counts as "can't tell", not as gone.
check() {
  local what=$1 found
  shift
  if ! found=$("$@" 2>&1); then
    printf '  \033[33mcan'"'"'t check\033[0m  %s: %s\n' "$what" "$found"
    left=1
  elif [[ -n $found && $found != None ]]; then
    printf '  \033[31mstill there\033[0m  %s: %s\n' "$what" "$found"
    left=1
  else
    printf '  \033[32mgone\033[0m         %s\n' "$what"
  fi
}

check "CloudFormation stacks" aws cloudformation list-stacks \
  --stack-status-filter CREATE_COMPLETE UPDATE_COMPLETE DELETE_FAILED ROLLBACK_COMPLETE UPDATE_ROLLBACK_COMPLETE \
  --query "StackSummaries[?starts_with(StackName, 'SyncBingo-$stage-')].StackName" --output text
# Every resource the stacks make carries these tags (InfraSettings.StackProps). The project's policy denies the
# tagging API in us-east-1, so CloudFront, which is global, is checked through its own API instead.
check "Tagged resources in $AWS_REGION" aws resourcegroupstaggingapi get-resources \
  --tag-filters Key=project,Values=syncbingo Key=stage,Values="$stage" Key=profile,Values=full \
  --query 'ResourceTagMappingList[].ResourceARN' --output text
check "CloudFront distributions" aws cloudfront list-distributions \
  --query "DistributionList.Items[?Comment=='SyncBingo $stage'].Id" --output text
check "Unattached Elastic IPs" aws ec2 describe-addresses \
  --query 'Addresses[?AssociationId==null].PublicIp' --output text

rm -f "$outputs" "$started"
if ((left)); then
  fail "Something is still there: delete it in the console, or run this script again in a few minutes."
fi
say "All gone. The live site is untouched, and the Budgets alert stays in place."
