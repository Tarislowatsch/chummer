# Running Chummer on Linux and macOS (spike)

Status: **spike**. Chummer runs under Mono on Linux and on macOS (Apple
Silicon, through XQuartz), unchanged as a net48 WinForms application. This page
records how to start it, what was changed to get there, and what is still open.

## Approach

Mono implements the .NET Framework 4.x API, including WinForms on X11, and runs
the `Chummer.exe` that MSBuild produces. No port to .NET 8 and no new UI
toolkit. Microsoft handed Mono to WineHQ in 2024; WineHQ released Mono 6.14 in
2025, but WinForms gets no active development. That is fine for a spike and
worth weighing before committing to it long-term.

## Starting Chummer

Each start script checks its requirements, names the missing packages with the
command to install them, and then starts Chummer. Put the script next to
`Chummer.exe`, or run it from the repository after a build:

```
tools/mono/start-linux.sh [file.chum]
tools/mono/start-macos.sh [file.chum]
```

`--setup` only checks the requirements (and on macOS builds libgdiplus).

### Linux

Needs Mono with WinForms and libgdiplus from the distribution, usually the
package `mono-complete` (Arch: `mono` and `libgdiplus`). Mono draws through X11;
on Wayland that is XWayland. Chummer's options go to `~/.mono/registry`.

### macOS

Needs [Homebrew](https://brew.sh), XQuartz and the Xcode command line tools:

```
brew install mono mono-libgdiplus pkgconf libx11
brew install --cask xquartz      # then log out and back in once
xcode-select --install
```

The first start builds libgdiplus with X11 support into
`~/.local/share/chummer`, which takes a few minutes. The script then sets:

| Setting | Why |
| --- | --- |
| own libgdiplus build | Homebrew's is configured `--without-x11`, which Mono's X11 driver needs |
| `MONO_MWF_MAC_FORCE_X11=1` | Mono's native macOS driver is 32-bit Carbon and fails on 64-bit macOS |
| `MONO_WINFORMS_XIM_STYLE=disabled` | the variadic `XGetIMValues` crashes under P/Invoke on Apple arm64; no X input methods as a result |
| `DYLD_LIBRARY_PATH`, `DYLD_FALLBACK_LIBRARY_PATH` | Homebrew and XQuartz libraries are not on Mono's search path |

### Building

Building needs Mono's MSBuild. Ubuntu and Debian ship Mono 6.8 without MSBuild,
so the toolchain comes from the `mono:6.12` Docker image. Docker is the only
requirement on the host:

```
tools/mono/chummer.sh build
tools/mono/chummer.sh run                      # empty main window
tools/mono/chummer.sh run path/to/file.chum    # open a character
```

`run` uses the host's X server (`DISPLAY`, `/tmp/.X11-unix`). If the window does
not appear, run `xhost +SI:localuser:$USER` once.

## What was verified

Linux, under Xvfb in the container, with screenshots:

| Area | Result |
| --- | --- |
| Build of `ChummerCS.sln` with Mono MSBuild | builds, no errors |
| Main window, menus, toolbar | works |
| Create mode, `Combat Mage` example | loads; attributes, qualities, contacts and BP summary correct |
| Career mode, `Combat Bodyguard`, `Team Zero Operator` | loads; tabs, calendar and expense lists work |
| Print (`File → Print`) | XSLT runs, the sheet is written as HTML and handed to the system browser |

The `Mono` workflow (`.github/workflows/mono.yml`, started by hand) runs both
start scripts. `Combat Mage` and `Team Zero Operator` open completely, with no
exceptions in the log:

- Ubuntu 24.04 with the distribution's Mono 6.8, under Xvfb.
- macOS 26 on Apple Silicon with Mono 6.14.1 from Homebrew, under Xvfb.
- The same Mac without `DISPLAY`: `start-macos.sh` starts XQuartz itself, and
  the window appears on the macOS desktop.

Not verified: the test projects under Mono, saving and reloading a character,
the many selection dialogs, other languages, printing on macOS, an interactive
session on a real Mac.

## What had to change

| Problem on Mono | Change |
| --- | --- |
| The SDK bundled with Mono's MSBuild ignores `UseWindowsForms` for net48, so 3,000+ `CS0234` errors | `Chummer.csproj` references `System.Windows.Forms` when `MSBuildRuntimeType` is `Mono` |
| `ToolStripManager.RevertMerge(string)` throws `ArgumentNullException` when nothing is merged (.NET returns `false`); every character window crashed on activation | `ToolStripMerge.RevertMerge` catches that case |
| No `WebBrowser` engine (Mono's needs the long-gone `libgluezilla`); print crashed | `frmViewer` writes the sheet to a temp `.htm` and opens it in the default browser; *Print* reopens it there, *Save as HTML* uses the generated text |
| `System.Windows.Forms.DataVisualization` is a stub; `Chart.BeginInit` throws `NotImplementedException`, so every career character crashed | The two charts moved from `frmCareer.Designer.cs` into `CreateExpenseCharts()`; on Mono they are not created |
| `SplitterPanel.Width`/`Height` return 0 while the panel is not visible, so controls sized from them on a hidden tab collapsed to 0 | The resize handlers in `frmCreate` and `frmCareer` read `Panel.Size` instead |

On Windows, everything except the chart creation path is behaviour-neutral, and
the charts are built with the same properties as before. The Windows build is
still covered only by CI and by a manual check.

## Open points

- **Charts on Mono**: the karma and nuyen history charts are missing in career
  mode. They would need a small custom-drawn control, or a replacement charting
  library. Either change would also remove a blocker for a .NET 8 port.
- **Printing**: printing goes through the browser. Without `xdg-open` (as in
  the Docker container) Chummer shows the file path instead.
  - A new sheet opens a new browser tab. Changes to the character only rewrite
    the file, so the open tab needs a reload to show them.
  - The temp `.htm` files and the `mugshots` folder stay until Chummer exits,
    because the browser still reads them after the viewer closes.
  - After a crash or `kill` they stay behind: `ApplicationExit` only fires on a
    normal exit.
- **Layout**: on hidden tabs, Mono's anchoring can leave containers smaller than
  the tab, for example the karma/nuyen split container. This is cosmetic and
  probably needs case-by-case fixes. Mono 6.14 on macOS also shows the tab
  "Vehicles && Drones" with both ampersands, Mono 6.8 on Linux shows one.
- **Tests**: `AppHost` isolates the registry with `advapi32` P/Invokes and sets
  a private `startupPath` field. Neither exists on Mono, so the golden master
  tests would need a Mono path there.
- **Build warning** `NETSDK1107` under Mono MSBuild is harmless.
- **macOS for end users**: XQuartz stays a requirement, since Mono's WinForms
  draws only through X11 there. A `Chummer.app` bundle with Mono, libgdiplus and
  the start script inside would remove Homebrew and the build step. Without an
  Apple developer account it stays unsigned, and macOS asks once before the
  first start.
