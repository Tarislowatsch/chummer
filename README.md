# Chummer

Chummer is a character generator for *Shadowrun, Fourth Edition* (SR4) — not
SR5. It builds and manages characters (attributes, skills, gear, cyberware,
magic, vehicles, …), tracks career advancement, and prints character sheets.

Chummer is unofficial and not endorsed by The Topps Company, Inc. or Catalyst
Game Labs, who own Shadowrun. See [`Chummer/data/NOTICE.txt`](Chummer/data/NOTICE.txt)
for the game-data disclaimer and [`Chummer/icons/NOTICE.txt`](Chummer/icons/NOTICE.txt)
for the icon set attribution.

## Relationship to Chummer5a

[Chummer5a](https://github.com/chummer5a/chummer5a) is the actively
maintained successor project, targeting SR5. This repository is a separate
codebase: the original SR4 application, last touched in 2013, its history
otherwise untouched since. It shares no code or history with Chummer5a's SR5
line beyond a common ancestor.

## Optional content packs

The `data` folder next to `Chummer.exe` holds six optional packs in
`data/custom content/`. Chummer only loads data files that sit directly in that
`data` folder, so a pack is inactive until you copy it there:

1. Close Chummer.
2. Copy the files from the pack's folder into the `data` folder next to
   `Chummer.exe`, beside `books.xml`. In a source checkout, copy into the build
   output, not into `Chummer/data/`: the build never deletes a file it copied,
   and the tests would read the pack twice.
3. Start Chummer and open **Tools → Options**. On the **General** tab, tick the
   pack's book under **Additional Sourcebooks to Use**. The main selection
   lists, such as metatypes and traditions, show only items from ticked books.

Five packs add content and each brings its own book. *Audio and Vision
Enhancement Options* is different: it replaces existing gear and has no book.
Its three files are alternatives (audio, vision or both), so copy only one of
them. To remove a pack, delete its files from `data/` again.

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
data or the bundled icon set; see the notice files linked above for those.

No `LICENSE` or `COPYING` file was ever committed to this repository; the
license choice above follows the original project's Google Code page,
[archived by the Wayback Machine on 2016-03-14](https://web.archive.org/web/20160314235832/https://code.google.com/p/chummer/),
which lists "Code license: GNU GPL v3".
