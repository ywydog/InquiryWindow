# 询问窗 (InquiryWindow)

ClassIsland 自动化 Action 插件。触发后弹出询问窗口（置顶、不可关闭、阻塞），让教师点击「执行」打开预配置的文件/软件/文件夹，点击「取消」跳过。

## 特性

- 弹窗置顶，**完全禁止通过 X 关闭**（最小化自动还原，必须点按钮）
- 阻塞自动化流程，等用户操作后才继续
- 标题和正文支持 ClassIsland 上下文变量
- 自动从 .exe 提取关联图标
- 目标路径可以是 .exe / 任意文件 / 文件夹

## 安装

把编译产物 `InquiryWindow.cipx`（或整个 `InquiryWindow/` 目录）放到 ClassIsland 的 `Plugins/` 目录，重启 ClassIsland。

## 使用

在 ClassIsland 自动化编辑中添加「询问窗」行动，配置：
- 窗口标题（OS 标题栏）
- 弹窗标题（窗口内顶部）
- 弹窗正文
- 目标路径

## 支持变量

- `{time}` 当前时间（HH:mm）
- `{date}` 当前日期（yyyy-MM-dd）
- `{subject}` 当前课程
- `{nextSubject}` 下一节课
- `{classroom}` 当前教室

找不到对应值时，渲染为空字符串。

## 兼容性

- ClassIsland 2.x
- .NET 8
- 仅 Windows（依赖 System.Drawing.Common）
