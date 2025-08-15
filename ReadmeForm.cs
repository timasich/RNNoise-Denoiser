using System;
using System.IO;
using System.Windows.Forms;

namespace RNNoise_Denoiser
{
    public class ReadmeForm : Form
    {
        readonly CheckBox _chkDontShow;
        public bool DontShow => _chkDontShow.Checked;
        public ReadmeForm()
        {
            Text = "README";
            Width = 600;
            Height = 400;
            StartPosition = FormStartPosition.CenterParent;

            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                WordWrap = false
            };
            try
            {
                box.Text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "README.md"));
            }
            catch
            {
                box.Text = "README not found.";
            }
            Controls.Add(box);

            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight };
            _chkDontShow = new CheckBox { Text = "Don't show again", AutoSize = true };
            var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK };
            panel.Controls.Add(_chkDontShow);
            panel.Controls.Add(btnOk);
            Controls.Add(panel);
            AcceptButton = btnOk;
        }
    }
}
