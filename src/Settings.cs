using System;
using System.IO;

namespace ModManager
{
    /// <summary>程序级设置的读写（保存在 %APPDATA%\MODManager\settings.json）。</summary>
    public static class SettingsStore
    {
        /// <summary>自检模式使用：强制指定要打开的工作区。</summary>
        public static string OverrideLastWorkspace = null;

        private static string DirPath
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "MODManager");
            }
        }

        private static string FilePath { get { return Path.Combine(DirPath, "settings.json"); } }

        public static AppSettings Load()
        {
            AppSettings s = Json.Load<AppSettings>(FilePath);
            if (s == null) s = new AppSettings();
            if (s.WindowWidth < 1000) s.WindowWidth = 1180;
            if (s.WindowHeight < 640) s.WindowHeight = 760;
            if (OverrideLastWorkspace != null) s.LastWorkspace = OverrideLastWorkspace;
            return s;
        }

        public static void Save(AppSettings s)
        {
            try
            {
                Directory.CreateDirectory(DirPath);
                Json.Save(FilePath, s);
            }
            catch (Exception) { }
        }
    }
}
