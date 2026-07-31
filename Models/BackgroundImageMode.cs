namespace InquiryWindow.Models;

/// <summary>
/// 窗口背景图片的缩放模式。索引值与 <see cref="MultiButtonPromptSettings.BackgroundImageMode"/> 持久化字段一一对应，
/// 不要调整顺序。
/// </summary>
public enum BackgroundImageMode
{
    /// <summary>覆盖：保持比例，裁剪超出区域（推荐，填满整个弹窗）。</summary>
    Cover = 0,

    /// <summary>包含：保持比例，居中显示，剩余区域留白。</summary>
    Contain = 1,

    /// <summary>拉伸：填满窗口，图片可能变形。</summary>
    Stretch = 2,

    /// <summary>平铺：按原尺寸重复铺满。</summary>
    Tile = 3,
}
