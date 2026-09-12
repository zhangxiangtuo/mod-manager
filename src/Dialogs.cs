using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ModManager
{
    /// <summary>备份与恢复：把删除掉的 MOD 找回来，或清理部署备份。</summary>
    public class BackupDialog : Form
    {
        private ModWorkspace _ws;
        private ListView _lv;
        private Button _btnRestore;
        private Button _btnDelete;
        private Label _lblSummary;

        public BackupDialog(ModWorkspace ws)
        {
            _ws = ws;
            this.Text = "备份与恢复 - " + VersionInfo.AppTitle;
            this.Font = UiKit.Ui(9F);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(800, 470);
            this.MinimumSize = new Size(640, 380);
            this.BackColor = UiKit.Card;

            Label head = UiKit.MakeLabel("工作区 backup 目录中的内容：删除的 MOD 可以恢复，部署备份请用主界面的「还原上次部署」。");
            head.Dock = DockStyle.Top;
            head.Height = 40;
            head.Padding = new Padding(14, 14, 14, 0);
            head.ForeColor = UiKit.SubText;

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 56;
            _btnRestore = UiKit.MakeButton("恢复选中的 MOD", 140, 32, true);
            _btnRestore.Location = new Point(14, 12);
            _btnRestore.Click += delegate { RestoreSelected(); };
            _btnDelete = UiKit.MakeButton("永久删除", 96, 32);
            _btnDelete.Location = new Point(162, 12);
            _btnDelete.ForeColor = UiKit.Danger;
            _btnDelete.Click += delegate { DeleteSelected(); };
            Button open = UiKit.MakeButton("打开备份目录", 116, 32);
            open.Location = new Point(266, 12);
            open.Click += delegate { OpenFolder(); };
            Button close = UiKit.MakeButton("关闭", 90, 32);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(this.ClientSize.Width - 104, 12);
            close.Click += delegate { this.Close(); };
            _lblSummary = UiKit.MakeLabel("");
            _lblSummary.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblSummary.Location = new Point(this.ClientSize.Width - 420, 20);
            _lblSummary.Size = new Size(300, 20);
            _lblSummary.TextAlign = ContentAlignment.MiddleRight;
            _lblSummary.ForeColor = UiKit.SubText;
            _lblSummary.AutoSize = false;
            bottom.Controls.Add(_btnRestore);
            bottom.Controls.Add(_btnDelete);
            bottom.Controls.Add(open);
            bottom.Controls.Add(close);
            bottom.Controls.Add(_lblSummary);

            _lv = new ListView();
            _lv.Dock = DockStyle.Fill;
            _lv.View = View.Details;
            _lv.FullRowSelect = true;
            _lv.MultiSelect = true;
            _lv.HideSelection = false;
            _lv.BorderStyle = BorderStyle.None;
            _lv.Font = UiKit.Ui(9F);
            _lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _lv.Columns.Add("备份名称", 260, HorizontalAlignment.Left);
            _lv.Columns.Add("类型", 170, HorizontalAlignment.Left);
            _lv.Columns.Add("文件", 70, HorizontalAlignment.Right);
            _lv.Columns.Add("大小", 90, HorizontalAlignment.Right);
            _lv.Columns.Add("时间", 150, HorizontalAlignment.Left);
            _lv.DoubleClick += delegate { RestoreSelected(); };

            Panel pad = new Panel();
            pad.Dock = DockStyle.Fill;
            pad.Padding = new Padding(14, 0, 14, 6);
            pad.Controls.Add(_lv);

            this.Controls.Add(pad);
            this.Controls.Add(bottom);
            this.Controls.Add(head);
            this.CancelButton = close;
            Reload();
        }

        private void Reload()
        {
            _lv.Items.Clear();
            List<string> dirs = new List<string>();
            try
            {
                if (Directory.Exists(_ws.BackupPath))
                    dirs.AddRange(Directory.GetDirectories(_ws.BackupPath));
            }
            catch (Exception ex)
            {
                _ws.Log.Error("读取备份目录失败：" + ex.Message);
            }
            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            int restorable = 0;
            foreach (string dir in dirs)
            {
                string name = Path.GetFileName(dir);
                string type;
                bool canRestore = false;
                string when = "";
                if (name.StartsWith("deleted_", StringComparison.OrdinalIgnoreCase))
                {
                    canRestore = true;
                    restorable++;
                    type = "已删除的 MOD";
                    BackupInfo info = Json.Load<BackupInfo>(Path.Combine(dir, ModWorkspace.BackupInfoFile));
                    if (info != null)
                    {
                        type += info.WasEnabled ? "（删除前已启用）" : "（删除前已禁用）";
                        when = info.DeletedAt;
                    }
                }
                else if (name.StartsWith("deploy_", StringComparison.OrdinalIgnoreCase))
                {
                    type = "部署备份";
                    if (_ws.LastDeploy != null && string.Equals(_ws.LastDeploy.BackupFolder, name, StringComparison.OrdinalIgnoreCase))
                        type += "（当前使用中）";
                }
                else if (name.StartsWith("_tmp_", StringComparison.OrdinalIgnoreCase))
                {
                    type = "临时目录";
                }
                else
                {
                    type = "其他";
                }
                if (when.Length == 0)
                {
                    try { when = Directory.GetLastWriteTime(dir).ToString("yyyy-MM-dd HH:mm"); }
                    catch (Exception) { }
                }
                int fc; long size;
                FileUtil.MeasureDirectory(dir, out fc, out size);
                ListViewItem it = new ListViewItem(new string[]
                {
                    name, type, fc.ToString(), FileUtil.FormatSize(size), when
                });
                it.Tag = dir;
                if (!canRestore) it.ForeColor = UiKit.SubText;
                _lv.Items.Add(it);
            }
            _lblSummary.Text = "共 " + dirs.Count + " 项，其中可恢复 " + restorable + " 个";
            _btnRestore.Enabled = restorable > 0;
            _btnDelete.Enabled = dirs.Count > 0;
            if (dirs.Count == 0)
            {
                ListViewItem empty = new ListViewItem(new string[] { "（备份目录为空）", "", "", "", "" });
                empty.ForeColor = UiKit.SubText;
                _lv.Items.Add(empty);
            }
        }

        private void RestoreSelected()
        {
            if (_lv.SelectedItems.Count == 0) return;
            int ok = 0;
            foreach (ListViewItem it in _lv.SelectedItems)
            {
                string dir = it.Tag as string;
                if (dir == null) continue;
                if (!Path.GetFileName(dir).StartsWith("deleted_", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "只有「已删除的 MOD」可以恢复。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    continue;
                }
                try
                {
                    _ws.RestoreFromBackup(dir);
                    ok++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "恢复失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            Reload();
            if (ok > 0) this.DialogResult = DialogResult.OK;
        }

        private void DeleteSelected()
        {
            if (_lv.SelectedItems.Count == 0) return;
            List<string> targets = new List<string>();
            foreach (ListViewItem it in _lv.SelectedItems)
            {
                string dir = it.Tag as string;
                if (dir != null) targets.Add(dir);
            }
            if (targets.Count == 0) return;
            string text = targets.Count == 1
                ? "永久删除备份「" + Path.GetFileName(targets[0]) + "」？该操作不可恢复。"
                : "永久删除选中的 " + targets.Count + " 个备份？该操作不可恢复。";
            if (MessageBox.Show(this, text, "确认删除", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            foreach (string dir in targets)
            {
                try { _ws.PurgeBackup(dir); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "无法删除", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            Reload();
        }

        private void OpenFolder()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("explorer.exe", "\"" + _ws.BackupPath + "\"");
                psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开目录：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
