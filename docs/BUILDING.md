# Building Chummer

## Requirements

- Windows
- MSBuild, from either Visual Studio (any edition, 2019 or newer) or the
  standalone [Build Tools for Visual Studio][buildtools]
- The .NET Framework 4.8 targeting pack (the "Developer Pack"), which the
  Visual Studio installer offers under *.NET desktop development*

```
msbuild ChummerCS.sln /p:Configuration=Debug /p:Platform=x86
```

`dotnet build` does **not** work. The project is an old-style MSBuild project
(`ToolsVersion="4.0"`), and the .NET SDK cannot drive that format. P0-12
converts it to SDK-style, after which `dotnet build` becomes available.

There are no NuGet dependencies. All twelve assembly references are .NET
Framework assemblies resolved from the targeting pack, so there is nothing to
restore and no package cache to warm.

[buildtools]: https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022

## Target framework

The project targets **.NET Framework 4.8** (`net48`). It previously targeted
`v4.0` with `<TargetFrameworkProfile>Client</TargetFrameworkProfile>`.

### Why the Client Profile had to go

The .NET Framework Client Profile was a reduced installation footprint aimed at
machines that would never host server workloads. Microsoft **discontinued it
with .NET Framework 4.5**; from 4.5 onward there is only the full framework, and
no current toolchain ships reference assemblies for the 4.0 Client Profile.

This was not a cosmetic problem. Building the repository failed outright with
two `MSB3644` errors ("the reference assemblies for
.NETFramework,Version=v4.0,Profile=Client were not found"), so nobody could
build it without installing a decade-old targeting pack.

Independently of that, the profile was already the wrong choice for this
codebase: it omits `System.Web` and parts of WCF, while the project references
`System.ServiceModel` for the Omae service proxies. Those references only
resolved because the full framework was installed on the original developer's
machine.

### Which Windows versions this supports

.NET Framework 4.8 runs on:

| Windows | Status |
| --- | --- |
| Windows 11, Windows 10 (1903 and newer) | Preinstalled |
| Windows 10 1607 – 1809 | Supported, installable |
| Windows 8.1 | Supported, installable |
| Windows 7 SP1 | Installable, but Windows 7 itself is out of support |
| Windows Server 2012 R2 / 2016 / 2019 / 2022 and newer | Supported |

### What compatibility this costs

Retargeting from 4.0 to 4.8 drops support for **Windows XP and Windows Vista**,
which .NET Framework 4.0 still supported and 4.5+ never did. Both have been out
of support since 2014 and 2017 respectively.

For every Windows version that is itself still supported, the change costs
nothing and mostly *gains* compatibility: 4.8 is preinstalled on Windows 10 1903
and later and on Windows 11, so the retargeted application needs no framework
install at all on any current machine, whereas the 4.0 Client Profile has to be
fetched and installed by hand.

.NET Framework 4.x is binary-backward-compatible, so the existing code and the
`.chum` save format are unaffected. Note that a 4.8 build will not run on a
machine that only has 4.0 installed — `app.config` declares the requirement so
such a machine gets a clear message instead of a load failure.

### Why not .NET 8/9

Deliberately out of scope; see the closing section of the backlog. In short:
`System.Windows.Forms.DataVisualization` (used for the two charts in
`frmCareer.cs`) was never ported by Microsoft, `XsltSettings.EnableScript`
throws on modern .NET, and roughly 90 `.resx` files would have to be
regenerated. Chummer5a evaluated the same migration and stayed on net48.

## Platform configuration

The solution offers only `x86`. It previously also listed `Any CPU` and
`Mixed Platforms`, which were aliases pointing at `x86` — and the `Any CPU`
entries had an `ActiveCfg` line but no `Build.0` line, so selecting the platform
Visual Studio offers by default marked the project as *not to be built*. The IDE
then reported success while producing nothing and launching the previous
executable. P0-11 removed them.

A genuine `AnyCPU` configuration is not offered because the application hosts
the Internet Explorer ActiveX `WebBrowser` control and is 32-bit in practice.
That belongs with P6-13.

## Continuous integration

[`.github/workflows/ci.yml`](../.github/workflows/ci.yml) builds on
`windows-latest` for every push and pull request.

- **Both `Debug` and `Release`** are built, as a matrix. Release is not merely a
  different output directory: it enables optimisation and changes `DebugType`,
  and a codebase with 715 empty `catch` blocks (P6-10) is exactly the kind that
  can behave differently under optimisation. Building only Debug would leave the
  configuration that ships unverified.
- **MSBuild, not `dotnet build`**, for the reason given above.
- **`Platform` is passed explicitly**, so the build does not depend on how a
  given MSBuild version resolves a default.

### The runtime content check

The last step verifies that the build output actually contains the files the
application needs at startup — game data, bundled custom content, translations,
character sheets including the localised ones, export templates, the default
settings profile and `changelog.txt`.

Any missing entry **fails the build**. This is not redundant with the compiler:
every one of these is read from `Application.StartupPath` at run time, so a
build can succeed and still produce an application that dies on startup. That
was the state of this repository before P0-13 — the csproj declared no `Content`
items at all, and the game data only reached the output directory because it had
been committed there by hand.

The check warned instead of failing between P0-15 and P0-13, while there was
nothing yet to find; P0-13 turned it into a hard failure in the same commit that
added the `CopyToOutputDirectory` entries.
