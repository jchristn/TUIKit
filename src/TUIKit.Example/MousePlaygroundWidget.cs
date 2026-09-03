namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// The live demo for the "Mouse playground" tour page: shows the pointer cell, hover
    /// Enter/Leave counts, single/double/triple click detection with modifiers, wheel activity on
    /// all four axes, and a paint area where dragging with the left button lights up cells.
    /// </summary>
    internal sealed class MousePlaygroundWidget : IWidget, IMouseAware
    {
        private const int CanvasTop = 6;

        private readonly HashSet<Point> _Painted = new HashSet<Point>();
        private int _PointerX = -1;
        private int _PointerY = -1;
        private bool _Inside;
        private int _EnterCount;
        private int _LeaveCount;
        private int _WheelUp;
        private int _WheelDown;
        private int _WheelLeft;
        private int _WheelRight;
        private bool _Dragging;
        private string _LastClick = "none yet — click, double-click, or triple-click anywhere here";

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            switch (mouse.Kind)
            {
                case MouseEventKind.Enter:
                    _Inside = true;
                    _EnterCount++;
                    _PointerX = mouse.X;
                    _PointerY = mouse.Y;
                    return false;
                case MouseEventKind.Leave:
                    _Inside = false;
                    _LeaveCount++;
                    _Dragging = false;
                    _PointerX = -1;
                    _PointerY = -1;
                    return false;
                case MouseEventKind.Move:
                    _PointerX = mouse.X;
                    _PointerY = mouse.Y;
                    if (_Dragging && mouse.Y >= CanvasTop)
                        _Painted.Add(new Point(mouse.X, mouse.Y));
                    return true;
                case MouseEventKind.Press:
                    _PointerX = mouse.X;
                    _PointerY = mouse.Y;
                    _LastClick = Describe(mouse);
                    if (mouse.Button == MouseButton.Left)
                    {
                        _Dragging = true;
                        if (mouse.Y >= CanvasTop)
                            _Painted.Add(new Point(mouse.X, mouse.Y));
                        if (mouse.ClickCount >= 3)
                            _Painted.Clear();
                    }

                    return true;
                case MouseEventKind.Release:
                    _Dragging = false;
                    return true;
                case MouseEventKind.Wheel:
                    switch (mouse.Button)
                    {
                        case MouseButton.WheelUp:
                            _WheelUp++;
                            break;
                        case MouseButton.WheelDown:
                            _WheelDown++;
                            break;
                        case MouseButton.WheelLeft:
                            _WheelLeft++;
                            break;
                        case MouseButton.WheelRight:
                            _WheelRight++;
                            break;
                        default:
                            break;
                    }

                    return true;
                default:
                    return false;
            }
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            CellStyle text = CellStyle.Default;
            CellStyle accent = CellStyle.Default.WithForeground(Color.FromPalette(6));
            CellStyle muted = CellStyle.Default.WithForeground(Color.FromPalette(8));

            string pointer = _Inside
                ? "(" + _PointerX.ToString(CultureInfo.InvariantCulture) + ", " + _PointerY.ToString(CultureInfo.InvariantCulture) + ")"
                : "outside";
            surface.DrawText(0, 0, "Pointer: ", text);
            surface.DrawText(9, 0, pointer, accent);

            surface.DrawText(0, 1,
                "Hover — enters: " + _EnterCount.ToString(CultureInfo.InvariantCulture)
                + "  leaves: " + _LeaveCount.ToString(CultureInfo.InvariantCulture), text);

            surface.DrawText(0, 2, "Last click: ", text);
            surface.DrawText(12, 2, _LastClick, accent);

            surface.DrawText(0, 3,
                "Wheel — up: " + _WheelUp.ToString(CultureInfo.InvariantCulture)
                + "  down: " + _WheelDown.ToString(CultureInfo.InvariantCulture)
                + "  left: " + _WheelLeft.ToString(CultureInfo.InvariantCulture)
                + "  right: " + _WheelRight.ToString(CultureInfo.InvariantCulture), text);

            surface.DrawText(0, 5, "Drag below with the left button to paint; triple-click clears:", muted);

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            for (int y = CanvasTop; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (_Painted.Contains(new Point(x, y)))
                        surface.DrawText(x, y, "█", accent);
                }
            }

            if (_Inside && _PointerY >= 0 && _PointerY < height && _PointerX >= 0 && _PointerX < width
                && !_Painted.Contains(new Point(_PointerX, _PointerY)))
            {
                surface.DrawText(_PointerX, _PointerY, "┼", muted);
            }
        }

        private static string Describe(MouseEvent mouse)
        {
            string count;
            switch (mouse.ClickCount)
            {
                case 2:
                    count = "double";
                    break;
                case 3:
                    count = "triple";
                    break;
                default:
                    count = "single";
                    break;
            }

            string modifiers = mouse.Modifiers == KeyModifiers.None ? string.Empty : " +" + mouse.Modifiers;
            return count + " " + mouse.Button + " at ("
                + mouse.X.ToString(CultureInfo.InvariantCulture) + ", "
                + mouse.Y.ToString(CultureInfo.InvariantCulture) + ")" + modifiers;
        }
    }
}
