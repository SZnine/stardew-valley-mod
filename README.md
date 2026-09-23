<p align="center"><strong>English</strong> · <a href="README.zh-CN.md">简体中文</a></p>

# Stardew Valley Mods

Small, independent mods for everyday life in Stardew Valley. Each mod has its own source, settings, and installation package.

## Behavior Automation

[![Behavior Automation](mods/BehaviorAutomation/assets/header-1300x372.png)](mods/BehaviorAutomation/README.md)

Select an area to automate farm work, gathering, animal care, and renovation with your existing tools and materials.

**2.3.3** · [Demos & controls](mods/BehaviorAutomation/README.md) · [Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/52289) · [Releases](https://github.com/SZnine/stardew-valley-mod/releases)

## Build

Install a .NET SDK supporting `net6.0`, Stardew Valley, and SMAPI, then run:

```powershell
./mods/BehaviorAutomation/build.ps1 -GamePath 'C:\Games\Stardew Valley'
```

Output: `.artifacts/BehaviorAutomation/package/`. You can also set `STARDEW_GAME_PATH`, or copy [GamePath.props.example](GamePath.props.example) to `GamePath.props` for local builds.

[Contributing](CONTRIBUTING.md) · [Development & tests](mods/BehaviorAutomation/docs/DEVELOPMENT.md) · [MIT license](LICENSE) · [Media credits](mods/BehaviorAutomation/assets/README.md)
