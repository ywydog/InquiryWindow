namespace InquiryWindow.Actions;

public enum InquiryWindowResult
{
    Execute,
    Cancel,
    /// <summary>
    /// 仅用于预览模式：用户点「看完了」关闭弹窗，不触发任何后续动作。
    /// </summary>
    Acknowledged
}
