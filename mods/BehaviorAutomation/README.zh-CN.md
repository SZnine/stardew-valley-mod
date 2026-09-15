<p align="center"><a href="README.md">English</a> · <a href="README.zh-CN.md"><strong>简体中文</strong></a></p>

![行为学](assets/header-1300x372.png)

<h1 align="center">行为学</h1>
<p align="center">框选一片区域，让农夫完成琐碎日常。</p>
<p align="center"><a href="https://github.com/SZnine/stardew-valley-mod/releases/tag/behavior-automation-v2.3.0">下载 2.3.0</a> · <a href="https://www.nexusmods.com/stardewvalley/mods/52289">Nexus Mods</a> · <a href="#操作">操作</a> · <a href="docs/CHANGELOG.zh-CN.md">更新记录</a></p>

通过框选安排农活、动物照料和农场改建。角色自动寻路，使用实际工具和材料，保留原版体力消耗与工具限制。

拖框时显示宽 × 高（格数），可在 F8 设置页或 GMCM 关闭。

## 实机演示

<table>
<tr><th>智能换工具</th><th>成片收获</th></tr>
<tr>
<td align="center" valign="top" width="50%"><img src="assets/demo-gifs/02-smart-tools.gif" alt="框选混合杂物后寻路、换工具清理" width="380"><br>根据目标切换已有工具，清理混合杂物。</td>
<td align="center" valign="top" width="50%"><img src="assets/demo-gifs/01-scythe-harvest.gif" alt="成片收割成熟小麦，保留下方幼苗" width="380"><br>成片收割成熟作物，保留幼苗。</td>
</tr>
<tr><th>动物日常照料</th><th>蓄力浇水与补水</th></tr>
<tr>
<td align="center" valign="top" width="50%"><img src="assets/demo-gifs/03-animal-care.gif" alt="抚摸、挤奶、剪毛和添干草" width="380"><br>完成抚摸、挤奶、剪毛和添干草。</td>
<td align="center" valign="top" width="50%"><img src="assets/demo-gifs/04-water-refill.gif" alt="蓄力浇水，缺水时走向池塘补水" width="380"><br>按水壶等级蓄力，缺水时寻找可达水源。</td>
</tr>
</table>

## 操作

| 默认按键 | 功能 |
| :--- | :--- |
| **Shift + 左键拖动** | 使用选定工具或材料，并加入已启用的左键全局行为。 |
| **Shift + 右键拖动** | 从右键工具池中选择合适的可用工具。 |
| **F8** | 打开行为配置面板。 |
| **再次按 Shift**、打开背包或**持续移动 0.5 秒** | 取消当前任务。 |

拖框到屏幕边缘可移动视角，继续扩大选区。松开鼠标开始作业，新选区替换旧任务。短暂手动移动时优先响应玩家，松开后继续作业。

## 支持的工作

| 类别 | 内容 |
| :--- | :--- |
| **农田与采集** | 开垦、挖掘点、播种、预留间距种树、浇水、成熟作物收获，以及采集物、果实、苔藓和机器成品。 |
| **清理与开采** | 杂草、牧草、枯苗、树枝、树木、树桩、矿石，以及当前工具可破坏的大型障碍。 |
| **动物日常照料** | 抚摸动物与宠物、挤奶、剪毛、添干草、宠物水碗加水和收取产物。 |
| **建筑内部** | 进入选中的鸡舍、畜棚和工坊，完成允许的任务后返回外部选区。 |
| **农场改建** | 铺设、拆除地板与路径，放置围栏、火把、洒水器、箱子和机器。 |

开垦、挖掘点、种植、放置和拆地板需要手持相应道具，通过左键框选执行。动物出售、迁移、改名和贵重道具使用仍由玩家手动操作。

## 选择行为

**F8** 面板包含三个独立行为页和一个设置页。点击图标切换，高亮表示启用；行为页支持**全选**与**清空**。

| 页面 | 作用 |
| :--- | :--- |
| **左键基础** | 手持选定工具、种子或材料时执行的行为。 |
| **左键全局** | 不限手持道具的附加行为，默认包含采集与动物日常照料。 |
| **右键工具池** | 允许自动选择工具的行为。 |
| **设置** | 样式、快捷键、选框尺寸、移镜、建筑内作业、仓库工具和树干间距。 |

安装 [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) 后，可设置快捷键、移动取消时间、斜向寻路、镰刀走位、补水、清障、仓库工具和体力下限。未安装时，F8 面板仍可使用。

## 安装与更新

1. 为 **Stardew Valley 1.6.15+** 安装 [SMAPI 4.5.2+](https://smapi.io/)。
2. 下载模组安装包，将其中的 **BehaviorAutomation** 文件夹解压到游戏 **Mods** 目录。
3. 通过 SMAPI 启动游戏。Generic Mod Config Menu 为可选依赖。

更新前退出游戏，保留 `config.json`，覆盖旧版文件。

<details>
<summary><strong>工具、仓库与联机</strong></summary>

左键基础任务及其清障使用选定的那把工具。智能和附加行为可选择已有工具。单人游戏可从仓库借用工具，用后归还；联机仅使用背包工具。

</details>

<details>
<summary><strong>从源码构建</strong></summary>

安装支持 `net6.0` 的 .NET SDK，在仓库根目录运行：

```powershell
./mods/BehaviorAutomation/build.ps1 -GamePath 'C:\Games\Stardew Valley'
```

输出位于 `.artifacts/BehaviorAutomation/package/`。

</details>

[使用说明](docs/USER-GUIDE.zh-CN.md) · [开发与测试](docs/DEVELOPMENT.md) · [反馈问题](https://github.com/SZnine/stardew-valley-mod/issues) · [MIT 许可](LICENSE)

感谢 ConcernedApe、SMAPI 与 Generic Mod Config Menu 的作者和维护者，以及提供测试和反馈的玩家。

## 开发方向

后续开发将以选框内自动化为主干，优先完善现有操作、寻路与兼容性。为控制模组体量和维护成本，尽量减少额外智能处理和职责外的子功能。


配置页默认侧栏图标，选框默认网格；F8 设置页可切换三种配置页样式和三种选框样式，并修改框选、面板、取消三个快捷键。
