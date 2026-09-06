namespace InquiryWindow.Models;

/// <summary>
/// 「操作验证」失败（用户未通过身份验证）时，多按钮询问弹窗的处理方式。
/// <list type="bullet">
///   <item><see cref="KeepOpen"/>：失败则不执行该按钮的 Action 链，弹窗保持打开，用户可重试或点其它按钮。</item>
///   <item><see cref="CloseDialog"/>：失败则不执行该按钮的 Action 链，同时关闭整个询问弹窗。</item>
/// </list>
/// </summary>
public enum VerificationFailureBehavior
{
    /// <summary>验证失败不执行 Action，弹窗保持打开。</summary>
    KeepOpen = 0,

    /// <summary>验证失败不执行 Action，关闭弹窗。</summary>
    CloseDialog = 1,
}