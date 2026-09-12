using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ModManager
{
    /// <summary>单个 MOD 的可编辑元信息。</summary>
    public class ModMeta
    {
        public string Version { get; set; }
        public string Author { get; set; }
        public string Note { get; set; }
        public string InstalledAt { get; set; }
        public string Source { get; set; }

        public ModMeta()
        {
            Version = "";
            Author = "";
            Note = "";
            InstalledAt = "";
            Source = "";
        }
    }

    /// <summary>部署时记录的一条文件改动，用于一键还原。</summary>
    public class DeployEntry
    {
        public string Path { get; set; }   // 相对游戏目录的路径
        public string Kind { get; set; }   // overwritten / created

        public DeployEntry() { Path = ""; Kind = "created"; }
    }

    public class DeployRecord
    {
        public string Time { get; set; }
        public string GameDirectory { get; set; }
        public string BackupFolder { get; set; }
        public List<DeployEntry> Entries { get; set; }

        public DeployRecord()
        {
            Time = "";
            GameDirectory = "";
            BackupFolder = "";
            Entries = new List<DeployEntry>();
        }
    }

    /// <summary>工作区数据，保存到工作区根目录的 modmanager.json。</summary>
    public class WorkspaceData
    {
        public string GameDirectory { get; set; }
        public string GameExecutable { get; set; }
        /// <summary>部署方式："root" = 合并复制到游戏根目录；"mods" = 整个 MOD 文件夹复制到 Mods 子目录。</summary>
        public string DeployMode { get; set; }
        /// <summary>Mods 模式下的子目录名，默认 Mods。</summary>
        public string ModsFolderName { get; set; }
        public List<string> Order { get; set; }
        public Dictionary<string, ModMeta> Mods { get; set; }
        public DeployRecord LastDeploy { get; set; }

        public WorkspaceData()
        {
            GameDirectory = "";
            GameExecutable = "";
            DeployMode = "root";
            ModsFolderName = "Mods";
            Order = new List<string>();
            Mods = new Dictionary<string, ModMeta>(StringComparer.OrdinalIgnoreCase);
            LastDeploy = null;
        }
    }

    /// <summary>程序级设置，保存在 %APPDATA%\MODManager\settings.json。</summary>
    public class AppSettings
    {
        public string LastWorkspace { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }

        public AppSettings()
        {
            LastWorkspace = "";
            WindowWidth = 1180;
            WindowHeight = 760;
            WindowMaximized = false;
        }
    }

    /// <summary>列表里展示的一个 MOD。</summary>
    public class ModInfo
    {
        public string Name { get; set; }
        public string FolderPath { get; set; }
        public bool Enabled { get; set; }
        public int FileCount { get; set; }
        public long SizeBytes { get; set; }
        public ModMeta Meta { get; set; }
        public int DeployIndex { get; set; }             // 启用后的加载序号，未启用为 0
        public List<string> ConflictPaths { get; set; }  // 与其他 MOD 重复的文件
        public List<string> ConflictMods { get; set; }

        public ModInfo()
        {
            Name = "";
            FolderPath = "";
            Meta = new ModMeta();
            ConflictPaths = new List<string>();
            ConflictMods = new List<string>();
        }

        public string StatusText { get { return Enabled ? "已启用" : "已禁用"; } }
    }

    public class ConflictGroup
    {
        public string RelativePath { get; set; }
        public List<string> Mods { get; set; }   // 按加载顺序
        public string Winner { get; set; }

        public ConflictGroup()
        {
            RelativePath = "";
            Mods = new List<string>();
            Winner = "";
        }
    }

    public class ManifestMod
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public int Order { get; set; }
        public ModMeta Meta { get; set; }

        public ManifestMod() { Name = ""; Meta = new ModMeta(); }
    }

    public class Manifest
    {
        public string Tool { get; set; }
        public string ToolVersion { get; set; }
        public string ExportedAt { get; set; }
        public string WorkspaceName { get; set; }
        public string GameDirectory { get; set; }
        public List<ManifestMod> Mods { get; set; }

        public Manifest()
        {
            Tool = "MOD 管理器";
            ToolVersion = VersionInfo.Version;
            ExportedAt = "";
            WorkspaceName = "";
            GameDirectory = "";
            Mods = new List<ManifestMod>();
        }
    }

    /// <summary>删除 MOD 时写的备份信息，用于“备份与恢复”。</summary>
    public class BackupInfo
    {
        public string OriginalName { get; set; }
        public bool WasEnabled { get; set; }
        public string DeletedAt { get; set; }
        public ModMeta Meta { get; set; }

        public BackupInfo()
        {
            OriginalName = "";
            WasEnabled = true;
            DeletedAt = "";
            Meta = null;
        }
    }

    public static class VersionInfo
    {
        public const string Version = "1.1.0";
        public const string AppTitle = "MOD 管理器";
    }

    public enum LogLevel { Info, Ok, Warn, Error }

    /// <summary>简单日志总线，UI 订阅后写入日志面板。</summary>
    public class Logger
    {
        public event Action<LogLevel, string> Logged;

        public void Write(LogLevel level, string message)
        {
            Action<LogLevel, string> h = Logged;
            if (h != null) h(level, message);
        }

        public void Info(string message) { Write(LogLevel.Info, message); }
        public void Ok(string message) { Write(LogLevel.Ok, message); }
        public void Warn(string message) { Write(LogLevel.Warn, message); }
        public void Error(string message) { Write(LogLevel.Error, message); }
    }

    /// <summary>极简 JSON 读写（使用 .NET 自带 JavaScriptSerializer）。</summary>
    public static class Json
    {
        public static void Save(string path, object value)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            string text = ser.Serialize(value);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        public static T Load<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try
            {
                string text = File.ReadAllText(path, Encoding.UTF8);
                if (text == null || text.Trim().Length == 0) return null;
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;
                return ser.Deserialize<T>(text);
            }
            catch
            {
                return null;
            }
        }

        public static string Now()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
