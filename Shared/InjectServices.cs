using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;

namespace InquiryWindow.Shared;

/// <summary>
/// 反射访问 ClassIsland 设置页面注册相关 API 的辅助类。
///
/// <para>
/// 设计目标：兼容不同版本的 ClassIsland PluginSdk。
/// ClassIsland.Core 较新版本提供了
/// <see cref="SettingsWindowRegistryExtensions.AddSettingsPageGroup(IServiceCollection, string, string, string)"/>
/// 扩展方法以及 <see cref="SettingsPageInfo.GroupId"/> 的可写访问器，
/// 但插件可能链接到较老的 SDK（没有这些成员），所以这里用反射探测：
/// </para>
/// <list type="bullet">
///   <item>运行时存在 <c>AddSettingsPageGroup</c> 4 参数重载 → 通过它注册分组 + 反射设置每个页面的 <c>GroupId</c>。</item>
///   <item>运行时不存在该方法 → 回退为直接给每个页面的 <c>Name</c> 字段加分组前缀，
///   让用户在旧版 ClassIsland 上也能看到「InquiryWindow 设置 - XXX」形式的菜单项。</item>
/// </list>
///
/// <para>
/// 实现完全照搬 <c>SystemTools.Shared.InjectServices</c>，仅把 ID 前缀与图标换成本插件的。
/// </para>
/// </summary>
public static class InjectServices
{
    /// <summary>
    /// 探测 ClassIsland 运行时是否暴露 4 参数的 <c>AddSettingsPageGroup</c> 扩展方法。
    /// </summary>
    /// <param name="method">命中时返回对应 <see cref="MethodInfo"/>，否则为 <c>null</c>。</param>
    /// <returns>找到返回 <c>true</c>，否则 <c>false</c>。</returns>
    public static bool TryGetAddSettingsPageGroupMethod([MaybeNullWhen(false)] out MethodInfo method)
    {
        var settingsWindowRegistryExtensionsType = typeof(SettingsWindowRegistryExtensions);
        method = settingsWindowRegistryExtensionsType
            .GetMethods()
            .FirstOrDefault(m => (m.ToString()?.Contains("AddSettingsPageGroup") ?? false)
                                 && m.GetParameters().Length == 4);
        return method != null;
    }

    /// <summary>
    /// 取 <see cref="SettingsPageInfo"/> 上的 <c>Name</c> 字段（回退方案使用）。
    /// </summary>
    public static FieldInfo GetSettingsPageInfoNameField()
    {
        var settingsPageInfoType = typeof(SettingsPageInfo);
        var field = settingsPageInfoType
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.ToString()?.Contains("Name") ?? false);
        return field!;
    }

    /// <summary>
    /// 取 <see cref="SettingsPageInfo"/> 上的 <c>GroupId</c> 属性（首选方案使用）。
    /// </summary>
    public static PropertyInfo GetSettingsPageInfoGroupIdProperty()
    {
        var settingsPageInfoType = typeof(SettingsPageInfo);
        var property = settingsPageInfoType
            .GetProperties()
            .FirstOrDefault(p => p.ToString()?.Contains("GroupId") ?? false);
        return property!;
    }
}
