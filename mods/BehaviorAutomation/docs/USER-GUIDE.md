# User guide

[English](USER-GUIDE.md) · [简体中文](USER-GUIDE.zh-CN.md)

See the [README](../README.md) for controls, supported work, installation, and demonstrations.

## Action panel

Press F8 to open the three independent action pages. Highlighted icons are enabled; click to save. Select all and Clear affect the whole current page, including cards outside the visible area. Tab changes pages; scroll to see more cards.

Left-click base work requires the matching held tool or material. Left-click extras run regardless of the held item. The right-click pool allows automatic tool selection. Tilling, digging spots, planting, placement, and floor removal are held-item actions only.

## General settings

Generic Mod Config Menu exposes 14 settings. Without it, close the game and edit `config.json` for general settings; the F8 action panel remains available.

| Setting | Default / range |
| --- | --- |
| Enabled, three hotkeys, stored tools, energy reserve | Preserve existing preferences on update |
| Movement cancellation | 0.5 s; adjustable from 0.1–2 s |
| Diagonal movement | Enabled; checks both side tiles at corners |
| Scythe extra travel | 12 tiles; adjustable from 2–24 |
| Scythe grouping preference | 16; adjustable from 4–32; higher values favor fewer swings |
| Automatic water refill | Enabled |
| Obstacle clearing | Enabled; obeys action permissions and tool requirements |
| Target refresh interval | 0.3 s; adjustable from 0.1–1 s |
| Completion recheck window | 1.5 s; adjustable from 0.3–3 s |

## Work rules

- A new rectangle replaces the old one. An empty selection ends immediately. Brief movement yields to the player; continuous movement or opening inventory cancels.
- Work is rechecked until the selection completes, allowing follow-up tasks such as animal produce after petting.
- Scythes target mature crops only. Regrowing crops are skipped during their cooldown.
- Charged watering uses the can's actual upgrade and Reaching enchantment. Empty cans seek reachable water in the current location.
- Wild trees leave at least one tile between trunks; fruit trees leave at least two. Native planting rules still apply.
- Floor and object placement uses backpack stock, skips occupied tiles, and keeps native item consumption. Bombs, staircases, furniture, and wallpaper are not area-fill materials.
- Floor removal requires a held axe or pickaxe. Floors beneath objects, furniture, or buildings are protected.
- Left-click base work and its clearance keep the selected tool. Smart/extra work can borrow available stored tools in single-player; multiplayer uses backpack tools only.
- Native tool restrictions apply. Copper axes can chop large stumps, steel axes large logs, steel pickaxes farm boulders, and gold pickaxes meteorites. Ordinary mine boulders and small tree stumps have different native rules.
- Animal care includes petting, milking, shearing, hay, pet water, and produce. Management and valuable items remain manual.

Tested on Windows, Stardew Valley 1.6.15, and SMAPI 4.5.2. Update after exiting the game and keep your `config.json`.
