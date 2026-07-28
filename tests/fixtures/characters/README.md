# Character fixtures

398 saved characters (`.chum`) in 12 packs, plus 6 `custom_*.xml` data files that
belong to the *10 Mercs* pack and are required to load its characters.

These files shipped with the application under `Chummer/bin/Debug/saves` and were
moved here in P0-07. They are **not** sample content for users — they are the
regression corpus for the Phase 2 golden master, where each character is loaded
and printed via `Character.PrintToStream()` and compared against a checked-in
baseline. Do not delete them, and prefer adding to them over replacing them.

## Uniqueness

Verified at the time of the move, by git blob hash:

- `bin/Release/saves` held 256 characters. Every one of them existed under
  `bin/Debug/saves` at the same relative path with an identical hash, so it was
  a strict subset and was dropped in P0-02 without losing content.
- Within `bin/Debug/saves`, all 404 files hash distinctly — there are no
  duplicate characters in this corpus.

The 654 `.chum` figure quoted in the backlog is the raw file count across both
output directories (398 + 256); the number of distinct characters is 398.

## Packs

| Pack | Files |
| --- | ---: |
| Contacts and Adventures | 150 |
| Horizon Adventures | 70 |
| SR4 NPCs | 54 |
| 10 Mercs | 47 |
| Runner's Companion Contacts | 28 |
| examples | 16 |
| Sprawl Sites - High Society and Low Life | 10 |
| Montreal 2074 | 9 |
| SR4 Contacts | 8 |
| The Way of the Samurai | 5 |
| Another Rainy Night | 4 |
| The Land of Promise | 3 |

Two files in *The Land of Promise* have non-ASCII names (`Tír …`). Anything
enumerating this directory has to be encoding-safe; a naive shell glob over
quoted `git` output will miss exactly those two.
