using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ModManager
{
    /// <summary>统一的配色、字体和控件工厂。</summary>
    public static class UiKit
    {
        public static readonly Color Back = Color.FromArgb(0xF4, 0xF5, 0xF7);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(0xE1, 0xE4, 0xEA);
        public static readonly Color Text = Color.FromArgb(0x1F, 0x23, 0x29);
        public static readonly Color SubText = Color.FromArgb(0x6B, 0x72, 0x80);
        public static readonly Color Accent = Color.FromArgb(0x2F, 0x6F, 0xED);
        public static readonly Color AccentHover = Color.FromArgb(0x4C, 0x86, 0xF3);
        public static readonly Color AccentDown = Color.FromArgb(0x25, 0x5C, 0xC9);
        public static readonly Color Danger = Color.FromArgb(0xD9, 0x3B, 0x3B);
        public static readonly Color BtnBack = Color.FromArgb(0xFB, 0xFB, 0xFC);
        public static readonly Color BtnHover = Color.FromArgb(0xEC, 0xF2, 0xFE);
        public static readonly Color BtnDown = Color.FromArgb(0xDD, 0xE8, 0xFD);
        public static readonly Color Ok = Color.FromArgb(0x1A, 0x7F, 0x37);
        public static readonly Color Warn = Color.FromArgb(0x9A, 0x67, 0x00);
        public static readonly Color Err = Color.FromArgb(0xCF, 0x22, 0x2E);

        private static string _family;

        public static Font Ui(float size)
        {
            return Ui(size, FontStyle.Regular);
        }

        public static Font Ui(float size, FontStyle style)
        {
            if (_family == null)
            {
                _family = "微软雅黑";
                try
                {
                    using (FontFamily f = new FontFamily("微软雅黑")) { }
                }
                catch (Exception)
                {
                    _family = SystemFonts.MessageBoxFont.FontFamily.Name;
                }
            }
            try { return new Font(_family, size, style); }
            catch (Exception) { return new Font(SystemFonts.MessageBoxFont.FontFamily, size, style); }
        }

        public static Button MakeButton(string text, int width, int height)
        {
            return MakeButton(text, width, height, false);
        }

        public static Button MakeButton(string text, int width, int height, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Size = new Size(width, height);
            b.FlatStyle = FlatStyle.Flat;
            b.Font = Ui(9F);
            b.UseVisualStyleBackColor = false;
            b.TabStop = true;
            b.FlatAppearance.BorderSize = 1;
            if (primary)
            {
                b.BackColor = Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderColor = Accent;
                b.FlatAppearance.MouseOverBackColor = AccentHover;
                b.FlatAppearance.MouseDownBackColor = AccentDown;
            }
            else
            {
                b.BackColor = BtnBack;
                b.ForeColor = Text;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.MouseOverBackColor = BtnHover;
                b.FlatAppearance.MouseDownBackColor = BtnDown;
            }
            b.Cursor = Cursors.Hand;
            return b;
        }

        public static Button MakeLinkButton(string text, int width, int height, Color fore)
        {
            Button b = MakeButton(text, width, height, false);
            b.ForeColor = fore;
            return b;
        }

        public static Label MakeLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = Text;
            l.Font = Ui(9F);
            l.BackColor = Color.Transparent;
            return l;
        }

        public static Label MakeFieldLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = false;
            l.ForeColor = SubText;
            l.Font = Ui(9F);
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.BackColor = Color.Transparent;
            return l;
        }

        public static TextBox MakeTextBox(bool readOnly)
        {
            TextBox t = new TextBox();
            t.Font = Ui(9F);
            t.ForeColor = Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.ReadOnly = readOnly;
            if (readOnly) t.BackColor = Color.FromArgb(0xFA, 0xFA, 0xFB);
            return t;
        }

        /// <summary>生成一个圆形状态图标。</summary>
        public static Bitmap Dot(Color color, int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush br = new SolidBrush(color))
                {
                    g.FillEllipse(br, 1, 1, size - 3, size - 3);
                }
                using (Pen p = new Pen(Color.FromArgb(60, 0, 0, 0)))
                {
                    g.DrawEllipse(p, 1, 1, size - 3, size - 3);
                }
            }
            return bmp;
        }

        public static Bitmap WarnDot(int size)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (SolidBrush br = new SolidBrush(Color.FromArgb(0xF0, 0x8C, 0x00)))
                {
                    PointF[] pts = new PointF[]
                    {
                        new PointF(size / 2f, 1.5f),
                        new PointF(size - 1.5f, size - 2.5f),
                        new PointF(1.5f, size - 2.5f)
                    };
                    g.FillPolygon(br, pts);
                }
                using (Pen p = new Pen(Color.White, 1.4f))
                {
                    g.DrawLine(p, size / 2f, 4.5f, size / 2f, size - 6f);
                    g.DrawLine(p, size / 2f, size - 4.5f, size / 2f, size - 3.5f);
                }
            }
            return bmp;
        }

        public static string Ellipsis(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ");
            if (text.Length <= max) return text;
            return text.Substring(0, max - 1) + "…";
        }

        /// <summary>超长路径时保留尾部（更有用的部分）。</summary>
        public static string EllipsisLeft(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= max) return text;
            return "…" + text.Substring(text.Length - max + 1);
        }
    }

    /// <summary>简单的单行输入对话框。</summary>
    public class InputDialog : Form
    {
        private TextBox _box;
        public string Value { get { return _box.Text.Trim(); } }

        public InputDialog(string title, string prompt, string defaultValue)
        {
            this.Text = title;
            this.Font = UiKit.Ui(9F);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(430, 148);
            this.BackColor = UiKit.Card;

            Label lbl = UiKit.MakeLabel(prompt);
            lbl.Location = new Point(18, 18);
            lbl.MaximumSize = new Size(394, 0);

            _box = UiKit.MakeTextBox(false);
            _box.Location = new Point(20, 50);
            _box.Size = new Size(390, 26);
            _box.Text = defaultValue == null ? "" : defaultValue;
            _box.SelectAll();

            Button ok = UiKit.MakeButton("确定", 92, 30, true);
            ok.Location = new Point(220, 98);
            ok.DialogResult = DialogResult.OK;
            Button cancel = UiKit.MakeButton("取消", 92, 30);
            cancel.Location = new Point(318, 98);
            cancel.DialogResult = DialogResult.Cancel;

            this.Controls.Add(lbl);
            this.Controls.Add(_box);
            this.Controls.Add(ok);
            this.Controls.Add(cancel);
            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }

        public static string Ask(IWin32Window owner, string title, string prompt, string defaultValue)
        {
            using (InputDialog d = new InputDialog(title, prompt, defaultValue))
            {
                if (d.ShowDialog(owner) == DialogResult.OK) return d.Value;
            }
            return null;
        }
    }

    /// <summary>纯文本说明窗口。</summary>
    public class TextDialog : Form
    {
        public TextDialog(string title, string content)
        {
            this.Text = title;
            this.Font = UiKit.Ui(9F);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(720, 560);
            this.MinimumSize = new Size(520, 380);
            this.BackColor = UiKit.Card;

            RichTextBox box = new RichTextBox();
            box.Dock = DockStyle.Fill;
            box.ReadOnly = true;
            box.BorderStyle = BorderStyle.None;
            box.BackColor = UiKit.Card;
            box.ForeColor = UiKit.Text;
            box.Font = UiKit.Ui(9.5F);
            box.WordWrap = true;
            box.ScrollBars = RichTextBoxScrollBars.Vertical;
            box.Text = content;
            box.Select(0, 0);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 54;
            bottom.BackColor = UiKit.Card;
            Button ok = UiKit.MakeButton("关闭", 100, 32, true);
            ok.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            ok.Location = new Point(this.ClientSize.Width - 118, 10);
            ok.Click += delegate { this.Close(); };
            bottom.Controls.Add(ok);

            Panel pad = new Panel();
            pad.Dock = DockStyle.Fill;
            pad.Padding = new Padding(16, 14, 16, 6);
            pad.BackColor = UiKit.Card;
            pad.Controls.Add(box);

            this.Controls.Add(pad);
            this.Controls.Add(bottom);
            this.AcceptButton = ok;
            this.CancelButton = ok;
        }
    }
}
