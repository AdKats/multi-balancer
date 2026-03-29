# MULTIbalancer — Procon v2 Plugin

## Project Overview

MULTIbalancer is a Procon v2 plugin that provides advanced team balancing for Battlefield servers. It supports skill-based balancing, clan stacking prevention, and configurable balancing rules per map/mode.

- **Language:** C#
- **License:** GPLv3
- **Supported games:** BF3, BF4, BFHL
- **Original author:** PapaCharlie9
- **Maintainer:** Prophet731
- **Dependencies:** Procon v2 (runtime only)

## Architecture

| File | Responsibility |
|------|---------------|
| `src/MULTIbalancer.cs` | Main entry point, plugin metadata, lifecycle, core state |
| `src/MULTIbalancer/Description.cs` | HTML plugin description (const string) |
| `src/MULTIbalancer/Settings.cs` | Plugin variables UI and persistence |
| `src/MULTIbalancer/Events.cs` | Procon event handlers |
| `src/MULTIbalancer/Balancer.cs` | Core balancing logic, team analysis |
| `src/MULTIbalancer/Battlelog.cs` | Battlelog stats fetching |
| `src/MULTIbalancer/Models.cs` | Data model classes |
| `src/MULTIbalancer/Support.cs` | Helper methods, logging, utilities |

## Code Style

See the master `CLAUDE.md` at the procon_plugins root for shared conventions.

## Build & CI

- `MULTIbalancer.csproj` at root is a **CI-only artifact** for `dotnet format`.
- **CI workflow**: `dotnet format` checks on push/PR to master.
- **Release workflow**: tag-triggered release packaging.

## Threading Model

Uses dedicated threads for balancing operations with Timer-based periodic checks.

## Supported Games

- BF3, BF4, BFHL

## Branch Structure

- `master` — current development, Procon v2 only
- `legacy` — archived pre-refactor code
