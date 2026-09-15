# 开发与测试

## 构建

需要支持 `net6.0` 的 .NET SDK、Stardew Valley 1.6.15 和 SMAPI 4.5.2。游戏程序集由本机安装目录提供。

在仓库根目录运行：

```powershell
./mods/BehaviorAutomation/build.ps1 -GamePath '你的 Stardew Valley 安装目录'
```

构建产物位于 `.artifacts/BehaviorAutomation/package/`。ZIP 包含 DLL、manifest、两份语言文件、使用说明和 LICENSE。

直接使用 MSBuild 时，可通过 `-p:GamePath=...`、`STARDEW_GAME_PATH` 环境变量，或仓库根目录的 `GamePath.props` 提供路径。

## 代码入口

| 源文件 | 职责 |
| --- | --- |
| `ModEntry.cs`、`NativeHooks.cs`、`WalkingInput.cs` | 输入、事件、菜单和步态保护 |
| `ModConfig.cs`、`ActionCatalog.cs` | 三套独立行为配置与可自动选择的行为 |
| `ActionMenu.cs`、`ActionIcons.cs` | 图标配置面板 |
| `WorkBoard.cs`、`SmartSelection.cs` | 选区、手持行为、附加行为和后续任务发现 |
| `WorldTargets.cs`、`ToolRequirements.cs` | 目标分类与待处理条件 |
| `WorkController.cs`、`ToolExecution.cs` | 调度和原生动作执行 |
| `Navigation.cs`、`PathGeometry.cs`、`WalkRoute.cs`、`ObstaclePlanner.cs` | 寻路、移动与受配置限制的清障 |
| `ScythePlanner.cs`、`Watering.cs` | 范围收割、蓄力浇水和补水 |
| `AnimalCare.cs`、`Planting.cs`、`Placement.cs`、`ToolStorage.cs` | 照料、种植、放置与原工具借还 |
| `Overlay.cs`、`OverlayRenderer.cs`、`PassabilityProbe.cs` | 预览、绘制与无副作用通行检查 |

以上文件位于 `src/`；原生回归场景位于 `tests/native/`。

左键基础使用 `Actions`，左键全局使用 `LeftActions`，右键工具池使用 `SmartActions`。三者独立，目标通过 `WorkTarget.Scope` 保留权限来源。锄地、挖掘点、种植、放置和拆地板不进入自动工具池。

新增行为时，同步行为目录、目标判定、执行方式、图标、两份语言文件和配置迁移。放置调用原版 `Utility.tryToPlaceItem`，成功后由原版扣除材料。

## 原生回归

Windows 下先保存并退出游戏，再运行：

```powershell
./mods/BehaviorAutomation/run-native.ps1 -GamePath '你的 Stardew Valley 安装目录'
```

测试会启动独立 SMAPI 进程，在 `.artifacts/BehaviorAutomation/native/<run>/mods` 中加载模组，创建临时地图和农夫，不加载玩家存档。完成后自动退出。

只检查构建和测试环境准备，不启动游戏：

```powershell
./mods/BehaviorAutomation/run-native.ps1 -GamePath '你的 Stardew Valley 安装目录' -PrepareOnly
```

结果位于同次运行的 `evidence/native-result.json` 与 `native-tests.txt`，包含被测 DLL 哈希、通过、失败和跳过数。完整回归使用不带 `-PrepareOnly` 的命令运行。

### 可选兼容测试

GMCM、Passable Crops 和独立智能水壶用于各自的兼容场景。不提供这些模组时，仅对应场景显示 `SKIP`，其余用例照常运行。需要覆盖时，用 `-CompatibilityModPaths` 传入各自包含 `manifest.json` 的模组目录：

```powershell
./mods/BehaviorAutomation/run-native.ps1 `
    -GamePath '你的 Stardew Valley 安装目录' `
    -CompatibilityModPaths @('外部模组目录/GenericModConfigMenu', '外部模组目录/PassableCrops')
```

外部模组只复制到本次隔离目录，其配置修改不回写原目录。旧智能水壶的兼容测试需要 `sznine.SmartWateringCan`，并非行为学的运行依赖。

## 修改后的验证

- 步态问题需覆盖实际 `Game1.UpdateControlInput`；只调用路线更新无法验证原版输入对步行动画的影响。
- 工具行为需检查成熟状态、选区边界、权限来源、原版消耗以及取消后的动画收尾。
- 放置需检查材料不足、已有对象和重复执行，避免重复扣除或覆盖。
- 将实际运行的检查与仍需实机验证的部分分开记录。


## 2.2 的执行边界

斜向寻路检查两侧格，最终站位限制剩余步长。再生作物必须检查 `fullyGrown` 和 `dayOfCurrentPhase`；延迟采集收尾保留操作作用域，防止 Harvest With Scythe 触发额外挥刀。

清障候选由 `WorkBoard.ObstacleCandidates` 产生，复用选区扫描规则。左键基础固定原工具，左键全局与右键智能仍可选择实际存在的工具。`ToolRequirements` 做无副作用原版预检；斧头命中时已经临时计入额外力量，不能重复叠加。镐子的额外伤害不代表等级提升。

兼容测试也接受 `bcmpinc.StardewHack` 和 `bcmpinc.HarvestWithScythe` 的模组目录，二者同时提供。测试目录不包含这些第三方模组的二进制文件。
