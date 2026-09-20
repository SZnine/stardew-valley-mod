# Behavior Automation 2.3.2

This release fixes two watering regressions without changing the original watering range, selection bounds, energy use, or water consumption.

- Sprinklers are excluded at the shared world-target classification boundary, so empty-hand and pickup work cannot treat them as ordinary items.
- Passable sprinklers are excluded from watering stances and charged watering anchors.
- Watering targets remain discoverable beneath mature tree canopies and around solid tree obstructions.

Validation: 140 isolated native checks passed, including sprinkler classification, watering stance safety, tree obstruction, and mature-canopy scenarios.
