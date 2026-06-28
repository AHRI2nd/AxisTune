using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AxisTune.App.ViewModels;
using AxisTune.Core.Curves;

namespace AxisTune.App.Controls;

/// <summary>
/// 응답 곡선 편집기. 제어점을 드래그(끝점은 Y만), 더블클릭으로 추가, 우클릭으로 삭제.
/// 데드존 영역과 실시간 입력 마커를 함께 그린다. 곡선 샘플링은 Core(<see cref="CurveDefinition"/>)를
/// 재사용해 UI와 실제 처리 결과가 일치하도록 한다.
/// </summary>
public sealed class CurveEditor : Control
{
    private const double Pad = 14;
    private const double HandleRadius = 6;
    private const int SampleCount = 128;

    public static readonly StyledProperty<ObservableCollection<CurveControlPoint>?> PointsProperty =
        AvaloniaProperty.Register<CurveEditor, ObservableCollection<CurveControlPoint>?>(nameof(Points));

    public static readonly StyledProperty<CurveInterpolation> InterpolationProperty =
        AvaloniaProperty.Register<CurveEditor, CurveInterpolation>(nameof(Interpolation), CurveInterpolation.MonotoneCubic);

    public static readonly StyledProperty<double> InnerDeadzoneProperty =
        AvaloniaProperty.Register<CurveEditor, double>(nameof(InnerDeadzone));

    public static readonly StyledProperty<double> OuterDeadzoneProperty =
        AvaloniaProperty.Register<CurveEditor, double>(nameof(OuterDeadzone));

    /// <summary>현재 입력 크기 [0,1]. 음수면 마커를 그리지 않음.</summary>
    public static readonly StyledProperty<double> PreviewInputProperty =
        AvaloniaProperty.Register<CurveEditor, double>(nameof(PreviewInput), -1);

    public ObservableCollection<CurveControlPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public CurveInterpolation Interpolation
    {
        get => GetValue(InterpolationProperty);
        set => SetValue(InterpolationProperty, value);
    }

    public double InnerDeadzone
    {
        get => GetValue(InnerDeadzoneProperty);
        set => SetValue(InnerDeadzoneProperty, value);
    }

    public double OuterDeadzone
    {
        get => GetValue(OuterDeadzoneProperty);
        set => SetValue(OuterDeadzoneProperty, value);
    }

    public double PreviewInput
    {
        get => GetValue(PreviewInputProperty);
        set => SetValue(PreviewInputProperty, value);
    }

    private int _dragIndex = -1;
    private ObservableCollection<CurveControlPoint>? _hooked;

    static CurveEditor()
    {
        AffectsRender<CurveEditor>(
            PointsProperty, InterpolationProperty, InnerDeadzoneProperty,
            OuterDeadzoneProperty, PreviewInputProperty);
    }

    public CurveEditor()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty)
            HookPoints(change.GetNewValue<ObservableCollection<CurveControlPoint>?>());
    }

    private void HookPoints(ObservableCollection<CurveControlPoint>? points)
    {
        if (_hooked is not null)
        {
            _hooked.CollectionChanged -= OnPointsCollectionChanged;
            foreach (var p in _hooked) p.PropertyChanged -= OnPointPropertyChanged;
        }
        _hooked = points;
        if (_hooked is not null)
        {
            _hooked.CollectionChanged += OnPointsCollectionChanged;
            foreach (var p in _hooked) p.PropertyChanged += OnPointPropertyChanged;
        }
        InvalidateVisual();
    }

    private void OnPointsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (CurveControlPoint p in e.OldItems) p.PropertyChanged -= OnPointPropertyChanged;
        if (e.NewItems is not null)
            foreach (CurveControlPoint p in e.NewItems) p.PropertyChanged += OnPointPropertyChanged;
        InvalidateVisual();
    }

    private void OnPointPropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    // ---- 좌표 변환 ----

    private Rect PlotRect => new(Pad, Pad, Math.Max(1, Bounds.Width - 2 * Pad), Math.Max(1, Bounds.Height - 2 * Pad));

    private Point ToScreen(double vx, double vy)
    {
        var r = PlotRect;
        return new Point(r.X + vx * r.Width, r.Y + (1 - vy) * r.Height);
    }

    private (double vx, double vy) ToValue(Point p)
    {
        var r = PlotRect;
        double vx = (p.X - r.X) / r.Width;
        double vy = 1 - (p.Y - r.Y) / r.Height;
        return (Clamp01(vx), Clamp01(vy));
    }

    // ---- 입력 처리 ----

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pts = Points;
        if (pts is null || pts.Count == 0) return;

        var pos = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsRightButtonPressed)
        {
            RemoveNearestInterior(pos);
            e.Handled = true;
            return;
        }

        _dragIndex = HitTest(pos);
        if (_dragIndex >= 0)
        {
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pts = Points;
        if (_dragIndex < 0 || pts is null || _dragIndex >= pts.Count) return;

        var (vx, vy) = ToValue(e.GetPosition(this));
        var point = pts[_dragIndex];

        bool isFirst = _dragIndex == 0;
        bool isLast = _dragIndex == pts.Count - 1;

        if (isFirst) vx = 0;
        else if (isLast) vx = 1;
        else
        {
            // 이웃 X 사이로 제한(단조 X 보장).
            float left = pts[_dragIndex - 1].X + 0.001f;
            float right = pts[_dragIndex + 1].X - 0.001f;
            vx = Math.Clamp(vx, left, right);
        }

        point.X = (float)vx;
        point.Y = (float)vy;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragIndex >= 0)
        {
            _dragIndex = -1;
            e.Pointer.Capture(null);
        }
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        var pts = Points;
        if (pts is null || pts.Count < 2) return;

        // 더블탭 위치에 내부 제어점 추가(끝점 사이).
        var (vx, vy) = ToValue(e.GetPosition(this));
        if (vx <= pts[0].X || vx >= pts[^1].X) return;

        int insert = pts.Count - 1;
        for (int i = 1; i < pts.Count; i++)
        {
            if (vx < pts[i].X) { insert = i; break; }
        }
        pts.Insert(insert, new CurveControlPoint((float)vx, (float)vy));
        e.Handled = true;
    }

    private int HitTest(Point pos)
    {
        var pts = Points!;
        for (int i = 0; i < pts.Count; i++)
        {
            var s = ToScreen(pts[i].X, pts[i].Y);
            if (Distance(s, pos) <= HandleRadius + 5)
                return i;
        }
        return -1;
    }

    private void RemoveNearestInterior(Point pos)
    {
        var pts = Points!;
        int best = -1;
        double bestDist = double.MaxValue;
        for (int i = 1; i < pts.Count - 1; i++) // 끝점 제외
        {
            var s = ToScreen(pts[i].X, pts[i].Y);
            double d = Distance(s, pos);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        if (best >= 0 && bestDist <= HandleRadius + 8)
            pts.RemoveAt(best);
    }

    // ---- 렌더 ----

    public override void Render(DrawingContext context)
    {
        var r = PlotRect;
        var bg = new SolidColorBrush(Color.Parse("#15151A"));
        context.FillRectangle(bg, new Rect(Bounds.Size));

        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A32")), 1);
        for (int i = 0; i <= 4; i++)
        {
            double t = i / 4.0;
            context.DrawLine(gridPen, ToScreen(t, 0), ToScreen(t, 1));
            context.DrawLine(gridPen, ToScreen(0, t), ToScreen(1, t));
        }

        // 데드존 음영
        var dzBrush = new SolidColorBrush(Color.Parse("#332B2B33"));
        double inner = Clamp01(InnerDeadzone);
        double outer = Clamp01(OuterDeadzone);
        if (inner > 0)
            context.FillRectangle(dzBrush, new Rect(ToScreen(0, 1), ToScreen(inner, 0)));
        if (outer > 0)
            context.FillRectangle(dzBrush, new Rect(ToScreen(1 - outer, 1), ToScreen(1, 0)));

        var border = new Pen(new SolidColorBrush(Color.Parse("#3A3A44")), 1);
        context.DrawRectangle(null, border, r);

        var pts = Points;
        if (pts is null || pts.Count < 2) return;

        // 곡선(Core 샘플링 재사용)
        CurveLut? lut = TryBuildLut(pts);
        if (lut is not null)
        {
            var curvePen = new Pen(new SolidColorBrush(Color.Parse("#4C8DF6")), 2.5);
            var geo = new StreamGeometry();
            using (var gc = geo.Open())
            {
                gc.BeginFigure(ToScreen(0, lut.Evaluate(0)), false);
                for (int i = 1; i <= SampleCount; i++)
                {
                    double x = (double)i / SampleCount;
                    gc.LineTo(ToScreen(x, lut.Evaluate((float)x)));
                }
                gc.EndFigure(false);
            }
            context.DrawGeometry(null, curvePen, geo);

            // 실시간 입력 마커
            double pin = PreviewInput;
            if (pin >= 0)
            {
                double pout = lut.Evaluate((float)Clamp01(pin));
                var marker = ToScreen(Clamp01(pin), pout);
                var guidePen = new Pen(new SolidColorBrush(Color.Parse("#3434C75A")), 1) { DashStyle = DashStyle.Dash };
                context.DrawLine(guidePen, ToScreen(Clamp01(pin), 0), marker);
                context.DrawLine(guidePen, ToScreen(0, pout), marker);
                var markBrush = new SolidColorBrush(Color.Parse("#34C759"));
                context.DrawEllipse(markBrush, null, marker, 5, 5);
            }
        }

        // 제어점 핸들
        var fill = new SolidColorBrush(Color.Parse("#FFFFFF"));
        var ring = new Pen(new SolidColorBrush(Color.Parse("#4C8DF6")), 2);
        foreach (var p in pts)
        {
            var s = ToScreen(p.X, p.Y);
            context.DrawEllipse(fill, ring, s, HandleRadius, HandleRadius);
        }
    }

    private CurveLut? TryBuildLut(ObservableCollection<CurveControlPoint> pts)
    {
        try
        {
            var arr = new CurvePoint[pts.Count];
            for (int i = 0; i < pts.Count; i++)
                arr[i] = new CurvePoint(pts[i].X, pts[i].Y);
            return new CurveDefinition(arr, Interpolation).BuildLut(SampleCount);
        }
        catch
        {
            return null;
        }
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
