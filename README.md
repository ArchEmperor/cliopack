# cliopack

Terminal UI over [clio](https://github.com/Advance-Technologies-Foundation/clio) for pulling packages from Creatio environments. Pick an environment, filter its packages, select some, and download, bundle or unzip them. A `pull` command does the same from scripts.

## Requirements

- clio 8.1 or newer on PATH: `dotnet tool install clio -g`
- Environments registered in clio with login and password (OAuth environments are not supported). cliopack can register new ones (F2).
- .NET 10 SDK only to install it as a .NET tool. The release binaries need nothing.
- Optional: cliogate 2.0.0.41+ on the environment (`clio install-gate -e <env>`) adds modified dates and dependencies ("Gate" mode). Without it the environment is in "Basic" mode and these columns stay empty.

## Install

```sh
dotnet tool install -g cliopack     # or run once without installing: dnx cliopack
```

On Windows x64, Linux x64 / arm64 and macOS this installs a native binary; elsewhere it runs on the .NET 10 runtime.

Without .NET: download the archive for your platform from [Releases](https://github.com/ArchEmperor/cliopack/releases) and unpack it to a folder on PATH (`cliopack` plus `libonigwrap`). The binaries are not signed: on macOS run `xattr -d com.apple.quarantine cliopack` once, on Windows SmartScreen asks to confirm the first run.

Build from source (Native AOT builds only for the OS it runs on; Windows needs the Visual Studio C++ build tools, Linux `clang` and `zlib1g-dev`):

```sh
dotnet publish src/Cliopack -c Release -r <rid> -o dist
```

## Use

Run `cliopack` in the folder where the packages should land.

| Key | Action |
|---|---|
| Enter | load the highlighted environment |
| F6 / Shift+F6 | next / previous pane |
| Type in Search | filter by name (case-insensitive substring) |
| Space | select / unselect a package; the selection survives filtering |
| F4 | show / hide `Terrasoft` and `Creatio` packages (hidden by default) |
| F5 | reload packages |
| F7 / F8, header click | sort by name, maintainer, version, modified / reverse |
| F9 | export the selection |
| F12 | presets: save, load, delete named package sets |
| F2 / F3 | register an environment / manage environments in `clio env-ui` |
| F1 | all keys |
| Esc | close a dialog, quit |

Export (F9) has three kinds:

- **Download**: one `<Package>.zip` per package, as clio produces it.
- **Bundle**: one zip with all packages, named `bundle.zip` by default.
- **Unzip**: one folder per package, for reading schemas.

If a target already exists, cliopack asks once for all of them: overwrite, rename (`A (2).zip`) or skip. Packages are pulled 10 per clio call; **One by one** pulls each package separately, for environments where batch export fails. The log shows live clio output; Esc cancels and leaves no half-written files. The summary lists every package as ok or failed.

The dependency pane shows what the highlighted package depends on (Gate mode, display only).

Presets are saved per environment in `<AppData>/cliopack/presets.json` (`%APPDATA%` on Windows, usually `~/.config` elsewhere). Loading one replaces the selection and lists packages the environment does not have.

## Scripts

```sh
cliopack pull -e <env> (-p <A,B,...> | --preset <name>) [options]
  --mode download|bundle|unzip          default download
  --name <bundle>                       bundle file name, default bundle
  --one-by-one                          one clio call per package
  --on-conflict overwrite|rename|skip   default: stop when a target exists
```

```sh
cliopack pull -e dev -p MyApp,MyAppUi --mode bundle --name release
cliopack pull -e prod --preset release --on-conflict overwrite
```

Exit codes: `0` all packages ok, `1` a package failed or a target exists without `--on-conflict`, `2` usage error, `130` cancelled with Ctrl+C.
