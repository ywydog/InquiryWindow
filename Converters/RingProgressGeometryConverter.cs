using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace InquiryWindow.Converters;

/// <summary>
/// 自动执行倒计时圆环几何转换器：与 ClassIsland 本体 <c>CountDownComponent</c> 的
/// <c>PercentToPathGeometryConverter</c> 完全一致，只是输入从「单个百分比」改为
/// 「(剩余秒数, 总秒数)」两值，便于直接绑定 ViewModel 现有属性，无需新增计算属性。
///
/// 输出：从正上方（12 点方向）起、顺时针绘制的圆弧几何；已用进度越大，圆环越接近闭合。
/// </summary>
public class RingProgressGeometryConverter : IMultiValueConverter
{
    public static readonly RingProgressGeometryConverter Instance = new();

    // 与本体 CountDownComponent 保持一致的尺寸参数
    private const double Width = 22;
    private const double Height = 22;
    private const double StrokeThickness = 3;
    private const double Radius = (Width / 2) - (StrokeThickness / 2);

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
        {
            return null;
        }

        var remaining = values[0] is double r ? r : 0.0;
        var max = values[1] is double m ? m : 0.0;
        if (max <= 0)
        {
            return null;
        }

        // 已用进度（0-100）：倒计时越接近归零，圆环越接近闭合
        var percentage = (max - remaining) / max * 100.0;
        return BuildGeometry(percentage);
    }

    private static Geometry? BuildGeometry(double percentage)
    {
        if (percentage >= 100)
        {
            percentage = 99.9999; // 防止闭合时出现渲染问题
        }
        if (percentage <= 0)
        {
            return null;
        }

        var center = new Point(Width / 2, Height / 2);
        var angle = (percentage / 100) * 360;

        // 起点固定在顶部（12 点方向）
        var startPoint = new Point(center.X, center.Y - Radius);

        var angleRad = (Math.PI / 180.0) * (angle - 90);
        var endPoint = new Point(
            center.X + Radius * Math.Cos(angleRad),
            center.Y + Radius * Math.Sin(angleRad));

        var segments = new PathSegments
        {
            new ArcSegment
            {
                Point = endPoint,
                Size = new Size(Radius, Radius),
                IsLargeArc = angle > 180,
                SweepDirection = SweepDirection.Clockwise,
                IsStroked = true
            }
        };

        var figure = new PathFigure
        {
            StartPoint = startPoint,
            Segments = segments,
            IsClosed = false
        };

        var geometry = new PathGeometry();
        geometry.Figures?.Add(figure);
        return geometry;
    }
}
