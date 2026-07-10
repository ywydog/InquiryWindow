# 询问窗 (InquiryWindow)

# **非常注意，此插件为纯 AI 生成产物，真人测试，bug 处理可能不及时**

# 已知问题（未测试修复状态）
-在多按钮询问下行动疑似不能拖拽顺序
-在应用预设时，可能会直接让 CI 崩溃

ClassIsland 自动化 Action 插件。提供两类询问行动 + 一个插件级按钮预设库：

- **询问窗**（`action.inquiryWindow`）：弹窗含固定「执行 / 取消」两按钮，「执行」打开预配置的文件/软件/文件夹
- **多按钮询问**（`InquiryWindow.MultiButtonPrompt`）：弹窗可配置 N 个按钮，每个按钮独立绑定一个 Action 链（多个 Action 按顺序自动执行，**不再向用户询问**）
- **按钮预设库**（插件设置页）：集中管理可复用的按钮模板

## 特性

### 询问窗（v1）
- 弹窗置顶，**完全禁止通过 X 关闭**（最小化自动还原，必须点按钮）
- 阻塞自动化流程，等用户操作后才继续
- 标题和正文支持 ClassIsland 上下文变量
- 自动从 .exe 提取关联图标
- 目标路径可以是 .exe / 任意文件 / 文件夹

### 多按钮询问（v2）
- 1～N 个按钮自由配置（默认 2 个：「执行 / 取消」）
- 每个按钮独立绑定 Action 链（可使用任何 ClassIsland 内置 Action）
- 按钮可增删、可重命名、可重排顺序
- 按下按钮 → 自动顺序执行该按钮的 Action 链 → 弹窗关闭（链执行过程**不再弹任何询问窗**）
- 弹窗置顶、禁用 X 关闭
- 链中 Action 抛异常不影响后续执行

### 按钮预设库
- 插件设置 → "InquiryWindow 设置" → 按钮预设库
- 在此统一管理可复用按钮（名称 + 图标 + Action 链）
- 在多按钮询问的设置面板里，每个按钮的 Expander 上点 "从预设插入" 即可把预设里的 Action 链追加到当前按钮

## 安装

把编译产物 `InquiryWindow.cipx`（或整个 `InquiryWindow/` 目录）放到 ClassIsland 的 `Plugins/` 目录，重启 ClassIsland。

## 使用

### 询问窗（v1）

在 ClassIsland 自动化编辑中添加「询问窗」行动，配置：
- 窗口标题（OS 标题栏）
- 弹窗标题（窗口内顶部）
- 弹窗正文
- 目标路径

支持变量：`{time}` `{date}` `{subject}` `{nextSubject}`

### 多按钮询问（v2）

在 ClassIsland 自动化编辑中添加「多按钮询问」行动，配置：
- 窗口标题、主提示、副提示
- 按钮列表（可增删）：每个按钮填名称 + 图标 + 用 `ActionControl` 配置 Action 链
- 也可以先在插件设置里建好预设，再在按钮里点 "从预设插入"

### 按钮预设库

到插件设置页 "InquiryWindow 设置" → 按钮预设库 → + 新建预设，编辑名称/图标/Action 链。

## 兼容性

- ClassIsland 2.x
- .NET 8
- 仅 Windows（依赖 System.Drawing.Common）
