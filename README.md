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

If signing in succeeds but the session does not survive a reload, check that the page is served over
`https` (the session cookie is `Secure`) and that `Authentication:Session:Directory` is writable.

## Session persistence

A signed-in session survives a full browser reload and an app restart, and no token ever reaches
the browser (issue #28).

- **What is stored, and where.** The browser holds only an opaque, random session identifier in the
  `hf_session` cookie. The API token pair (access and refresh token with their UTC expiries) lives
  server-side, one encrypted file per session, under `Authentication:Session:Directory`. The default
  is `%LOCALAPPDATA%\HouseholdFinances\Frontend\sessions`, outside the repository, and the file name
  is a SHA-256 hash of the identifier so a directory listing does not disclose cookie values.
- **What encrypts it.** Records are encrypted at rest with ASP.NET Core Data Protection under the
  purpose `HouseholdFinances.Frontend.SessionTokenStore.v1`. No key is committed or configured by
  hand: the Data Protection key ring lives in `Authentication:Session:KeyRingDirectory`, defaulting
  to `%LOCALAPPDATA%\HouseholdFinances\Frontend\keys`. Losing the key ring signs everyone out (the
  records become unreadable and are discarded); it never reveals tokens.
  In production the key ring must be a persistent volume shared by every instance and protected at
  rest by the host; an ephemeral container filesystem would sign users out on every restart.
- **Cookie flags.** `HttpOnly`, `Secure`, `SameSite=Lax`, host-only, `Path=/`, and a bounded
  `MaxAge` from `Authentication:Session:CookieMaxAgeDays` (default 7, maximum 90, so it should not
  outlive the API's refresh-token lifetime). Because the cookie is `Secure`, local persistence needs
  the `https` launch profile (`https://localhost:7073`); over plain `http` the browser stores it but
  never sends it back.
- **Refresh-token rotation.** The backend issues a new pair on every refresh and invalidates the
  presented refresh token, so each sign-in and each refresh rewrites the stored record with the
  newly returned pair. If the API rejects the stored refresh token, the record is cleared and the
  user returns to sign-in.
- **Fail closed.** A missing, expired, tampered, or undecryptable record is treated as signed out and
  removed. A Blazor Server circuit cannot read or write cookies, so the cookie is issued on document
  requests by `SessionCookieMiddleware` and the identifier reaches the circuit through
  `SessionStateBridge` (`PersistentComponentState`). Clearing the server-side record signs the user
  out; a cookie left behind is inert and is replaced on the next document request once no live
  circuit still holds it.

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
