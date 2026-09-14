# Stardew Valley Mods

星露谷模组源码合集。每个子模组独立构建、配置和安装。

| 模组 | 功能 | 当前版本 |
| --- | --- | --- |
| [行为学 · Behavior Automation](mods/BehaviorAutomation/README.md) | 框选区域，自动寻路并执行农活、采集、动物照料和农场改建 | 2.1.1 |

## 开发

安装支持 `net6.0` 的 .NET SDK，以及对应版本的 Stardew Valley 和 SMAPI。

```powershell
./mods/BehaviorAutomation/build.ps1 -GamePath '你的 Stardew Valley 安装目录'
```

成品输出到 `.artifacts/BehaviorAutomation/package/`。游戏程序集从本机安装目录引用。

也可以设置环境变量 `STARDEW_GAME_PATH`，或将 [GamePath.props.example](GamePath.props.example) 复制为 `GamePath.props`，供 IDE 和 `dotnet build` 使用。

查看 [贡献指南](CONTRIBUTING.md) 和 [行为学开发说明](mods/BehaviorAutomation/docs/DEVELOPMENT.md)。

## 许可

源码采用 [MIT](LICENSE) 许可。素材说明见各子模组的 `assets/README.md`。
