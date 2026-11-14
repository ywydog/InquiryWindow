using System;
using System.Diagnostics;
using Avalonia.Threading;

namespace InquiryWindow.Services;

/// <summary>
/// 自动执行倒计时驱动。
///
/// <para>
/// 进度条 / 圆环如果只在整秒时跳一次，视觉上会一格一格地跳动，看起来不流畅。
/// 这里用约 60 FPS 的 UI 计时器，配合单调时钟 <see cref="Stopwatch"/> 计算剩余秒数，
/// 让进度连续推进；文字仍然只在整秒变化时更新（避免每秒重排文字造成额外抖动）。
/// 用 Stopwatch 而不是逐次自减，还可以在 UI 线程被短暂阻塞后自动追上真实进度。
/// </para>
/// </summary>
internal sealed class SmoothCountdown
{
    /// <summary>约 60 FPS，与显示器刷新节奏大致对齐。</summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(16);

    private readonly int _totalSeconds;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();
    private int _lastSecond = -1;
    private bool _stopped;

    /// <summary>每帧回调，参数为剩余秒数（带小数），用于驱动进度条 / 圆环。</summary>
    public event Action<double>? FrameChanged;

    /// <summary>整秒变化时回调，参数为剩余整秒（向上取整），用于更新倒计时文字。</summary>
    public event Action<int>? SecondChanged;

    /// <summary>倒计时归零时回调（只会触发一次）。</summary>
    public event Action? Completed;

    public SmoothCountdown(int totalSeconds)
    {
        _totalSeconds = Math.Max(1, totalSeconds);
        _timer = new DispatcherTimer { Interval = FrameInterval };
        _timer.Tick += OnTick;
    }

    /// <summary>剩余总秒数（向上取整的初始值）。</summary>
    public int TotalSeconds => _totalSeconds;

    public void Start()
    {
        _stopwatch.Restart();
        _lastSecond = _totalSeconds;
        _timer.Start();
    }

    /// <summary>停止计时并解除计时器回调（可重复调用）。</summary>
    public void Stop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _stopwatch.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var remaining = _totalSeconds - _stopwatch.Elapsed.TotalSeconds;
        if (remaining <= 0)
        {
            Stop();
            FrameChanged?.Invoke(0);
            SecondChanged?.Invoke(0);
            Completed?.Invoke();
            return;
        }

        FrameChanged?.Invoke(remaining);

        var second = (int)Math.Ceiling(remaining);
        if (second != _lastSecond)
        {
            _lastSecond = second;
            SecondChanged?.Invoke(second);
        }
    }
}
