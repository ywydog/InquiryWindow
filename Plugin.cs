using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using InquiryWindow.Actions;
using InquiryWindow.Services;
using InquiryWindow.SettingsPage;
using InquiryWindow.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InquiryWindow;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 注册日志服务与行动提供方（含设置控件）。
        services.AddLogging();

        // v1 行动：询问窗（打开文件/文件夹）
        services.AddAction<InquiryWindowAction, InquiryWindowSettingsControl>();

        // v2 行动：多按钮询问（按钮链触发多个 Action）
        services.AddAction<MultiButtonPromptAction, MultiButtonPromptSettingsControl>();

        // 插件级 ViewPage：按钮预设库
        services.AddSettingsPage<InquiryWindowSettingsPage>();

        // 初始化按钮预设库（落盘到 PluginConfigFolder/presets.json）
        PresetsStore.PluginConfigFolder = PluginConfigFolder;
        PresetsStore.Instance.Load();

        // 初始化插件级设置（亚克力背景开关等，落盘到 PluginConfigFolder/plugin-settings.json）。
        // 必须在 PresetsStore 之后初始化，便于设置页加载时同时拿到两者。
        PluginSettingsStore.PluginConfigFolder = PluginConfigFolder;
        PluginSettingsStore.Instance.Load();
    }
}
