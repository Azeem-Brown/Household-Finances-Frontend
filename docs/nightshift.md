# Nightshift

Scheduled automation that implements one eligible issue overnight and opens a pull request into `DEV`. It never merges, never promotes a branch, and never touches `QA` or `Prod`.

- Workflow: [`.github/workflows/nightshift.yml`](../.github/workflows/nightshift.yml)
- Selection filter: [`.github/scripts/nightshift/select.jq`](../.github/scripts/nightshift/select.jq)
- Agent instructions: [`.github/nightshift/prompt.md`](../.github/nightshift/prompt.md)
- Label contract: [`labels.md`](./labels.md)

## How a run works

1. Checks out `DEV` on an `ubuntu-latest` runner and installs .NET 10, Node, and the Copilot CLI.
2. Selects the oldest eligible issue and claims it by swapping `nightshift` for `nightshift:running`.
3. Creates the branch `<issue>-<slug>` from `DEV` and builds a prompt from `.github/nightshift/prompt.md` plus the issue body.
4. Runs `copilot -p <prompt> --allow-all-tools --model auto`.
5. Commits and pushes anything the agent left behind, then reuses or creates the pull request into `DEV`.
6. On success, moves the issue to `in development` and comments with the PR number. On failure, moves it to `nightshift:failed` and comments with the last 40 lines of the agent log.
7. Uploads the full agent log as the `nightshift-logs` artifact (7-day retention).

## Eligibility

An issue is picked only when all of these hold, which mirrors [`labels.md`](./labels.md):

- it has both `nightshift` and `agent-ready`;
- it has none of `blocked`, `decision required`, `human-only`, `in development`, `awaiting-review`, `awaiting-qa`;
- its body is longer than 200 characters;
- no open pull request already closes it.

The lowest issue number wins, so the queue is FIFO by issue number.

## One-time setup

1. **Create a token.** A classic PAT with `repo` scope works; a fine-grained PAT is better. Give it access to this repository only, with:
   - Contents: read and write (push branches)
   - Pull requests: read and write (open PRs)
   - Issues: read and write (labels and comments)
   - Metadata: read

   The account must have an active Copilot subscription. The workflow's built-in `GITHUB_TOKEN` is not used, because it cannot call Copilot and pushes made with it do not trigger other workflows.
2. **Add the secret.** Repository settings, Secrets and variables, Actions, new repository secret named `NIGHTSHIFT_TOKEN`. Never paste the token into an issue, a PR, or a file.
3. **Enable the workflow.** Merge this pull request, then check the Actions tab shows `Nightshift`. Scheduled workflows only start once the workflow file exists on the default branch.

Nothing runs until step 3. An unmerged workflow file is inert.

## Running it manually

Actions, Nightshift, Run workflow:

- `dry_run` defaults to **true**: it selects and claims an issue, posts a comment, then releases the issue without touching code. Use this to check the queue.
- Set `dry_run` to false to perform a real run.
- `max_issues` controls how many attempts happen in one run. The default is 1; each attempt can take up to the workflow timeout of 150 minutes.

## Turning it off

- `gh workflow disable nightshift` stops the schedule immediately.
- Removing the `nightshift` label from every issue empties the queue without disabling the workflow.
- Deleting the `NIGHTSHIFT_TOKEN` secret makes every run fail at the preflight step, which fails loudly rather than silently.

## Cost and limits

- Each run consumes Copilot premium requests for the model used by `--model auto`.
- One issue per run by default, capped by the job timeout of 150 minutes.
- The runner has no access to your machine, so nothing depends on your laptop being awake. This is the main reason the workflow exists.

## Security notes

- The token can push to this repository. Keep it scoped to this repository and rotate it if it is ever exposed.
- `--allow-all-tools` lets the agent run shell commands in the runner. That is required for non-interactive mode, and it is confined to an ephemeral runner with no secrets beyond the token.
- The prompt forbids reading tokens from the environment and passing them to other tools. That is guidance, not a technical control; the real control is keeping the token narrowly scoped.
- The agent may edit workflow files. Review the diff before merging any PR it opens, especially changes under `.github/`.

## Porting to the backend repository

The workflow is repo-agnostic except for validation commands. To add it to `Household-Finances-Backend`:

1. Copy `.github/workflows/nightshift.yml`, `.github/scripts/nightshift/`, `.github/nightshift/prompt.md`, and this document.
2. Change the validation commands in `.github/nightshift/prompt.md` to the backend build and test commands.
3. Add the `NIGHTSHIFT_TOKEN` secret in that repository as well, and keep it separate from this one.

## Self-hosted alternative

To run on your own always-on machine instead of hosted runners, set `runs-on: [self-hosted, windows]`, keep the checkout and script steps, and replace the `setup-dotnet` / `setup-node` / install steps with the tools already on that machine. The scripts are plain bash, so a Windows runner needs `bash` available (Git for Windows provides it).

## Troubleshooting

| Symptom | Cause | Fix |
| --- | --- | --- |
| Preflight fails with "NIGHTSHIFT_TOKEN is missing" | Secret not created, or created in an environment rather than repository scope | Create the repository secret |
| Run selects nothing | No issue has both `nightshift` and `agent-ready`, or every candidate is blocked | Check the label contract and the issue bodies |
| Run ends with `nightshift:failed` | Agent crashed, ran out of time, or produced no commits | Read the log artifact and the issue comment, then either fix the issue or relabel it |
| PR opens but CI is missing | This repository has no CI workflow yet (issue #25) | Review the diff manually until CI exists |
| Two runs overlap | `concurrency` should prevent this; a cancelled run can leave `nightshift:running` behind | Reclaim the stale label after 24 hours |
