# Household Finances — Frontend

MudBlazor frontend for the Household Finances family expense planner.

## Stack

- .NET 10, Blazor **Server** hosting model (decided in issue #1)
- MudBlazor component library
- Consumes the API in [Household-Finances-Backend](https://github.com/Azeem-Brown/Household-Finances-Backend)

## Branching

| Branch | Purpose |
| --- | --- |
| `main` | Default branch; release history |
| `DEV` | Integration branch — **all pull requests target `DEV`** |
| `QA` | QA promotion |
| `Prod` | Production |

Feature branches are created from `DEV` and named after the issue they implement, for example `issue-6-scaffold`.

## Documentation

- Product specification: [`Family Expense Planner.md`](./Family%20Expense%20Planner.md)
- Work items: [frontend issues](https://github.com/Azeem-Brown/Household-Finances-Frontend/issues)
