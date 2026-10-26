#!/usr/bin/env bash
# Runs up to MAX_ISSUES nightshift attempts. One issue per iteration:
# claim, branch, implement with the Copilot CLI, open a pull request into DEV,
# then move the issue to `in development`, `nightshift:needs-you`, or `nightshift:failed`.
set -euo pipefail

repo="${GITHUB_REPOSITORY:?GITHUB_REPOSITORY is not set}"
base="${BASE_BRANCH:-DEV}"
max="${MAX_ISSUES:-1}"
dry_run="${DRY_RUN:-false}"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$script_dir/../../.." && pwd)"
work_dir="${RUNNER_TEMP:-/tmp}/nightshift"
mkdir -p "$work_dir"

completed=0
blocked=0
failures=0

summary() {
  {
    echo "### Nightshift run"
    echo ""
    echo "- Pull requests opened: $completed"
    echo "- Stopped for a decision: $blocked"
    echo "- Failures: $failures"
    echo "- Dry run: $dry_run"
  } >> "${GITHUB_STEP_SUMMARY:-/dev/null}"
}

# Label updates are best effort: the agent may already have moved the issue on.
relabel() {
  local issue="$1"
  shift
  gh issue edit "$issue" --repo "$repo" "$@" \
    || echo "::warning::Could not update labels on #$issue with: $*"
}

for _ in $(seq 1 "$max"); do
  selected="$(bash "$script_dir/select-issue.sh")"
  if [ "$selected" = "null" ] || [ -z "$selected" ]; then
    echo "No eligible issue in the nightshift queue."
    break
  fi

  number="$(printf '%s' "$selected" | jq -r '.number')"
  title="$(printf '%s' "$selected" | jq -r '.title')"
  branch="$(printf '%s' "$selected" | jq -r '.branch')"
  echo "Selected #$number ($title) for branch $branch"

  relabel "$number" --remove-label nightshift --add-label nightshift:running

  if [ "$dry_run" = "true" ]; then
    relabel "$number" --remove-label nightshift:running --add-label nightshift
    gh issue comment "$number" --repo "$repo" \
      --body "Nightshift dry run: this issue would be implemented on branch \`$branch\`. Nothing was changed."
    echo "Dry run: claimed and released #$number."
    break
  fi

  git switch -c "$branch"

  {
    printf '# Nightshift task\n\n'
    printf 'Repository: %s\n' "$repo"
    printf 'Issue: #%s\n' "$number"
    printf 'Title: %s\n' "$title"
    printf 'Branch: %s\n' "$branch"
    printf 'Base branch: %s\n\n' "$base"
    cat "$root/.github/nightshift/prompt.md"
    printf '\n## Issue body\n\n'
    gh issue view "$number" --repo "$repo" --json body --jq .body
  } > "$work_dir/prompt-$number.md"

  agent_status=0
  copilot -p "$(cat "$work_dir/prompt-$number.md")" \
    --allow-all-tools \
    --model auto \
    --silent \
    --log-dir "$work_dir/logs-$number" \
    > "$work_dir/agent-$number.log" 2>&1 || agent_status=$?

  # Keep whatever the agent left uncommitted so an abrupt end still leaves work.
  if [ -n "$(git status --porcelain)" ]; then
    git add -A
    git commit -m "$number: nightshift run" -m "Closes #$number" || true
  fi
  git push --set-upstream origin "$branch" || echo "Nothing to push for #$number."

  current_labels="$(gh issue view "$number" --repo "$repo" --json labels --jq '.labels[].name' || true)"
  if printf '%s\n' "$current_labels" | grep -qx "nightshift:needs-you"; then
    gh issue comment "$number" --repo "$repo" \
      --body "Nightshift stopped without opening a pull request: this needs a decision. See the blocker above."
    blocked=$((blocked + 1))
    git switch "$base" 2>/dev/null || git switch -c "$base" "origin/$base"
    continue
  fi

  pr="$(gh pr list --repo "$repo" --head "$branch" --state open --json number --jq '.[0].number // empty')"
  if [ -z "$pr" ] && [ -n "$(git log "origin/$base..HEAD" --oneline)" ]; then
    pr="$(gh pr create --repo "$repo" --base "$base" --head "$branch" \
      --title "$title (#$number)" \
      --body "## Summary

Automated nightshift attempt at #$number: $title.

## What changed

See the commits on this branch.

## How it was tested

Not verified. The automated run did not report a test result; check the nightshift-logs artifact.

## Out of scope

Review required before merge.

Closes #$number")"
  fi

  if [ -n "$pr" ]; then
    relabel "$number" --remove-label nightshift:running --add-label in development
    gh issue comment "$number" --repo "$repo" \
      --body "Nightshift opened #$pr for this issue. Nothing was merged; review the diff."
    completed=$((completed + 1))
  else
    relabel "$number" --remove-label nightshift:running --add-label nightshift:failed
    log_tail="$(tail -n 40 "$work_dir/agent-$number.log" 2>/dev/null || true)"
    gh issue comment "$number" --repo "$repo" \
      --body "Nightshift did not produce a pull request (agent exit code $agent_status). Log tail:

\`\`\`
$log_tail
\`\`\`"
    failures=$((failures + 1))
  fi

  git switch "$base" 2>/dev/null || git switch -c "$base" "origin/$base"
done

summary
echo "Nightshift finished: $completed pull request(s), $blocked blocked, $failures failure(s)."
test "$failures" -eq 0
