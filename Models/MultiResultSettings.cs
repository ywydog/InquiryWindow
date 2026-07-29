using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InquiryWindow.Models;

/// <summary>
/// 「多结果行动」触发时从多个候选组中按某种顺序选一个执行的策略。
/// </summary>
public enum MultiResultOrderMode
{
    /// <summary>按 Groups 中的顺序，循环取下一个（用 <see cref="MultiResultSettings.LastIndex"/> 记忆位置）。</summary>
    Sequential = 0,

    /// <summary>每次完全独立地随机选一个（非空时）。可重复选中同一个组。</summary>
    Random = 1,

    /// <summary>
    /// 洗牌后按队列顺序一个个执行，一轮内不重复；队列耗尽后重新洗牌。
    /// 使用 <see cref="MultiResultSettings.RemainingOrder"/> + <see cref="MultiResultSettings.Cursor"/>
    /// 维护剩余队列与游标。
    /// </summary>
    RandomNoRepeat = 2,
}

/// <summary>
/// 「多结果行动」Action 的设置：
/// 多个 <see cref="MultiResultGroup"/> 候选 + 选择顺序（顺序 / 随机 / 随机不重复）。
/// 触发时按顺序模式选一个组，后台执行其 Action 链。不弹窗，不与用户交互。
/// </summary>
public partial class MultiResultSettings : ObservableObject
{
    /// <summary>
    /// 候选组集合（按 Groups 中的顺序执行「顺序」模式）。
    /// set 仍保留以兼容 ConfigureFileHelper 的 JSON 反序列化（整体赋值）。
    /// 反序列化之外请通过 Add/Remove 变更集合，**不要**整体替换。
    /// </summary>
    public ObservableCollection<MultiResultGroup> Groups { get; set; } = new();

    /// <summary>
    /// 选择顺序：顺序（按 Groups 顺序循环）/ 随机 / 随机不重复。
    /// </summary>
    [ObservableProperty]
    private MultiResultOrderMode _orderMode = MultiResultOrderMode.Sequential;

    /// <summary>
    /// 顺序模式下记忆的上次执行索引。
    /// 下次按 Sequential 执行时取 (LastIndex + 1) % Groups.Count，循环。
    /// 集合为空或索引越界时回退到 0。
    /// </summary>
    [ObservableProperty]
    private int _lastIndex = -1;

    /// <summary>
    /// 「随机不重复」模式的剩余队列：保存打乱后的 Group 索引。
    /// 每次触发从 <see cref="Cursor"/> 处取一个，Cursor++；耗尽时 <see cref="MultiResultAction"/>
    /// 会重新洗牌并把 Cursor 归 0。
    ///
    /// 这是运行时状态，**不**参与 JSON 持久化（标记为 [JsonIgnore]）。
    /// 设计取舍：每次进程启动都重新洗一次，避免磁盘上的旧队列引用已被删的 Group 索引；
    /// 同时"随机"在用户感知上也更自然。
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public List<int> RemainingOrder { get; set; } = new();

    /// <summary>
    /// 「随机不重复」模式在 <see cref="RemainingOrder"/> 中的当前位置。
    /// 同样为运行时状态，不持久化。
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public int Cursor { get; set; }

    public MultiResultSettings()
    {
        // 默认放两个候选组，方便用户直观理解"多种结果"
        Groups.Add(new MultiResultGroup { Name = "结果 1" });
        Groups.Add(new MultiResultGroup { Name = "结果 2" });
    }
}
