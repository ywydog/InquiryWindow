# 多按钮询问（InquiryWindow.MultiButtonPrompt）— 设计文档

- 日期：2026-06-28
- 状态：草稿，待用户审阅
- 适用仓库：`InquiryWindow`
- 范围：在现有 `InquiryWindow` 插件中**新增**一个 Action；旧 Action 保持不变

---

## 1. 背景与目标

### 1.1 现状

`InquiryWindow` 当前只有一个 Action「询问窗」（id: `InquiryWindow.Open`）：

- 弹窗包含两个固定按钮（执行 / 取消）
- 「执行」按钮触发 `Process.Start(TargetPath)`，用于打开一个文件、程序或文件夹
- 「取消」按钮仅关闭弹窗
- 设置面板只能配置标题/小字/主信息/目标路径/图标

这个能力对"询问用户一个简单二元选择 + 立即打开某个东西"已经够用，但**缺乏自动化能力**：

- 用户希望在按按钮后能**触发一个或多个 ClassIsland 自动化 Action**（发通知、播声音、写文件、推流到其他触发器……），而不是只打开一个外部文件
- 按钮数量必须是 2 个，无法加补充按钮
- 按钮含义被「执行/取消」固定，不能改名为"再响铃"/"稍后提醒"等

### 1.2 目标

新增 Action「多按钮询问」（`InquiryWindow.MultiButtonPrompt`），允许用户：

1. 在系统自动化的工作流里插入该 Action
2. 配置 1～N 个按钮（默认 2 个："执行" / "取消"）
3. 每个按钮独立绑定一个 Action 链（≥0 个 Action）
4. 触发时弹窗，按下任一按钮：关闭弹窗并按顺序执行该按钮的 Action 链
5. 按钮可重命名、可增删、可调整顺序

### 1.3 非目标

- **不**修改旧 Action「询问窗」（`InquiryWindow.Open`）的任何字段、行为、id、显示名
- **不**支持按钮响应链以外的扩展点（例如按钮被点击时回调、按钮 hover 提示等）
- **不**实现嵌套弹窗处理（链里如果触发另一个"多按钮询问"，走 ClassIsland 自身的栈）
- **不**给插件加新 `ViewPage` 设置页（按钮配置只属于这个 Action 自身的 `ActionSettingsControl`）
- **不**改动工作流文件 `dot-net-build.yml`（构建流程已经能跑，再加一个新 Action 不需要改）

---

## 2. 架构

### 2.1 组件视图

```
                 ┌──────────────────────────────┐
                 │  ClassIsland 自动化系统       │
                 │  （工作流 + 触发器）         │
                 └──────────────┬───────────────┘
                                │ 触发
                                ▼
        ┌──────────────────────────────────────┐
        │  MultiButtonPromptAction             │  ← 新增
        │  (ActionBase<MultiButtonPromptSettings>) │
        └──────────────┬───────────────────────┘
                       │ OnInvoke
                       ▼
        ┌──────────────────────────────────────┐
        │  MultiButtonPromptWindow              │
        │  (MyWindow, 显示 N 个按钮)            │
        └──────────────┬───────────────────────┘
                       │ 点击按钮 i
                       ▼
        ┌──────────────────────────────────────┐
        │  IActionService.InvokeActionSetAsync │  ← 系统服务
        │  (传入临时构造的 ActionSet)            │
        └──────────────────────────────────────┘
```

### 2.2 配置文件视图

```
[Action 1: MultiButtonPrompt]
  Settings = {
    Title: "要继续吗？",
    Prompt: "主提示文本",
    SubPrompt: "副提示文本",
    Buttons: [
      { Name: "执行",   Icon: "\uE73E", Actions: [ <ActionItem 1>, <ActionItem 2> ] },
      { Name: "取消",   Icon: "\uE711", Actions: [] }
    ]
  }
```

`Buttons[i].Actions` 直接复用 `ClassIsland.Shared.Models.Automation.ActionSet.ActionItems`，**不**自创新的容器类型。

### 2.3 新增/改动文件

| 文件 | 类型 | 说明 |
|---|---|---|
| `Actions/MultiButtonPromptAction.cs` | 新增 | Action 主类，`ActionBase<MultiButtonPromptSettings>` |
| `Models/MultiButtonPromptSettings.cs` | 新增 | 顶层设置（标题/按钮集合） |
| `Models/MultiButtonPromptButton.cs` | 新增 | 单个按钮设置（Name/Icon/ActionSet） |
| `Views/MultiButtonPromptSettingsControl.axaml` | 新增 | Action 的设置面板 axaml |
| `Views/MultiButtonPromptSettingsControl.axaml.cs` | 新增 | 配套代码后置 |
| `Views/MultiButtonPromptWindow.axaml` | 新增 | 弹窗 axaml |
| `Views/MultiButtonPromptWindow.axaml.cs` | 新增 | 弹窗代码后置 |
| `Plugin.cs` | 改动 | `services.AddAction<MultiButtonPromptAction, MultiButtonPromptSettingsControl>()` |
| `manifest.yml` | 改动 | 不改入口 dll，id 仍由 `[ActionInfo]` 标注 |
| `InquiryWindow.csproj` | 改动 | 包含新文件（csproj 已用通配包含新文件则可能不用改） |
| `README.md` | 改动 | 文档说明新 Action |

> 注：v1 的所有文件保持原状。

---

## 3. 详细设计

### 3.1 `MultiButtonPromptSettings`

```csharp
namespace InquiryWindow.Models;

public class MultiButtonPromptSettings
{
    public string Title { get; set; } = "ClassIsland - 询问";
    public string Prompt { get; set; } = "请选择";
    public string SubPrompt { get; set; } = string.Empty;
    public ObservableCollection<MultiButtonPromptButton> Buttons { get; set; } = new();

    public MultiButtonPromptSettings()
    {
        Buttons.Add(new MultiButtonPromptButton { Name = "执行", Icon = "\uE73E" });
        Buttons.Add(new MultiButtonPromptButton { Name = "取消", Icon = "\uE711" });
    }
}
```

- `ObservableCollection<>` 以便设置面板双向绑定
- 构造里默认放两个按钮：执行 / 取消
- 持久化由 ClassIsland 完成（通过 `[JsonPropertyName]` + ActionItem 自带序列化）

### 3.2 `MultiButtonPromptButton`

```csharp
namespace InquiryWindow.Models;

public class MultiButtonPromptButton
{
    public string Name { get; set; } = "按钮";
    public string Icon { get; set; } = "\uE10F";
    public ActionSet Actions { get; set; } = new();
}
```

- `Actions` 复用 `ClassIsland.Shared.Models.Automation.ActionSet`
- 序列化时整个 `ActionSet` 嵌套在按钮对象里

### 3.3 `MultiButtonPromptAction`

```csharp
namespace InquiryWindow.Actions;

[ActionInfo("InquiryWindow.MultiButtonPrompt", "多按钮询问", "\uE82D")]
public class MultiButtonPromptAction : ActionBase<MultiButtonPromptSettings>
{
    protected override async Task OnInvoke()
    {
        if (Settings.Buttons.Count == 0) return;

        var window = new MultiButtonPromptWindow
        {
            DataContext = new MultiButtonPromptViewModel(Settings)
        };
        await window.ShowDialog();
    }
}
```

- `OnInvoke` 调 `ShowDialog` 等待用户选择
- ViewModel 负责把按钮点击映射成 ActionSet 并触发

### 3.4 `MultiButtonPromptViewModel`

```csharp
namespace InquiryWindow.ViewModels;

public class MultiButtonPromptViewModel : ObservableRecipient
{
    public ObservableCollection<MultiButtonPromptButton> Buttons { get; }
    public string Title { get; }
    public string Prompt { get; }
    public string SubPrompt { get; }

    public MultiButtonPromptViewModel(MultiButtonPromptSettings settings)
    {
        Buttons = settings.Buttons;
        Title = settings.Title;
        Prompt = settings.Prompt;
        SubPrompt = settings.SubPrompt;
    }

    [RelayCommand]
    public async Task PressButton(MultiButtonPromptButton button)
    {
        var actionService = IAppHost.GetService<IActionService>();
        if (actionService != null && button.Actions.ActionItems.Count > 0)
        {
            try
            {
                await actionService.InvokeActionSetAsync(button.Actions);
            }
            catch (Exception ex)
            {
                // 单个 Action 失败不应阻断弹窗关闭，其它 Action 也应继续。
                // ClassIsland 的 InvokeActionSetAsync 不吞异常，这里手动保护。
                System.Diagnostics.Debug.WriteLine($"[InquiryWindow] 链执行失败：{ex}");
            }
        }
        RequestClose?.Invoke();
    }

    public event Action? RequestClose;
}
```

- `PressButton` 通过 DI 拿 `IActionService` 执行按钮的 Action 链
- 链为空时只关闭弹窗

### 3.5 弹窗 `MultiButtonPromptWindow.axaml`

```xml
<my:MyWindow xmlns:my="..."
             Title="{Binding Title}"
             ...>
  <StackPanel Margin="24" Spacing="16">
    <TextBlock Text="{Binding Prompt}" FontSize="22" />
    <TextBlock Text="{Binding SubPrompt}" FontSize="14"
               Foreground="{DynamicResource TextFillColorSecondaryBrush}"
               IsVisible="{Binding SubPrompt, Converter={x:Static StringConverters.IsNotNullOrEmpty}}" />

    <ItemsRepeater ItemsSource="{Binding Buttons}" Margin="0,16,0,0">
      <ItemsRepeater.Layout>
        <StackLayout Orientation="Horizontal" Spacing="12" />
      </ItemsRepeater.Layout>
      <ItemsRepeater.ItemTemplate>
        <DataTemplate>
          <Button Content="{Binding Name}" Command="{Binding $parent[Window].DataContext.PressButtonCommand}"
                  CommandParameter="{Binding}" />
        </DataTemplate>
      </ItemsRepeater.ItemTemplate>
    </ItemsRepeater>
  </StackPanel>
</my:MyWindow>
```

- 复刻 v1「询问窗」风格：居中、强制置顶、禁止 X 关闭
- 用 `ItemsRepeater` 渲染按钮列表，按钮数等于 `Settings.Buttons.Count`

### 3.6 设置面板 `MultiButtonPromptSettingsControl.axaml`

```xml
<ci:ActionSettingsControlBase ...>
  <StackPanel Margin="12" Spacing="12">
    <!-- 标题/主提示/副提示 -->
    <TextBox Watermark="窗口标题" Text="{Binding Settings.Title, Mode=TwoWay}" />
    <TextBox Watermark="主提示" Text="{Binding Settings.Prompt, Mode=TwoWay}" />
    <TextBox Watermark="副提示（可选）" Text="{Binding Settings.SubPrompt, Mode=TwoWay}" />

    <!-- 按钮列表 -->
    <TextBlock Text="按钮" FontSize="16" />
    <ItemsControl ItemsSource="{Binding Settings.Buttons}">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <Expander Header="{Binding Name}">
            <StackPanel Spacing="8">
              <TextBox Watermark="按钮名称" Text="{Binding Name, Mode=TwoWay}" />
              <TextBox Watermark="图标" Text="{Binding Icon, Mode=TwoWay}" />
              <!-- 复用 ClassIsland 自带的 ActionControl -->
              <core:ActionControl ActionSet="{Binding Actions}" />
            </StackPanel>
          </Expander>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
    <Button Content="+ 添加按钮" Click="OnAddButtonClick" />
  </StackPanel>
</ci:ActionSettingsControlBase>
```

- **核心**：`<core:ActionControl ActionSet="{Binding Actions}" />` 直接嵌入 ClassIsland 自带的 Action 链编辑器
- `ActionControl` 自带"添加/删除/拖拽排序/选 Action/动态加载该 Action 的设置控件"全套能力
- 用户在每个按钮的 Expander 里就能完成"这个按钮触发哪些 Action"的配置

### 3.7 Plugin.cs 注册

```csharp
services.AddAction<MultiButtonPromptAction, MultiButtonPromptSettingsControl>();
```

放在 `InquiryWindowAction` 注册之后即可。ClassIsland 会自动：
- 把它登记到 `IActionService.ActionInfos`（id = `InquiryWindow.MultiButtonPrompt`）
- 出现在工作流编辑器右边的 Action 菜单中
- 在选中该 Action 时自动加载 `MultiButtonPromptSettingsControl`

---

## 4. 数据流

### 4.1 配置阶段

1. 用户在系统自动化里添加 "多按钮询问" Action
2. ClassIsland 反序列化 `MultiButtonPromptSettings`（含 Buttons + ActionSet）
3. 系统渲染 `MultiButtonPromptSettingsControl`，绑到 `Settings`
4. 用户编辑按钮 / Action 链 → 实时写回 `Settings` → ClassIsland 自动保存
5. 关闭时 ClassIsland 持久化整个 `Settings` 到工作流文件

### 4.2 运行阶段

1. 触发器触发工作流 → `MultiButtonPromptAction.OnInvoke()`
2. Action 用 `ShowDialog` 弹出 `MultiButtonPromptWindow`
3. 弹窗显示标题 + 主/副提示 + N 个按钮
4. 用户点击按钮 i → `ViewModel.PressButton(button)`
5. ViewModel 通过 `IAppHost.GetService<IActionService>()` 拿 `IActionService`
6. 调用 `InvokeActionSetAsync(button.Actions)`
7. 链中每个 Action 顺序执行；任意一个抛异常只记日志，不影响后续
8. 链执行完毕（或链为空）→ `RequestClose` 事件 → 弹窗关闭
9. `OnInvoke` 的 `await window.ShowDialog()` 完成
10. 工作流继续走下一项

### 4.3 链的执行方式（关键约束）

**一个按钮触发后，链里所有 Action 顺序自动执行，整个过程不再向用户询问、不再弹任何提示窗。**

- 链由 `IActionService.InvokeActionSetAsync(ActionSet)` 一次性执行，该 API 本身即按顺序串行执行链中所有 Action
- 链里**禁止**再插入会向用户询问的 Action（如本插件的"询问窗"/"多按钮询问"），如果用户这样配，ClassIsland 会按其自身栈处理嵌套弹窗
- 链执行期间不阻塞 UI 主线程（在 ClassIsland 内部由 `IActionService` 调度）
- 任一 Action 抛异常 → 写到 `ActionItem.Exception` 字段、记录日志，**继续**执行后续 Action
- 整个链跑完（或链为空）后弹窗才关闭

### 4.4 异常处理

- **`IActionService` 拿不到**：弹窗打不开，记 `LogError` 后 `OnInvoke` 返回
- **弹窗里按钮触发链抛异常**：`IActionService.InvokeActionSetAsync` **不会**自动吞异常，由 `ViewModel.PressButton` 的 `try/catch` 保护，失败仅记 Debug 日志，不影响弹窗关闭
- **链中 Action 不存在**（被卸载/拼错 id）：`ActionBase.GetInstance` 会 `MigrateUnknownActionItem`，无法迁移时返回 `null`，该 Action 跳过不执行
- **按钮名为空**：ViewModel 在渲染时用 `string.IsNullOrEmpty(button.Name)` 退化为"按钮 {i+1}"

---

## 5. 隔离与边界

- **`MultiButtonPromptAction`** 只负责"创建并显示弹窗"，不知道 Action 链怎么跑
- **`MultiButtonPromptViewModel`** 只负责"接收点击 + 调用 `IActionService`"，不知道弹窗长什么样
- **`MultiButtonPromptWindow`** 只负责 UI 渲染 + 触发 `ViewModel.PressButton`
- **`MultiButtonPromptSettings`** 是数据，不依赖任何 UI 类型
- **Action 链本身**由 ClassIsland 拥有，插件不维护其执行逻辑

每个文件都能独立读、独立改；UI 控件换样式不需要动 ViewModel；链执行失败不需要动 UI 代码。

---

## 6. 测试

由于沙箱无 `dotnet` SDK，无法本地运行测试。验证策略：

- **静态检查**：把改完的代码逐文件 review（命名空间/类名/资源路径一致）
- **构建验证**：推送到 GitHub 后 `dotnet-build.yml` 工作流会跑 `dotnet publish`，所有错误会在 Actions 日志里暴露
- **手工测试清单**（用户在使用 ClassIsland 加载插件后做）：
  1. 工作流里添加"多按钮询问"，设置 2 个按钮
  2. 给按钮 1 添加一个 `ShowToast` Action
  3. 触发工作流 → 看到弹窗
  4. 点击按钮 1 → 通知出现 + 弹窗关闭
  5. 把按钮 1 链清空 → 重新触发 → 弹窗关掉，链为空不报错
  6. 拖拽按钮 2 排到按钮 1 之前 → 重新触发 → 弹窗按钮顺序变化
  7. 在设置面板里再添加一个按钮 3 → 弹窗上出现 3 个按钮
  8. 把按钮 1 改名为"再响铃" → 弹窗按钮显示为"再响铃"
  9. 链里加一个不存在的 Action id → 触发 → 不报错，弹窗正常关闭
  10. 启动旧 Action「询问窗」 → 行为完全不变

---

## 7. 风险与缓解

| 风险 | 缓解 |
|---|---|
| `IActionService` 在插件上下文不可用 | 走 `IAppHost.GetService<>()`，拿不到时 `LogError` 并提前返回 |
| 旧用户配置里 `InquiryWindow.Open` 被改坏 | 新 Action 用新 id，v1 文件不改动 |
| 按钮内嵌 `ActionControl` 的样式和 v1 设置面板不搭 | 用 `Expander` 包起来给一个明显的视觉边界 |
| 弹窗内按钮 0 个时无意义 | `OnInvoke` 检查 `Buttons.Count == 0` 直接 return |
| 弹窗被其它窗口挡住 | 沿用 v1 强制置顶 |
| 弹窗 X 按钮关不掉链 | 沿用 v1 禁用 X；链触发用 `await InvokeActionSetAsync` 在按钮事件里调 |
| 嵌套弹窗（链里再触发多按钮询问） | 不处理，走 ClassIsland 自身栈（如果出现死锁用户自己改链） |
| 链中 Action 异常中断后续 | 不阻断，ClassIsland 自带 `catch` 写到 `ActionItem.Exception` |

---

## 8. 范围与未来工作（YAGNI 暂不实现）

- ❌ 按钮图标/主题/颜色自定义（用 Fluent 字体图标）
- ❌ 弹窗倒计时/自动选择（v1 也没有）
- ❌ 按钮点击后留弹窗不关（用户没要求）
- ❌ 按钮按"主/次/危险"区分样式
- ❌ 多按钮询问触发后保存/复用按钮预设
- ❌ 插件级 `ViewPage` 管理"默认按钮模板"（用户已确认不需要）
- ❌ 国际化（依赖 ClassIsland 自身 i18n）
