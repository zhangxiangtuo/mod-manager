using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ModManager
{
    public class MainForm : Form
    {
        private readonly Logger _log = new Logger();
        private ModWorkspace _ws;
        private AppSettings _settings;
        private List<ModInfo> _mods = new List<ModInfo>();
        private List<ConflictGroup> _conflicts = new List<ConflictGroup>();
        private bool _suspendEvents;
        private bool _busy;

        // 顶部
        private TextBox _txtWorkspace;
        private TextBox _txtGameDir;
        private Button _btnPickWs;
        private Button _btnNewWs;
        private Button _btnOpenWs;
        private Button _btnPickGame;
        private Button _btnLaunch;
        private Button _btnDetect;

        // 列表
        private ListView _lv;
        private Panel _emptyPanel;
        private TextBox _txtSearch;
        private Button _btnInstall;
        private Button _btnEnable;
        private Button _btnDisable;
        private Button _btnUp;
        private Button _btnDown;
        private Button _btnRename;
        private Button _btnDelete;
        private ComboBox _cboDeployMode;

        // 详情
        private Label _lblName;
        private Label _lblStatusV;
        private Label _lblFilesV;
        private Label _lblSizeV;
        private Label _lblTimeV;
        private Label _lblSourceV;
        private TextBox _txtVersion;
        private TextBox _txtAuthor;
        private TextBox _txtNote;
        private ListBox _lstConflicts;
        private Label _lblConflictSummary;
        private Button _btnOpenMod;
        private Button _btnFlatten;
        private Button _btnSave;

        // 底部
        private RichTextBox _logBox;
        private ToolStripStatusLabel _statLeft;
        private ToolStripStatusLabel _statDeploy;
        private ToolStripStatusLabel _statRight;

        private const string EmptyHint = "请先选择或新建一个 MOD 工作区，然后就可以安装、启用和管理 MOD 了。";

        public MainForm()
        {
            _settings = SettingsStore.Load();
            BuildUi();
            _log.Logged += OnLogged;
            _log.Info("欢迎使用 " + VersionInfo.AppTitle + " v" + VersionInfo.Version + "，把 MOD 压缩包直接拖到窗口里即可安装。");
            if (ModWorkspace.FindSevenZip() == null)
                _log.Warn("未检测到 7-Zip：zip 格式可直接安装，7z / rar 需要先安装 7-Zip 或手动解压。");
            OpenLastWorkspace();
        }

        // =====================================================================
        // 界面构建
        // =====================================================================

        private void BuildUi()
        {
            this.Text = VersionInfo.AppTitle + " v" + VersionInfo.Version + " — 游戏 MOD 管理工具";
            this.Font = UiKit.Ui(9F);
            this.BackColor = UiKit.Back;
            this.ClientSize = new Size(_settings.WindowWidth, _settings.WindowHeight);
            this.MinimumSize = new Size(1100, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AllowDrop = true;
            this.KeyPreview = true;
            this.DoubleBuffered = true;
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            // ---------- 顶部 ----------
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Size = new Size(1180, 96);
            top.Height = 96;
            top.BackColor = UiKit.Card;
            top.Paint += delegate(object s, PaintEventArgs e) { DrawBottomLine(e.Graphics, top.ClientSize.Width, top.ClientSize.Height); };

            Label lblWs = UiKit.MakeFieldLabel("MOD 工作区");
            lblWs.Location = new Point(16, 18);
            lblWs.Size = new Size(84, 26);

            _txtWorkspace = UiKit.MakeTextBox(true);
            _txtWorkspace.Location = new Point(104, 18);
            _txtWorkspace.Size = new Size(694, 26);
            _txtWorkspace.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _btnPickWs = UiKit.MakeButton("选择工作区", 110, 28);
            _btnPickWs.Location = new Point(810, 17);
            _btnPickWs.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnPickWs.Click += delegate { PickWorkspace(); };

            _btnNewWs = UiKit.MakeButton("新建工作区", 110, 28);
            _btnNewWs.Location = new Point(928, 17);
            _btnNewWs.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnNewWs.Click += delegate { NewWorkspace(); };

            _btnOpenWs = UiKit.MakeButton("打开目录", 120, 28);
            _btnOpenWs.Location = new Point(1046, 17);
            _btnOpenWs.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnOpenWs.Click += delegate { OpenPath(_ws == null ? null : _ws.Root, "工作区目录"); };

            Label lblGame = UiKit.MakeFieldLabel("游戏目录");
            lblGame.Location = new Point(16, 58);
            lblGame.Size = new Size(84, 26);

            _txtGameDir = UiKit.MakeTextBox(true);
            _txtGameDir.Location = new Point(104, 56);
            _txtGameDir.Size = new Size(694, 26);
            _txtGameDir.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _btnPickGame = UiKit.MakeButton("选择目录", 110, 28);
            _btnPickGame.Location = new Point(810, 55);
            _btnPickGame.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnPickGame.Click += delegate { PickGameDir(); };

            _btnLaunch = UiKit.MakeButton("启动游戏", 110, 28);
            _btnLaunch.Location = new Point(928, 55);
            _btnLaunch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnLaunch.Click += delegate { LaunchGame(); };

            _btnDetect = UiKit.MakeButton("选择游戏程序", 120, 28);
            _btnDetect.Location = new Point(1046, 55);
            _btnDetect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnDetect.Click += delegate { PickGameExe(); };

            top.Controls.Add(lblWs);
            top.Controls.Add(_txtWorkspace);
            top.Controls.Add(_btnPickWs);
            top.Controls.Add(_btnNewWs);
            top.Controls.Add(_btnOpenWs);
            top.Controls.Add(lblGame);
            top.Controls.Add(_txtGameDir);
            top.Controls.Add(_btnPickGame);
            top.Controls.Add(_btnLaunch);
            top.Controls.Add(_btnDetect);

            // ---------- 中部：左侧列表 ----------
            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            left.BackColor = UiKit.Card;
            left.Size = new Size(830, 420);

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Size = new Size(830, 46);
            toolbar.Height = 46;
            toolbar.BackColor = UiKit.Card;
            toolbar.Paint += delegate(object s, PaintEventArgs e) { DrawBottomLine(e.Graphics, toolbar.ClientSize.Width, toolbar.ClientSize.Height); };

            _btnInstall = UiKit.MakeButton("＋ 安装 MOD", 116, 30, true);
            _btnInstall.Location = new Point(14, 9);
            _btnInstall.Click += delegate { InstallFromDialog(); };
            _btnEnable = UiKit.MakeButton("启用", 72, 30);
            _btnEnable.Location = new Point(138, 9);
            _btnEnable.Click += delegate { ToggleSelected(true); };
            _btnDisable = UiKit.MakeButton("禁用", 72, 30);
            _btnDisable.Location = new Point(216, 9);
            _btnDisable.Click += delegate { ToggleSelected(false); };
            _btnUp = UiKit.MakeButton("上移", 64, 30);
            _btnUp.Location = new Point(294, 9);
            _btnUp.Click += delegate { MoveSelected(-1); };
            _btnDown = UiKit.MakeButton("下移", 64, 30);
            _btnDown.Location = new Point(364, 9);
            _btnDown.Click += delegate { MoveSelected(1); };
            _btnRename = UiKit.MakeButton("重命名", 76, 30);
            _btnRename.Location = new Point(434, 9);
            _btnRename.Click += delegate { RenameSelected(); };
            _btnDelete = UiKit.MakeButton("删除", 68, 30);
            _btnDelete.Location = new Point(516, 9);
            _btnDelete.ForeColor = UiKit.Danger;
            _btnDelete.Click += delegate { DeleteSelected(); };

            Label lblSearch = UiKit.MakeFieldLabel("搜索");
            lblSearch.TextAlign = ContentAlignment.MiddleRight;
            lblSearch.Size = new Size(40, 30);
            lblSearch.Location = new Point(614, 9);
            lblSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            _txtSearch = UiKit.MakeTextBox(false);
            _txtSearch.Location = new Point(660, 10);
            _txtSearch.Size = new Size(180, 26);
            _txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _txtSearch.TextChanged += delegate { FillList(SelectedName()); };

            toolbar.Controls.Add(_btnInstall);
            toolbar.Controls.Add(_btnEnable);
            toolbar.Controls.Add(_btnDisable);
            toolbar.Controls.Add(_btnUp);
            toolbar.Controls.Add(_btnDown);
            toolbar.Controls.Add(_btnRename);
            toolbar.Controls.Add(_btnDelete);
            toolbar.Controls.Add(lblSearch);
            toolbar.Controls.Add(_txtSearch);

            _lv = new ListView();
            _lv.Dock = DockStyle.Fill;
            _lv.View = View.Details;
            _lv.CheckBoxes = true;
            _lv.FullRowSelect = true;
            _lv.MultiSelect = true;
            _lv.HideSelection = false;
            _lv.GridLines = false;
            _lv.BorderStyle = BorderStyle.None;
            _lv.Font = UiKit.Ui(9F);
            _lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _lv.Columns.Add("顺序", 52, HorizontalAlignment.Center);
            _lv.Columns.Add("MOD 名称", 300, HorizontalAlignment.Left);
            _lv.Columns.Add("状态", 64, HorizontalAlignment.Center);
            _lv.Columns.Add("文件", 62, HorizontalAlignment.Right);
            _lv.Columns.Add("大小", 80, HorizontalAlignment.Right);
            _lv.Columns.Add("冲突", 52, HorizontalAlignment.Center);
            _lv.Columns.Add("备注 / 版本", 150, HorizontalAlignment.Left);
            _lv.ItemChecked += Lv_ItemChecked;
            _lv.SelectedIndexChanged += delegate { ShowDetails(SelectedMod()); };
            _lv.DoubleClick += delegate { OpenSelectedModFolder(); };
            _lv.KeyDown += Lv_KeyDown;
            _lv.MouseUp += Lv_MouseUp;

            ImageList icons = new ImageList();
            icons.ImageSize = new Size(16, 16);
            icons.ColorDepth = ColorDepth.Depth32Bit;
            icons.Images.Add(UiKit.Dot(UiKit.Ok, 16));
            icons.Images.Add(UiKit.Dot(Color.FromArgb(0xB0, 0xB6, 0xBF), 16));
            _lv.SmallImageList = icons;
            _lv.AllowDrop = true;
            _lv.DragEnter += Form_DragEnter;
            _lv.DragDrop += Form_DragDrop;

            left.Controls.Add(_lv);
            left.Controls.Add(toolbar);
            left.Controls.Add(CreateEmptyState());

            // ---------- 中部：右侧详情 ----------
            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.BackColor = UiKit.Back;
            right.Padding = new Padding(10, 8, 4, 8);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = UiKit.Ui(9F);
            tabs.Padding = new Point(14, 5);

            TabPage tabInfo = new TabPage("MOD 详情");
            tabInfo.BackColor = UiKit.Card;
            tabInfo.Padding = new Padding(12, 10, 12, 8);

            BuildInfoTab(tabInfo);

            TabPage tabConf = new TabPage("文件冲突");
            tabConf.BackColor = UiKit.Card;
            tabConf.Padding = new Padding(12, 10, 12, 8);

            Panel confBottom = new Panel();
            confBottom.Dock = DockStyle.Bottom;
            confBottom.Height = 42;
            Button btnRecheck = UiKit.MakeButton("重新检测冲突", 118, 30);
            btnRecheck.Location = new Point(0, 6);
            btnRecheck.Click += delegate { RunConflictCheck(); };
            confBottom.Controls.Add(btnRecheck);

            _lstConflicts = new ListBox();
            _lstConflicts.Dock = DockStyle.Fill;
            _lstConflicts.Font = UiKit.Ui(9F);
            _lstConflicts.BorderStyle = BorderStyle.FixedSingle;
            _lstConflicts.IntegralHeight = false;
            _lstConflicts.HorizontalScrollbar = true;

            _lblConflictSummary = UiKit.MakeLabel("");
            _lblConflictSummary.Dock = DockStyle.Top;
            _lblConflictSummary.Height = 48;
            _lblConflictSummary.ForeColor = UiKit.SubText;
            _lblConflictSummary.AutoSize = false;

            tabConf.Controls.Add(_lstConflicts);
            tabConf.Controls.Add(_lblConflictSummary);
            tabConf.Controls.Add(confBottom);

            tabs.TabPages.Add(tabInfo);
            tabs.TabPages.Add(tabConf);
            right.Controls.Add(tabs);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterWidth = 7;
            split.Size = new Size(1180, 420);   // 先给足尺寸，否则 MinSize 校验会失败
            split.FixedPanel = FixedPanel.Panel2;
            split.BackColor = UiKit.Back;
            split.Panel1.BackColor = UiKit.Card;
            split.Panel2.BackColor = UiKit.Back;
            split.Panel1MinSize = 480;
            split.Panel2MinSize = 290;
            split.Panel1.Controls.Add(left);
            split.Panel2.Controls.Add(right);
            SetSplitDistance(split, 340);

            // ---------- 底部：日志 ----------
            Panel logPanel = new Panel();
            logPanel.Dock = DockStyle.Bottom;
            logPanel.Size = new Size(1180, 150);
            logPanel.Height = 150;
            logPanel.BackColor = UiKit.Card;
            logPanel.Paint += delegate(object s, PaintEventArgs e) { DrawTopLine(e.Graphics, logPanel.ClientSize.Width); };

            Label lblLog = UiKit.MakeLabel("运行日志");
            lblLog.Location = new Point(14, 8);
            lblLog.ForeColor = UiKit.SubText;

            Button btnClear = UiKit.MakeButton("清空日志", 82, 24);
            btnClear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClear.Location = new Point(logPanel.Width - 96, 6);
            btnClear.Click += delegate { _logBox.Clear(); };

            _logBox = new RichTextBox();
            _logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _logBox.Location = new Point(14, 34);
            _logBox.Size = new Size(logPanel.Width - 28, logPanel.Height - 46);
            _logBox.ReadOnly = true;
            _logBox.BackColor = Color.FromArgb(0xFC, 0xFC, 0xFD);
            _logBox.BorderStyle = BorderStyle.FixedSingle;
            _logBox.Font = UiKit.Ui(9F);
            _logBox.WordWrap = false;
            _logBox.ScrollBars = RichTextBoxScrollBars.Both;
            _logBox.DetectUrls = false;

            logPanel.Controls.Add(lblLog);
            logPanel.Controls.Add(btnClear);
            logPanel.Controls.Add(_logBox);

            // ---------- 底部：操作按钮 ----------
            Panel actions = new Panel();
            actions.Dock = DockStyle.Bottom;
            actions.Size = new Size(1180, 54);
            actions.Height = 54;
            actions.BackColor = UiKit.Card;
            actions.Paint += delegate(object s, PaintEventArgs e) { DrawTopLine(e.Graphics, actions.ClientSize.Width); };

            Button btnRefresh = UiKit.MakeButton("刷新列表", 92, 32);
            btnRefresh.Location = new Point(14, 11);
            btnRefresh.Click += delegate { RefreshMods(true); };
            Button btnConflict = UiKit.MakeButton("冲突检测", 96, 32);
            btnConflict.Location = new Point(112, 11);
            btnConflict.Click += delegate { RunConflictCheck(); };
            Button btnDeploy = UiKit.MakeButton("部署到游戏目录", 146, 32, true);
            btnDeploy.Location = new Point(216, 11);
            btnDeploy.Click += delegate { DoDeploy(); };
            Button btnUndo = UiKit.MakeButton("还原上次部署", 124, 32);
            btnUndo.Location = new Point(370, 11);
            btnUndo.Click += delegate { DoUndo(); };
            Button btnExport = UiKit.MakeButton("导出清单", 96, 32);
            btnExport.Location = new Point(502, 11);
            btnExport.Click += delegate { ExportManifest(); };
            Button btnImport = UiKit.MakeButton("导入清单", 96, 32);
            btnImport.Location = new Point(604, 11);
            btnImport.Click += delegate { ImportManifest(); };
            Button btnBackup = UiKit.MakeButton("备份与恢复", 116, 32);
            btnBackup.Location = new Point(706, 11);
            btnBackup.Click += delegate { OpenBackupDialog(); };
            Button btnHelp = UiKit.MakeButton("使用说明", 96, 32);
            btnHelp.Location = new Point(828, 11);
            btnHelp.Click += delegate { ShowHelp(); };

            Label lblMode = UiKit.MakeLabel("部署方式");
            lblMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblMode.Location = new Point(930, 20);
            lblMode.ForeColor = UiKit.SubText;

            _cboDeployMode = new ComboBox();
            _cboDeployMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDeployMode.Font = UiKit.Ui(9F);
            _cboDeployMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cboDeployMode.Location = new Point(990, 14);
            _cboDeployMode.Size = new Size(178, 24);
            _cboDeployMode.Items.Add("游戏根目录（通用）");
            _cboDeployMode.Items.Add("Mods 子目录（星露谷）");
            _cboDeployMode.SelectedIndex = 0;
            _cboDeployMode.SelectedIndexChanged += delegate { OnDeployModeChanged(); };

            actions.Controls.Add(btnRefresh);
            actions.Controls.Add(btnConflict);
            actions.Controls.Add(btnDeploy);
            actions.Controls.Add(btnUndo);
            actions.Controls.Add(btnExport);
            actions.Controls.Add(btnImport);
            actions.Controls.Add(btnBackup);
            actions.Controls.Add(btnHelp);
            actions.Controls.Add(lblMode);
            actions.Controls.Add(_cboDeployMode);

            // ---------- 状态栏 ----------
            StatusStrip status = new StatusStrip();
            status.SizingGrip = false;
            status.BackColor = UiKit.Card;
            _statLeft = new ToolStripStatusLabel("");
            _statLeft.ForeColor = UiKit.Text;
            _statDeploy = new ToolStripStatusLabel("");
            _statDeploy.ForeColor = UiKit.SubText;
            _statDeploy.Spring = true;
            _statDeploy.TextAlign = ContentAlignment.MiddleRight;
            _statRight = new ToolStripStatusLabel("");
            _statRight.ForeColor = UiKit.SubText;
            status.Items.Add(_statLeft);
            status.Items.Add(_statDeploy);
            status.Items.Add(_statRight);

            this.Controls.Add(split);
            this.Controls.Add(logPanel);
            this.Controls.Add(actions);
            this.Controls.Add(status);
            this.Controls.Add(top);

            this.AllowDrop = true;
            this.DragEnter += Form_DragEnter;
            this.DragDrop += Form_DragDrop;
            this.FormClosing += Form_Closing;
            this.KeyDown += Form_KeyDown;
            this.Load += delegate { SetSplitDistance(split, 340); };
            this.Shown += delegate
            {
                // 窗口句柄创建后，让列表第一行默认选中（句柄不存在时的选中状态会被原生控件覆盖）
                if (_lv != null && _lv.Items.Count > 0 && _lv.SelectedItems.Count == 0)
                {
                    _lv.Items[0].Selected = true;
                    _lv.Items[0].Focused = true;
                    ShowDetails(SelectedMod());
                    UpdateStatusBar();
                }
            };
        }

        /// <summary>让右侧面板保持固定宽度，避免窗口缩放时布局被压扁。</summary>
        private static void SetSplitDistance(SplitContainer split, int rightWidth)
        {
            try
            {
                int d = split.Width - rightWidth - split.SplitterWidth;
                int max = split.Width - split.Panel2MinSize - split.SplitterWidth;
                if (d < split.Panel1MinSize) d = split.Panel1MinSize;
                if (d > max) d = max;
                if (d > 0) split.SplitterDistance = d;
            }
            catch (Exception) { }
        }

        private void BuildInfoTab(TabPage tab)
        {
            Panel bottom2 = new Panel();
            bottom2.Dock = DockStyle.Bottom;
            bottom2.Height = 40;

            _btnSave = UiKit.MakeButton("保存修改的信息", 152, 30, true);
            _btnSave.Location = new Point(0, 5);
            _btnSave.Click += delegate { SaveSelectedMeta(); };
            bottom2.Controls.Add(_btnSave);

            Panel bottom1 = new Panel();
            bottom1.Dock = DockStyle.Bottom;
            bottom1.Height = 40;

            _btnOpenMod = UiKit.MakeButton("打开 MOD 文件夹", 140, 30);
            _btnOpenMod.Location = new Point(0, 5);
            _btnOpenMod.Click += delegate { OpenSelectedModFolder(); };
            _btnFlatten = UiKit.MakeButton("展开一层目录", 130, 30);
            _btnFlatten.Location = new Point(148, 5);
            _btnFlatten.Click += delegate { FlattenSelected(); };
            bottom1.Controls.Add(_btnOpenMod);
            bottom1.Controls.Add(_btnFlatten);

            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.Height = 236;

            _lblName = new Label();
            _lblName.Font = UiKit.Ui(12F, FontStyle.Bold);
            _lblName.ForeColor = UiKit.Text;
            _lblName.AutoSize = false;
            _lblName.Location = new Point(0, 2);
            _lblName.Size = new Size(266, 42);
            _lblName.Text = "未选择 MOD";

            head.Controls.Add(_lblName);
            head.Controls.Add(MakeRow(head, "状态", 50, out _lblStatusV));
            head.Controls.Add(MakeRow(head, "文件数量", 74, out _lblFilesV));
            head.Controls.Add(MakeRow(head, "占用空间", 98, out _lblSizeV));
            head.Controls.Add(MakeRow(head, "安装时间", 122, out _lblTimeV));

            Label lblSource = UiKit.MakeFieldLabel("来源文件");
            lblSource.Location = new Point(0, 146);
            lblSource.Size = new Size(66, 24);
            _lblSourceV = UiKit.MakeLabel("");
            _lblSourceV.Location = new Point(70, 146);
            _lblSourceV.Size = new Size(196, 24);
            _lblSourceV.AutoSize = false;
            _lblSourceV.ForeColor = UiKit.Text;
            head.Controls.Add(lblSource);
            head.Controls.Add(_lblSourceV);

            Label lblVer = UiKit.MakeFieldLabel("版本");
            lblVer.Location = new Point(0, 172);
            lblVer.Size = new Size(66, 26);
            _txtVersion = UiKit.MakeTextBox(false);
            _txtVersion.Location = new Point(70, 172);
            _txtVersion.Size = new Size(196, 26);
            head.Controls.Add(lblVer);
            head.Controls.Add(_txtVersion);

            Label lblAuthor = UiKit.MakeFieldLabel("作者");
            lblAuthor.Location = new Point(0, 204);
            lblAuthor.Size = new Size(66, 26);
            _txtAuthor = UiKit.MakeTextBox(false);
            _txtAuthor.Location = new Point(70, 204);
            _txtAuthor.Size = new Size(196, 26);
            head.Controls.Add(lblAuthor);
            head.Controls.Add(_txtAuthor);

            Label lblNote = UiKit.MakeFieldLabel("备注");
            lblNote.Dock = DockStyle.Top;
            lblNote.Height = 26;
            lblNote.AutoSize = false;

            _txtNote = new TextBox();
            _txtNote.Dock = DockStyle.Fill;
            _txtNote.Multiline = true;
            _txtNote.ScrollBars = ScrollBars.Vertical;
            _txtNote.BorderStyle = BorderStyle.FixedSingle;
            _txtNote.Font = UiKit.Ui(9F);

            tab.Controls.Add(_txtNote);
            tab.Controls.Add(bottom2);
            tab.Controls.Add(bottom1);
            tab.Controls.Add(lblNote);
            tab.Controls.Add(head);
        }

        private Label MakeRow(Control parent, string caption, int y, out Label value)
        {
            Label lbl = UiKit.MakeFieldLabel(caption);
            lbl.Location = new Point(0, y);
            lbl.Size = new Size(66, 24);
            value = UiKit.MakeLabel("");
            value.Location = new Point(70, y);
            value.Size = new Size(196, 24);
            value.AutoSize = false;
            parent.Controls.Add(lbl);
            parent.Controls.Add(value);
            return lbl;
        }

        /// <summary>没有打开工作区时的引导面板。</summary>
        private Panel CreateEmptyState()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = UiKit.Card;
            p.Visible = false;

            Label title = new Label();
            title.Text = "还没有打开 MOD 工作区";
            title.Font = UiKit.Ui(15F, FontStyle.Bold);
            title.ForeColor = UiKit.Text;
            title.AutoSize = false;
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.Size = new Size(520, 42);
            title.Location = new Point(155, 88);

            Label sub = new Label();
            sub.Text = "工作区用来存放你的 MOD，程序会在其中自动建立：\r\n" +
                       "mods（已启用） · disabled（已禁用） · backup（备份）\r\n\r\n" +
                       "打开工作区后，可以直接把 zip / 7z / rar 压缩包或已解压的文件夹拖进窗口安装。";
            sub.Font = UiKit.Ui(9.5F);
            sub.ForeColor = UiKit.SubText;
            sub.AutoSize = false;
            sub.TextAlign = ContentAlignment.TopCenter;
            sub.Size = new Size(600, 96);
            sub.Location = new Point(115, 138);

            Button bNew = UiKit.MakeButton("新建工作区", 150, 40, true);
            bNew.Font = UiKit.Ui(10F);
            bNew.Location = new Point(252, 250);
            bNew.Click += delegate { NewWorkspace(); };

            Button bOpen = UiKit.MakeButton("打开已有工作区", 150, 40);
            bOpen.Font = UiKit.Ui(10F);
            bOpen.Location = new Point(414, 250);
            bOpen.Click += delegate { PickWorkspace(); };

            p.Controls.Add(title);
            p.Controls.Add(sub);
            p.Controls.Add(bNew);
            p.Controls.Add(bOpen);
            p.AllowDrop = true;
            p.DragEnter += Form_DragEnter;
            p.DragDrop += Form_DragDrop;
            _emptyPanel = p;
            return p;
        }

        private void OpenBackupDialog()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            using (BackupDialog d = new BackupDialog(_ws))
            {
                if (d.ShowDialog(this) == DialogResult.OK) RefreshMods(true);
            }
        }

        private string DeployModeName()
        {
            return _cboDeployMode.SelectedIndex == 1
                ? "Mods 子目录（星露谷 / SMAPI）"
                : "游戏根目录（通用）";
        }

        /// <summary>切换部署方式：通用模式合并文件到游戏根目录；Mods 模式整个文件夹复制到 Mods 子目录。</summary>
        private void OnDeployModeChanged()
        {
            if (_suspendEvents || _ws == null) return;
            string mode = _cboDeployMode.SelectedIndex == 1 ? "mods" : "root";
            if (string.Equals(_ws.Data.DeployMode, mode, StringComparison.OrdinalIgnoreCase)) return;
            _ws.Data.DeployMode = mode;
            _ws.Save();
            _conflicts = new List<ConflictGroup>();
            FillList(SelectedName());
            if (mode == "mods")
            {
                _log.Ok("部署方式：Mods 模式 —— 每个已启用的 MOD 会作为一个完整文件夹复制到 <游戏目录>\\Mods，交给 SMAPI 加载。");
                if (_ws.Data.GameDirectory != null && _ws.Data.GameDirectory.Length > 0)
                    _log.Info("实际写入位置：" + _ws.ResolveModsTarget(_ws.Data.GameDirectory));
            }
            else
            {
                _log.Ok("部署方式：通用模式 —— 已启用 MOD 的文件会按加载顺序合并复制到游戏根目录。");
            }
        }

        private void SyncDeployModeCombo()
        {
            if (_cboDeployMode == null) return;
            _suspendEvents = true;
            try
            {
                bool mods = _ws != null && _ws.IsModsMode;
                _cboDeployMode.SelectedIndex = mods ? 1 : 0;
            }
            finally { _suspendEvents = false; }
        }

        /// <summary>把游戏 Mods 目录里已有的 MOD 复制进工作区纳管。</summary>
        private void ImportGameMods()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            string game = _ws.Data.GameDirectory;
            if (string.IsNullOrEmpty(game) || !Directory.Exists(game))
            {
                ShowInfo("请先选择「游戏目录」，再导入游戏 Mods 目录里的 MOD。", "需要游戏目录");
                if (!Program.SelfTestMode) PickGameDir();
                return;
            }
            List<string> names = _ws.ScanGameMods(game);
            if (names.Count == 0)
            {
                ShowInfo("在 " + _ws.ResolveModsTarget(game) + " 里没有找到 MOD 文件夹。", "提示");
                return;
            }
            if (!Confirm("将把游戏 Mods 目录里的 " + names.Count + " 个 MOD 文件夹复制到工作区纳管" +
                "（只复制，不动游戏里的原有文件）：\r\n\r\n" + string.Join("、", names.ToArray()) +
                "\r\n\r\n是否继续？", "导入已有 MOD")) return;
            int skipped;
            int imported = _ws.ImportFromGameMods(game, out skipped);
            _log.Ok("从游戏 Mods 目录导入 " + imported + " 个 MOD，跳过 " + skipped + " 个。");
            RefreshMods(true);
            ShowInfo("导入完成：新增 " + imported + " 个 MOD，跳过 " + skipped + " 个（已存在或读取失败）。", "导入完成");
        }

        private static void DrawBottomLine(Graphics g, int width, int height)
        {
            using (Pen p = new Pen(UiKit.Border))
            {
                g.DrawLine(p, 0, height - 1, width, height - 1);
            }
        }

        private static void DrawTopLine(Graphics g, int width)
        {
            using (Pen p = new Pen(UiKit.Border))
            {
                g.DrawLine(p, 0, 0, width, 0);
            }
        }

        // =====================================================================
        // 工作区
        // =====================================================================

        private void OpenLastWorkspace()
        {
            if (!string.IsNullOrEmpty(_settings.LastWorkspace) && Directory.Exists(_settings.LastWorkspace))
            {
                try
                {
                    OpenWorkspace(_settings.LastWorkspace);
                    return;
                }
                catch (Exception ex) { _log.Error("打开上次的工作区失败：" + ex.Message); }
            }
            // 便携模式：如果程序目录旁边就有「MOD工作区」，直接打开它
            try
            {
                string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
                string beside = Path.Combine(exeDir, "MOD工作区");
                if (Directory.Exists(beside) && Directory.Exists(Path.Combine(beside, "mods")))
                {
                    _log.Info("在程序目录下发现「MOD工作区」，正在自动打开。");
                    OpenWorkspace(beside);
                    return;
                }
            }
            catch (Exception ex) { _log.Warn("检查程序目录下的工作区失败：" + ex.Message); }
            UpdateUiState();
            _log.Info(EmptyHint);
        }

        private void OpenWorkspace(string path)
        {
            _ws = ModWorkspace.Open(path, _log);
            _settings.LastWorkspace = _ws.Root;
            SettingsStore.Save(_settings);
            _txtWorkspace.Text = _ws.Root;
            _txtGameDir.Text = _ws.Data.GameDirectory;
            _log.Ok("已打开工作区：" + _ws.Root);
            SyncDeployModeCombo();
            RefreshMods(false);
            UpdateUiState();
            if (_ws.IsModsMode)
            {
                _log.Info("部署方式：Mods 模式 —— 每个已启用的 MOD 会作为一个完整文件夹复制到 <游戏目录>\\" +
                    (_ws.Data.ModsFolderName == null ? "Mods" : _ws.Data.ModsFolderName) + "，交给 SMAPI 加载。");
                if (_mods.Count == 0 && !string.IsNullOrEmpty(_ws.Data.GameDirectory) && Directory.Exists(_ws.Data.GameDirectory))
                    _log.Info("工作区还是空的，可以右键列表选择「导入游戏 Mods 目录中的现有 MOD」，把游戏里已有的 MOD 纳管进来。");
            }
        }

        private void PickWorkspace()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择 MOD 工作区文件夹（程序会在其中建立 mods / disabled / backup 子目录）";
                if (Directory.Exists(_settings.LastWorkspace)) d.SelectedPath = _settings.LastWorkspace;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { OpenWorkspace(d.SelectedPath); }
                catch (Exception ex) { MessageBox.Show(this, "打开工作区失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void NewWorkspace()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择一个空文件夹，或选中某个文件夹后确认：程序会在其中创建 MOD 工作区结构";
                if (Directory.Exists(_settings.LastWorkspace)) d.SelectedPath = _settings.LastWorkspace;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    OpenWorkspace(d.SelectedPath);
                    _log.Info("工作区已就绪，包含 mods（已启用）、disabled（已禁用）、backup（备份）三个子目录。");
                }
                catch (Exception ex) { MessageBox.Show(this, "新建工作区失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void UpdateUiState()
        {
            bool hasWs = _ws != null;
            _btnInstall.Enabled = hasWs;
            _btnEnable.Enabled = hasWs;
            _btnDisable.Enabled = hasWs;
            _btnUp.Enabled = hasWs;
            _btnDown.Enabled = hasWs;
            _btnRename.Enabled = hasWs;
            _btnDelete.Enabled = hasWs;
            _btnPickGame.Enabled = hasWs;
            _btnLaunch.Enabled = hasWs;
            _btnDetect.Enabled = hasWs;
            _btnOpenWs.Enabled = hasWs;
            _txtGameDir.Enabled = hasWs;
            _txtWorkspace.Text = hasWs ? _ws.Root : "";
            if (_emptyPanel != null) _emptyPanel.Visible = !hasWs;
            if (_lv != null) _lv.Visible = hasWs;
            if (!hasWs)
            {
                _statLeft.Text = EmptyHint;
                _statRight.Text = "";
                _statDeploy.Text = "";
            }
        }

        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            // 关闭前先清空列表项，避免 WinForms 在销毁 ListView 时抛出空引用（已知渲染/销毁顺序问题）
            try { if (_lv != null) _lv.Items.Clear(); }
            catch (Exception) { }
            _settings.WindowWidth = this.WindowState == FormWindowState.Normal ? this.Width : _settings.WindowWidth;
            _settings.WindowHeight = this.WindowState == FormWindowState.Normal ? this.Height : _settings.WindowHeight;
            _settings.WindowMaximized = this.WindowState == FormWindowState.Maximized;
            SettingsStore.Save(_settings);
        }

        // =====================================================================
        // 列表刷新与选中
        // =====================================================================

        private void RefreshMods(bool keepSelection)
        {
            if (_ws == null) { UpdateUiState(); return; }
            string name = keepSelection ? SelectedName() : null;
            Cursor old = this.Cursor;
            this.Cursor = Cursors.WaitCursor;
            try
            {
                _mods = _ws.Scan();
                ClearConflictMarks();
            }
            catch (Exception ex)
            {
                _log.Error("扫描 MOD 失败：" + ex.Message);
            }
            finally { this.Cursor = old; }
            FillList(name);
        }

        /// <summary>MOD 状态变化后，之前的冲突检测结果就失效了。</summary>
        private void ClearConflictMarks()
        {
            _conflicts = new List<ConflictGroup>();
            foreach (ModInfo m in _mods)
            {
                m.ConflictPaths = new List<string>();
                m.ConflictMods = new List<string>();
            }
        }

        /// <summary>只根据磁盘状态更新启用/禁用，不重新递归统计大小。</summary>
        private void SoftRefresh()
        {
            if (_ws == null) return;
            foreach (ModInfo m in _mods)
            {
                bool inMods = Directory.Exists(Path.Combine(_ws.ModsPath, m.Name));
                m.Enabled = inMods;
                m.FolderPath = Path.Combine(inMods ? _ws.ModsPath : _ws.DisabledPath, m.Name);
            }
            FillList(SelectedName());
        }

        private void FillList(string selectName)
        {
            if (_lv == null) return;
            RenumberDeployIndex();
            string filter = _txtSearch == null ? "" : _txtSearch.Text.Trim();
            _suspendEvents = true;
            _lv.BeginUpdate();
            try
            {
                _lv.Items.Clear();
                foreach (ModInfo m in _mods)
                {
                    if (filter.Length > 0 && m.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    ListViewItem it = new ListViewItem(new string[]
                    {
                        m.Enabled ? m.DeployIndex.ToString() : "—",
                        m.Name,
                        m.StatusText,
                        m.FileCount.ToString(),
                        FileUtil.FormatSize(m.SizeBytes),
                        m.ConflictPaths.Count > 0 ? m.ConflictPaths.Count.ToString() : "",
                        MetaSummary(m)
                    });
                    it.Tag = m;
                    it.ImageIndex = m.Enabled ? 0 : 1;
                    it.UseItemStyleForSubItems = false;
                    it.ForeColor = m.Enabled ? UiKit.Text : UiKit.SubText;
                    for (int i = 1; i < it.SubItems.Count; i++)
                        it.SubItems[i].ForeColor = m.Enabled ? UiKit.Text : UiKit.SubText;
                    if (m.ConflictPaths.Count > 0)
                    {
                        it.SubItems[5].ForeColor = UiKit.Warn;
                        it.SubItems[5].Font = UiKit.Ui(9F, FontStyle.Bold);
                    }
                    _lv.Items.Add(it);
                    // 复选框状态必须在加入列表之后再设置：
                    // 否则 ListView 会先按“未勾选”插入，随后异步发出一次状态变化通知，
                    // 被误当成用户操作而把 MOD 禁用掉。
                    it.Checked = m.Enabled;
                }
            }
            finally
            {
                _lv.EndUpdate();
                _suspendEvents = false;
            }
            if (!string.IsNullOrEmpty(selectName)) SelectByName(selectName);
            else if (_lv.Items.Count > 0 && _lv.SelectedItems.Count == 0) _lv.Items[0].Selected = true;
            ShowDetails(SelectedMod());
            UpdateStatusBar();
        }

        private string MetaSummary(ModInfo m)
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(m.Meta.Version)) sb.Append("v").Append(m.Meta.Version);
            if (!string.IsNullOrEmpty(m.Meta.Author))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(m.Meta.Author);
            }
            if (!string.IsNullOrEmpty(m.Meta.Note))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(m.Meta.Note);
            }
            return UiKit.Ellipsis(sb.ToString(), 40);
        }

        private void RenumberDeployIndex()
        {
            int idx = 0;
            foreach (ModInfo m in _mods)
            {
                if (m.Enabled) m.DeployIndex = ++idx; else m.DeployIndex = 0;
            }
        }

        private void SelectByName(string name)
        {
            foreach (ListViewItem it in _lv.Items)
            {
                ModInfo m = it.Tag as ModInfo;
                if (m != null && string.Equals(m.Name, name, StringComparison.Ordinal))
                {
                    it.Selected = true;
                    it.Focused = true;
                    it.EnsureVisible();
                    return;
                }
            }
        }

        private string SelectedName()
        {
            ModInfo m = SelectedMod();
            return m == null ? null : m.Name;
        }

        private ModInfo SelectedMod()
        {
            if (_lv.SelectedItems.Count == 0) return null;
            return _lv.SelectedItems[0].Tag as ModInfo;
        }

        private List<ModInfo> SelectedMods()
        {
            List<ModInfo> result = new List<ModInfo>();
            foreach (ListViewItem it in _lv.SelectedItems)
            {
                ModInfo m = it.Tag as ModInfo;
                if (m != null) result.Add(m);
            }
            return result;
        }

        private List<ModInfo> EnabledInOrder()
        {
            List<ModInfo> result = new List<ModInfo>();
            foreach (ModInfo m in _mods) if (m.Enabled) result.Add(m);
            return result;
        }

        private void UpdateStatusBar()
        {
            if (_ws == null) { UpdateUiState(); return; }
            int on = 0, off = 0;
            foreach (ModInfo m in _mods) { if (m.Enabled) on++; else off++; }
            _statLeft.Text = string.Format("共 {0} 个 MOD｜已启用 {1}｜已禁用 {2}｜列表显示 {3}｜已选 {4}",
                _mods.Count, on, off, _lv.Items.Count, _lv.SelectedItems.Count);
            _statRight.Text = "工作区：" + UiKit.EllipsisLeft(_ws.Root, 52);
            if (_ws.HasDeployRecord)
            {
                DeployRecord r = _ws.LastDeploy;
                _statDeploy.Text = "上次部署：" + r.Time + "（" + r.Entries.Count.ToString() + " 个文件，尚未还原）";
                _statDeploy.ForeColor = UiKit.Warn;
            }
            else
            {
                _statDeploy.Text = "当前没有未还原的部署记录";
                _statDeploy.ForeColor = UiKit.SubText;
            }
        }

        // =====================================================================
        // 详情面板
        // =====================================================================

        private void ShowDetails(ModInfo m)
        {
            bool has = m != null;
            if (!has)
            {
                _lblName.Text = "未选择 MOD";
                _lblStatusV.Text = "—";
                _lblFilesV.Text = "—";
                _lblSizeV.Text = "—";
                _lblTimeV.Text = "—";
                _lblSourceV.Text = "—";
                _txtVersion.Text = "";
                _txtAuthor.Text = "";
                _txtNote.Text = "";
                _lstConflicts.Items.Clear();
                _lblConflictSummary.Text = "请先在左侧选择一个 MOD。";
                SetDetailsEnabled(false);
                return;
            }
            SetDetailsEnabled(true);
            _lblName.Text = m.Name;
            _lblStatusV.Text = m.Enabled ? "已启用（部署序号 " + m.DeployIndex + "）" : "已禁用";
            _lblStatusV.ForeColor = m.Enabled ? UiKit.Ok : UiKit.SubText;
            _lblFilesV.Text = m.FileCount.ToString() + " 个文件";
            _lblSizeV.Text = FileUtil.FormatSize(m.SizeBytes);
            _lblTimeV.Text = string.IsNullOrEmpty(m.Meta.InstalledAt) ? "—" : m.Meta.InstalledAt;
            _lblSourceV.Text = UiKit.Ellipsis(string.IsNullOrEmpty(m.Meta.Source) ? "—" : Path.GetFileName(m.Meta.Source), 26);
            _txtVersion.Text = m.Meta.Version;
            _txtAuthor.Text = m.Meta.Author;
            _txtNote.Text = m.Meta.Note;
            FillConflictTab(m);
        }

        private void SetDetailsEnabled(bool on)
        {
            _btnSave.Enabled = on;
            _btnOpenMod.Enabled = on;
            _btnFlatten.Enabled = on;
            _txtVersion.Enabled = on;
            _txtAuthor.Enabled = on;
            _txtNote.Enabled = on;
        }

        private void FillConflictTab(ModInfo m)
        {
            _lstConflicts.Items.Clear();
            if (_conflicts.Count == 0)
            {
                _lblConflictSummary.Text = "尚未检测。点「冲突检测」查看已启用 MOD 之间的重复文件。";
                return;
            }
            if (m.ConflictPaths.Count == 0)
            {
                _lblConflictSummary.Text = "该 MOD 与其他已启用 MOD 没有重复文件。";
                return;
            }
            _lblConflictSummary.Text = "该 MOD 有 " + m.ConflictPaths.Count + " 个文件与其他 MOD 重复：" +
                string.Join("、", m.ConflictMods.ToArray());
            foreach (ConflictGroup g in _conflicts)
            {
                if (!m.ConflictPaths.Contains(g.RelativePath)) continue;
                _lstConflicts.Items.Add(g.RelativePath + "  ←  " + string.Join(" / ", g.Mods.ToArray()) + "（生效：" + g.Winner + "）");
            }
        }

        private void SaveSelectedMeta()
        {
            ModInfo m = SelectedMod();
            if (m == null || _ws == null) return;
            ModMeta meta = _ws.GetMeta(m.Name);
            meta.Version = _txtVersion.Text.Trim();
            meta.Author = _txtAuthor.Text.Trim();
            meta.Note = _txtNote.Text.Trim();
            _ws.Save();
            m.Meta = meta;
            _log.Ok("已保存「" + m.Name + "」的信息。");
            FillList(m.Name);
        }

        // =====================================================================
        // 列表事件
        // =====================================================================

        private void Lv_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            try
            {
                if (_suspendEvents || _busy) return;
                if (e == null || e.Item == null) return;
                ModInfo m = e.Item.Tag as ModInfo;
                if (m == null) return;
                if (_ws == null) { m.Enabled = e.Item.Checked; return; }
                if (m.Enabled == e.Item.Checked) return;
                // 注意：这里正处于 ListView 的原生通知处理流程中，
                // 如果此时清空/重建列表会破坏控件内部状态（出现幽灵行、迭代异常、退出崩溃）。
                // 所以延后到消息处理结束后再真正执行。
                string name = m.Name;
                bool want = e.Item.Checked;
                this.BeginInvoke((MethodInvoker)delegate { ApplyCheckChange(name, want); });
            }
            catch (Exception ex)
            {
                _log.Error("勾选 MOD 时出错：" + ex.Message);
            }
        }

        /// <summary>处理用户勾选：只对“确实发生变化的、仍然有效的”勾选动作生效。</summary>
        private void ApplyCheckChange(string name, bool want)
        {
            if (_ws == null || _suspendEvents || _busy) return;
            ListViewItem it = FindItem(name);
            if (it == null) return;
            if (it.Checked != want) return;        // 过期的通知，忽略
            ModInfo m = it.Tag as ModInfo;
            if (m == null) return;
            bool actual = Directory.Exists(Path.Combine(_ws.ModsPath, m.Name));
            if (actual == want) return;            // 磁盘状态已经一致
            SetEnabledOnDisk(new string[] { m.Name }, want);
        }

        private ListViewItem FindItem(string name)
        {
            if (_lv == null) return null;
            foreach (ListViewItem it in _lv.Items)
            {
                ModInfo m = it.Tag as ModInfo;
                if (m != null && string.Equals(m.Name, name, StringComparison.Ordinal)) return it;
            }
            return null;
        }

        private void Lv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && !e.Control)
            {
                DeleteSelected();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space)
            {
                List<ModInfo> sel = SelectedMods();
                if (sel.Count > 0)
                {
                    bool target = !sel[0].Enabled;
                    string[] names = new string[sel.Count];
                    for (int i = 0; i < sel.Count; i++) names[i] = sel[i].Name;
                    SetEnabledOnDisk(names, target);
                }
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem it in _lv.Items) it.Selected = true;
                e.Handled = true;
            }
        }

        private void Lv_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            ListViewItem it = _lv.GetItemAt(e.X, e.Y);
            if (it == null) return;
            if (!it.Selected)
            {
                _lv.SelectedItems.Clear();
                it.Selected = true;
            }
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = UiKit.Ui(9F);
            menu.Items.Add("启用", null, delegate { ToggleSelected(true); });
            menu.Items.Add("禁用", null, delegate { ToggleSelected(false); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("上移（提高优先级）", null, delegate { MoveSelected(-1); });
            menu.Items.Add("下移（降低优先级）", null, delegate { MoveSelected(1); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("打开 MOD 文件夹", null, delegate { OpenSelectedModFolder(); });
            menu.Items.Add("展开一层目录", null, delegate { FlattenSelected(); });
            menu.Items.Add("重命名", null, delegate { RenameSelected(); });
            if (_ws != null && _ws.IsModsMode)
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("导入游戏 Mods 目录中的现有 MOD", null, delegate { ImportGameMods(); });
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除", null, delegate { DeleteSelected(); });
            menu.Show(_lv, e.Location);
        }

        private void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5) { RefreshMods(true); e.Handled = true; }
        }

        // =====================================================================
        // 操作
        // =====================================================================

        private void InstallFromDialog()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "选择要安装的 MOD（可多选）";
                d.Filter = "MOD 压缩包 (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|所有文件 (*.*)|*.*";
                d.Multiselect = true;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                InstallSources(d.FileNames);
            }
        }

        private void InstallSources(string[] paths)
        {
            if (_ws == null) { NeedWorkspace(); return; }
            List<string> ok = new List<string>();
            List<string> fail = new List<string>();
            _busy = true;
            Cursor old = this.Cursor;
            this.Cursor = Cursors.WaitCursor;
            try
            {
                foreach (string p in paths)
                {
                    try
                    {
                        string name = _ws.Install(p);
                        ok.Add(name);
                        _log.Ok("已安装 MOD：" + name + "（来源：" + Path.GetFileName(p.TrimEnd('\\')) + "）");
                    }
                    catch (Exception ex)
                    {
                        fail.Add(Path.GetFileName(p.TrimEnd('\\')) + "：" + ex.Message);
                        _log.Error("安装失败 " + Path.GetFileName(p.TrimEnd('\\')) + "：" + ex.Message);
                    }
                }
            }
            finally
            {
                this.Cursor = old;
                _busy = false;
            }
            RefreshMods(true);
            if (ok.Count > 0) SelectByName(ok[0]);
            StringBuilder sb = new StringBuilder();
            if (ok.Count > 0) sb.Append("成功安装 ").Append(ok.Count).Append(" 个 MOD：\r\n").Append(string.Join("、", ok.ToArray()));
            if (fail.Count > 0)
            {
                if (sb.Length > 0) sb.Append("\r\n\r\n");
                sb.Append("失败 ").Append(fail.Count).Append(" 个：\r\n").Append(string.Join("\r\n", fail.ToArray()));
            }
            ShowInfo(sb.ToString(), "安装结果");
        }

        private void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (_ws != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            else e.Effect = DragDropEffects.None;
        }

        private void Form_DragDrop(object sender, DragEventArgs e)
        {
            if (_ws == null) { NeedWorkspace(); return; }
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            List<string> usable = new List<string>();
            List<string> skipped = new List<string>();
            foreach (string f in files)
            {
                if (_ws.IsSupportedSource(f)) usable.Add(f);
                else skipped.Add(Path.GetFileName(f.TrimEnd('\\')));
            }
            if (skipped.Count > 0)
                _log.Warn("已跳过不支持的项目：" + string.Join("、", skipped.ToArray()) + "（支持 zip / 7z / rar 或文件夹）");
            if (usable.Count > 0) InstallSources(usable.ToArray());
        }

        private void SetEnabledOnDisk(string[] names, bool enabled)
        {
            if (_ws == null) { NeedWorkspace(); return; }
            _busy = true;
            try
            {
                int ok = _ws.SetEnabledBatch(names, enabled);
                if (ok > 0)
                    _log.Info((enabled ? "已启用：" : "已禁用：") + string.Join("、", names));
                ClearConflictMarks();
            }
            catch (Exception ex)
            {
                _log.Error("切换启用状态失败：" + ex.Message);
                MessageBox.Show(this, "切换启用状态失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _busy = false; }
            SoftRefresh();
        }

        private void ToggleSelected(bool enabled)
        {
            if (_ws == null) { NeedWorkspace(); return; }
            List<ModInfo> sel = SelectedMods();
            if (sel.Count == 0) { _log.Warn("请先选择要操作的 MOD。"); return; }
            string[] names = new string[sel.Count];
            for (int i = 0; i < sel.Count; i++) names[i] = sel[i].Name;
            SetEnabledOnDisk(names, enabled);
        }

        private void MoveSelected(int delta)
        {
            if (_ws == null) { NeedWorkspace(); return; }
            ModInfo m = SelectedMod();
            if (m == null) { _log.Warn("请先选择要移动的 MOD。"); return; }
            _ws.MoveOrder(m.Name, delta);
            _mods = _ws.Scan();
            FillList(m.Name);
            _log.Info("已调整加载顺序：「" + m.Name + "」" + (delta < 0 ? "上移" : "下移") + "一位。");
        }

        private void RenameSelected()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            ModInfo m = SelectedMod();
            if (m == null) { _log.Warn("请先选择要重命名的 MOD。"); return; }
            string name = InputDialog.Ask(this, "重命名 MOD", "输入新的 MOD 名称（会同时修改文件夹名）：", m.Name);
            if (name == null || name.Length == 0 || name == m.Name) return;
            try
            {
                _ws.Rename(m.Name, name);
                _log.Ok("已重命名：「" + m.Name + "」→「" + FileUtil.SanitizeName(name) + "」");
                RefreshMods(false);
                SelectByName(FileUtil.SanitizeName(name));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "重命名失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteSelected()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            List<ModInfo> sel = SelectedMods();
            if (sel.Count == 0) { _log.Warn("请先选择要删除的 MOD。"); return; }
            string text = sel.Count == 1
                ? "确定要删除「" + sel[0].Name + "」吗？\r\n\r\nMOD 文件夹会被移动到工作区的 backup 目录，不会立刻从磁盘清除，之后可以手动找回。"
                : "确定要删除选中的 " + sel.Count + " 个 MOD 吗？\r\n\r\n它们的文件夹会被移动到工作区的 backup 目录，之后可以手动找回。";
            if (!Confirm(text, "删除确认")) return;
            int ok = 0;
            foreach (ModInfo m in sel)
            {
                try
                {
                    string dest = _ws.Delete(m.Name);
                    _log.Ok("已删除「" + m.Name + "」，备份位置：" + dest);
                    ok++;
                }
                catch (Exception ex) { _log.Error("删除失败 " + m.Name + "：" + ex.Message); }
            }
            RefreshMods(false);
            if (ok > 0)
                ShowInfo("已删除 " + ok + " 个 MOD，可在「备份与恢复」中找回。", "完成");
        }

        private void OpenSelectedModFolder()
        {
            ModInfo m = SelectedMod();
            if (m == null) return;
            OpenPath(m.FolderPath, "MOD 文件夹");
        }

        private void FlattenSelected()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            ModInfo m = SelectedMod();
            if (m == null) return;
            string before = UiKit.Ellipsis(FileUtil.FirstLevelSummary(m.FolderPath, 70), 80);
            try
            {
                if (_ws.FlattenOneLevel(m.Name))
                {
                    _log.Ok("已展开「" + m.Name + "」的多余外壳目录（原结构：" + before + "）。");
                    RefreshMods(true);
                }
                else
                {
                    MessageBox.Show(this, "「" + m.Name + "」当前不是“只有一个子文件夹”的结构，无需展开。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "展开失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =====================================================================
        // 冲突 / 部署
        // =====================================================================

        private void RunConflictCheck()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            if (_ws.IsModsMode)
            {
                RunModsModeCheck();
                return;
            }
            Cursor old = this.Cursor;
            this.Cursor = Cursors.WaitCursor;
            try
            {
                List<ModInfo> enabled = EnabledInOrder();
                _conflicts = _ws.FindConflicts(_mods, enabled);
            }
            catch (Exception ex)
            {
                _log.Error("冲突检测失败：" + ex.Message);
                this.Cursor = old;
                return;
            }
            finally { this.Cursor = old; }

            FillList(SelectedName());
            if (_conflicts.Count == 0)
            {
                _log.Ok("冲突检测完成：已启用 MOD 之间没有重复文件。");
            }
            else
            {
                _log.Warn("冲突检测完成：发现 " + _conflicts.Count + " 个重复文件（加载顺序靠后的 MOD 会覆盖靠前的）。");
                int shown = 0;
                foreach (ConflictGroup g in _conflicts)
                {
                    if (shown >= 15) { _log.Warn("…… 其余 " + (_conflicts.Count - shown) + " 条请在上方「文件冲突」标签页查看。"); break; }
                    _log.Info("  " + g.RelativePath + "  ←  " + string.Join(" / ", g.Mods.ToArray()) + "（生效：" + g.Winner + "）");
                    shown++;
                }
            }
            ShowDetails(SelectedMod());
        }

        /// <summary>Mods 模式的检查：不做文件级冲突检测，改为检查 SMAPI 兼容性与覆盖情况。</summary>
        private void RunModsModeCheck()
        {
            List<ModInfo> enabled = EnabledInOrder();
            List<string> noManifest = _ws.CheckModsMode(_mods);
            _log.Info("当前是 Mods 模式：每个 MOD 作为完整文件夹放进 Mods 目录，不做文件级冲突检测。");
            if (enabled.Count == 0)
            {
                _log.Warn("当前没有已启用的 MOD，先勾选要生效的 MOD 再检查。");
            }
            else if (noManifest.Count == 0)
            {
                _log.Ok("已启用的 " + enabled.Count + " 个 MOD 里都有 manifest.json，SMAPI 可以正常识别。");
            }
            else
            {
                _log.Warn("以下 " + noManifest.Count + " 个 MOD 里没有 manifest.json，SMAPI 可能无法加载：" +
                    string.Join("、", noManifest.ToArray()));
            }
            string game = _ws.Data.GameDirectory;
            if (!string.IsNullOrEmpty(game) && Directory.Exists(game))
            {
                List<string> gameMods = _ws.ScanGameMods(game);
                List<string> overwrite = new List<string>();
                foreach (ModInfo m in enabled) if (gameMods.Contains(m.Name)) overwrite.Add(m.Name);
                if (overwrite.Count > 0)
                    _log.Warn("部署时会覆盖这些已有文件夹（会先整体备份）：" + string.Join("、", overwrite.ToArray()));
                _log.Info("目标目录：" + _ws.ResolveModsTarget(game) + "（当前有 " + gameMods.Count + " 个 MOD 文件夹）");
            }
            ClearConflictMarks();
            FillList(SelectedName());
        }

        private void DoDeploy()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            string game = _ws.Data.GameDirectory;
            if (string.IsNullOrEmpty(game) || !Directory.Exists(game))
            {
                ShowInfo("请先选择「游戏目录」——部署会把已启用 MOD 的文件合并复制到该目录。", "需要游戏目录");
                if (!Program.SelfTestMode) PickGameDir();
                return;
            }
            List<ModInfo> enabled = EnabledInOrder();
            if (enabled.Count == 0)
            {
                ShowInfo("当前没有已启用的 MOD。\r\n请先在列表里勾选要生效的 MOD。", "无法部署");
                return;
            }
            int files = 0;
            foreach (ModInfo m in enabled) files += m.FileCount;
            StringBuilder msg = new StringBuilder();
            if (_ws.IsModsMode)
            {
                string target = _ws.ResolveModsTarget(game);
                int existing = 0;
                List<string> gameMods = _ws.ScanGameMods(game);
                foreach (ModInfo m in enabled) if (gameMods.Contains(m.Name)) existing++;
                msg.Append("将把 ").Append(enabled.Count).Append(" 个已启用 MOD 的完整文件夹复制到：\r\n\r\n");
                msg.Append(target).Append("\r\n\r\n");
                if (existing > 0)
                    msg.Append("其中 ").Append(existing).Append(" 个会覆盖 Mods 目录里已有的同名文件夹（原文件夹会先整体备份，可随时还原）。\r\n\r\n");
                msg.Append("这是星露谷 / SMAPI 的标准安装方式：每个 MOD 一个文件夹，游戏启动时由 SMAPI 加载。");
            }
            else
            {
                msg.Append("将按加载顺序把 ").Append(enabled.Count).Append(" 个已启用 MOD（共约 ").Append(files).Append(" 个文件）复制到：\r\n\r\n");
                msg.Append(game).Append("\r\n\r\n被覆盖的原文件会先备份到工作区的 backup 目录，可随时用「还原上次部署」恢复到部署前的状态。");
            }
            if (_ws.HasDeployRecord)
                msg.Append("\r\n\r\n注意：当前还有一次未还原的部署记录，本次会继续叠加在同一份备份上（还原时仍可回到最初的原始状态）。");
            msg.Append("\r\n\r\n是否继续？");
            if (!Confirm(msg.ToString(), "部署到游戏目录")) return;

            Cursor old = this.Cursor;
            this.Cursor = Cursors.WaitCursor;
            try
            {
                ModWorkspace.DeployResult res = _ws.Deploy(enabled, game);
                if (res.ModsMode)
                {
                    _log.Ok(string.Format("部署完成：{0} 个 MOD 文件夹 → {1}（覆盖 {2} 个，新增 {3} 个），备份目录 backup\\{4}",
                        res.ModCount, res.TargetDirectory, res.OverwrittenCount, res.CreatedCount, res.BackupFolder));
                    ShowInfo(string.Format(
                        "部署完成。\r\n\r\nMOD 数量：{0}\r\n目标目录：{1}\r\n覆盖原有文件夹：{2}（已备份）\r\n新增文件夹：{3}\r\n\r\n启动游戏时请用 StardewModdingAPI.exe（SMAPI）加载 MOD。\r\n如需撤销，点「还原上次部署」。",
                        res.ModCount, res.TargetDirectory, res.OverwrittenCount, res.CreatedCount),
                        "部署成功");
                }
                else
                {
                    _log.Ok(string.Format("部署完成：{0} 个 MOD，写入 {1} 个文件（覆盖 {2} 个，新增 {3} 个），共复制 {4} 次，备份目录 backup\\{5}",
                        res.ModCount, res.FileCount, res.OverwrittenCount, res.CreatedCount, res.Copies, res.BackupFolder));
                    ShowInfo(string.Format(
                        "部署完成。\r\n\r\nMOD 数量：{0}\r\n写入文件：{1}\r\n覆盖原文件：{2}（已备份）\r\n新增文件：{3}\r\n\r\n如需撤销，点「还原上次部署」。",
                        res.ModCount, res.FileCount, res.OverwrittenCount, res.CreatedCount),
                        "部署成功");
                }
            }
            catch (Exception ex)
            {
                _log.Error("部署失败：" + ex.Message);
                MessageBox.Show(this, "部署失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { this.Cursor = old; }
            RefreshMods(true);
        }

        private void DoUndo()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            if (!_ws.HasDeployRecord)
            {
                ShowInfo("当前没有可还原的部署记录。", "提示");
                return;
            }
            DeployRecord r = _ws.LastDeploy;
            int overwritten = 0, created = 0;
            foreach (DeployEntry e in r.Entries) { if (e.Kind == "overwritten") overwritten++; else created++; }
            string text = string.Format(
                "将还原 {0} 的部署：\r\n\r\n恢复被覆盖的文件：{1} 个\r\n删除本次新增的文件：{2} 个\r\n目标目录：{3}\r\n\r\n是否继续？",
                r.Time, overwritten, created, r.GameDirectory);
            if (!Confirm(text, "还原上次部署")) return;
            Cursor old = this.Cursor;
            this.Cursor = Cursors.WaitCursor;
            try
            {
                string msg = _ws.UndoDeploy();
                _log.Ok(msg);
                ShowInfo(msg, "还原完成");
            }
            catch (Exception ex)
            {
                _log.Error("还原失败：" + ex.Message);
                MessageBox.Show(this, "还原失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { this.Cursor = old; }
            RefreshMods(true);
        }

        // =====================================================================
        // 游戏目录 / 清单 / 其他
        // =====================================================================

        private void PickGameDir()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择游戏的安装目录（MOD 文件会被合并复制到这里）";
                if (Directory.Exists(_ws.Data.GameDirectory)) d.SelectedPath = _ws.Data.GameDirectory;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                _ws.Data.GameDirectory = d.SelectedPath;
                _ws.Save();
                _txtGameDir.Text = d.SelectedPath;
                _log.Ok("游戏目录已设置为：" + d.SelectedPath);
                if (string.IsNullOrEmpty(_ws.Data.GameExecutable))
                {
                    List<string> exes = FindGameExes(d.SelectedPath);
                    if (exes.Count == 1)
                    {
                        _ws.Data.GameExecutable = exes[0];
                        _ws.Save();
                        _log.Info("已自动识别游戏程序：" + Path.GetFileName(exes[0]) + "，可直接点「启动游戏」。");
                    }
                    else if (exes.Count > 1)
                    {
                        _log.Info("在游戏目录中发现多个 exe，可点「选择游戏程序」指定要启动的那个。");
                    }
                }
                if (_ws.IsModsMode && _mods.Count == 0)
                {
                    List<string> gameMods = _ws.ScanGameMods(d.SelectedPath);
                    if (gameMods.Count > 0)
                    {
                        _log.Info("检测到游戏 Mods 目录里已有 " + gameMods.Count + " 个 MOD，可以导入到工作区统一管理。");
                        if (!Program.SelfTestMode &&
                            MessageBox.Show(this, "检测到游戏 Mods 目录里已有 " + gameMods.Count + " 个 MOD（" +
                                UiKit.Ellipsis(string.Join("、", gameMods.ToArray()), 80) +
                                "）。\r\n\r\n要现在把它们复制到工作区纳管吗？（只复制，不会改动游戏里的文件）",
                                "导入已有 MOD", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            ImportGameMods();
                        }
                    }
                }
            }
        }

        private void PickGameExe()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            string game = _ws.Data.GameDirectory;
            List<string> exes = string.IsNullOrEmpty(game) ? new List<string>() : FindGameExes(game);
            string chosen = null;
            if (exes.Count > 0)
            {
                chosen = ListPickerDialog.Pick(this, "选择游戏程序",
                    "在游戏目录中找到以下可执行文件，请选择启动游戏要运行的那个：", exes, _ws.Data.GameExecutable);
            }
            if (chosen == null)
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Title = "选择游戏主程序（.exe）";
                    d.Filter = "可执行文件 (*.exe)|*.exe";
                    if (!string.IsNullOrEmpty(game) && Directory.Exists(game)) d.InitialDirectory = game;
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    chosen = d.FileName;
                }
            }
            _ws.Data.GameExecutable = chosen;
            _ws.Save();
            _log.Ok("启动程序已设置为：" + chosen);
        }

        private static List<string> FindGameExes(string gameDir)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir)) return result;
            string[] skip = new string[] { "unins", "setup", "vc_redist", "dxsetup", "launcher_setup", "crashreport" };
            try
            {
                foreach (string f in Directory.GetFiles(gameDir, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    string n = Path.GetFileName(f).ToLowerInvariant();
                    bool bad = false;
                    foreach (string s in skip) if (n.StartsWith(s, StringComparison.OrdinalIgnoreCase)) { bad = true; break; }
                    if (!bad) result.Add(f);
                }
                if (result.Count == 0)
                {
                    foreach (string d in Directory.GetDirectories(gameDir))
                    {
                        foreach (string f in Directory.GetFiles(d, "*.exe", SearchOption.TopDirectoryOnly))
                        {
                            string n = Path.GetFileName(f).ToLowerInvariant();
                            bool bad = false;
                            foreach (string s in skip) if (n.StartsWith(s, StringComparison.OrdinalIgnoreCase)) { bad = true; break; }
                            if (!bad) result.Add(f);
                            if (result.Count >= 20) break;
                        }
                        if (result.Count >= 20) break;
                    }
                }
            }
            catch (Exception) { }
            return result;
        }

        private void LaunchGame()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            string exe = _ws.Data.GameExecutable;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                PickGameExe();
                exe = _ws.Data.GameExecutable;
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.WorkingDirectory = Path.GetDirectoryName(exe);
                psi.UseShellExecute = true;
                Process.Start(psi);
                _log.Ok("已启动游戏：" + Path.GetFileName(exe));
            }
            catch (Exception ex)
            {
                _log.Error("启动游戏失败：" + ex.Message);
                MessageBox.Show(this, "启动游戏失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportManifest()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = "导出 MOD 清单";
                d.Filter = "MOD 清单 (*.json)|*.json";
                d.FileName = _ws.Name + "_MOD清单_" + DateTime.Now.ToString("yyyyMMdd") + ".json";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Manifest mf = _ws.BuildManifest(_mods);
                    Json.Save(d.FileName, mf);
                    _log.Ok("清单已导出：" + d.FileName + "（含 " + mf.Mods.Count + " 个 MOD）");
                    MessageBox.Show(this, "清单已导出：\r\n" + d.FileName, "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导出失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportManifest()
        {
            if (_ws == null) { NeedWorkspace(); return; }
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "导入 MOD 清单";
                d.Filter = "MOD 清单 (*.json)|*.json|所有文件 (*.*)|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Manifest mf = Json.Load<Manifest>(d.FileName);
                    if (mf == null) { MessageBox.Show(this, "无法读取该清单文件。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                    string msg = _ws.ImportManifest(mf, _mods);
                    _log.Ok("导入清单：" + d.FileName);
                    MessageBox.Show(this, msg, "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshMods(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导入失败：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowHelp()
        {
            string text =
                "MOD 管理器 使用说明\r\n" +
                "================================================\r\n\r\n" +
                "一、工作区\r\n" +
                "  工作区是存放 MOD 的文件夹，程序会在其中自动建立：\r\n" +
                "    mods\\      已启用的 MOD（每个 MOD 一个子文件夹）\r\n" +
                "    disabled\\  已禁用的 MOD\r\n" +
                "    backup\\    删除备份、部署备份、临时解压目录\r\n" +
                "    modmanager.json  配置（游戏目录、加载顺序、备注等）\r\n\r\n" +
                "二、安装 MOD\r\n" +
                "  1. 点「＋ 安装 MOD」选择压缩包（支持 zip；安装了 7-Zip 后支持 7z / rar）；\r\n" +
                "  2. 或直接把压缩包 / 已解压的文件夹拖到窗口里；\r\n" +
                "  3. 安装时会自动去掉 __MACOSX、Thumbs.db 等垃圾文件，\r\n" +
                "     如果压缩包只有一层外壳文件夹也会自动打开。\r\n\r\n" +
                "三、启用 / 禁用\r\n" +
                "  勾选列表前的复选框即可启用，取消勾选即禁用。\r\n" +
                "  启用 = 把文件夹从 disabled 移到 mods，禁用 = 反过来。\r\n" +
                "  也可以用右键菜单、或按空格键批量切换。\r\n\r\n" +
                "四、加载顺序\r\n" +
                "  「顺序」列显示部署时的优先级，数字越大越靠后加载，\r\n" +
                "  靠后的 MOD 会覆盖靠前的 MOD 中的同名文件。\r\n" +
                "  用「上移 / 下移」按钮或右键菜单调整。\r\n\r\n" +
                "五、冲突检测\r\n" +
                "  点「冲突检测」会扫描所有已启用 MOD，列出重复的文件路径，\r\n" +
                "  并告诉你最终哪个 MOD 生效。在详情面板的「文件冲突」标签页查看完整列表。\r\n\r\n" +
                "六、部署到游戏目录（把 MOD 真正装进游戏）\r\n" +
                "  先设置「游戏目录」，再选择右下角的「部署方式」：\r\n\r\n" +
                "  【Mods 子目录（星露谷）】—— 星露谷 / SMAPI 用这种\r\n" +
                "    每个已启用的 MOD 作为一个完整文件夹复制到 游戏目录\\Mods，\r\n" +
                "    例如「自动门」→ Stardew Valley\\Mods\\自动门\\（内含 manifest.json）。\r\n" +
                "    MOD 之间互不干扰，不存在文件覆盖问题；\r\n" +
                "    点「冲突检测」会检查每个 MOD 是否包含 manifest.json。\r\n" +
                "    右键列表可以选择「导入游戏 Mods 目录中的现有 MOD」，\r\n" +
                "    把已经手动装好的 MOD 复制进工作区统一管理。\r\n\r\n" +
                "  【游戏根目录（通用）】—— 其他单机游戏用这种\r\n" +
                "    按加载顺序把已启用 MOD 的文件合并复制到游戏根目录，\r\n" +
                "    例如 MOD 里的 data\\textures\\a.dds → 游戏目录\\data\\textures\\a.dds，\r\n" +
                "    同名文件后面的 MOD 覆盖前面的。\r\n" +
                "    如果 MOD 多套了一层外壳目录，可以用「展开一层目录」修正。\r\n\r\n" +
                "  两种方式在覆盖前都会把原文件 / 原文件夹备份到 backup\\deploy_时间戳\\，\r\n" +
                "  点「还原上次部署」即可完全恢复到部署前。\r\n\r\n" +
                "七、其他功能\r\n" +
                "  · 备注 / 版本 / 作者：在「MOD 详情」中填写后点「保存修改的信息」；\r\n" +
                "  · 导出 / 导入清单：把 MOD 列表、顺序和备注保存为 json，换电脑时可复原；\r\n" +
                "  · 打开备份目录：找回被删除的 MOD（backup\\deleted_*）；\r\n" +
                "  · 快捷键：F5 刷新列表，Delete 删除，空格 启用/禁用，Ctrl+A 全选，双击打开 MOD 文件夹。\r\n\r\n" +
                "八、说明\r\n" +
                "  本工具不修改游戏本体文件，只按你的指令复制文件，并在复制前备份。\r\n" +
                "  建议在部署前确认游戏目录正确；重要存档请自行备份。\r\n";
            using (TextDialog d = new TextDialog("使用说明 - " + VersionInfo.AppTitle, text))
            {
                d.ShowDialog(this);
            }
        }

        private void NeedWorkspace()
        {
            MessageBox.Show(this, EmptyHint, "还没有工作区", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>自检模式下一律自动确认，正常模式下弹确认框。</summary>
        private bool Confirm(string text, string title)
        {
            if (Program.SelfTestMode)
            {
                _log.Info("[自检] 自动确认：" + title);
                return true;
            }
            return MessageBox.Show(this, text, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK;
        }

        private void ShowInfo(string text, string title)
        {
            if (Program.SelfTestMode) { _log.Info("[自检] " + title + "：" + text.Replace("\r\n", " ")); return; }
            MessageBox.Show(this, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenPath(string path, string what)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show(this, what + "不存在。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("explorer.exe", "\"" + path + "\"");
                psi.UseShellExecute = false;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开" + what + "：\r\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =====================================================================
        // 日志
        // =====================================================================

        private void OnLogged(LogLevel level, string message)
        {
            if (_logBox == null) return;
            if (_logBox.InvokeRequired)
            {
                try { _logBox.BeginInvoke(new Action<LogLevel, string>(OnLogged), level, message); }
                catch (Exception) { }
                return;
            }
            Color color = UiKit.Text;
            if (level == LogLevel.Ok) color = UiKit.Ok;
            else if (level == LogLevel.Warn) color = UiKit.Warn;
            else if (level == LogLevel.Error) color = UiKit.Err;
            string prefix = level == LogLevel.Error ? "[错误] " : (level == LogLevel.Warn ? "[提示] " : "");
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + prefix + message + "\n";
            if (_logBox.TextLength > 200000)
            {
                _logBox.SelectionStart = 0;
                _logBox.SelectionLength = 40000;
                _logBox.SelectedText = "";
            }
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.SelectionLength = 0;
            _logBox.SelectionColor = level == LogLevel.Info ? UiKit.SubText : color;
            _logBox.AppendText(line);
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.ScrollToCaret();
        }

        public Logger Log { get { return _log; } }

        // =====================================================================
        // 自检辅助（仅开发期使用，见 Program.RunSelfTest）
        // =====================================================================

        public void TestRunConflictCheck()
        {
            RunConflictCheck();
        }

        public void TestSelectMod(string name)
        {
            SelectByName(name);
            ShowDetails(SelectedMod());
        }

        /// <summary>自检：模拟用户点击复选框（用于验证异步勾选处理链路）。</summary>
        public void TestToggleCheck(string name)
        {
            ListViewItem it = FindItem(name);
            if (it != null) it.Checked = !it.Checked;
        }

        public void TestInstall(string path)
        {
            InstallSources(new string[] { path });
        }

        public void TestDeploy()
        {
            DoDeploy();
        }

        public void TestUndo()
        {
            DoUndo();
        }

        public void TestSelectTab(int index)
        {
            foreach (Control c in this.Controls)
            {
                TabControl tc = FindTabs(c);
                if (tc != null && index >= 0 && index < tc.TabPages.Count)
                {
                    tc.SelectedIndex = index;
                    return;
                }
            }
        }

        /// <summary>自检时把界面上的关键信息导成文本，便于开发期核对逻辑。</summary>
        public string DumpState()
        {
            StringBuilder sb = new StringBuilder();
            try { sb.AppendLine("WORKSPACE: " + (_ws == null ? "(none)" : _ws.Root)); }
            catch (Exception ex) { sb.AppendLine("ERR@workspace " + ex.GetType().Name + ": " + ex.Message); }
            try { sb.AppendLine("MODS: " + _mods.Count + "  ENABLED: " + EnabledInOrder().Count); }
            catch (Exception ex) { sb.AppendLine("ERR@mods " + ex.GetType().Name + ": " + ex.Message); }
            try { sb.AppendLine("CONFLICT GROUPS: " + _conflicts.Count); }
            catch (Exception ex) { sb.AppendLine("ERR@conflicts " + ex.GetType().Name + ": " + ex.Message); }
            try { sb.AppendLine("SELECTED: " + _lv.SelectedItems.Count + "  DETAIL: " + _lblName.Text); }
            catch (Exception ex) { sb.AppendLine("ERR@selected " + ex.GetType().Name + ": " + ex.Message); }
            try { sb.AppendLine("CTRL: lv=" + (_lv == null ? "null" : "ok") + " log=" + (_logBox == null ? "null" : "ok")); }
            catch (Exception ex) { sb.AppendLine("ERR@ctrl " + ex.GetType().Name + ": " + ex.Message); }
            sb.AppendLine();
            sb.AppendLine("--- LIST ---");
            if (_lv != null)
            {
                try
                {
                    foreach (ListViewItem it in _lv.Items)
                    {
                        StringBuilder row = new StringBuilder();
                        for (int i = 0; i < it.SubItems.Count; i++)
                        {
                            if (i > 0) row.Append(" | ");
                            row.Append(it.SubItems[i].Text);
                        }
                        row.Append("   [checked=").Append(it.Checked).Append("]");
                        sb.AppendLine(row.ToString());
                    }
                }
                catch (Exception ex) { sb.AppendLine("ERR@list " + ex.GetType().Name + ": " + ex.Message); }
            }
            sb.AppendLine();
            sb.AppendLine("--- LOG ---");
            try { sb.AppendLine(_logBox == null ? "(no log box)" : _logBox.Text); }
            catch (Exception ex) { sb.AppendLine("ERR@log " + ex.GetType().Name + ": " + ex.Message); }
            return sb.ToString();
        }

        private static TabControl FindTabs(Control root)
        {
            TabControl tc = root as TabControl;
            if (tc != null) return tc;
            foreach (Control c in root.Controls)
            {
                TabControl found = FindTabs(c);
                if (found != null) return found;
            }
            return null;
        }
    }

    /// <summary>列表选择对话框。</summary>
    public class ListPickerDialog : Form
    {
        private ListBox _list;
        public string Selected { get { return _list.SelectedItem == null ? null : _list.SelectedItem.ToString(); } }

        public ListPickerDialog(string title, string prompt, List<string> items, string preselect)
        {
            this.Text = title;
            this.Font = UiKit.Ui(9F);
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(560, 380);
            this.MinimumSize = new Size(420, 300);
            this.BackColor = UiKit.Card;

            Label lbl = UiKit.MakeLabel(prompt);
            lbl.Dock = DockStyle.Top;
            lbl.Height = 46;
            lbl.Padding = new Padding(14, 12, 14, 0);
            lbl.ForeColor = UiKit.SubText;

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 52;
            Button ok = UiKit.MakeButton("确定", 96, 32, true);
            ok.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            ok.Location = new Point(this.ClientSize.Width - 226, 10);
            ok.Click += delegate { this.DialogResult = DialogResult.OK; this.Close(); };
            Button cancel = UiKit.MakeButton("取消", 96, 32);
            cancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            cancel.Location = new Point(this.ClientSize.Width - 118, 10);
            cancel.Click += delegate { this.DialogResult = DialogResult.Cancel; this.Close(); };
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);

            _list = new ListBox();
            _list.Dock = DockStyle.Fill;
            _list.Font = UiKit.Ui(9F);
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.IntegralHeight = false;
            _list.DoubleClick += delegate { this.DialogResult = DialogResult.OK; this.Close(); };
            foreach (string s in items)
            {
                int idx = _list.Items.Add(s);
                if (preselect != null && string.Equals(s, preselect, StringComparison.OrdinalIgnoreCase)) _list.SelectedIndex = idx;
            }
            if (_list.SelectedIndex < 0 && _list.Items.Count > 0) _list.SelectedIndex = 0;

            Panel pad = new Panel();
            pad.Dock = DockStyle.Fill;
            pad.Padding = new Padding(14, 0, 14, 8);
            pad.Controls.Add(_list);

            this.Controls.Add(pad);
            this.Controls.Add(bottom);
            this.Controls.Add(lbl);
            this.AcceptButton = ok;
            this.CancelButton = cancel;
        }

        public static string Pick(IWin32Window owner, string title, string prompt, List<string> items, string preselect)
        {
            using (ListPickerDialog d = new ListPickerDialog(title, prompt, items, preselect))
            {
                if (d.ShowDialog(owner) == DialogResult.OK) return d.Selected;
            }
            return null;
        }
    }
}
