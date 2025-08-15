using System.Drawing;
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
    }
}
