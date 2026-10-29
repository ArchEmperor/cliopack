# cliopack roadmap

MVP = M0-M4: pick an environment, find packages, select them, get files on disk. Rules and stack: [AGENTS.md](AGENTS.md).

## M0 - Scaffold

- [x] `cliopack.slnx`, `src/Cliopack`, `tests/Cliopack.Tests`
- [x] `Directory.Build.props`: net10.0, nullable, warnings as errors; `Directory.Packages.props` with central versions
- [x] Global tool props: `PackAsTool`, `ToolCommandName=cliopack`
- [x] `.gitignore`, empty main window that opens and closes on Esc

Done: `dotnet build`, `dotnet test`, `dotnet run` work; the window shows and exits cleanly.

## M1 - clio adapter + spikes

- [x] `Clio/`: run clio via CliWrap, stream stdout/stderr, cancel by killing the process tree
- [x] Envelope parsing into typed results; clio errors become readable messages
- [x] Startup check: clio on PATH and version >= 8.1, else show the install/update command
- [x] Mask `-p` values in every logged command line
- [x] Capture fixtures from a real environment (secrets removed)

Spikes - record outcomes in [Decisions](#decisions):

- [x] S1: does `pull-pkg A,B,C` work without cliogate? clio code says the sync path uses the core `PackageInstallerService`, which needs no cliogate. If it works, Basic mode can batch too.
- [x] S2: is a bundle repacked from per-package zips identical in structure to a batch zip, and does `push-pkg` install it?
- [x] S3: `execute-sql-script -v json` output shape; SQL for `SysPackage.ModifiedOn` and `SysPackageDependency` on MSSQL and PostgreSQL
- [x] S4: can Terminal.Gui v2 hand the terminal to `clio env-ui` and take it back? Fallback: open it in a new terminal window.
- [x] S5: does `pull-pkg --unzip` with many packages hit the 255-char file-name limit?

Done: adapter is covered by tests on fixtures; all spikes have a recorded decision.

## M2 - Environments

- [x] Environment list: name, URL, active marker
- [x] Register form: name, URL, login, password -> `reg-web-app`; clio errors are shown in the form
- [x] "Manage in clio" action -> `clio env-ui` (per S4)

Manual check: register a real environment, see it in the list, open env-ui and come back.

## M3 - Packages

- [x] Load via `list-packages`, cache per environment, refresh key
- [x] Table with checkboxes; selection survives filtering and sorting
- [x] Case-insensitive name search
- [x] `Terrasoft` / `Creatio` hidden by default, toggle to show
- [x] Sort by name, maintainer, version
- [x] Mode badge: Basic / Gate

Manual check: on an environment with several hundred packages, search and selection stay responsive.

## M4 - Export

- [x] Download: one `<Package>.zip` per package, split from the batch zip (S1, S2)
- [x] Bundle: the batch zip itself, name defaults to `bundle`, editable
- [x] Unzip: each `.gz` of the batch zip via `extract-pkg-zip`, never `pull-pkg --unzip` (S5)
- [x] Batch `pull-pkg` in both modes: sync, chunked to stay under the 10-min timeout (`--async` dropped, see Decisions)
- [x] "One by one" switch (compatibility): one `pull-pkg` per package
- [x] Output to the current directory; existing target -> overwrite / rename / skip
- [x] Log panel with live clio output, cancel at any time
- [x] Result summary per package (ok / failed), checked by `<Name>.gz` entries on disk, not by exit code

Manual check: all three actions on Basic and Gate environments; cancel mid-way leaves no half-written files. Exporter checked live on Basic (all three actions, unknown package, cancel) and on Gate (all three actions, batch and one by one, unknown package). TUI still open: F9 with nothing selected shows a hint; Download / Bundle (custom name) / Unzip, with and without One by one; conflict prompt (Overwrite / Rename / Skip); Esc during a download closes the dialog after cleanup.

## M5 - Gate extras

- [x] Modified date column + sort
- [x] Dependencies of the highlighted package in a side panel (display only)
- [x] Basic mode: both features disabled with a `requires cliogate` hint
- [ ] S3 follow-up: run both queries through `execute-sql-script` on a Gate environment. PostgreSQL done: 847 rows in ~1 s, dates come as `2023-09-08T08:43:15` (no zone, UTC) and show in local time. MSSQL still open

Manual check (TUI, open): on Gate the Modified column shows, F7 cycles through Modified, a header click sorts, the dependency pane follows the cursor and survives filtering; on Basic the `requires cliogate` hint.

## M6 - Presets and UX polish

- [x] Presets: named package sets per environment; save the selection, load (replaces the selection, lists missing names), delete
- [x] Key hints in the status bar, F1 help screen

Manual check (TUI, open): F12 saves, loads and deletes a preset; loading on another environment lists missing packages; F1 shows the keys.

## M7 - Script mode

- [x] `cliopack pull -e <env> (-p A,B | --preset <name>) [--mode download|bundle|unzip] [--name <bundle>] [--one-by-one] [--on-conflict overwrite|rename|skip]`, `cliopack --help`
- [x] Exit codes: 0 all ok, 1 any package failed or a target exists without `--on-conflict`, 2 usage error, 130 Ctrl+C

Checked live: help, usage error, unzip with an unknown package (exit 1), conflict stop. Manual check (open): Ctrl+C during a pull exits 130 and leaves no `.cliopack-*` folder.

## M8 - Packaging

- [x] Tool packages: a pointer package, one Native AOT package per RID (win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64) and an `any` fallback. Install from a local feed picks `cliopack.win-x64` on Windows
- [x] Native AOT: 12 MB `cliopack.exe` with no trim warnings (Terminal.Gui 2.5 needs no workaround, #5069 fixed). JSON goes through source-generated contexts; `IsAotCompatible` keeps the analyzers on in every build. Checked on Windows: help, live `pull --preset`, runs without `libonigwrap.dll`
- [x] Self-contained single file as the cross-OS fallback (84 MB, `libonigwrap.dll` next to it)
- [x] README: requirements, install, keys, export, presets, scripts

Manual check: the AOT binary's TUI passed on Windows.

## M9 - Release 0.1.0

- [x] Package metadata (MIT, ArchEmperor, 0.1.0, README in the package), LICENSE
- [x] History squashed into an initial release commit
- [x] CI: test and format on Windows and Linux for every push and PR (`ci.yml`). Tests also pass in a Linux container
- [x] Release on a `v*` tag (`release.yml`): native archives on GitHub Releases, packages pushed to NuGet via trusted publishing. Linux AOT pack and install checked in Docker; the workflows pass actionlint but have not run on GitHub yet
- [ ] Repository public, nuget.org trusted publishing policy, first tag `v0.1.0`

## Decisions

| Topic | Decision |
|---|---|
| Stack | C# / .NET 10 + Terminal.Gui v2, global tool |
| Why .NET 10 | Terminal.Gui 2.x ships net10.0 only; .NET 8 and 9 reach end of support on 2026-11-10; .NET 10 LTS runs to 2028-11 |
| Auth | login/password only |
| Output | current directory, ask on conflict |
| Dates, dependencies | Gate mode only, disabled in Basic; no clio changes |
| S1 batch without cliogate | Works: `pull-pkg A,B,C` on a Basic env gives one zip with `A.gz`, `B.gz`, `C.gz`. Both modes batch the same way |
| S2 bundle structure | Batch zip and single zips are flat `<Name>.gz` entries with byte-identical `.gz`. A bundle repacked with .NET `ZipArchive` installs via `push-pkg`. Bundle = batch zip; Download = batch zip split per package |
| S3 SQL | Needs cliogate >= 2.0.0.41; `-v json` prints a raw JSON array of row objects, no envelope. Portable SQL (quoted identifiers), checked on PostgreSQL 16: `SELECT "UId", "ModifiedOn" FROM "SysPackage"` and `SELECT p."Name" AS "Package", d."Name" AS "DependsOn" FROM "SysPackageDependency" pd JOIN "SysPackage" p ON p."Id" = pd."SysPackageId" JOIN "SysPackage" d ON d."Id" = pd."DependOnPackageId"` - load all dependencies once per env and cache. Live clio run on PostgreSQL done; MSSQL open (M5) |
| S4 env-ui hand-off | Terminal.Gui 2.5 cannot lend the terminal to a child (`Suspend` is Unix SIGTSTP only). Plan: end the TUI session (dispose `IApplication`), run `clio env-ui` in the same console, re-create the app; state lives outside views. Fallback: new terminal window. Open: manual check in M2 |
| Presets | Package sets, not filters (the filter is only search, system toggle and sort). Per environment, chosen by the user. Stored in `<AppData>/cliopack/presets.json`, re-read on every call so parallel cliopacks keep each other's edits. Save replaces a same-named preset without asking |
| Packaging | One `dotnet tool install` for everyone: Native AOT package per RID behind a pointer package, `any` (framework-dependent) for other platforms. Release archives for machines without .NET. AOT builds only on its own OS, so CI builds each RID on a matching runner; osx-x64 is cross-built on the arm64 runner. `libonigwrap` (Terminal.Gui native dependency) ships next to the binary. MIT, version in the csproj |
| Script mode | Hand-rolled parser: one command, no extra dependency. Same `ExportPlan` and `PackageExporter` as the TUI. Without `--on-conflict` an existing target stops the run before any clio call: a script cannot be asked |
| Package cache | In memory per environment for the session; F5 reloads; dropped after `clio env-ui` (URL or credentials may have changed) |
| Register | No `--checkLogin`: a wrong password shows up when packages load. Existing names are rejected, editing goes to env-ui |
| Keys | Enter loads an environment, F2 register, F3 env-ui, F4 system packages, F5 refresh, F7 sort, F8 reverse, F9 export, F12 presets, F1 help, Space select, F6 switch pane |
| Export | Staged in `<out>/.cliopack-<guid>/`, moved out only at the end, staging deleted on cancel. Conflicts asked once for all targets. A bundle keeps the packages that downloaded; failed ones are listed. Unzip runs `extract-pkg-zip` once per batch zip into a fresh folder |
| `pull-pkg --async` | Dropped. On the dev Gate env (cliogate 2.0.0.53) it fails even for valid packages (`not completed`); its cliogate endpoints `rest/PackagesGateway/DeleteExistsPackagesZip` and `ExistsPackageZip` return error pages (checked with `clio call-service`). Both modes use the chunked sync path |
| One by one | Export dialog switch, off by default: one `pull-pkg` per package (chunk size 1). From planning: download without cliogate one at a time. S1 showed batching works there, so it stays an opt-in fallback for environments where batch export fails. Live on Gate, 3 packages + 1 unknown: batch 21 s, one by one 38 s |
| Chunk size | 10 packages per sync `pull-pkg`. Not measured: a 40-package timing run took too long and was dropped; 5 small packages take ~20 s, of which ~8 s is clio start + login |
| Gate details | One SQL call per environment after the list shows: `SysPackage` LEFT JOIN `SysPackageDependency`, rows keyed by `UId`. Cached with the list; F5 reloads both |
| S5 `--unzip` limit | Fails: batch `--unzip` writes a temp `<A,B,C...>.zip`, names over 255 chars -> `not completed`, exit 1. With an explicit short `-d file.zip` the same batch works. Never use `--unzip` |

## Out of scope for now

- OAuth environments
- Pushing packages to environments
- Editing environments inside cliopack (delegated to `clio env-ui`)

## After the initial release (ideas)

Both change the environment: confirm before running, Gate mode only.

- Maintainer: show the environment's `Maintainer` system setting and change it (`clio set-syssetting`); mark packages whose maintainer matches it (editable ones).
- Lock / unlock: an install type column (`SysPackage.InstallType`: 1 locked, 0 unlocked, via the details SQL) and lock / unlock for the selection via `clio lock-package` / `unlock-package` (cliogate). Unlock sets the package Maintainer to the system setting and keeps the original in Description; lock restores it.
