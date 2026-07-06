using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AxisTune.Core.Controls;

namespace AxisTune.App.Controls;

/// <summary>
/// Xbox 360 게임패드 다이어그램. 본체 윤곽은 실제 컨트롤러 SVG(CC0, Wikimedia Commons
/// "Xbox_Controller.svg")의 실루엣 path 데이터를 <see cref="Geometry.Parse"/>로 직접 파싱해
/// 그린다 — 외부 SVG 렌더링 라이브러리 의존성 없이 Avalonia 자체 벡터 API만 사용하므로
/// 렌더 파이프라인 충돌 위험이 없다. 컨트롤 배치 좌표는 그 SVG를 픽셀 단위로 측정해 보정했다.
/// 클릭 시 <see cref="SelectedControl"/> 갱신(양방향), <see cref="LiveState"/>로 실시간 표시.
/// </summary>
public sealed class ControllerView : Control
{
    // 원본 SVG(744×500) 좌표계를 그대로 설계 공간으로 사용 — 본체 path와 컨트롤 좌표가 동일 기준.
    private const double DesignW = 744;
    private const double DesignH = 500;

    // SVG layer1의 group transform. 본체 path는 이 오프셋 기준 로컬 좌표라 그리기 전에 더해야 한다.
    private const double LayerOffsetX = -11.035862;
    private const double LayerOffsetY = -267.29676;

    // Wikimedia Commons "Xbox_Controller.svg" (CC0) 의 본체 실루엣 path(id="path3048") 원문 그대로.
    private const string BodyPathData =
        "m 200.94643,395.58196 c -29.99163,-0.21964 -72.46512,16.98877 -89.29209,23.66106 " +
        "-4.7881,1.89859 -17.391875,18.81355 -25.21684,35.25698 -3.499515,7.3539 -29.28125,82.15625 -29.28125,82.15625 " +
        "0,0 -12.142857,57.83929 -10,92.125 C 49.299107,663.06697 70,694.5 70,694.5 c 0,0 17.556941,17.10009 27,20.6875 " +
        "11.30169,4.29349 31.85416,6.90966 43,5.03125 25.86623,-4.35924 98.4375,-41.29464 103.4375,-43.4375 " +
        "5,-2.14286 9.91728,-4.09997 14.6875,-5.3125 3.51949,3.34304 30.11608,5.62946 43.6875,1.34375 " +
        "13.57143,-4.28572 20.91792,-7.94663 27.21875,-13.25 11.76086,-0.63704 32.97319,-0.60097 52.40625,-0.5 " +
        "19.39384,-0.10095 40.54411,-0.13689 52.28125,0.5 6.28811,5.30213 13.6122,8.96528 27.15625,13.25 " +
        "13.54403,4.2847 40.11262,1.99851 43.625,-1.34375 4.76061,1.21225 9.63508,3.17014 14.625,5.3125 " +
        "4.98991,2.14236 77.43603,39.07928 103.25,43.4375 11.12331,1.87797 31.62735,-0.77002 42.90625,-5.0625 " +
        "9.42403,-3.58657 26.9375,-20.65625 26.9375,-20.65625 0,0 20.67387,-31.40982 22.8125,-65.6875 " +
        "2.13854,-34.27767 -9.96875,-92.125 -9.96875,-92.125 0,0 -25.72628,-74.77283 -29.21875,-82.125 " +
        "-7.80919,-16.43958 -21.03854,-34.91275 -25.81696,-36.8109 -12.27915,-4.87767 -36.23003,-16.61854 " +
        "-63.11882,-20.78617 -16.69431,-2.58753 -36.59644,-2.90053 -50.7522,1.72381 -9.54715,3.11882 " +
        "-17.96182,11.17602 -24.28077,14.77665 -7.38047,4.2055 -37.51418,22.4323 -54.62246,26.717 " +
        "-24.91481,4.16722 -50.53019,3.97599 -75.5625,3.31763 0,0 -58.82844,0.68436 -75.97129,-3.60136 " +
        "-17.14286,-4.28571 -40.01532,-19.75798 -47.41071,-23.96447 -6.33172,-3.60147 -10.84522,-7.52581 " +
        "-24.95536,-14.16518 -10.90694,-6.36097 -22.63368,-6.18723 -32.40625,-6.18755 z";

    // 같은 SVG의 트리거/범퍼 돌출부 path(id="path4945"=LT, "path4943"=LB, "path4951"=RT, "path4953"=RB).
    private const string LeftTriggerTabData =
        "m 168.75,379.86218 c 0,0 6.42857,-39.46428 6.60714,-42.14286 0.17857,-2.67857 2.14286,-19.10714 2.67857,-21.42857 " +
        "0.53572,-2.32143 6.78572,-7.67857 9.10715,-7.67857 2.32143,0 19.46428,0 19.46428,0 0,0 11.96429,1.96429 12.32143,10.35714 " +
        "0.35714,8.39286 0.53572,17.32143 0.53572,17.32143 L 216.25,377.36218 z";

    private const string LeftBumperTabData =
        "m 110.89286,411.82647 1.07143,-10.53572 c 0,0 -0.71429,-9.64285 13.57142,-15.71428 14.28572,-6.07143 26.42858,-13.75 " +
        "43.03572,-17.14286 16.60714,-3.39286 26.60714,-4.28571 34.64286,-4.10714 8.03571,0.17857 20.71428,1.07143 22.14285,1.60714 " +
        "1.42857,0.53571 3.57143,6.96429 5.35715,7.32143 1.78571,0.35714 6.25,1.60714 6.25,1.60714 l 0.53571,12.67857 " +
        "-4.10714,14.10715 c 0,0 -12.85715,-8.92858 -46.60715,-4.28572 -33.75,4.64286 -44.28571,9.82143 -44.28571,9.82143 " +
        "0,0 -30.17857,11.78571 -31.25,10.89286 -1.07143,-0.89286 -0.35714,-6.25 -0.35714,-6.25 z";

    private const string RightTriggerTabData =
        "m 597.21353,379.42254 c 0,0 -6.4156,-39.45502 -6.59384,-42.13298 -0.17815,-2.67794 -2.13844,-19.10266 -2.67318,-21.42354 " +
        "-0.53453,-2.32089 -6.77189,-7.67677 -9.08878,-7.67677 -2.31667,0 -19.42496,0 -19.42496,0 0,0 -11.94019,1.96383 " +
        "-12.29658,10.35471 -0.35639,8.39089 -0.53463,17.31737 -0.53463,17.31737 l 3.20781,41.0618 z";

    private const string RightBumperTabData =
        "m 651.41836,411.88442 -1.06927,-10.53325 c 0,0 0.71288,-9.64059 -13.54409,-15.7106 -14.25677,-6.07 -26.3751,-13.74677 " +
        "-42.94875,-17.13884 -16.57365,-3.39206 -26.55345,-4.2847 -34.57296,-4.10617 -8.01951,0.17852 -20.67247,1.07117 " +
        "-22.09821,1.60676 -1.42568,0.53558 -3.56421,6.96266 -5.34634,7.31971 -1.78211,0.35706 -6.23739,1.60677 -6.23739,1.60677 " +
        "l -0.53462,12.67559 4.09884,14.10385 c 0,0 12.8312,-8.92649 46.51312,-4.28472 33.68194,4.64177 44.19628,9.81913 " +
        "44.19628,9.81913 0,0 30.11773,11.78295 31.187,10.89031 1.06927,-0.89265 0.35639,-6.24854 0.35639,-6.24854 z";

    private static readonly Geometry BodyGeometry = Geometry.Parse(BodyPathData);
    private static readonly Geometry LeftTriggerTab = Geometry.Parse(LeftTriggerTabData);
    private static readonly Geometry LeftBumperTab = Geometry.Parse(LeftBumperTabData);
    private static readonly Geometry RightTriggerTab = Geometry.Parse(RightTriggerTabData);
    private static readonly Geometry RightBumperTab = Geometry.Parse(RightBumperTabData);

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

    // 실측(744×500, SVG 렌더 결과 기준) 좌표. circle: A=cx,B=cy,C=r / rect: A=x,B=y,C=w,D=h
    private static readonly Region[] Regions =
    {
        new(ControllerControl.LeftStick, Shape.Circle, 160, 260, 60, 0),
        new(ControllerControl.RightStick, Shape.Circle, 470, 372, 60, 0),
        new(ControllerControl.Guide, Shape.Circle, 368, 240, 30, 0),
        new(ControllerControl.Back, Shape.Circle, 300, 246, 18, 0),
        new(ControllerControl.Start, Shape.Circle, 442, 246, 18, 0),
        new(ControllerControl.Y, Shape.Circle, 576, 205, 30, 0),
        new(ControllerControl.X, Shape.Circle, 520, 250, 30, 0),
        new(ControllerControl.B, Shape.Circle, 630, 248, 30, 0),
        new(ControllerControl.A, Shape.Circle, 578, 293, 30, 0),
        // 중심(265,363) 기준 대칭 십자 모양(각 팔 50px, 중앙 폭 34px)으로 계산 — 이전엔
        // 네 사각형 크기가 제각각이라 겹치거나 벌어져 비대칭으로 보였다.
        new(ControllerControl.DpadUp, Shape.Rect, 248, 313, 34, 50),
        new(ControllerControl.DpadDown, Shape.Rect, 248, 363, 34, 50),
        new(ControllerControl.DpadLeft, Shape.Rect, 215, 346, 50, 34),
        new(ControllerControl.DpadRight, Shape.Rect, 265, 346, 50, 34),
        new(ControllerControl.LeftBumper, Shape.Rect, 128, 98, 120, 22),
        new(ControllerControl.RightBumper, Shape.Rect, 496, 98, 120, 22),
        new(ControllerControl.LeftTrigger, Shape.Rect, 150, 62, 95, 30),
        new(ControllerControl.RightTrigger, Shape.Rect, 500, 62, 95, 30),
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
            double ex = dx - r.A, ey = dy - r.B, rr = r.C + 4;
            return ex * ex + ey * ey <= rr * rr;
        }
        return dx >= r.A - 3 && dx <= r.A + r.C + 3 && dy >= r.B - 3 && dy <= r.B + r.D + 3;
    }

    public override void Render(DrawingContext context)
    {
        UpdateTransform();

        var bodyFill = new SolidColorBrush(Color.Parse("#26262E"));
        var bodyStroke = new Pen(new SolidColorBrush(Color.Parse("#454552")), 1.5 / _scale);
        var tabFill = new SolidColorBrush(Color.Parse("#33333D"));

        // 실제 SVG 본체 윤곽(+트리거/범퍼 돌출부): layer1 오프셋을 더한 뒤
        // 우리 스케일/오프셋 행렬을 적용해서 그린다.
        var matrix = Matrix.CreateTranslation(LayerOffsetX, LayerOffsetY)
                     * Matrix.CreateScale(_scale, _scale)
                     * Matrix.CreateTranslation(_offsetX, _offsetY);
        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(bodyFill, bodyStroke, BodyGeometry);
            context.DrawGeometry(tabFill, bodyStroke, LeftTriggerTab);
            context.DrawGeometry(tabFill, bodyStroke, LeftBumperTab);
            context.DrawGeometry(tabFill, bodyStroke, RightTriggerTab);
            context.DrawGeometry(tabFill, bodyStroke, RightBumperTab);
        }

        var ctlFill = new SolidColorBrush(Color.Parse("#2C2C34"));
        var ctlStroke = new Pen(new SolidColorBrush(Color.Parse("#48485C")), 1);
        var accent = new SolidColorBrush(Color.Parse("#4C8DF6"));
        var accentPen = new Pen(accent, 2.5);
        var liveFill = new SolidColorBrush(Color.Parse("#34C759"));
        var pressFill = new SolidColorBrush(Color.Parse("#8034C759"));
        var gaugeFill = new SolidColorBrush(Color.Parse("#B334C759"));
        var labelBrush = new SolidColorBrush(Color.Parse("#B8BCC4"));

        var state = LiveState;
        var selected = SelectedControl;

        foreach (var r in Regions)
        {
            bool isSel = r.Control == selected;
            var pen = isSel ? accentPen : ctlStroke;

            if (r.Control is ControllerControl.LeftStick or ControllerControl.RightStick)
                DrawStick(context, r, state, ctlFill, pen, liveFill);
            else if (r.Control is ControllerControl.LeftTrigger or ControllerControl.RightTrigger)
                DrawTrigger(context, r, state, ctlFill, pen, gaugeFill);
            else
                DrawButton(context, r, state, ctlFill, pen, pressFill, labelBrush);
        }
    }

    private void DrawStick(DrawingContext context, Region r, XboxOutputState state, IBrush fill, IPen pen, IBrush live)
    {
        var center = ToScreen(r.A, r.B);
        double radius = S(r.C);
        context.DrawEllipse(fill, pen, center, radius, radius);
        var capStroke = new Pen(new SolidColorBrush(Color.Parse("#3A3A44")), 1);
        context.DrawEllipse(null, capStroke, center, radius * 0.62, radius * 0.62);

        bool isLeft = r.Control == ControllerControl.LeftStick;
        float sx = state.GetAxis(isLeft ? AxisChannel.LeftStickX : AxisChannel.RightStickX);
        float sy = state.GetAxis(isLeft ? AxisChannel.LeftStickY : AxisChannel.RightStickY);
        double maxOff = radius * 0.5;
        var dot = new Point(center.X + sx * maxOff, center.Y - sy * maxOff);
        context.DrawEllipse(live, null, dot, radius * 0.22, radius * 0.22);
    }

    private void DrawTrigger(DrawingContext context, Region r, XboxOutputState state, IBrush fill, IPen pen, IBrush gauge)
    {
        var rect = new Rect(ToScreen(r.A, r.B), new Size(S(r.C), S(r.D)));
        double rx = S(6);
        context.DrawRectangle(fill, pen, rect, rx, rx);

        float v = state.GetAxis(r.Control == ControllerControl.LeftTrigger ? AxisChannel.LeftTrigger : AxisChannel.RightTrigger);
        if (v > 0.01f)
        {
            var fillRect = new Rect(rect.X, rect.Y, rect.Width * Math.Clamp(v, 0f, 1f), rect.Height);
            context.DrawRectangle(gauge, null, fillRect, rx, rx);
        }
    }

    private void DrawButton(DrawingContext context, Region r, XboxOutputState state, IBrush fill, IPen pen, IBrush press, IBrush labelBrush)
    {
        bool pressed = (state.Buttons & r.Control.ToButton()) != 0;

        if (r.Shape == Shape.Circle)
        {
            var center = ToScreen(r.A, r.B);
            double radius = S(r.C);
            context.DrawEllipse(fill, pen, center, radius, radius);
            if (pressed) context.DrawEllipse(press, null, center, radius, radius);
            DrawLabel(context, r.Control, center, radius, labelBrush);
        }
        else
        {
            var rect = new Rect(ToScreen(r.A, r.B), new Size(S(r.C), S(r.D)));
            double rx = S(5);
            context.DrawRectangle(fill, pen, rect, rx, rx);
            if (pressed) context.DrawRectangle(press, null, rect, rx, rx);
        }
    }

    private void DrawLabel(DrawingContext context, ControllerControl c, Point center, double radius, IBrush brush)
    {
        string? text = c switch
        {
            ControllerControl.A => "A",
            ControllerControl.B => "B",
            ControllerControl.X => "X",
            ControllerControl.Y => "Y",
            _ => null,
        };
        if (text is null || radius < 6) return;

        var ft = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), radius * 1.1, brush);
        context.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }
}
