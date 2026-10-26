# Household Finances — Frontend

MudBlazor frontend for the Household Finances family expense planner.

## Stack

- .NET 10, Blazor **Server** hosting model (decided in issue #1)
- MudBlazor component library
- Consumes the API in [Household-Finances-Backend](https://github.com/Azeem-Brown/Household-Finances-Backend)

## Project structure

- `HouseholdFinances.slnx` — solution file (repository root)
- `src/HouseholdFinances.Frontend/` — Blazor Server (interactive server rendering) app
  - `Components/Pages/` — routable pages
  - `Components/Shared/` — reusable (non-routable) components
  - `Components/Layout/` — app shell, navigation, and MudBlazor providers
  - `Services/` — frontend-only client services

## Build and run

```powershell
dotnet build HouseholdFinances.slnx
dotnet run --project src/HouseholdFinances.Frontend
```

## Local setup and authentication

The frontend signs the user in with Google Identity Services in the browser, then exchanges the
Google ID token for the API-issued token pair server to server (so the API tokens never reach the
browser). To run it locally:

1. Configure the Google OAuth client id the frontend should use. It is configuration, not a secret,
   and is left empty in `appsettings.json`, so supply it locally with user secrets (or the
   `Authentication__Google__ClientId` environment variable):

   ```powershell
   dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>.apps.googleusercontent.com" --project src/HouseholdFinances.Frontend
   ```

   It must match the client id the backend validates the token audience against
   (`Authentication:Google:ClientId` in the backend).

2. Register the development origin with the Google OAuth client. In the Google Cloud console, add
   the origin the browser will use to the client's **Authorized JavaScript origins**:

   - `http://localhost:5033` (the `http` launch profile)
   - `https://localhost:7073` (the `https` launch profile)

   Google matches the origin exactly, including scheme and port, so `http://localhost:5033` and
   `https://localhost:7073` are different origins as far as Google is concerned. Register both if
   you use both profiles.

3. Run the backend API first (see the backend README) so the frontend has an API to exchange the
   token with and to read the current user profile from. The frontend reads the API base URL from
   `Api:BaseUrl` (`appsettings.json`, default `http://localhost:5252`).

4. Run the frontend and click "Sign in with Google" on `/login`. A user with no household is routed
   to `/setup`; a user with at least one household is routed to `/dashboard`.

For local testing without real Google credentials, the backend registers a development-only Google
token validator that accepts a locally generated token prefixed with `dev:` (for example
`dev:local-user-1`); its subject becomes the signed-in user. That bypass is Development-only and
backend issue #24 tightens it to require an explicit opt-in - see the backend README for how it is
currently enabled. It exercises the API directly; the frontend button still uses real Google
Identity Services and therefore needs a configured client id.

### Troubleshooting sign-in

If sign-in fails and no request reaches the API, the page origin is most likely not registered with
the Google OAuth client. Google Identity Services rejects the request in the browser, so nothing is
sent to the backend and the failure looks like an application bug. The browser console shows one of:

- `FedCM get() rejects with NetworkError: Error retrieving a token.`
- an origin-not-allowed message naming the unregistered origin

Register the exact origin the browser address bar is on (scheme and port included) under
**Authorized JavaScript origins** for the client, as described in step 2.

## Branching

| Branch | Purpose |
| --- | --- |
| `main` | Default branch; release history |
| `DEV` | Integration branch — **all pull requests target `DEV`** |
| `QA` | QA promotion |
| `Prod` | Production |

Feature branches are created from `DEV` and named after the issue they implement, for example `issue-6-scaffold`.

Every pull request into `DEV` is gated by the `CI` GitHub Actions workflow
(`.github/workflows/ci.yml`), which restores, builds in Release, and runs the test suite on
`ubuntu-latest`. A broken build or failing test fails the check before review.

## Documentation

- Product specification: [`Family Expense Planner.md`](./Family%20Expense%20Planner.md)
- Label conventions: [`docs/labels.md`](./docs/labels.md)
- Work items: [frontend issues](https://github.com/Azeem-Brown/Household-Finances-Frontend/issues)
