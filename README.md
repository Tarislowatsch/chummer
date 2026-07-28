# Chummer

Chummer is a character generator for *Shadowrun, Fourth Edition* (SR4) — not
SR5. It builds and manages characters (attributes, skills, gear, cyberware,
magic, vehicles, …), tracks career advancement, and prints character sheets.

Chummer is unofficial and not endorsed by The Topps Company, Inc. or Catalyst
Game Labs, who own Shadowrun. See [`Chummer/data/LICENSE.txt`](Chummer/data/LICENSE.txt)
for the game-data disclaimer and [`Chummer/icons/LICENSE.txt`](Chummer/icons/LICENSE.txt)
for the icon set attribution.

## Relationship to Chummer5a

[Chummer5a](https://github.com/chummer5a/chummer5a) is the actively
maintained successor project, targeting SR5. This repository is a separate
codebase: the original SR4 application, last touched in 2013, its history
otherwise untouched since. It shares no code or history with Chummer5a's SR5
line beyond a common ancestor.

## Building

See [`docs/BUILDING.md`](docs/BUILDING.md) for toolchain requirements, the
target framework, platform choice, and how CI builds the project.

## State of the rehabilitation

This codebase had not been built successfully in years: an obsolete target
framework, no declared build output, and no CI. It is being rehabilitated in
phases — repo made buildable first, then a regression safety net, then
incremental internal cleanup — without changing the `.chum` save format, the
game-data schema, or the WinForms UI. The phase plan lives in a local,
untracked working document (not part of this repository) and is not
reproduced here; what matters for a contributor is that the application
builds and runs today, per `docs/BUILDING.md`.

## License

The application code is licensed under the GNU General Public License v3.0 —
see [`LICENSE`](LICENSE). This does not cover the transcribed Shadowrun rules
data or the bundled icon set; see the license files linked above for those.

No `LICENSE` or `COPYING` file was ever committed to this repository. The
license choice above is not a guess: the original project's Google Code page
(`code.google.com/p/chummer`), archived by the
[Wayback Machine snapshot from 2016-03-14](https://web.archive.org/web/20160314235832/https://code.google.com/p/chummer/),
lists "Code license: GNU GPL v3" — a choice Google Code required project
creators to actively make, with no pre-selected default. The page's "People"
tab names the same account as the sole committer of this history, and its
Google Code shutdown notice points to `github.com/Arkeas/chummer` as the
export target, the confirmed root of this SR4 fork's history.
