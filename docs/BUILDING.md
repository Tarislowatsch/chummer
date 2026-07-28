# Building Chummer

## Requirements

- Windows
- MSBuild, from either Visual Studio (any edition, 2019 or newer) or the
  standalone [Build Tools for Visual Studio][buildtools]
- The .NET Framework 4.8 targeting pack (the "Developer Pack"), which the
  Visual Studio installer offers under *.NET desktop development*

```
msbuild ChummerCS.sln -restore /p:Configuration=Debug /p:Platform=x86
```

`-restore` is required since P0-12 even though there is not a single NuGet
dependency: an SDK-style project fails with `NETSDK1004` until a restore pass
has written `obj/project.assets.json`. Every assembly reference is a .NET
Framework assembly from the targeting pack, so the restore has nothing to
download — it just has to have happened once.

### Building with a minimal Build Tools install

A plain "Build Tools for Visual Studio" install — just the *MSBuild tools*
workload plus the 4.8 targeting pack, no full Visual Studio and no *.NET
Desktop Build Tools*/*NetCoreBuildTools* workload — can build old-style
projects but cannot resolve `Microsoft.NET.Sdk` on its own: it has no
`Microsoft.DotNet.MSBuildSdkResolver` and no bundled `MSBuild\Sdks` folder.
Installing a standalone [.NET SDK](https://dotnet.microsoft.com/download) and
pointing at its `Sdks` folder gets past that:

```
$env:MSBuildSDKsPath = "C:\Program Files\dotnet\sdk\<version>\Sdks"
```

That is enough to resolve `Microsoft.NET.Sdk` itself, but not the nested
`Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` SDK that
`Microsoft.NET.Sdk.ImportWorkloads.props` references — that one is resolved
through the full VS workload-resolver mechanism, not plain directory probing,
so it still fails with `MSB4236` even with `MSBuildSDKsPath` set. It exists to
auto-import build logic for SDK-installed workloads (MAUI, Android, …), which
this desktop WinForms project has none of, so it can simply be turned off:

```
/p:MSBuildEnableWorkloadResolver=false
```

Together:

```
$env:MSBuildSDKsPath = "C:\Program Files\dotnet\sdk\<version>\Sdks"
msbuild ChummerCS.sln -restore /p:Configuration=Debug /p:Platform=x86 /p:MSBuildEnableWorkloadResolver=false
```

None of this is needed with a full Visual Studio install or the *.NET Desktop
Build Tools* workload (both carry the SDK resolver already), and it is not
needed in CI: `windows-latest` ships a complete installation.

### Why `dotnet build` still does not work

P0-12 converted the project to SDK-style, and the backlog expected that to make
`dotnet build` available. It does not, and the reason is worth writing down so
nobody spends the afternoon on it a second time.

`dotnet build` runs MSBuild on .NET, where the `GenerateResource` task cannot
serialise non-string resources. Ten of the 78 `.resx` files contain some — the
menu icons in `frmMain`, the window icons of the two character forms and a few
dialogs, and the 56 famfamfam PNGs in `Properties\Resources.resx` — so the
build stops with one pair of errors per affected file:

```
error MSB3823: Non-string resources require the property
               GenerateResourceUsePreserializedResources to be set to true.
error MSB3822: Non-string resources require the System.Resources.Extensions
               assembly at runtime, but it was not found in this project's
               references.
```

The errors name their own fix, and taking it would be a bad trade: it means a
`PackageReference` to `System.Resources.Extensions` — the first NuGet dependency
this repository would ever have — and it changes the format of the compiled
resources inside the shipped executable, which then need that assembly deployed
beside them to be readable at run time. Phase 0 does not change what the build
produces, so the project keeps the format full MSBuild writes, and full MSBuild
stays the only supported driver.

This is specific to the combination of a .NET Framework target with MSBuild
running on .NET. It is *not* an argument that would carry over to a `net8.0`
port: there, `System.Resources.Extensions` is part of the shared framework and
the SDK enables the preserialized format by default. The port stays out of
scope for the reasons the backlog gives, but this is not one of them.

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

## The project file

`Chummer/Chummer.csproj` is SDK-style since P0-12. It went from 1,053 lines to
about 110, most of which are comments: roughly 230 `<Compile>` entries, 90
`<EmbeddedResource>` entries, 56 single-item `<ItemGroup>` blocks each holding
one icon PNG, a ClickOnce bootstrapper block and nine of the twelve assembly
references are now supplied by globs or by the SDK itself.

Four deliberate decisions are worth knowing about, and each is commented in the
project file itself:

- **The output stays in `bin\Debug` and `bin\Release`.** The SDK default would
  be `bin\x86\Debug\net48`. With exactly one target framework and one platform
  those levels carry no information while breaking every existing path.
- **`Properties\AssemblyInfo.cs` is still hand-maintained**
  (`GenerateAssemblyInfo=false`), because it holds the version that `frmAbout`
  displays and that `Character.Save` writes into every `.chum` file. The
  generated `TargetFrameworkAttribute` is kept, since the .NET Framework reads
  it to decide between 4.8 behaviour and 4.0 quirks mode.
- **Debug information is now portable PDBs**, the SDK default, instead of
  `full` for Debug and `pdbonly` for Release. This affects the debugger and
  nothing the application does.
- **`InheritedListView.cs` is explicitly excluded from compilation.** The file
  contains `class MyListView`, a `ListView` that drops focus when scrolled. It
  has a single commit — the 2013 import — was never listed in the old project
  file and is referenced nowhere, so it has never been compiled. The SDK's
  `**/*.cs` glob would have quietly started compiling it. Phase 0 does not
  change what the build produces, so it is excluded rather than adopted or
  deleted; whether it goes belongs to Phase 3, where dead code is the subject.
  Anyone who removes the `<Compile Remove>` line is adding an unused type to
  the assembly, not fixing an oversight.

The roughly 90 `.resx` files needed no per-file entries. The SDK pairs
`frmX.resx` with `frmX.cs` by convention and derives the same manifest resource
names the old file produced, and the one `.resx` without a same-named source
file, `Properties\Resources.resx`, falls back to root namespace plus path —
`Chummer.Properties.Resources`, exactly what `Resources.Designer.cs` asks for.

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
- **MSBuild, not `dotnet build`**, for the reason given above. A `dotnet build`
  job was added with P0-12 and removed again in the same pull request, once it
  turned out that the resources — not the project format — are what rules that
  driver out.
- **`Platform` is passed explicitly**, so the build does not depend on how a
  given MSBuild version resolves a default.

### The runtime content check

The last step verifies that the build output actually contains the files the
application needs at startup — game data, bundled custom content, translations,
character sheets including the localised ones, export templates, the default
settings profile, `changelog.txt`, and `Chummer.exe.config` — without which the
runtime declaration from P0-10 never reaches the user and the application falls
back to 4.0 semantics with no build diagnostic anywhere.

Any missing entry **fails the build**. This is not redundant with the compiler:
every one of these is read from `Application.StartupPath` at run time, so a
build can succeed and still produce an application that dies on startup. That
was the state of this repository before P0-13 — the csproj declared no `Content`
items at all, and the game data only reached the output directory because it had
been committed there by hand.

The check warned instead of failing between P0-15 and P0-13, while there was
nothing yet to find; P0-13 turned it into a hard failure in the same commit that
added the `CopyToOutputDirectory` entries.
