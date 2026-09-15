# User guide

[English](USER-GUIDE.md) · [简体中文](USER-GUIDE.zh-CN.md)

See the [README](../README.md) for controls, supported work, installation, and demonstrations.

## Action panel

Press F8 to open the three independent action pages and Settings. Highlighted icons are enabled; click to save. Select all and Clear affect the whole current page, including cards outside the visible area. Tab changes pages; scroll to see more cards.

Left-click base work requires the matching held tool or material. Left-click extras run regardless of the held item. The right-click pool allows automatic tool selection. Tilling, digging spots, planting, placement, and floor removal are held-item actions only.

Click a key field, press a key or chord, then release to save. Esc cancels recording; Reset restores that binding's default. Recording replaces that field with one chord; existing alternative bindings remain until edited. Scroll or use the bottom arrows for more settings.

## General settings

Generic Mod Config Menu exposes 22 settings. The F8 settings page also provides menu and selection styles, three hotkeys, selection dimensions, camera panning, interior work, stored tools, and tree spacing. Other general settings can be edited in `config.json` while the game is closed.

| Setting | Default / range |
| --- | --- |
| Enabled, three hotkeys, stored tools, energy reserve | Preserve existing preferences on update |
| Movement cancellation | 0.5 s; adjustable from 0.1–2 s |
| Diagonal movement | Enabled; checks both side tiles at corners |
| Scythe extra travel | 12 tiles; adjustable from 2–24 |
| Scythe grouping preference | 16; adjustable from 4–32; higher values favor fewer swings |
| Automatic water refill | Enabled |
| Obstacle clearing | Enabled; obeys action permissions and tool requirements |
| Menu style | B · Sidebar icons; also framed cards and compact list |
| Selection style | C · Grid; also filled and outline |
| Selection dimensions | Enabled while dragging; width × height in tiles |
| Edge panning | Enabled |
| Camera pan speed | 12 tiles/s; 4–24 |
| Selected building interiors | Enabled |
| Wild-tree trunk spacing | 2 tiles; 2–8 |
| Fruit-tree trunk spacing | 3 tiles; 3–8 |
| Target refresh interval | 0.3 s; adjustable from 0.1–1 s |
| Completion recheck window | 1.5 s; adjustable from 0.3–3 s |

## Work rules

- A new rectangle replaces the old one. An empty selection ends immediately. Brief movement yields to the player; continuous movement or opening inventory cancels.
- Dragging near the screen edge pans the camera without moving the farmer. Release to restore camera follow. Selections remain in the current map, up to 64×64 tiles. The size badge includes both endpoint tiles, stays on screen, and disappears on release. Toggle it in F8 settings or GMCM.
- Completed buildings intersecting the selection are visited if enabled work exists inside. Enter through the native door, perform the same allowed actions, return, and continue outside. Each selected building is visited once.
- Work is rechecked until the selection completes, allowing follow-up tasks such as animal produce after petting.
- Scythes target mature crops only. Regrowing crops are skipped during their cooldown.
- Charged watering uses the can's actual upgrade and Reaching enchantment. Empty cans seek reachable water in the current location.
- Wild-tree trunk spacing defaults to 2 tiles, fruit-tree spacing to 3 (one or two empty tiles between trunks). Increase these in settings. Native planting rules still apply.
- Floor and object placement uses backpack stock, skips occupied tiles, and keeps native item consumption. Bombs, staircases, furniture, and wallpaper are not area-fill materials.
- Floor removal requires a held axe or pickaxe. Floors beneath objects, furniture, or buildings are protected.
- Left-click base work and its clearance keep the selected tool. Smart/extra work can borrow available stored tools in single-player; multiplayer uses backpack tools only.
- Native tool restrictions apply. Copper axes can chop large stumps, steel axes large logs, steel pickaxes farm boulders, and gold pickaxes meteorites. Ordinary mine boulders and small tree stumps have different native rules.
- Animal care includes petting, milking, shearing, hay, pet water, and produce. Management and valuable items remain manual.

Tested on Windows, Stardew Valley 1.6.15, and SMAPI 4.5.2. Update after exiting the game and keep your `config.json`.
