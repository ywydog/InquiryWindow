using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Core.Models.Automation;
using ClassIsland.Core.Services.Registry;
using InquiryWindow.Actions;
using InquiryWindow.Services;
using InquiryWindow.SettingsPage;
using InquiryWindow.Shared;
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
    /// 设置页面分组 ID：所有 InquiryWindow 设置页面都归属到这个分组。
    /// 命名与 SystemTools 的 <c>systemtools.settings</c> 保持风格一致。
    /// </summary>
    private const string SettingsGroupId = "inquirywindow.settings";

    /// <summary>设置页面分组图标（与主菜单图标一致，方便辨识）。</summary>
    private const string SettingsGroupIcon = "\uE82D";

    /// <summary>设置页面分组的显示名（回退方案中也会作为名称前缀使用）。</summary>
    private const string SettingsGroupName = "InquiryWindow 设置";

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

        // 把所有设置页面归入 InquiryWindow 设置分组。
        // 必须在 AppStarted 后再执行：此时 SettingsWindowRegistryService.Registered 已经被填充，
        // 我们才能拿到所有已注册页面来打 GroupId。
        AppBase.Current.AppStarted += (_, _) => RegisterSettingsPageGroup(services);
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

    /// <summary>
    /// 把所有 <c>Id</c> 以 <see cref="SettingsGroupId"/> 开头的 <see cref="SettingsPageInfo"/>
    /// 收编到 InquiryWindow 设置分组下。
    ///
    /// <para>
    /// 借鉴 <c>SystemTools.Plugin.RegisterSettingsPageGroup</c>：用反射探测当前加载的
    /// ClassIsland 版本是否暴露 <c>AddSettingsPageGroup</c> 4 参数重载——
    /// </para>
    /// <list type="bullet">
    ///   <item>存在 → 调用它注册分组（图标 + 名称），再反射设置每个页面的 <c>GroupId</c>。</item>
    ///   <item>不存在 → 回退为给每个页面的 <c>Name</c> 字段加上 <see cref="SettingsGroupName"/> 前缀，
    ///   这样旧版 ClassIsland 也能在菜单里看到「InquiryWindow 设置 - XXX」字样。</item>
    /// </list>
    /// </summary>
    /// <param name="services">
    /// 插件 DI 容器。<c>AddSettingsPageGroup</c> 扩展方法会把 <paramref name="services"/>
    /// 作为返回值链路上的第一棒用上，所以这里必须传一个真实实例，传入 <c>null</c> 会触发 NRE。
    /// </param>
    private void RegisterSettingsPageGroup(IServiceCollection services)
    {
        if (InjectServices.TryGetAddSettingsPageGroupMethod(out var addSettingsPageGroupMethod))
        {
            // 首选方案：直接注册分组，然后逐个把 SettingsPageInfo.GroupId 改写。
            // services 必须真实存在 —— AddSettingsPageGroup 内部还有
            // `return services.AddSettingsPageGroup(id, info);` 这一行。
            addSettingsPageGroupMethod.Invoke(
                null,
                [services, SettingsGroupId, SettingsGroupIcon, SettingsGroupName]);

            var groupIdProperty = InjectServices.GetSettingsPageInfoGroupIdProperty();

            foreach (var info in SettingsWindowRegistryService.Registered
                         .Where(info => info.Id.StartsWith(SettingsGroupId, StringComparison.OrdinalIgnoreCase)))
            {
                groupIdProperty?.SetValue(info, SettingsGroupId);
            }
        }
        else
        {
            // 回退方案：旧版 ClassIsland 不支持 AddSettingsPageGroup，那就只能改 Name 字段。
            var nameField = InjectServices.GetSettingsPageInfoNameField();
            foreach (var info in SettingsWindowRegistryService.Registered
                         .Where(info => info.Id.StartsWith(SettingsGroupId, StringComparison.OrdinalIgnoreCase)))
            {
                var currentName = (string?)nameField.GetValue(info);
                nameField.SetValue(info, $"{SettingsGroupName} - {currentName}");
            }
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
