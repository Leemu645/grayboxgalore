# Audio credits

Clips live in this folder. Unity writes the `.meta` import settings the first time it imports them. Do not hand-author those metas.

Volumes in the game are set in `GameAudio` and on the looping AudioSources. This file only records where each clip came from.

## CC0 recordings

Licence for every file in this section: [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/).

### Kenney — 50 RPG sound effects

Author: Kenney Vleugels (www.kenney.nl). Credit is appreciated and not required.

- Page: https://opengameart.org/content/50-rpg-sound-effects
- File: https://opengameart.org/sites/default/files/RPGsounds_Kenney.zip

| Clip in this folder | File inside the zip |
| --- | --- |
| `creak_01.ogg` | `OGG/creak1.ogg` |
| `creak_02.ogg` | `OGG/creak2.ogg` |
| `creak_03.ogg` | `OGG/creak3.ogg` |
| `hit_wood_01.ogg` | `OGG/chop.ogg` |
| `axe_whoosh_01.ogg` | `OGG/drawKnife1.ogg` |
| `axe_whoosh_02.ogg` | `OGG/drawKnife2.ogg` |
| `closet_open_01.ogg` | `OGG/doorOpen_1.ogg` |
| `closet_open_02.ogg` | `OGG/doorOpen_2.ogg` |
| `key_jingle.ogg` | `OGG/handleCoins.ogg` |
| `hook_success.ogg` | `OGG/handleCoins2.ogg` |
| `hook_tick.ogg` | `OGG/metalClick.ogg` |

`creak_03.ogg` is also the hook-game scratch.

### TinyWorlds — steps on wood

Author: TinyWorlds.

- Page: https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud
- File: https://opengameart.org/sites/default/files/%5Bkdd%5DDifferentSteps_0.zip

| Clip in this folder | File inside the zip |
| --- | --- |
| `footstep_wood_01.ogg` | `wood01.ogg` |
| `footstep_wood_02.ogg` | `wood02.ogg` |
| `footstep_wood_03.ogg` | `wood03.ogg` |

Stone, leaves, gravel, and mud from that pack are not used.

### rubberduck — 100 CC0 SFX

Author: rubberduck. Recorded on an Android device, published as their first pack.

- Page: https://opengameart.org/content/100-cc0-sfx
- File: https://opengameart.org/sites/default/files/100-CC0-SFX_0.zip

| Clip in this folder | File inside the zip |
| --- | --- |
| `land.ogg` | `wooden_02.ogg` |
| `board_pop.ogg` | `wooded_box_open.ogg` |
| `crate_smash.ogg` | `slam_02.ogg` |
| `hit_hard_02.ogg` | `hit_01.ogg` |
| `flashlight_on.ogg` | `switch_01.ogg` |
| `flashlight_off.ogg` | `switch_02.ogg` |
| `lock_unlock_01.ogg` | `key_open_01.ogg` |

### rubberduck — 100 CC0 SFX #2

Author: rubberduck.

- Page: https://opengameart.org/content/100-cc0-sfx-2
- File: https://opengameart.org/sites/default/files/sfx_100_v2.zip

| Clip in this folder | File inside the zip |
| --- | --- |
| `footstep_wood_04.ogg` | `sfx100v2_footstep_wood_01.ogg` |
| `footstep_wood_05.ogg` | `sfx100v2_footstep_wood_02.ogg` |
| `footstep_wood_06.ogg` | `sfx100v2_footstep_wood_03.ogg` |
| `footstep_wood_07.ogg` | `sfx100v2_footstep_wood_04.ogg` |
| `jump.ogg` | `sfx100v2_air_03.ogg` |
| `pickup.ogg` | `sfx100v2_items_01.ogg` |
| `hit_wood_02.ogg` | `sfx100v2_wood_hit_01.ogg` |
| `hit_wood_03.ogg` | `sfx100v2_wood_hit_02.ogg` |
| `hit_hard_01.ogg` | `sfx100v2_metal_hit_02.ogg` |
| `lock_unlock_02.ogg` | `sfx100v2_lock_open_01.ogg` |

Kenney.nl itself returned 403 from the machine that downloaded these, so the Kenney clips are the OpenGameArt reupload, not a fresh download from kenney.nl.

## Placeholders

These were synthesized in Python (mono, 22050 Hz, 16-bit PCM) because no short CC0 recording in the packs above was a fire bed, a furnace, a dark room with drips, a key skid, a bar slide, or a lock rattle. They are not recordings. Replace them when a suitable CC0 file is available.

| Clip | What it stands in for |
| --- | --- |
| `fire_ignite.wav` | The moment a carried item catches |
| `fire_crackle.wav` | Loop while that item burns |
| `furnace_roar.wav` | Low furnace roar with a little crackle, looped on the furnace |
| `ambient_basement.wav` | Quiet hum and occasional drips, looped in 2D |
| `key_skid.wav` | The jail key sliding across the floor |
| `bars_slide.wav` | The barred door sliding open or shut |
| `lock_rattle.wav` | Jail lock (and the closet) when the key does not fit |
