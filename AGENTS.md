# cliopack - agent guide

TUI (plus a script CLI) over [clio](../clio) for pulling packages from Creatio environments: pick an environment, filter its packages, select some, then download / bundle / unzip them. Plan and status: [ROADMAP.md](ROADMAP.md).

## Stack

- C# / .NET 10 (Terminal.Gui 2.x ships net10.0 only), `Terminal.Gui` 2.x (TUI), `CliWrap` (runs clio), `System.Text.Json`, xUnit.
- Shipped as a .NET global tool `cliopack` and as a Native AOT binary per OS. Cross-platform: Windows, macOS, Linux.
- UI text, code, comments and docs are in English.

## Commands

```sh
dotnet build
dotnet test                # xunit v3 on Microsoft.Testing.Platform (global.json); VSTest flags like --nologo fail
dotnet run --project src/Cliopack
dotnet run --project src/Cliopack -- pull -e <env> -p A,B   # script mode, see --help
dotnet format --verify-no-changes
dotnet publish src/Cliopack -c Release -r win-x64 -o dist   # Native AOT, only for the OS you are on
dotnet pack src/Cliopack -c Release -r win-x64 -o nupkg     # native tool package; -r any = fallback, no -r = pointer
# Git Bash drops ProgramFiles(x86), so AOT cannot find vswhere: put "/c/Program Files (x86)/Microsoft Visual Studio/Installer" on PATH
```

## Layout

```
src/Cliopack/
  Clio/          the only place that runs clio: process, JSON envelope, version check, secret masking
  Environments/  list, register, hand-off to `clio env-ui`
  Packages/      catalog load + cache, search, maintainer toggle, sort, mode detection
  Export/        download / bundle / unzip, conflict prompt, result verification
  Presets/       named package sets per environment, `<AppData>/cliopack/presets.json`
  Script/        `cliopack pull`: option parsing, run, exit codes
  Shell/         main window, log dialog, shared dialogs and widgets
  Program.cs     TUI by default, subcommands for scripts
tests/Cliopack.Tests/   mirrors src; Fixtures/ holds real clio JSON with secrets removed
```

- Dependencies point `Shell -> features -> Clio`. Features use each other only through public types. No cycles.
- Views stay thin. Filtering, sorting, selection and export planning live in plain classes covered by unit tests.
- Max 500 lines per file, no god objects. Reuse `Shell` widgets instead of building new dialogs.
- Comments: 1-2 lines, only the "why".
- JSON only through source-generated contexts (one per feature, unique names: the generator's file names collide otherwise). `IsAotCompatible` makes any reflection call a build error.

## clio is the source of truth

- Never call Creatio HTTP APIs and never read clio's `appsettings.json`. Everything goes through `Clio/`.
- cliopack never stores passwords. Environments live in clio.
- clio source in `../clio` is reference only. Read one file on demand: `../clio/clio/help/en/<command>.txt` first, then the command source. Never read it in bulk.
- Min clio version 8.1. Check `clio --version` on startup; if clio is missing or older, show the install/update command.

| Need | Command |
|---|---|
| environments | `clio list-environments --json` |
| register | `clio reg-web-app <name> -u <url> -l <login> -p <password>` |
| manage envs | `clio env-ui` (interactive, terminal hand-off) |
| packages | `clio list-packages -e <env> --json` |
| download | `clio pull-pkg A,B,C -e <env> -d <file.zip>` (never `--async`) |
| extract | `clio extract-pkg-zip <batch.zip> -d <dir>` -> `<dir>/<Name>/` per `.gz` |
| SQL (Gate) | `clio execute-sql-script "<sql>" -e <env> -v json -d <file> --silent`, rows in `<file>` |

### clio pitfalls (checked in clio source and on a real environment)

- JSON output is the envelope `{schemaVersion, ok, command, data, error}`. Only `list-environments`, `list-packages`, `healthcheck` support `--json`; `ClioCli.RunJsonAsync` adds it.
- An unknown environment prints no JSON even with `--json`: `[ERR] - <message>` on stderr, exit 1.
- `list-environments` without `--json` prints passwords in plain text. Never call it that way.
- `pull-pkg` exits with 0 when the download failed (`not completed`), and for an unknown package prints `completed` and writes an empty zip. Verify the zip holds `<Name>.gz` for every requested package.
- `pull-pkg` builds file names from comma-joined package names. Always pass an explicit short `-d` path and never use `--unzip` (fails past 255 chars).
- `execute-sql-script` has no envelope and stdout mixes in `[INF]` lines: read the raw JSON row array from the `-d` file (`ClioCli.QueryAsync`). Needs cliogate.
- `extract-pkg-zip` asks y/n via `Console.ReadKey` when a package folder exists (fails with redirected stdin): extract into a fresh folder only.
- Package `.gz` is a Creatio format, not gzip: extract only via clio. The outer download archive is a plain zip.
- `pull-pkg` rejects OAuth authorization-code environments. cliopack supports login/password only.
- Sync download times out after 10 min: both modes split big batches into chunks. `--async` fails on a live Gate environment (`not completed` even for valid packages), so it is not used.
- `list-packages` returns only Name, UId, Maintainer, Version. Modified date and dependencies come from SQL in Gate mode.
- `reg-web-app` probes the site (a failure takes ~25 s) and silently overwrites an existing name: run it cancellable and reject existing names.
- Mask the `-p` value everywhere a command line is logged or shown.

## Modes

Derived once per environment from the package catalog: package `cliogate` or `cliogate_netcore` with version >= 2.0.0.41 means Gate, otherwise Basic. Features read the mode and never re-detect it.

| | Basic | Gate |
|---|---|---|
| download, bundle, unzip | sync batch calls, chunked under the 10-min timeout | same as Basic |
| modified date, dependencies | not available | via `execute-sql-script` |

In Basic mode Gate-only features stay visible but disabled, with a `requires cliogate` hint.

## Product decisions

- Download: one `<Package>.zip` per package, as clio produces it.
- Bundle: one zip with all selected packages. Name defaults to `bundle`, the user can type another.
- Unzip: package folders extracted via clio, for reading schemas.
- Output goes to the current working directory. If targets exist, ask once for all: overwrite / rename / skip.
- Export stages in `<out>/.cliopack-<guid>/` and moves results out only at the end; cancel deletes the staging folder.
- "One by one" switch in the export dialog (off by default): one `pull-pkg` per package, a compatibility path for environments where batch export fails.
- Maintainers `Terrasoft` and `Creatio` are hidden by default; one key shows them.
- Name search: case-insensitive substring.
- Dependencies are shown for the highlighted package only and never auto-selected.
- Package list is cached per environment, refreshed on demand.
- Presets are named package sets per environment (F12). Loading one replaces the selection and lists names the environment lacks.
- Live clio output goes to `Shell/LogDialog`. Every long operation is cancellable (kill the clio process tree).

## Terminal.Gui v2

Model knowledge is mostly v1 / early v2 and the API changed a lot. Check https://tui-cs.github.io/Terminal.Gui/docs/ before using an unfamiliar API.

- Instance model: `using IApplication app = Application.Create().Init();`. Static `Application.*` is obsolete.
- Side effects go in `Accepted`; `Accepting` is only for cancelling. Exception: a dialog that must stay open while it works (`RegisterDialog`) overrides `OnAccepting`, starts the work and returns true; `Dialog<T>.OnAccepting` would call `RequestStop`.
- `CheckBoxTableSourceWrapperByObject<T>` hooks the `TableView` in its ctor and never unhooks: create it once over a live `IEnumerableTableSource<T>` (see `PackageTable`), never per filter. Selection lives in `PackageBrowser`, keyed by name.
- Never touch views from background threads; marshal with `app.Invoke(...)`.
- The UI `SynchronizationContext` is installed by `Begin`/`Run`, not `Init`: start async work in `OnIsRunningChanged(true)`, not in a ctor, or `await` resumes on the thread pool.
- App default keys: Esc quit, F5 redraw, F6 / Shift+F6 next / previous pane (`FrameView` is a tab group). View shortcuts win over them; keep F6 free.
- `TextView` is obsolete in 2.5: logs use `ListView` over an `ObservableCollection<string>`.
- `TableView.Value` is null until the cursor first moves; the cursor starts on row 0. After rows shrink, `Update()` (and `EnsureValidSelection()`) leave `Value` on a gone row: clamp with `SetSelection` (see `PackageTable.Refresh`).
- `TableView` and `ListView` bind Ctrl+P to Up: keep it out of shortcuts. Ctrl+S (flow control), F10 (GNOME Terminal menu) and F11 (fullscreen) clash with terminals.
- `TableView` handles header clicks before the `Activating` event fires: intercept them in an `OnActivating` override.
- `CancellationTokenSource.Cancel()` on the UI thread can run the awaiting job to its end inline. In `OnIsRunningChanging` re-check state after `Cancel()` (see `LogDialog`).
- No API lends the terminal to a child process (`Suspend` is Unix SIGTSTP only). To run `clio env-ui`, dispose the `IApplication`, run it, then create a new one (ROADMAP S4).

## Testing

- Unit-test logic classes (envelope parsing, filter, sort, mode detection, export plan) against fixtures.
- `Clio/` sits behind an interface; tests use a fake returning fixtures. No test hits a real Creatio.
- Views are tested headless via `tests/Cliopack.Tests/Headless.cs` (`app.Begin(view)` + `app.InjectKey(...)`, processed synchronously). The headless screen is 0x0: for mouse tests call `app.Driver.SetScreenSize` and `app.LayoutAndDraw(true)`, then `app.InjectMouse`. `FakeClioProcess.WithFixtures()` answers `list-environments` and `list-packages`; `FakeClioProcess.Gate(rows)` is a Gate environment whose SQL returns `rows`.
- Tests run one at a time (`Headless.cs`): Terminal.Gui shares static state, and parallel headless tests lose keys now and then.
- Headless `Begin` never pumps posted work: a continuation after real async I/O does not run. To test it, `app.Run` with an `Iteration` handler that injects keys and calls `VirtualTimeProvider.Advance` (see `LogDialogTests`). `RequestStop` under `Begin` only sets `StopRequested`.
- Manual checks against a real environment are listed per milestone in ROADMAP.md. The dev environment is clio `Creatio` (http://localhost:8080, Gate mode with cliogate 2.0.0.53, PostgreSQL in Docker `creatio-postgres`).
