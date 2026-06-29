using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AxisTune.Core.Controls;

namespace AxisTune.App.Controls;

/// <summary>
/// 스타일라이즈된 Xbox 360 게임패드 다이어그램. 각 컨트롤을 클릭하면 <see cref="SelectedControl"/>이
/// 갱신되고(양방향 바인딩), <see cref="LiveState"/>에 따라 눌린 버튼 점등·스틱 위치·트리거 게이지를
/// 실시간 표시한다. 좌표는 320×240 설계 공간 기준이며 컨트롤 크기에 맞춰 비율 유지 스케일.
/// </summary>
public sealed class ControllerView : Control
{
    private const double DesignW = 320;
    private const double DesignH = 240;

    public static readonly StyledProperty<ControllerControl> SelectedControlProperty =
        AvaloniaProperty.Register<ControllerView, ControllerControl>(
            nameof(SelectedControl), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<XboxOutputState> LiveStateProperty =
        AvaloniaProperty.Register<ControllerView, XboxOutputState>(nameof(LiveState));

    public ControllerControl SelectedControl
    {
        get => GetValue(SelectedControlProperty);
        set => SetValue(SelectedControlProperty, value);
    }

    public XboxOutputState LiveState
    {
        get => GetValue(LiveStateProperty);
        set => SetValue(LiveStateProperty, value);
    }

    private enum Shape { Circle, Rect }

    private readonly record struct Region(ControllerControl Control, Shape Shape, double A, double B, double C, double D);

    // 설계 공간(320×240) 좌표. circle: A=cx,B=cy,C=r / rect: A=x,B=y,C=w,D=h
    private static readonly Region[] Regions =
    {
        new(ControllerControl.LeftTrigger, Shape.Rect, 48, 26, 52, 13),
        new(ControllerControl.RightTrigger, Shape.Rect, 220, 26, 52, 13),
        new(ControllerControl.LeftBumper, Shape.Rect, 48, 46, 52, 15),
        new(ControllerControl.RightBumper, Shape.Rect, 220, 46, 52, 15),

        new(ControllerControl.LeftStick, Shape.Circle, 78, 108, 23, 0),
        new(ControllerControl.RightStick, Shape.Circle, 200, 158, 23, 0),

        new(ControllerControl.DpadUp, Shape.Rect, 106, 140, 16, 18),
        new(ControllerControl.DpadDown, Shape.Rect, 106, 174, 16, 18),
        new(ControllerControl.DpadLeft, Shape.Rect, 88, 158, 18, 16),
        new(ControllerControl.DpadRight, Shape.Rect, 122, 158, 18, 16),

        new(ControllerControl.Y, Shape.Circle, 250, 96, 13, 0),
        new(ControllerControl.B, Shape.Circle, 272, 118, 13, 0),
        new(ControllerControl.A, Shape.Circle, 250, 140, 13, 0),
        new(ControllerControl.X, Shape.Circle, 228, 118, 13, 0),

        new(ControllerControl.Back, Shape.Circle, 138, 112, 8, 0),
        new(ControllerControl.Start, Shape.Circle, 182, 112, 8, 0),
        new(ControllerControl.Guide, Shape.Circle, 160, 92, 11, 0),
    };

    private double _scale = 1;
    private double _offsetX;
    private double _offsetY;

    static ControllerView()
    {
        AffectsRender<ControllerView>(SelectedControlProperty, LiveStateProperty);
    }

    public ControllerView()
    {
        Focusable = true;
    }

    // ---- 좌표 변환 ----

    private void UpdateTransform()
    {
        _scale = Math.Min(Bounds.Width / DesignW, Bounds.Height / DesignH);
        if (_scale <= 0) _scale = 1;
        _offsetX = (Bounds.Width - DesignW * _scale) / 2;
        _offsetY = (Bounds.Height - DesignH * _scale) / 2;
    }

    private Point ToScreen(double dx, double dy) => new(_offsetX + dx * _scale, _offsetY + dy * _scale);
    private double S(double v) => v * _scale;

    private (double dx, double dy) ToDesign(Point p) => ((p.X - _offsetX) / _scale, (p.Y - _offsetY) / _scale);

    // ---- 입력 ----

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        UpdateTransform();
        var (dx, dy) = ToDesign(e.GetPosition(this));
        foreach (var r in Regions)
        {
            if (HitTest(r, dx, dy))
            {
                SetCurrentValue(SelectedControlProperty, r.Control);
                e.Handled = true;
                return;
            }
        }
    }

    private static bool HitTest(Region r, double dx, double dy)
    {
        if (r.Shape == Shape.Circle)
        {
            double ex = dx - r.A, ey = dy - r.B;
            double rr = r.C + 3;
            return ex * ex + ey * ey <= rr * rr;
        }
        return dx >= r.A - 2 && dx <= r.A + r.C + 2 && dy >= r.B - 2 && dy <= r.B + r.D + 2;
    }

    // ---- 렌더 ----

    public override void Render(DrawingContext context)
    {
        UpdateTransform();

        var bodyFill = new SolidColorBrush(Color.Parse("#1C1C22"));
        var bodyStroke = new Pen(new SolidColorBrush(Color.Parse("#3A3A44")), 1.5);
        var ctlFill = new SolidColorBrush(Color.Parse("#26262E"));
        var ctlStroke = new Pen(new SolidColorBrush(Color.Parse("#454552")), 1);
        var accent = new SolidColorBrush(Color.Parse("#4C8DF6"));
        var accentStroke = new Pen(accent, 2.5);
        var live = new SolidColorBrush(Color.Parse("#34C759"));
        var labelBrush = new SolidColorBrush(Color.Parse("#C8CCD0"));

        DrawBody(context, bodyFill, bodyStroke);

        var state = LiveState;
        var selected = SelectedControl;

        foreach (var r in Regions)
        {
            bool isSel = r.Control == selected;
            var pen = isSel ? accentStroke : ctlStroke;

            switch (r.Control)
            {
                case ControllerControl.LeftStick:
                case ControllerControl.RightStick:
                    DrawStick(context, r, state, ctlFill, pen, live, isSel, accent);
                    break;
                case ControllerControl.LeftTrigger:
                case ControllerControl.RightTrigger:
                    DrawTrigger(context, r, state, ctlFill, pen, live);
                    break;
                default:
                    DrawButton(context, r, state, ctlFill, pen, live, labelBrush, isSel, accent);
                    break;
            }
        }
    }

    private void DrawBody(DrawingContext context, IBrush fill, IPen stroke)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(ToScreen(60, 66), true);
            g.CubicBezierTo(ToScreen(30, 56), ToScreen(16, 92), ToScreen(22, 124));
            g.CubicBezierTo(ToScreen(28, 156), ToScreen(36, 182), ToScreen(64, 190));
            g.CubicBezierTo(ToScreen(90, 198), ToScreen(112, 182), ToScreen(138, 180));
            g.CubicBezierTo(ToScreen(150, 179), ToScreen(170, 179), ToScreen(182, 180));
            g.CubicBezierTo(ToScreen(208, 182), ToScreen(230, 198), ToScreen(256, 190));
            g.CubicBezierTo(ToScreen(284, 182), ToScreen(292, 156), ToScreen(298, 124));
            g.CubicBezierTo(ToScreen(304, 92), ToScreen(290, 56), ToScreen(260, 66));
            g.CubicBezierTo(ToScreen(222, 80), ToScreen(196, 86), ToScreen(160, 86));
            g.CubicBezierTo(ToScreen(124, 86), ToScreen(98, 80), ToScreen(60, 66));
            g.EndFigure(true);
        }
        context.DrawGeometry(fill, stroke, geo);
    }

    private void DrawStick(DrawingContext context, Region r, XboxOutputState state,
        IBrush fill, IPen pen, IBrush live, bool selected, IBrush accent)
    {
        var center = ToScreen(r.A, r.B);
        double radius = S(r.C);
        // 바깥 링
        context.DrawEllipse(fill, pen, center, radius, radius);

        // 라이브 스틱 위치 점
        float sx = state.GetAxis(r.Control == ControllerControl.LeftStick ? AxisChannel.LeftStickX : AxisChannel.RightStickX);
        float sy = state.GetAxis(r.Control == ControllerControl.LeftStick ? AxisChannel.LeftStickY : AxisChannel.RightStickY);
        double maxOff = radius * 0.55;
        var dot = new Point(center.X + sx * maxOff, center.Y - sy * maxOff);
        context.DrawEllipse(live, null, dot, radius * 0.28, radius * 0.28);
    }

    private void DrawTrigger(DrawingContext context, Region r, XboxOutputState state, IBrush fill, IPen pen, IBrush live)
    {
        var rect = new Rect(ToScreen(r.A, r.B), new Size(S(r.C), S(r.D)));
        double rx = S(6);
        context.DrawRectangle(fill, pen, rect, rx, rx);

        // 라이브 게이지(아래에서 위로 채움 — 가로 막대로 좌→우)
        float v = state.GetAxis(r.Control == ControllerControl.LeftTrigger ? AxisChannel.LeftTrigger : AxisChannel.RightTrigger);
        if (v > 0.01f)
        {
            var fillRect = new Rect(rect.X, rect.Y, rect.Width * Math.Clamp(v, 0f, 1f), rect.Height);
            context.DrawRectangle(live, null, fillRect, rx, rx);
        }
    }

    private void DrawButton(DrawingContext context, Region r, XboxOutputState state,
        IBrush fill, IPen pen, IBrush live, IBrush labelBrush, bool selected, IBrush accent)
    {
        bool pressed = (state.Buttons & r.Control.ToButton()) != 0;
        IBrush brush = pressed ? live : fill;

        if (r.Shape == Shape.Circle)
        {
            var center = ToScreen(r.A, r.B);
            double radius = S(r.C);
            context.DrawEllipse(brush, pen, center, radius, radius);
            DrawLabel(context, r.Control, center, radius, labelBrush, pressed);
        }
        else
        {
            var rect = new Rect(ToScreen(r.A, r.B), new Size(S(r.C), S(r.D)));
            double rx = S(4);
            context.DrawRectangle(brush, pen, rect, rx, rx);
            DrawLabel(context, r.Control, rect.Center, Math.Min(rect.Width, rect.Height) / 2, labelBrush, pressed);
        }
    }

    private void DrawLabel(DrawingContext context, ControllerControl c, Point center, double radius, IBrush brush, bool pressed)
    {
        string? text = c switch
        {
            ControllerControl.A => "A",
            ControllerControl.B => "B",
            ControllerControl.X => "X",
            ControllerControl.Y => "Y",
            ControllerControl.LeftBumper => "LB",
            ControllerControl.RightBumper => "RB",
            _ => null,
        };
        if (text is null) return;

        double size = radius * (text.Length > 1 ? 0.85 : 1.1);
        if (size < 7) return;
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), size,
            pressed ? new SolidColorBrush(Color.Parse("#0B2A14")) : brush);
        context.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }
}
