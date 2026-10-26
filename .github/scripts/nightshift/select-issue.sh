#!/usr/bin/env bash
# Selects the oldest eligible nightshift issue and prints it as one JSON object,
# or prints null when nothing is eligible. Filter logic lives in select.jq.
set -euo pipefail

repo="${GITHUB_REPOSITORY:?GITHUB_REPOSITORY is not set}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

issues_json="$(gh issue list --repo "$repo" --state open --limit 100 \
  --json number,title,labels,body)"

in_flight="$(gh pr list --repo "$repo" --state open --limit 100 \
  --json closingIssuesReferences --jq '.closingIssuesReferences[].number' || true)"

printf '%s' "$issues_json" | jq -c --arg in_flight "$in_flight" -f "$script_dir/select.jq"
