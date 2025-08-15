using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RNNoise_Denoiser
{
    static class Theme
    {
        public static readonly Color BgBase = ColorTranslator.FromHtml("#0B1220");
        public static readonly Color BgSurface = ColorTranslator.FromHtml("#111827");
        public static readonly Color BgElevated = ColorTranslator.FromHtml("#151B2D");
        public static readonly Color LineBorder = ColorTranslator.FromHtml("#283046");
        public static readonly Color TextPrimary = ColorTranslator.FromHtml("#E5E7EB");
        public static readonly Color TextSecondary = ColorTranslator.FromHtml("#B6BDC9");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#8B93A3");
        public static readonly Color AccentPrimary = ColorTranslator.FromHtml("#3DD6FF");
        public static readonly Color AccentPrimaryHover = ColorTranslator.FromHtml("#5FE2FF");
        public static readonly Color AccentPrimaryPressed = ColorTranslator.FromHtml("#1FBEE8");
        public static readonly Color Success = ColorTranslator.FromHtml("#10B981");
        public static readonly Color Warning = ColorTranslator.FromHtml("#F59E0B");
        public static readonly Color Error = ColorTranslator.FromHtml("#EF4444");
        public static readonly Color Info = ColorTranslator.FromHtml("#60A5FA");

        public static void Apply(Form form)
        {
            form.BackColor = BgBase;
            form.ForeColor = TextPrimary;
            EnableDarkTitleBar(form.Handle);
        }

        public static void StyleInput(Control ctrl)
        {
            ctrl.BackColor = ColorTranslator.FromHtml("#0E1628");
            ctrl.ForeColor = TextPrimary;
        }

        public static void StylePrimary(Button btn)
        {
            btn.BackColor = ColorTranslator.FromHtml("#1F2A44");
            btn.ForeColor = TextPrimary;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = LineBorder;
            btn.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#263356");
            btn.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#1A233A");
        }

        public static void StyleSecondary(Button btn)
        {
            btn.BackColor = ColorTranslator.FromHtml("#141C2E");
            btn.ForeColor = TextPrimary;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = LineBorder;
            btn.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#1A2438");
            btn.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#121A2C");
        }

        public static void StyleDanger(Button btn)
        {
            btn.BackColor = ColorTranslator.FromHtml("#3A0F16");
            btn.ForeColor = ColorTranslator.FromHtml("#FCA5A5");
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = LineBorder;
            btn.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#4C121B");
            btn.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#27080E");
        }

        public static void StyleStatusStrip(StatusStrip strip)
        {
            strip.BackColor = BgSurface;
            strip.ForeColor = TextSecondary;
        }

        public static void StyleComboBox(ComboBox box)
        {
            box.DrawMode = DrawMode.OwnerDrawFixed;
            box.FlatStyle = FlatStyle.Flat;
            box.BackColor = ColorTranslator.FromHtml("#0E1628");
            box.ForeColor = TextPrimary;
            UseDarkScrollbars(box);
            box.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                var bg = isSelected ? AccentPrimary : box.BackColor;
                var fg = isSelected ? BgBase : TextPrimary;
                using var b = new SolidBrush(bg);
                using var t = new SolidBrush(fg);
                e.Graphics.FillRectangle(b, e.Bounds);
                e.Graphics.DrawString(box.Items[e.Index].ToString()!, e.Font, t, e.Bounds);
                e.DrawFocusRectangle();
            };
        }

        public static void UseDarkScrollbars(Control ctrl)
        {
            if (OperatingSystem.IsWindows())
                SetWindowTheme(ctrl.Handle, "DarkMode_Explorer", null);
        }

        static void EnableDarkTitleBar(nint handle)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10)) return;
            int useDark = 1;
            DwmSetWindowAttribute(handle, 19, ref useDark, sizeof(int));
            DwmSetWindowAttribute(handle, 20, ref useDark, sizeof(int));
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(nint hWnd, string? appName, string? idList);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);
    }
}
