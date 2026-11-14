# InquiryWindow 技术文档

面向插件维护者与贡献者。用户安装/使用说明见 [README](../README.md)。

## 1. 兼容性与依赖

| 项目 | 版本 / 说明 |
| --- | --- |
| 宿主 | ClassIsland 2.2 开发分支（`manifest.yml` 的 `apiVersion: 2.2.0.0`） |
| 插件 SDK | `ClassIsland.PluginSdk` 2.1.1（NuGet） |
| 目标框架 | `net10.0` |
| UI 栈 | Avalonia 12.x + FluentAvalonia（由宿主/SDK 传递） |
| 清单平台 | `supportedOSPlatforms: [Windows]` |
| CI | `.github/workflows/dotnet-build.yml`，Windows + .NET 10，`dotnet publish -p:CreateCipx=true -c Release` |

代码在非 Windows 平台做了降级，但仍建议以 Windows 为主要验证环境：

- `IconExtractorService` 按平台拆分：Windows 用 `System.Drawing` 提取 `.exe` 关联图标，其它平台返回 `null`。
- Android 无法创建 Avalonia `Window`，`MultiButtonPromptAction` 在 Android 上会记录警告并跳过弹窗。

## 2. 目录结构

| 路径 | 职责 |
| --- | --- |
| `Plugin.cs` | 插件入口：Action/设置页注册、行动菜单树、配置目录注入、生命周期兜底 |
| `Actions/` | 三个 Action 的实现 |
| `Models/` | Action 设置、按钮/预设/结果组模型、枚举 |
| `Models/UI/` | 图标选择器 ViewModel 与图标项 |
| `ViewModels/` | 弹窗与设置页 ViewModel |
| `Views/` | 弹窗、设置控件、交互融合覆盖层、详细设置窗口 |
| `SettingsPage/` | 两个插件级设置页 |
| `Controls/` | 图标表达式编辑器、设置页懒加载容器 |
| `Services/` | 预设/全局设置存储、图标解析与迁移、变量替换、倒计时、图标提取 |
| `Helpers/SimpleExpression/` | 宿主图标表达式协议的轻量解析器（宿主内部解析器不可引用） |
| `Shared/InjectServices.cs` | 反射兼容不同版本宿主的设置页分组 API |
| `docs/TECHNICAL.md` | 本文档 |

## 3. 启动与注册流程

`Plugin.Initialize` 的执行顺序：

1. `services.AddLogging()`。
2. 遍历 `ActionRegistrations`，为每个 Action 注册 Action 与对应的 `ActionSettingsControl`。
3. 注册两个设置页：`InquiryWindowSettingsPage`、`ButtonPresetSettingsPage`。
4. `BuildActionMenuTree()`：把所有 Action 收进「InquiryWindow 行动」菜单组。
5. 注入 `PluginConfigFolder`，加载 `presets.json` 与 `plugin-settings.json`。
6. 订阅 `AppBase.Current.AppStopping`，退出前强制落盘。
7. 订阅 `AppBase.Current.AppStarted`，把插件设置页归入「InquiryWindow 设置」分组。

Action 元信息集中在 `Plugin.ActionRegistrations`（id / 名称 / 图标），新增 Action 时需要：

1. 在集合里加一行；
2. 在 `ActionRegistration.Register` 的 `switch` 里补注册；
3. 给 Action 类加 `[ActionInfo(..., addDefaultToMenu: false)]`。

设置页分组通过 `InjectServices` 反射探测宿主是否提供 `AddSettingsPageGroup`；不存在时回退为给页面名称加前缀。

| 设置页 | Id |
| --- | --- |
| 主设置页 | `inquiryWindow.settings.main` |
| 按钮预设库 | `inquiryWindow.settings.presets` |

两个设置页都套了 `SettingsPageLazy`：打开时先显示加载圈，内容渲染后淡入，并遵循宿主的「禁用动画等待」设置。

## 4. Action 行为

### 4.1 询问窗 `action.inquiryWindow`

- 设置类型：`InquiryWindowActionSettings`。
- 支持变量：`{time}` `{date}` `{subject}` `{nextSubject}`，由 `VariableReplacer` 替换。
- 目标路径可为 `.exe` / 任意文件 / 文件夹；`.exe` 可按开关提取关联图标（仅 Windows）。
- 默认打开独立窗口；开启 `IsInteractiveFusion` 后改为贴合 ClassIsland 主界面的覆盖层。
- 支持自动执行倒计时与亚克力背景（背景参数来自插件级 `PluginSettings`）。
- Windows 专用（`[SupportedOSPlatform("windows")]`）。

### 4.2 多按钮询问 `InquiryWindow.MultiButtonPrompt`

- 设置类型：`MultiButtonPromptSettings`。
- 每个按钮包含名称、图标表达式、独立 `ActionSet`。
- 按钮级能力：
  - 交互倒计时：打开弹窗后锁定按钮 N 秒，期间按钮禁用并显示剩余秒数。
  - 强调样式：默认 / 系统高亮 / 自定义颜色。
  - 操作验证：通过宿主的 `AuthorizeService` 校验凭据；失败时按 `VerificationFailureBehavior` 保持弹窗或关闭。
- 窗口级能力：
  - 标题、主提示、副提示（支持 Markdown，可关闭渲染回退纯文本）。
  - 置顶、是否允许标题栏关闭、自定义尺寸。
  - 背景图片（Cover / Contain / Stretch / Tile），加载失败降级为纯色。
  - 自动执行：倒计时结束后触发指定按钮的 Action 链或「无事发生」。
  - 进度条样式：`0` 线性、`1` 环形、`2` 隐藏。
  - 交互融合：用覆盖层替代独立窗口。
- 「详细设置」是独立的三栏窗口（窗口设置 / 按钮列表 / 按钮详情），直接编辑同一个设置实例。
- 按钮列表支持拖拽排序；拖拽手柄的 `PointerPressed/Moved/Released` 在 `MultiButtonPromptSettingsControl.axaml.cs` 中处理。

### 4.3 多结果行动 `InquiryWindow.MultiResult`

- 设置类型：`MultiResultSettings`，包含多个 `MultiResultGroup`（名称 + Action 链）。
- 不显示 UI，触发时按 `MultiResultOrderMode` 选一组后台执行：
  - `Sequential`：`(LastIndex + 1) % Count`，`LastIndex` 持久化。
  - `Random`：每次独立随机。
  - `RandomNoRepeat`：`RemainingOrder` + `Cursor` 维护洗牌队列；二者是运行时状态，不持久化。
- 集合增删导致旧索引失效时，`MultiResultAction` 会自动重洗。

## 5. 配置与持久化

| 数据 | 位置 | 所有者 |
| --- | --- | --- |
| 各 Action 设置 | 宿主档案（profile）JSON | 宿主通过 `ConfigureFileHelper` 反序列化 |
| 按钮预设库 | `<PluginConfigFolder>/presets.json` | `PresetsStore` |
| 插件全局设置 | `<PluginConfigFolder>/plugin-settings.json` | `PluginSettingsStore` |

- `PresetsStore` 使用 400 ms 防抖保存；预设增删、任意属性变化都会触发保存。
- `PluginSettingsStore.SaveNow()` 由设置页显式调用。
- 退出时 `Plugin.OnAppStopping` 调用 `PresetsStore.FlushPendingSaves()` 与 `PluginSettingsStore.SaveNow()`。
- 两个 Store 都是懒加载单例；`PluginConfigFolder` 未注入时 `Load()` 会延后，不带兜底路径。

`PluginSettings` 当前字段：

| 字段 | 说明 |
| --- | --- |
| `UseAcrylicBackground` | 询问窗是否启用亚克力背景 |
| `AcrylicTintOpacity` | 亚克力色调不透明度，加载时夹到 `[0,1]` |

## 6. 图标系统

### 6.1 字体来源

ClassIsland 使用 **Fluent System Icons Resizable**，不是 Windows 的 Segoe MDL2 / Segoe Fluent Icons：

- 字体资源：`avares://ClassIsland.Core/Assets/Fonts/#FluentSystemIcons-Resizable`
- 映射数据：`FluentSystemIcons-Resizable.json`（2666 个图标 × regular/filled）
- 码点范围：`U+E000` – `U+F4D3`，与 Segoe 的常用区（约 `U+E700` 起）完全不对应

因此 **禁止在插件里写裸码点**。正确写法：

```csharp
using ClassIsland.Core.Icons;

var icon = FluentIcons.ChatMultipleRegular;
```

```xml
xmlns:icons="clr-namespace:ClassIsland.Core.Icons;assembly=ClassIsland.Core"

<ci:FluentIcon Glyph="{x:Static icons:FluentIcons.SettingsRegular}" />
<controls:FASettingsExpander
    IconSource="{ci:FluentIconSource {x:Static icons:FluentIcons.SettingsRegular}}" />
```

`ci:FluentIcon` 构造时自动设置 Fluent 字体族；`FluentIconSource` 同理。图标常量分为 `Regular` / `Filled` 成对，设置页图标需要一对都提供。

### 6.2 图标表达式协议

配置中保存的是宿主图标表达式，而不是直接保存字形：

| 形式 | 说明 |
| --- | --- |
| `fluent("…")` | Fluent 字形 |
| `lucide("…")` | Lucide 字形 |
| `img("…")` | 图片路径 |
| 单个字形字符 | 历史数据，仍可解析 |
| `/uXXXX`、`\uXXXX` | 历史标记，仍可解析 |

统一的解析入口是 `IconExpressionService`：

- `Resolve` 把表达式解析为 `FAIconSource`；
- `FormatExpression` 生成标准表达式；
- `DescribeCodePoints` 把表达式转成 `U+XXXX` 描述；
- `LogIconChange` 在任意图标变化时输出 `[图标变更]` 日志。

图标编辑器 `IconExpressionEditor` 提供 Fluent / Lucide / 图像三个标签页，Fluent 与 Lucide 目录直接来自宿主的 `FluentIconKind` / `LucideIconKind` 枚举。

### 6.3 旧码点迁移

早期版本按 Segoe 习惯把码点写进默认值。插件启动/反序列化时会迁移以下旧默认值（仅这些，避免误改用户后来选择的 Fluent 图标）：

| 旧码点 | 在 Fluent 字体的实际字形 | 迁移为 |
| --- | --- | --- |
| `U+E10F` | arrow_minimize | `QuestionCircleRegular` |
| `U+E82D` | filmstrip_image | `ChatMultipleRegular` |
| `U+E73E` | document_sync | `CheckmarkRegular` |
| `U+E711` | document_page_number | `DismissRegular` |

迁移入口是 `IconMigration.TryMigrate`：

- `MultiButtonPromptSettings` / `MultiButtonPromptButton` 实现 `IJsonOnDeserialized`，在宿主加载档案后自动迁移；
- `PresetsStore.Load` 加载 `presets.json` 后迁移，并立即落盘；
- 迁移通过属性赋值完成，因此会照常触发 `[图标变更]` 日志。

若用户确实主动选择了上述四个 Fluent 码点之一，首次加载时会被迁移；这是有意的取舍，用于修复旧默认值造成的错误图标。

## 7. 窗口与交互实现

- 弹窗基类为 `ci:MyWindow`（`MyWindow`），通过 `ShowDialogCompat` 统一选择 owner 并显示。
- 多按钮弹窗关闭规则：
  - 默认拦截 `Closing`，必须点按钮关闭；
  - `IsClosable=true` 时允许标题栏 X，关闭按「关闭时执行的按钮」处理；
  - 最小化会自动还原。
- 自定义尺寸在打开前应用；`OnOpened` 再按屏幕工作区（`Screens.WorkingArea / RenderScaling`）收缩，避免超出屏幕。
- 自动执行/交互倒计时由 `SmoothCountdown` 驱动：16 ms 一帧更新进度，文字只在整秒变化。
- 交互融合由 `InquiryWindowFusionOverlay` 实现：无边框、透明、置顶，贴合 `AppBase.Current.MainWindow` 的位置与尺寸；主轴窗口不可用时回退为居中独立窗口。
- Markdown 渲染使用 `MarkdownScrollViewer`，关闭 `IsMarkdownRendered` 时切换为纯文本 `TextBlock`，两者互斥显示。

## 8. 构建与验证

```bash
# 本地构建
dotnet build InquiryWindow.csproj -c Debug

# 生成插件包（Windows CI 同款）
dotnet publish -p:CreateCipx=true -c Release
# 产物：cipx/InquiryWindow.cipx
```

当前仓库没有单元测试。改动后至少执行一次 `dotnet build`，并按改动范围手动验证：

| 改动范围 | 手动验证点 |
| --- | --- |
| 图标/图标迁移 | 旧 `presets.json` 与档案加载后图标正确；日志出现迁移记录 |
| 窗口属性/布局 | 小屏、高缩放、自定义尺寸、置顶、关闭/最小化 |
| 倒计时 | 线性/环形/隐藏三种样式；自动执行与交互倒计时互不干扰 |
| 预设库 | 新建/删除/编辑后重启仍存在；「从预设插入」为深拷贝 |
| 多结果行动 | 三种顺序模式；集合增删后索引不越界 |

## 9. 已知限制

- `manifest.yml` 仅声明 Windows；跨平台能力属于“可加载/可降级”，不是完整支持。
- 非 Windows 无法提取 `.exe` 关联图标。
- Android 不支持运行时弹窗（`Window` 创建受限）。
- 旧码点迁移只覆盖插件历史默认值，不做全量码点重映射。
- README 中的部分兼容性描述（.NET 版本、平台）可能滞后，以本文档与 `InquiryWindow.csproj` / `manifest.yml` 为准。
