using System.Linq;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using InquiryWindow.Models;
using Microsoft.Extensions.Logging;

namespace InquiryWindow.Actions;

/// <summary>
/// 「多结果行动」Action：触发时从 <see cref="MultiResultSettings.Groups"/> 中按
/// <see cref="MultiResultSettings.OrderMode"/> 选一个组，后台执行其 Action 链。
/// 不显示任何 UI 弹窗 —— 与多按钮询问 (MultiButtonPromptAction) 的本质区别。
///
/// 设计目的：让自动化触发的 Action 看起来"多样、不重复"。
/// 经典用法：每节课的提醒内容不同、每次下课播放的音效不同等。
/// </summary>
// addDefaultToMenu: false —— 关闭系统自动加默认菜单，改由 Plugin.BuildActionMenuTree
// 统一注册到「InquiryWindow 行动」集下。
[ActionInfo("InquiryWindow.MultiResult", "多结果行动", "\uE8B5", addDefaultToMenu: false)]
public class MultiResultAction(
    IActionService actionService,
    ILogger<MultiResultAction> logger)
    : ActionBase<MultiResultSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var groups = Settings.Groups;
        if (groups.Count == 0)
        {
            // 没配置候选组，安静地什么都不做；写条日志方便排查
            logger.LogWarning("多结果行动触发但 Groups 为空，工作流表现为无操作。请检查 Action 配置。");
            return;
        }

        var pickedIndex = PickIndex(groups.Count);
        if (pickedIndex < 0 || pickedIndex >= groups.Count)
        {
            logger.LogWarning("多结果行动：PickIndex 返回越界 index={Index}，Count={Count}", pickedIndex, groups.Count);
            return;
        }

        var group = groups[pickedIndex];
        logger.LogDebug(
            "多结果行动触发：模式={Mode}，选第 {Index} 个组（{Name}），Action 数={Count}",
            Settings.OrderMode, pickedIndex, group.Name, group.Actions.ActionItems.Count);

        if (group.Actions.ActionItems.Count > 0)
        {
            // 单个 Action 抛异常不应阻断后续 Action；IActionService.InvokeActionSetAsync
            // 本身已经捕获每个 ActionItem 的错误并继续。
            try
            {
                await actionService.InvokeActionSetAsync(group.Actions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "多结果行动：执行第 {Index} 个组的 Action 链时出现未捕获异常", pickedIndex);
            }
        }

        // 顺序模式：写回 LastIndex 以便下次循环
        if (Settings.OrderMode == MultiResultOrderMode.Sequential)
        {
            Settings.LastIndex = pickedIndex;
        }
    }

    /// <summary>
    /// 根据当前 <see cref="MultiResultSettings.OrderMode"/> 选一个候选索引。
    ///
    /// - <see cref="MultiResultOrderMode.Sequential"/>：用 <see cref="MultiResultSettings.LastIndex"/> 循环。
    /// - <see cref="MultiResultOrderMode.Random"/>：每次独立采样，可能连续多次选到同一组。
    /// - <see cref="MultiResultOrderMode.RandomNoRepeat"/>：从 <see cref="MultiResultSettings.RemainingOrder"/>
    ///   当前游标处取一个，Cursor++；队列耗尽时 Fisher-Yates 重新洗牌再继续。
    /// </summary>
    private int PickIndex(int count)
    {
        if (count <= 0) return -1;
        if (count == 1) return 0;

        return Settings.OrderMode switch
        {
            MultiResultOrderMode.Sequential => (Settings.LastIndex + 1 + count) % count,
            MultiResultOrderMode.Random     => Random.Shared.Next(0, count),
            MultiResultOrderMode.RandomNoRepeat => PickFromShuffledQueue(count),
            _ => 0,
        };
    }

    /// <summary>
    /// 洗牌队列取下一个：若 RemainingOrder 为空 / 长度与 count 不一致 / 越界，
    /// 视为「新一轮」并重洗一次。
    ///
    /// 越界判定：只要 RemainingOrder 中存在任何 ≥ count 的元素就重洗。
    /// 这是因为 Groups 增删后，老的索引可能已经失效。
    /// </summary>
    private int PickFromShuffledQueue(int count)
    {
        var order = Settings.RemainingOrder;
        var cursor = Settings.Cursor;

        var needReshuffle =
            order.Count != count ||
            cursor >= order.Count ||
            cursor < 0 ||
            order.Any(i => i < 0 || i >= count);

        if (needReshuffle)
        {
            order.Clear();
            for (var i = 0; i < count; i++) order.Add(i);
            ShuffleInPlace(order);
            cursor = 0;
            logger.LogDebug("多结果行动：随机不重复模式触发重洗，新队列=[{Queue}]", string.Join(",", order));
        }

        var picked = order[cursor];
        Settings.Cursor = cursor + 1;
        logger.LogDebug(
            "多结果行动：随机不重复模式，队列=[{Queue}]，游标={Cursor}，本轮取 {Picked}",
            string.Join(",", order), cursor, picked);
        return picked;
    }

    /// <summary>
    /// Fisher-Yates 洗牌：O(n)，均匀分布。
    /// 用 <see cref="Random.Shared"/> 保持调用方一致（多结果行动里 Random 模式也用它）。
    /// </summary>
    private static void ShuffleInPlace<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
