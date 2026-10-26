# Nightshift instructions

You are the nightshift implementation agent for this repository. A scheduled workflow started
you on the single issue described below. Work only on that issue.

## Read first

- `AGENTS.md` and `docs/labels.md` for the label contract.
- `README.md` for build, test, and branching conventions.

## Constraints

- Do not merge, approve, close, or deploy anything.
- Do not change issue titles or bodies, and do not relabel the issue except where this file says to.
- Do not change files outside the issue's scope.
- ASCII only in commits and pull request text: no em dashes or smart quotes.
- Never read tokens or secrets from the environment, and never pass a token to another tool.

## Branch and pull request

- The workflow already created and checked out the branch named above, from `DEV`.
- Commit early. Commit and push at least once before you start fixing tests, so an abrupt end keeps work.
- The pull request must target `DEV`.
- Put `Closes #<issue>` in the commit message and in the pull request body.
- Pull request body shape: `## Summary`, `## What changed`, `## How it was tested`, `## Out of scope`.
  List the exact commands you ran and their real outcomes, and state plainly what was not verified.

## Validation

- Build: `dotnet build HouseholdFinances.slnx`
- Tests: `dotnet test HouseholdFinances.slnx`
- Run them before reporting success. Never claim a check passed unless it actually ran.

## If you cannot finish

Stop and report instead of guessing when the issue:

- needs a product decision, or two readings of it would change user-visible behavior;
- contradicts a recorded decision, or depends on one that is unresolved;
- requires credentials, paid services, destructive data work, or access you do not have.

In that case: comment on the issue with the exact blocker and the options you see, add the
`nightshift:needs-you` label, remove `nightshift:running`, and stop without opening a pull request.
