# Audio Credits

Every file here is **CC0 (Creative Commons Zero, public domain)**. No attribution
is legally required; it is recorded anyway so the next person does not have to
re-derive it — and because the sprite sets in this project shipped with
`License: unconfirmed`, which is a hole worth not repeating.

Added 2026-08-06.

## Music

### `Music/crystal_cave.mp3`

- **Title:** Crystal Cave (song18)
- **Author:** cynicmusic
- **Source:** https://opengameart.org/content/crystal-cave-song18
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Original file:** `song18_0.mp3`, downloaded unmodified.

Chosen for the obvious reason: the game is set in a crystal cavern and this is a
track called Crystal Cave. Played as a seamless loop at low volume behind the
board.

## Sound effects

All three come from Kenney (https://kenney.nl), CC0, and are unmodified apart
from being renamed for what they do in this game.

| File here | Original | Pack |
|---|---|---|
| `SFX/merge_chime.ogg` | `bong_001.ogg` | [Interface Sounds](https://kenney.nl/assets/interface-sounds) |
| `SFX/pickaxe_arm.ogg` | `select_003.ogg` | [Interface Sounds](https://kenney.nl/assets/interface-sounds) |
| `SFX/shatter.ogg` | `glass_004.ogg` | [Impact Sounds](https://kenney.nl/assets/impact-sounds) |

- **License:** CC0 1.0 Universal, per the `License.txt` shipped in each pack.
- **Author/distributor:** Kenney (www.kenney.nl)

### Why these three

`bong_001` is a single short resonant tone (0.12s), which is what makes the
per-tier pitch shift in `AudioDirector` work — a clip with its own melody would
fight the pitching, and a longer one would smear when merges land in quick
succession.

`glass_004` is the longest of the glass breaks (0.69s). The shatter costs a
banked charge and is meant to feel like a decision, so it gets the weightier take
rather than the snappy one.

## Not auditioned

These were selected from descriptions, waveform metadata and filenames — nobody
has listened to them in place. Play the game before publishing and swap anything
that does not fit; the clips are plain serialized references on the `AudioDirector`
component, so replacing one is a drag-and-drop in the Inspector.
