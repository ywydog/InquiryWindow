using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ConfirmDialogAction.Actions;
using ConfirmDialogAction.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConfirmDialogAction;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 注册日志服务与行动提供方（含设置控件）。
        services.AddLogging();
        services.AddAction<ConfirmDialogAction, ConfirmDialogSettingsControl>();
    }
}
