<p align="center"><a href="README.md">English</a> · <strong>简体中文</strong></p>

# Stardew Valley Mods

面向星露谷日常游玩的独立模组。每个子模组分别维护源码、配置和安装包。

## 行为学

[![行为学](mods/BehaviorAutomation/assets/header-1300x372.png)](mods/BehaviorAutomation/README.zh-CN.md)

框选区域，使用已有工具和材料，自动完成农活、采集、动物照料和农场改建。

**2.3.1** · [演示与操作](mods/BehaviorAutomation/README.zh-CN.md) · [Nexus Mods](https://www.nexusmods.com/stardewvalley/mods/52289) · [版本下载](https://github.com/SZnine/stardew-valley-mod/releases)

## 构建

安装支持 `net6.0` 的 .NET SDK、Stardew Valley 和 SMAPI，然后运行：

```powershell
./mods/BehaviorAutomation/build.ps1 -GamePath 'C:\Games\Stardew Valley'
```

输出位于 `.artifacts/BehaviorAutomation/package/`。也可设置 `STARDEW_GAME_PATH`，或将 [GamePath.props.example](GamePath.props.example) 复制为本地 `GamePath.props`。

[贡献指南](CONTRIBUTING.md) · [开发与测试](mods/BehaviorAutomation/docs/DEVELOPMENT.md) · [MIT 许可](LICENSE) · [素材说明](mods/BehaviorAutomation/assets/README.md)
