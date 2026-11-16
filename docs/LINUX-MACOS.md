# Running Chummer on Linux and macOS (spike)

Status: **spike**. Chummer runs on Linux under Mono, unchanged as a net48
WinForms application. macOS is untested. This page records what works, what
was changed to get there, and what is still open.

## Approach

Mono implements the .NET Framework 4.x API, including WinForms on X11, and runs
the `Chummer.exe` that MSBuild produces. No port to .NET 8 and no new UI
toolkit. Mono 6.12 is the last release; it is maintained by WineHQ now and gets
no further WinForms work. That is fine for a spike and worth weighing before
committing to it long-term.

## Linux

Building needs Mono's MSBuild. Ubuntu and Debian ship Mono 6.8 without MSBuild,
so the toolchain comes from the `mono:6.12` Docker image. Docker is the only
requirement on the host:

```
tools/mono/chummer.sh build
tools/mono/chummer.sh run                      # empty main window
tools/mono/chummer.sh run path/to/file.chum    # open a character
```

`run` uses the host's X server (`DISPLAY`, `/tmp/.X11-unix`). On Wayland that is
XWayland; if the window does not appear, run `xhost +SI:localuser:$USER` once.
Chummer's options go to `~/.mono/registry` on the host.

Instead of Docker, an installed Mono runtime can run the output directly:
`cd Chummer/bin/Debug && mono Chummer.exe`. It needs `libgdiplus` and the
WinForms assemblies (`mono-complete` covers both).

## macOS (untested)

Mono 6.12 has a macOS installer that includes WinForms. Whether its native
driver works on current macOS, and on Apple Silicon, was not checked. The
fallback is X11 via XQuartz (`MONO_MWF_MAC_FORCE_X11=1`). The code changes
below are not Linux-specific; `Platform.OpenWithDefaultApplication` uses `open`
on macOS.

## What was verified

Under Xvfb in the container, with screenshots:

| Area | Result |
| --- | --- |
| Build of `ChummerCS.sln` with Mono MSBuild | builds, no errors |
| Main window, menus, toolbar | works |
| Create mode, `Combat Mage` example | loads; attributes, qualities, contacts and BP summary correct |
| Career mode, `Combat Bodyguard`, `Team Zero Operator` | loads; tabs, calendar and expense lists work |
| Print (`File → Print`) | XSLT runs, the sheet is written as HTML and handed to the system browser |

Not verified: the test projects under Mono, saving and reloading a character,
the many selection dialogs, other languages, macOS.

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
  probably needs case-by-case fixes.
- **Tests**: `AppHost` isolates the registry with `advapi32` P/Invokes and sets
  a private `startupPath` field. Neither exists on Mono, so the golden master
  tests would need a Mono path there.
- **Build warning** `NETSDK1107` under Mono MSBuild is harmless.
