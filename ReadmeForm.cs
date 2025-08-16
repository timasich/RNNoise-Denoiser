using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;

namespace RNNoise_Denoiser
{
    public class ReadmeForm : Form
    {
        readonly CheckBox _chkDontShow;
        public bool DontShow
        {
            get => _chkDontShow.Checked;
            set => _chkDontShow.Checked = value;
        }
        public ReadmeForm()
        {
            Text = "README";
            Width = 600;
            Height = 400;
            StartPosition = FormStartPosition.CenterParent;

            Theme.Apply(this);
            var iconPath = Path.Combine(AppContext.BaseDirectory, "RNNoise Denoiser Icon.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);

            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                WordWrap = false
            };
            Theme.StyleInput(box);
            try
            {
                box.Text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "README.md"));
            }
            catch
            {
                box.Text = "README not found.";
            }
            Controls.Add(box);

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight, BackColor = Theme.BgSurface };
            _chkDontShow = new CheckBox { Text = "Don't show again", AutoSize = true, ForeColor = Theme.TextPrimary, BackColor = Theme.BgSurface };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK };
            Theme.StylePrimary(btnOk);
            panel.Controls.Add(_chkDontShow);
            panel.Controls.Add(btnOk);
            Controls.Add(panel);
            AcceptButton = btnOk;
        }
    }
}
