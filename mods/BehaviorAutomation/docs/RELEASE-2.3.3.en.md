# Behavior Automation 2.3.3

Fixes placed torches, drum blocks, flute blocks, jack-o-lanterns and similar objects being mistaken for ground pickups, causing repeated interactions or stalled work.

The shared classifier now follows the game's ground-pickup state instead of treating the default `CanBeGrabbed` flag or an item's forage category as proof that it can be collected. This applies to empty-hand and scythe gathering, left-click extra actions and right-click smart work. Normal forage, animal produce, machine output collection and watering remain supported.

Validation: 146 isolated native checks passed, including placed-object classification, stale-target rechecks, normal collection and completion of mixed watering/gathering beside a torch. No player save was loaded or written.

Requires Stardew Valley 1.6.15+ and SMAPI 4.5.2+. Replace the previous BehaviorAutomation folder; retain `config.json` to keep your settings.
