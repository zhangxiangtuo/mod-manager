using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ModManager
{
    public static class Program
    {
        /// <summary>自检模式：不弹任何对话框，异常写文件，避免无人值守时卡住。</summary>
        internal static bool SelfTestMode = false;

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            string shot = ArgValue(args, "--shot");
            string workspace = ArgValue(args, "--workspace");
            if (ArgValue(args, "--empty") != null) workspace = "";
            if (workspace != null) SettingsStore.OverrideLastWorkspace = workspace;
            SelfTestMode = shot != null;

            try
            {
                MainForm form = new MainForm();
                if (shot != null)
                {
                    RunSelfTest(form, args, shot);
                    return;
                }
                Application.Run(form);
            }
            catch (Exception ex)
            {
                ReportFatal(args, ex);
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ReportFatal(new string[0], e.Exception);
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null) ReportFatal(new string[0], ex);
        }

        private static void ReportFatal(string[] args, Exception ex)
        {
            string shot = args == null ? null : ArgValue(args, "--shot");
            if (shot == null && SelfTestMode) shot = "";
            string path = !string.IsNullOrEmpty(shot)
                ? shot + ".error.txt"
                : Path.Combine(Path.GetTempPath(), "MODManager_crash.log");
            try
            {
                File.WriteAllText(path, ex.ToString(), new UTF8Encoding(false));
            }
            catch (Exception) { }
            Environment.ExitCode = 1;
            if (SelfTestMode || shot != null) return;   // 自检模式不弹窗，避免卡住
            try
            {
                MessageBox.Show("程序出现异常：\r\n\r\n" + ex.Message +
                    "\r\n\r\n详细信息已写入：\r\n" + path,
                    "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception) { }
        }

        /// <summary>自检：渲染界面并截图，用于开发期检查排版，不进入消息循环。</summary>
        private static void RunSelfTest(MainForm form, string[] args, string shotPath)
        {
            form.Show();
            Application.DoEvents();
            Thread.Sleep(250);
            Application.DoEvents();

            string conflicts = ArgValue(args, "--conflicts");
            if (conflicts != null) form.TestRunConflictCheck();
            string tab = ArgValue(args, "--tab");
            if (tab != null) form.TestSelectTab(int.Parse(tab));
            string sel = ArgValue(args, "--select");
            if (sel != null) form.TestSelectMod(sel);
            string toggle = ArgValue(args, "--toggle");
            if (toggle != null) form.TestToggleCheck(toggle);
            string install = ArgValue(args, "--install");
            if (install != null) form.TestInstall(install);
            string scan = ArgValue(args, "--scan");
            if (scan != null) form.TestScan(scan);
            string import = ArgValue(args, "--import");
            if (import != null) form.TestImportBatch(import);
            if (ArgValue(args, "--deploy") != null) form.TestDeploy();
            if (ArgValue(args, "--undo") != null) form.TestUndo();
            Application.DoEvents();
            Thread.Sleep(400);
            Application.DoEvents();

            using (Bitmap bmp = new Bitmap(form.Width, form.Height))
            {
                bool ok = false;
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    try { ok = PrintWindow(form.Handle, hdc, 2 /* PW_RENDERFULLCONTENT */); }
                    finally { g.ReleaseHdc(hdc); }
                }
                if (!ok) form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(shotPath, ImageFormat.Png);
            }
            try
            {
                File.WriteAllText(shotPath + ".dump.txt", form.DumpState(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(shotPath + ".dump.txt", "DUMP ERROR: " + ex.ToString(), new UTF8Encoding(false)); }
                catch (Exception) { }
            }
            form.Close();
        }

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        private static string ArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;
                // 开关式参数（后面没有值，或紧跟另一个 -- 参数）
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    return args[i + 1];
                return name;
            }
            return null;
        }
    }
}
