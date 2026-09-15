# Changelog

[English](CHANGELOG.md) · [简体中文 / full history](CHANGELOG.zh-CN.md)

## 2.3.1

- Add the Nexus update key so SMAPI can notify players about newer versions.

## 2.3.0

- Use sidebar icons and grid selections by default. Choose from three menu styles and three selection styles, and rebind the selection, panel, and cancel keys in F8 settings.
- Show selection width × height in tiles while dragging; toggle it in F8 settings or GMCM.
- Pan the camera by dragging selections to the screen edge; adjust panning speed in settings.
- Balance reachable travel distance with batch coverage, and start planting or placement near the farmer.
- Visit selected coops, barns, and sheds, perform enabled indoor work, then return to the remaining outdoor tasks. Interior work can be disabled.
- Configure wild-tree and fruit-tree trunk spacing while keeping native planting rules.
- Add an F8 settings page for panning, interiors, stored tools, and tree spacing, also available through GMCM.
- Check the native fruit-sapling ground requirements before queuing a planting spot.

## 2.2.1

- Keep the selected tool when clearing obstacles for left-click base work; do not silently borrow a higher-grade tool.
- Check native tool types and upgrade requirements before work and on impact for large stumps, logs, boulders, and meteorites.
- Respect the axe's native Powerful enchantment. Pickaxe damage bonuses do not count as an upgrade.

## 2.2.0

- Use eight-direction paths with corner checks and more precise final positioning.
- Cancel after 0.5 seconds of continuous movement by default; the delay is configurable.
- Improve grouped scythe work and check actual coverage after walking.
- Fix extra scythe swings triggered by deferred forage collection with Harvest With Scythe; skip immature and regrowing crops until ready.
- Add movement, scythe, refilling, obstacle, and refresh options to GMCM.

## 2.1.1

- Add floor and path removal to left-click base actions using the held axe or pickaxe.
- Preserve native material drops and protect unselected or covered flooring.
