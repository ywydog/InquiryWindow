using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Core.Models.Automation;
using InquiryWindow.Actions;
using InquiryWindow.Services;
using InquiryWindow.SettingsPage;
using InquiryWindow.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace InquiryWindow;

[PluginEntrance]
public partial class Plugin : PluginBase
{
    /// <summary>
    /// 行动根菜单组名：所有 InquiryWindow Action 都归到这里。
    /// 借鉴 SystemTools 的「SystemTools 行动」设计 —— 多个 Action 集中到一个集。
    /// </summary>
    private const string ActionGroupName = "InquiryWindow 行动";

    /// <summary>根菜单组图标：与 InquiryWindowSettingsPage 上使用的图标保持一致。</summary>
    private const string ActionGroupIcon = "\uE82D";

    /// <summary>
    /// 插件全部 Action 的元信息集合（id / name / icon 三元组）。
    /// 数据驱动：注册到 <see cref="IServiceCollection"/> 和构造菜单树时都遍历这个集合，
    /// 避免出现"Action 注册了一个、菜单加了另一个"两边对不上的情况。
    /// </summary>
    private static readonly IReadOnlyList<ActionRegistration> ActionRegistrations = new ActionRegistration[]
    {
        new("action.inquiryWindow",            "询问窗",       "\uE4C4"),
        new("InquiryWindow.MultiButtonPrompt", "多按钮询问",   "\uE82D"),
        new("InquiryWindow.MultiResult",       "多结果行动",   "\uE8B5"),
    };

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 注册日志服务与行动提供方（含设置控件）。
        services.AddLogging();

        // 行动注册：遍历集合调用 AddAction，集中表达"插件提供了哪些 Action"。
        // 菜单归类交给下面的 BuildActionMenuTree，Action 上的 [ActionInfo] 用
        // addDefaultToMenu: false 关掉系统的自动归类，避免重复。
        foreach (var reg in ActionRegistrations)
        {
            reg.Register(services);
        }

        // 插件级 ViewPage：按钮预设库（独立页面）+ 主设置页（弹窗外观 / 预览）。
        // 多页面注册风格与 SystemTools 一致：在 Plugin.Initialize 里多次 AddSettingsPage。
        services.AddSettingsPage<InquiryWindowSettingsPage>();
        services.AddSettingsPage<ButtonPresetSettingsPage>();

        // 把所有 Action 注册到一个"集"（菜单根组）。
        BuildActionMenuTree();

        // 初始化按钮预设库（落盘到 PluginConfigFolder/presets.json）
        PresetsStore.PluginConfigFolder = PluginConfigFolder;
        PresetsStore.Instance.Load();

        // 初始化插件全局设置（落盘到 PluginConfigFolder/plugin-settings.json）
        PluginSettingsStore.PluginConfigFolder = PluginConfigFolder;
        PluginSettingsStore.Instance.Load();
    }

    /// <summary>
    /// 借鉴 SystemTools.BuildBaseActionTree：把所有 Action 归到一个根菜单组下。
    /// 集合（<see cref="ActionRegistrations"/>）数据驱动，保证菜单项与
    /// <see cref="IServiceCollection"/> 中注册的 Action 一一对应。
    /// </summary>
    private void BuildActionMenuTree()
    {
        var group = new ActionMenuTreeGroup(ActionGroupName, ActionGroupIcon);
        IActionService.ActionMenuTree.Add(group);

        foreach (var reg in ActionRegistrations)
        {
            group.Children.Add(new ActionMenuTreeItem(reg.Id, reg.Name, reg.IconGlyph));
        }
    }
}

/// <summary>
/// 单个 Action 的元信息 + 注册逻辑（数据驱动的"集"元素）。
/// 与 <see cref="Plugin.ActionRegistrations"/> 配套使用：
/// - 字段对应 [ActionInfo] 的 id / name / iconGlyph
/// - <see cref="Register"/> 在 DI 中注册对应的 Action + SettingsControl
/// 这样新增 Action 只需往集合里加一行，不再两处手动同步。
/// </summary>
internal record ActionRegistration(string Id, string Name, string IconGlyph)
{
    /// <summary>
    /// 在 DI 中注册本 Action 及其设置控件。
    /// 这里的 switch 是为了把「Action 类型 + 可选 SettingsControl」对应起来，
    /// 避免在 Plugin 里散落多个 if / AddAction 重载。
    /// </summary>
    public void Register(IServiceCollection services)
    {
        switch (Id)
        {
            case "action.inquiryWindow":
                services.AddAction<InquiryWindowAction, InquiryWindowSettingsControl>();
                break;
            case "InquiryWindow.MultiButtonPrompt":
                services.AddAction<MultiButtonPromptAction, MultiButtonPromptSettingsControl>();
                break;
            case "InquiryWindow.MultiResult":
                services.AddAction<MultiResultAction, MultiResultSettingsControl>();
                break;
            default:
                throw new InvalidOperationException(
                    $"未为 Action「{Id}」配置 Register 实现，请在 Plugin.ActionRegistration.Register 中补齐。");
        }
    }
}
