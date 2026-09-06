namespace InquiryWindow.Models;

/// <summary>
/// 按钮的强调样式模式（学习 ClassIsland 倒计时组件的"强调色"思路）。
/// <list type="bullet">
///   <item><see cref="Default"/>：跟随应用主题色，由全局样式决定。</item>
///   <item><see cref="Highlighted"/>：强制使用系统蓝色（AccentFillColorDefaultBrush）作为强调色，类似"主操作"按钮。</item>
///   <item><see cref="Custom"/>：使用用户在"外观"卡片中自定义的颜色作为强调色。</item>
/// </list>
/// </summary>
public enum ButtonAccentMode
{
    /// <summary>默认：跟随主题色（不强调）。</summary>
    Default = 0,

    /// <summary>高亮：使用系统蓝色（AccentFillColorDefaultBrush）作为强调色。</summary>
    Highlighted = 1,

    /// <summary>自定义：使用用户在"外观"卡片中选取的颜色作为强调色。</summary>
    Custom = 2,
}
