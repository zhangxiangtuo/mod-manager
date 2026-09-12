using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ModManager
{
    /// <summary>
    /// MOD 工作区。目录结构：
    ///   工作区\mods\       已启用的 MOD（每个 MOD 一个文件夹）
    ///   工作区\disabled\   已禁用的 MOD
    ///   工作区\backup\     删除备份 / 部署备份 / 临时解压
    ///   工作区\modmanager.json  配置与元信息
    /// </summary>
    public class ModWorkspace
    {
        /// <summary>批量导入时的一个待安装项。</summary>
        public class BatchEntry
        {
            public string SourcePath;
            public string Name;        // 计划安装成什么名字
            public bool IsArchive;     // true = 压缩包，false = 已解压的 MOD 文件夹
            public string SkipReason;  // 不为空表示会跳过（已存在 / 重复）
            public string UniqueId;    // manifest 里的 UniqueID，用于去重
            public long Size;
            public string Version;
        }

        public class BatchPlan
        {
            public List<BatchEntry> Entries = new List<BatchEntry>();
            public long TotalSize;
            public int ExistsCount;

            public int ToInstallCount
            {
                get
                {
                    int n = 0;
                    foreach (BatchEntry e in Entries) if (e.SkipReason == null) n++;
                    return n;
                }
            }
        }

        public string Root { get; private set; }
        public Logger Log { get; private set; }
        public WorkspaceData Data { get; private set; }

        public string ModsPath { get { return Path.Combine(Root, "mods"); } }
        public string DisabledPath { get { return Path.Combine(Root, "disabled"); } }
        public string BackupPath { get { return Path.Combine(Root, "backup"); } }
        public string DataFile { get { return Path.Combine(Root, "modmanager.json"); } }
        public string Name { get { return new DirectoryInfo(Root).Name; } }

        private ModWorkspace(string root, Logger log)
        {
            Root = root;
            Log = log;
        }

        public static ModWorkspace Open(string root, Logger log)
        {
            return Open(root, log, true);
        }

        public static ModWorkspace Open(string root, Logger log, bool createIfMissing)
        {
            if (string.IsNullOrEmpty(root)) throw new ArgumentException("工作区路径为空");
            string full = Path.GetFullPath(root);
            if (!Directory.Exists(full))
            {
                if (!createIfMissing) throw new DirectoryNotFoundException("工作区不存在：" + full);
                Directory.CreateDirectory(full);
            }
            ModWorkspace ws = new ModWorkspace(full, log);
            ws.Data = Json.Load<WorkspaceData>(ws.DataFile);
            if (ws.Data == null) ws.Data = new WorkspaceData();
            if (ws.Data.Order == null) ws.Data.Order = new List<string>();
            if (ws.Data.Mods == null) ws.Data.Mods = new Dictionary<string, ModMeta>(StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(ws.ModsPath);
            Directory.CreateDirectory(ws.DisabledPath);
            Directory.CreateDirectory(ws.BackupPath);
            ws.Save();
            return ws;
        }

        public void Save()
        {
            try { Json.Save(DataFile, Data); }
            catch (Exception ex) { Log.Error("保存配置失败：" + ex.Message); }
        }

        public ModMeta GetMeta(string name)
        {
            ModMeta meta;
            if (Data.Mods.TryGetValue(name, out meta) && meta != null) return meta;
            meta = new ModMeta();
            Data.Mods[name] = meta;
            return meta;
        }

        // ---------------------------------------------------------------- 扫描

        public List<ModInfo> Scan()
        {
            List<string> names = new List<string>();
            CollectModNames(ModsPath, names);
            CollectModNames(DisabledPath, names);

            // 用保存的顺序排前面，新出现的 MOD 追加在后面
            List<string> ordered = new List<string>();
            foreach (string n in Data.Order)
            {
                if (names.Contains(n) && !ordered.Contains(n)) ordered.Add(n);
            }
            List<string> rest = new List<string>();
            foreach (string n in names) if (!ordered.Contains(n)) rest.Add(n);
            rest.Sort(StringComparer.OrdinalIgnoreCase);
            ordered.AddRange(rest);

            bool orderChanged = ordered.Count != Data.Order.Count;
            if (!orderChanged)
            {
                for (int i = 0; i < ordered.Count; i++)
                    if (!string.Equals(ordered[i], Data.Order[i], StringComparison.Ordinal)) { orderChanged = true; break; }
            }
            Data.Order = ordered;
            if (orderChanged) Save();

            List<ModInfo> list = new List<ModInfo>();
            int index = 0;
            foreach (string name in ordered)
            {
                ModInfo info = new ModInfo();
                info.Name = name;
                bool inMods = Directory.Exists(Path.Combine(ModsPath, name));
                info.Enabled = inMods;
                info.FolderPath = Path.Combine(inMods ? ModsPath : DisabledPath, name);
                info.Meta = GetMeta(name);
                int fc; long size;
                FileUtil.MeasureDirectory(info.FolderPath, out fc, out size);
                info.FileCount = fc;
                info.SizeBytes = size;
                if (info.Enabled) info.DeployIndex = ++index;
                list.Add(info);
            }
            return list;
        }

        private static void CollectModNames(string dir, List<string> names)
        {
            if (!Directory.Exists(dir)) return;
            foreach (string d in Directory.GetDirectories(dir))
            {
                string n = Path.GetFileName(d);
                if (!names.Contains(n)) names.Add(n);
            }
        }

        // ------------------------------------------------------------ 启用/禁用

        public void SetEnabled(string name, bool enabled)
        {
            string from = Path.Combine(enabled ? DisabledPath : ModsPath, name);
            string to = Path.Combine(enabled ? ModsPath : DisabledPath, name);
            if (!Directory.Exists(from))
            {
                if (Directory.Exists(to)) return;   // 已经是目标状态
                throw new DirectoryNotFoundException("找不到 MOD 文件夹：" + name);
            }
            if (Directory.Exists(to)) throw new IOException("目标位置已存在同名 MOD：" + name);
            Directory.Move(from, to);
        }

        public int SetEnabledBatch(IEnumerable<string> names, bool enabled)
        {
            int ok = 0;
            foreach (string n in names)
            {
                try { SetEnabled(n, enabled); ok++; }
                catch (Exception ex) { Log.Error("切换状态失败 " + n + "：" + ex.Message); }
            }
            return ok;
        }

        public void MoveOrder(string name, int delta)
        {
            int i = Data.Order.FindIndex(delegate(string s) { return string.Equals(s, name, StringComparison.Ordinal); });
            if (i < 0) return;
            int j = i + delta;
            if (j < 0 || j >= Data.Order.Count) return;
            string tmp = Data.Order[i];
            Data.Order[i] = Data.Order[j];
            Data.Order[j] = tmp;
            Save();
        }

        public void Rename(string oldName, string newName)
        {
            newName = FileUtil.SanitizeName(newName);
            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return;
            bool enabled = Directory.Exists(Path.Combine(ModsPath, oldName));
            string baseDir = enabled ? ModsPath : DisabledPath;
            string from = Path.Combine(baseDir, oldName);
            string to = Path.Combine(baseDir, newName);
            if (!Directory.Exists(from)) throw new DirectoryNotFoundException("找不到 MOD 文件夹：" + oldName);
            if (Directory.Exists(to)) throw new IOException("已存在同名 MOD：" + newName);
            Directory.Move(from, to);
            ModMeta meta = GetMeta(oldName);
            Data.Mods.Remove(oldName);
            Data.Mods[newName] = meta;
            int idx = Data.Order.FindIndex(delegate(string s) { return string.Equals(s, oldName, StringComparison.Ordinal); });
            if (idx >= 0) Data.Order[idx] = newName; else Data.Order.Add(newName);
            Save();
        }

        /// <summary>删除 MOD：移动到 backup 目录，可手动找回。</summary>
        public string Delete(string name)
        {
            bool enabled = Directory.Exists(Path.Combine(ModsPath, name));
            string from = Path.Combine(enabled ? ModsPath : DisabledPath, name);
            if (!Directory.Exists(from)) throw new DirectoryNotFoundException("找不到 MOD 文件夹：" + name);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string dest = Path.Combine(BackupPath, "deleted_" + FileUtil.SanitizeName(name) + "_" + stamp);
            int k = 2;
            while (Directory.Exists(dest))
            {
                dest = Path.Combine(BackupPath, "deleted_" + FileUtil.SanitizeName(name) + "_" + stamp + "_" + k);
                k++;
            }
            Directory.Move(from, dest);
            // 记录原始信息，方便“备份与恢复”还原
            ModMeta meta;
            Data.Mods.TryGetValue(name, out meta);
            BackupInfo info = new BackupInfo();
            info.OriginalName = name;
            info.WasEnabled = enabled;
            info.DeletedAt = Json.Now();
            info.Meta = meta;
            try { Json.Save(Path.Combine(dest, "_backup_info.json"), info); }
            catch (Exception ex) { Log.Warn("写入备份信息失败：" + ex.Message); }
            Data.Mods.Remove(name);
            Data.Order.Remove(name);
            Save();
            return dest;
        }

        public const string BackupInfoFile = "_backup_info.json";

        /// <summary>把备份目录里的 MOD 恢复到工作区（沿用删除前的启用状态）。</summary>
        public string RestoreFromBackup(string backupFolder)
        {
            if (!Directory.Exists(backupFolder)) throw new DirectoryNotFoundException("备份不存在：" + backupFolder);
            string folderName = Path.GetFileName(backupFolder.TrimEnd('\\'));
            BackupInfo info = Json.Load<BackupInfo>(Path.Combine(backupFolder, BackupInfoFile));
            string name = info != null && !string.IsNullOrEmpty(info.OriginalName)
                ? info.OriginalName
                : GuessOriginalName(folderName);
            bool enabled = info == null || info.WasEnabled;
            string infoPath = Path.Combine(backupFolder, BackupInfoFile);
            if (File.Exists(infoPath)) { FileUtil.ClearReadOnly(infoPath); File.Delete(infoPath); }
            string target = Path.Combine(enabled ? ModsPath : DisabledPath, UniqueName(FileUtil.SanitizeName(name)));
            Directory.Move(backupFolder, target);
            string finalName = Path.GetFileName(target);
            if (info != null && info.Meta != null) Data.Mods[finalName] = info.Meta;
            else GetMeta(finalName);
            if (!Data.Order.Contains(finalName)) Data.Order.Add(finalName);
            Save();
            Log.Ok("已从备份恢复 MOD：「" + finalName + "」" + (enabled ? "（已启用）" : "（已禁用）"));
            return finalName;
        }

        /// <summary>永久删除某个备份（不可恢复）。</summary>
        public void PurgeBackup(string backupFolder)
        {
            string folderName = Path.GetFileName(backupFolder.TrimEnd('\\'));
            if (Data.LastDeploy != null && string.Equals(Data.LastDeploy.BackupFolder, folderName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("这是当前尚未还原的部署备份，请先点「还原上次部署」再删除。");
            FileUtil.DeleteDirectorySafe(backupFolder);
            Log.Ok("已永久删除备份：" + folderName);
        }

        private static string GuessOriginalName(string folderName)
        {
            string s = folderName;
            if (s.StartsWith("deleted_", StringComparison.OrdinalIgnoreCase)) s = s.Substring(8);
            // 去掉结尾的 _20260912_161500 形式的时间戳
            int idx = s.LastIndexOf('_');
            if (idx > 0)
            {
                int idx2 = s.LastIndexOf('_', idx - 1);
                if (idx2 > 0 && s.Length - idx2 - 1 >= 15 && s.Length - idx - 1 == 6)
                    s = s.Substring(0, idx2);
            }
            return s.Length == 0 ? folderName : s;
        }

        /// <summary>如果 MOD 里只有一层多余的外壳目录，把它展开。</summary>
        public bool FlattenOneLevel(string name)
        {
            string path = Directory.Exists(Path.Combine(ModsPath, name))
                ? Path.Combine(ModsPath, name) : Path.Combine(DisabledPath, name);
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("找不到 MOD 文件夹：" + name);
            string[] entries = Directory.GetFileSystemEntries(path);
            if (entries.Length != 1 || !Directory.Exists(entries[0]))
                return false;
            string inner = entries[0];
            List<string> files = FileUtil.ListFilesSafe(inner);
            foreach (string f in files)
            {
                string rel = FileUtil.RelativeTo(inner, f);
                string dest = Path.Combine(path, rel);
                string destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(f, dest, true);
            }
            foreach (string d in Directory.GetDirectories(inner, "*", SearchOption.AllDirectories))
            {
                string rel = FileUtil.RelativeTo(inner, d);
                try { Directory.CreateDirectory(Path.Combine(path, rel)); } catch (Exception) { }
            }
            FileUtil.DeleteDirectorySafe(inner);
            return true;
        }

        // -------------------------------------------------------------- 安装 MOD

        public static string[] SupportedExtensions { get { return new string[] { ".zip", ".7z", ".rar" }; } }

        public bool IsSupportedSource(string path)
        {
            if (Directory.Exists(path)) return true;
            if (!File.Exists(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            foreach (string s in SupportedExtensions) if (s == ext) return true;
            return false;
        }

        public string Install(string sourcePath)
        {
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                throw new FileNotFoundException("找不到文件：" + sourcePath);

            string tempRoot = Path.Combine(BackupPath, "_tmp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                string contentRoot;
                string baseName;
                if (Directory.Exists(sourcePath))
                {
                    contentRoot = sourcePath;
                    baseName = new DirectoryInfo(sourcePath).Name;
                }
                else
                {
                    string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
                    if (ext == ".zip") ExtractZip(sourcePath, tempRoot);
                    else if (ext == ".7z" || ext == ".rar") ExtractWithExternal(sourcePath, tempRoot);
                    else throw new NotSupportedException("暂不支持 " + ext + " 格式，请使用 zip / 7z / rar，或先解压后拖入文件夹。");
                    baseName = Path.GetFileNameWithoutExtension(sourcePath);
                    contentRoot = tempRoot;
                }

                CleanJunk(contentRoot);
                string realRoot = UnwrapSingleFolder(contentRoot);
                if (!string.Equals(realRoot, contentRoot, StringComparison.OrdinalIgnoreCase))
                    Log.Info("已自动去掉压缩包最外层的包装文件夹：" + FileUtil.RelativeTo(contentRoot, realRoot));
                string name = UniqueName(FileUtil.SanitizeName(baseName));
                string dest = Path.Combine(ModsPath, name);
                FileUtil.CopyDirectory(realRoot, dest);
                if (Directory.GetFileSystemEntries(dest).Length == 0)
                {
                    FileUtil.DeleteDirectorySafe(dest);
                    throw new InvalidDataException("压缩包内没有可用文件。");
                }
                ModMeta meta = GetMeta(name);
                meta.InstalledAt = Json.Now();
                meta.Source = sourcePath;
                Data.Order.Add(name);
                Save();
                Log.Info("  「" + name + "」顶层结构：" + FileUtil.FirstLevelSummary(dest, 90));
                return name;
            }
            finally
            {
                FileUtil.DeleteDirectorySafe(tempRoot);
            }
        }

        private string UniqueName(string baseName)
        {
            string name = baseName;
            int i = 2;
            while (Directory.Exists(Path.Combine(ModsPath, name)) || Directory.Exists(Path.Combine(DisabledPath, name)))
            {
                name = baseName + " (" + i.ToString() + ")";
                i++;
            }
            return name;
        }

        private void ExtractZip(string zipPath, string destDir)
        {
            string destFull = Path.GetFullPath(destDir);
            if (!destFull.EndsWith("\\", StringComparison.Ordinal)) destFull += "\\";
            int count = 0;
            using (FileStream fs = File.OpenRead(zipPath))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string rel = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    if (rel.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) continue;
                    string target = Path.GetFullPath(Path.Combine(destDir, rel));
                    if (!target.StartsWith(destFull, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("压缩包内包含非法路径，已中止：" + entry.FullName);
                    if (FileUtil.IsJunkFile(Path.GetFileName(target))) continue;
                    string dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    entry.ExtractToFile(target, true);
                    count++;
                }
            }
            if (count == 0) throw new InvalidDataException("压缩包是空的：" + Path.GetFileName(zipPath));
        }

        private void ExtractWithExternal(string archivePath, string destDir)
        {
            string sevenZip = FindSevenZip();
            if (sevenZip == null)
                throw new NotSupportedException("未检测到 7-Zip，无法解压 " + Path.GetExtension(archivePath) +
                    " 文件。请安装 7-Zip，或先手动解压后把文件夹拖进本工具。");
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = sevenZip;
            psi.Arguments = "x -y -bso0 -bsp0 -o\"" + destDir + "\" \"" + archivePath + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit();
                if (p.ExitCode != 0) throw new IOException("7-Zip 解压失败，返回码 " + p.ExitCode.ToString());
            }
        }

        public static string FindSevenZip()
        {
            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinRAR", "UnRAR.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WinRAR", "UnRAR.exe")
            };
            foreach (string c in candidates) if (File.Exists(c)) return c;
            string path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                foreach (string dir in path.Split(';'))
                {
                    if (dir.Trim().Length == 0) continue;
                    try
                    {
                        string p7 = Path.Combine(dir.Trim(), "7z.exe");
                        if (File.Exists(p7)) return p7;
                        string pr = Path.Combine(dir.Trim(), "UnRAR.exe");
                        if (File.Exists(pr)) return pr;
                    }
                    catch (Exception) { }
                }
            }
            return null;
        }

        private static void CleanJunk(string root)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                {
                    if (FileUtil.IsJunkDir(Path.GetFileName(d))) FileUtil.DeleteDirectorySafe(d);
                }
            }
            catch (Exception) { }
            try
            {
                foreach (string f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (FileUtil.IsJunkFile(Path.GetFileName(f))) { FileUtil.ClearReadOnly(f); File.Delete(f); }
                }
            }
            catch (Exception) { }
        }

        /// <summary>游戏内容里常见的顶层目录，遇到它们就不再去壳，避免破坏结构。</summary>
        private static readonly string[] KnownTopFolders = new string[]
        {
            "mods", "data", "bin", "assets", "plugins", "config", "saves", "logs",
            "textures", "texturepacks", "resourcepack", "resourcepacks", "shaderpacks",
            "scripts", "content", "media", "cache", "addons", "lua", "interface", "sound", "text"
        };

        private static bool IsKnownTopFolder(string name)
        {
            foreach (string s in KnownTopFolders)
                if (string.Equals(name, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>如果压缩包里只有一层"外壳"目录，就去掉它；遇到游戏标准目录则保留。</summary>
        private static string UnwrapSingleFolder(string root)
        {
            string current = root;
            for (int i = 0; i < 3; i++)
            {
                string[] dirs = Directory.GetDirectories(current);
                string[] files = Directory.GetFiles(current);
                if (dirs.Length == 1 && files.Length == 0 && !IsKnownTopFolder(Path.GetFileName(dirs[0])))
                    current = dirs[0];
                else break;
            }
            return current;
        }

        // ----------------------------------------------------------- 冲突检测

        // ------------------------------------------------------------ 批量导入

        /// <summary>
        /// 扫描一个文件夹，找出里面所有可以直接纳管的 MOD：
        /// 含 manifest.json 的文件夹（星露谷/SMAPI）、压缩包（zip/7z/rar）。
        /// 如果一个都找不到，就把这个文件夹的一级子目录当作 MOD（通用游戏用）。
        /// </summary>
        public BatchPlan ScanBatch(string root)
        {
            BatchPlan plan = new BatchPlan();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return plan;
            List<string> dirs = new List<string>();
            List<string> archives = new List<string>();
            CollectModSources(root, dirs, archives, 0);
            if (dirs.Count == 0 && archives.Count == 0)
            {
                try
                {
                    foreach (string d in Directory.GetDirectories(root))
                    {
                        if (FileUtil.IsJunkDir(Path.GetFileName(d))) continue;
                        dirs.Add(d);
                    }
                }
                catch (Exception ex) { Log.Warn("扫描文件夹失败：" + ex.Message); }
            }
            List<string> existingUids = CollectWorkspaceUniqueIds();
            foreach (string d in dirs) AddBatchEntry(plan, existingUids, d, false);
            foreach (string a in archives) AddBatchEntry(plan, existingUids, a, true);
            return plan;
        }

        /// <summary>收集工作区里已有 MOD 的 UniqueID（用来判断"这个 MOD 是不是已经装过了"）。</summary>
        private List<string> CollectWorkspaceUniqueIds()
        {
            List<string> list = new List<string>();
            foreach (string baseDir in new string[] { ModsPath, DisabledPath })
            {
                if (!Directory.Exists(baseDir)) continue;
                try
                {
                    foreach (string dir in Directory.GetDirectories(baseDir))
                    {
                        string n, v, uid;
                        ReadManifestInfo(dir, out n, out v, out uid);
                        if (!string.IsNullOrEmpty(uid)) list.Add(uid.Trim().ToLowerInvariant());
                    }
                }
                catch (Exception) { }
            }
            return list;
        }

        private static void CollectModSources(string root, List<string> dirs, List<string> archives, int depth)
        {
            if (File.Exists(Path.Combine(root, "manifest.json"))) { dirs.Add(root); return; }
            if (depth > 4) return;
            try
            {
                foreach (string sub in Directory.GetDirectories(root))
                {
                    if (FileUtil.IsJunkDir(Path.GetFileName(sub))) continue;
                    if (File.Exists(Path.Combine(sub, "manifest.json"))) dirs.Add(sub);
                    else CollectModSources(sub, dirs, archives, depth + 1);
                }
                foreach (string f in Directory.GetFiles(root))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".zip" || ext == ".7z" || ext == ".rar") archives.Add(f);
                }
            }
            catch (Exception) { }
        }

        private void AddBatchEntry(BatchPlan plan, List<string> existingUids, string path, bool isArchive)
        {
            BatchEntry e = new BatchEntry();
            e.SourcePath = path;
            e.IsArchive = isArchive;
            string baseName = isArchive ? Path.GetFileNameWithoutExtension(path) : new DirectoryInfo(path).Name;
            string modName = null;
            string version = null;
            string uid = null;
            if (!isArchive) ReadManifestInfo(path, out modName, out version, out uid);
            e.Version = version;
            e.UniqueId = uid;

            string primary = FileUtil.SanitizeName(string.IsNullOrEmpty(modName) ? baseName : modName);
            string secondary = FileUtil.SanitizeName(baseName);

            // 判断是否要跳过：同一个 UniqueID / 已存在的同名 MOD
            if (!isArchive && !string.IsNullOrEmpty(uid))
            {
                if (existingUids.Contains(uid.Trim().ToLowerInvariant()))
                    e.SkipReason = "工作区里已经有同一个 MOD";
                else if (PlanHasUniqueId(plan, uid))
                    e.SkipReason = "和前面某个 MOD 重复";
            }
            if (e.SkipReason == null && (ExistsInWorkspace(primary) || (secondary != primary && ExistsInWorkspace(secondary))))
                e.SkipReason = "工作区里已有同名 MOD";

            if (e.SkipReason != null)
            {
                e.Name = primary;
                plan.ExistsCount++;
            }
            else
            {
                string unique = primary;
                if (PlanHasName(plan, unique))
                {
                    // 重名（比如两个 MOD 的 manifest 名字一样）→ 用文件夹名区分
                    if (secondary != primary && !PlanHasName(plan, secondary)) unique = secondary;
                    else
                    {
                        int i = 2;
                        while (PlanHasName(plan, unique + " (" + i.ToString() + ")")) i++;
                        unique = unique + " (" + i.ToString() + ")";
                    }
                }
                e.Name = unique;
            }
            if (isArchive)
            {
                try { e.Size = new FileInfo(path).Length; }
                catch (Exception) { }
            }
            else
            {
                int fc; long size;
                FileUtil.MeasureDirectory(path, out fc, out size);
                e.Size = size;
            }
            plan.TotalSize += e.Size;
            plan.Entries.Add(e);
        }

        private bool ExistsInWorkspace(string name)
        {
            return Directory.Exists(Path.Combine(ModsPath, name)) || Directory.Exists(Path.Combine(DisabledPath, name));
        }

        private static bool PlanHasUniqueId(BatchPlan plan, string uid)
        {
            if (string.IsNullOrEmpty(uid)) return false;
            foreach (BatchEntry e in plan.Entries)
                if (!string.IsNullOrEmpty(e.UniqueId) && string.Equals(e.UniqueId.Trim(), uid.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool PlanHasName(BatchPlan plan, string name)
        {
            foreach (BatchEntry e in plan.Entries)
                if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>读取 MOD 的 manifest.json，取出显示名、版本号和 UniqueID。</summary>
        private static void ReadManifestInfo(string folder, out string name, out string version)
        {
            string uid;
            ReadManifestInfo(folder, out name, out version, out uid);
        }

        private static void ReadManifestInfo(string folder, out string name, out string version, out string uniqueId)
        {
            name = null;
            version = null;
            uniqueId = null;
            try
            {
                string file = Path.Combine(folder, "manifest.json");
                if (!File.Exists(file)) return;
                ManifestInfo info = Json.Load<ManifestInfo>(file);
                if (info == null) return;
                name = info.Name;
                version = info.Version;
                uniqueId = info.UniqueID;
            }
            catch (Exception) { }
        }

        private class ManifestInfo
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string UniqueID { get; set; }
        }

        /// <summary>按计划批量安装（已存在的自动跳过）。</summary>
        public int ExecuteBatch(BatchPlan plan, List<string> installed, List<string> failed)
        {
            int ok = 0;
            foreach (BatchEntry e in plan.Entries)
            {
                if (e.SkipReason != null) continue;
                try
                {
                    string name = e.IsArchive ? Install(e.SourcePath) : InstallModFolder(e.SourcePath, e.Name);
                    installed.Add(name);
                    ok++;
                }
                catch (Exception ex)
                {
                    failed.Add(e.Name + "：" + ex.Message);
                    Log.Error("导入失败 " + e.Name + "：" + ex.Message);
                }
            }
            return ok;
        }

        /// <summary>把已经解压好的 MOD 文件夹复制进工作区。</summary>
        public string InstallModFolder(string folderPath, string preferredName)
        {
            if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException("找不到文件夹：" + folderPath);
            string modName = null;
            string version = null;
            ReadManifestInfo(folderPath, out modName, out version);
            string want = string.IsNullOrEmpty(preferredName)
                ? (string.IsNullOrEmpty(modName) ? new DirectoryInfo(folderPath).Name : modName)
                : preferredName;
            string name = UniqueName(FileUtil.SanitizeName(want));
            string dest = Path.Combine(ModsPath, name);
            FileUtil.CopyDirectory(folderPath, dest);
            if (Directory.GetFileSystemEntries(dest).Length == 0)
            {
                FileUtil.DeleteDirectorySafe(dest);
                throw new InvalidDataException("文件夹是空的。");
            }
            ModMeta meta = GetMeta(name);
            meta.InstalledAt = Json.Now();
            meta.Source = folderPath;
            if (!string.IsNullOrEmpty(version)) meta.Version = version;
            if (!Data.Order.Contains(name)) Data.Order.Add(name);
            Save();
            return name;
        }

        /// <summary>检测已启用 MOD 之间重复的文件（后面的 MOD 会覆盖前面的）。</summary>
        public List<ConflictGroup> FindConflicts(List<ModInfo> mods, List<ModInfo> orderedEnabled)
        {
            foreach (ModInfo m in mods)
            {
                m.ConflictPaths = new List<string>();
                m.ConflictMods = new List<string>();
            }
            Dictionary<string, ConflictGroup> map = new Dictionary<string, ConflictGroup>(StringComparer.OrdinalIgnoreCase);
            List<string> order = new List<string>();
            foreach (ModInfo m in orderedEnabled)
            {
                List<string> files = FileUtil.ListFilesSafe(m.FolderPath);
                foreach (string f in files)
                {
                    string rel = FileUtil.RelativeTo(m.FolderPath, f);
                    ConflictGroup g;
                    if (!map.TryGetValue(rel, out g))
                    {
                        g = new ConflictGroup();
                        g.RelativePath = rel;
                        map[rel] = g;
                        order.Add(rel);
                    }
                    if (!g.Mods.Contains(m.Name)) g.Mods.Add(m.Name);
                }
                m.DeployIndex = indexOf(orderedEnabled, m.Name) + 1;
            }
            List<ConflictGroup> result = new List<ConflictGroup>();
            foreach (string rel in order)
            {
                ConflictGroup g = map[rel];
                if (g.Mods.Count < 2) continue;
                g.Winner = g.Mods[g.Mods.Count - 1];
                result.Add(g);
                foreach (string modName in g.Mods)
                {
                    ModInfo mi = findMod(mods, modName);
                    if (mi == null) continue;
                    if (!mi.ConflictPaths.Contains(rel)) mi.ConflictPaths.Add(rel);
                    foreach (string other in g.Mods)
                        if (!string.Equals(other, modName, StringComparison.Ordinal) && !mi.ConflictMods.Contains(other))
                            mi.ConflictMods.Add(other);
                }
            }
            return result;
        }

        private static int indexOf(List<ModInfo> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].Name, name, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static ModInfo findMod(List<ModInfo> list, string name)
        {
            int i = indexOf(list, name);
            return i < 0 ? null : list[i];
        }

        // ------------------------------------------------------------- 部署/还原

        public class DeployResult
        {
            public int ModCount;
            public int FileCount;        // 去重后的目标文件数
            public int Copies;           // 实际复制次数（同名文件被多个 MOD 写过会重复计数）
            public int OverwrittenCount;
            public int CreatedCount;
            public string BackupFolder;
            public string TargetDirectory;   // 实际写入的目标目录
            public bool ModsMode;            // 是否使用了 Mods 模式
        }

        public bool IsModsMode
        {
            get { return string.Equals(Data.DeployMode, "mods", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>Mods 模式的部署目录：游戏目录\Mods（如果游戏目录本身就叫 Mods 则直接用）。</summary>
        public string ResolveModsTarget(string gameDir)
        {
            string folder = string.IsNullOrEmpty(Data.ModsFolderName) ? "Mods" : Data.ModsFolderName.Trim();
            string full = Path.GetFullPath(gameDir).TrimEnd('\\');
            if (string.Equals(Path.GetFileName(full), folder, StringComparison.OrdinalIgnoreCase)) return full;
            return Path.Combine(full, folder);
        }

        /// <summary>列出游戏 Mods 目录里已有的 MOD 文件夹。</summary>
        public List<string> ScanGameMods(string gameDir)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir)) return result;
            string target = ResolveModsTarget(gameDir);
            if (!Directory.Exists(target)) return result;
            try
            {
                foreach (string d in Directory.GetDirectories(target))
                    result.Add(Path.GetFileName(d));
            }
            catch (Exception ex) { Log.Warn("读取游戏 Mods 目录失败：" + ex.Message); }
            return result;
        }

        /// <summary>把游戏 Mods 目录里已有的 MOD 复制进工作区纳管（不会移动或删除游戏里的文件）。</summary>
        public int ImportFromGameMods(string gameDir, out int skipped)
        {
            List<string> names = ScanGameMods(gameDir);
            string target = ResolveModsTarget(gameDir);
            int imported = 0;
            skipped = 0;
            foreach (string name in names)
            {
                if (Directory.Exists(Path.Combine(ModsPath, name)) || Directory.Exists(Path.Combine(DisabledPath, name)))
                {
                    skipped++;
                    continue;
                }
                try
                {
                    string dest = Path.Combine(ModsPath, FileUtil.SanitizeName(name));
                    FileUtil.CopyDirectory(Path.Combine(target, name), dest);
                    ModMeta meta = GetMeta(name);
                    meta.InstalledAt = Json.Now();
                    meta.Source = "游戏 Mods 目录";
                    if (!Data.Order.Contains(name)) Data.Order.Add(name);
                    imported++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    Log.Error("导入 " + name + " 失败：" + ex.Message);
                }
            }
            if (imported > 0) Save();
            return imported;
        }

        /// <summary>SMAPI 的 MOD 目录里应当有 manifest.json。</summary>
        public static bool HasManifest(string modFolder)
        {
            try
            {
                return File.Exists(Path.Combine(modFolder, "manifest.json"));
            }
            catch (Exception) { return false; }
        }

        /// <summary>Mods 模式下的检查：找出可能无法被 SMAPI 识别的 MOD。</summary>
        public List<string> CheckModsMode(List<ModInfo> mods)
        {
            List<string> noManifest = new List<string>();
            foreach (ModInfo m in mods)
            {
                if (!m.Enabled) continue;
                if (!HasManifest(m.FolderPath)) noManifest.Add(m.Name);
            }
            return noManifest;
        }

        /// <summary>按加载顺序把已启用 MOD 的文件合并复制到游戏目录，被覆盖的文件先备份。</summary>
        public DeployResult Deploy(List<ModInfo> orderedEnabled, string gameDir)
        {
            if (orderedEnabled.Count == 0) throw new InvalidOperationException("没有已启用的 MOD，无法部署。");
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
                throw new DirectoryNotFoundException("游戏目录无效：" + gameDir);
            string gameFull = Path.GetFullPath(gameDir);
            if (IsModsMode) return DeployIntoModsFolder(orderedEnabled, gameFull);

            DeployRecord rec = Data.LastDeploy;
            if (rec == null || !string.Equals(rec.GameDirectory, gameFull, StringComparison.OrdinalIgnoreCase))
            {
                rec = new DeployRecord();
                rec.Time = Json.Now();
                rec.GameDirectory = gameFull;
                rec.BackupFolder = "deploy_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }
            string backupRoot = Path.Combine(BackupPath, rec.BackupFolder);
            Directory.CreateDirectory(backupRoot);

            Dictionary<string, DeployEntry> seen = new Dictionary<string, DeployEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (DeployEntry e in rec.Entries) if (!seen.ContainsKey(e.Path)) seen[e.Path] = e;

            DeployResult res = new DeployResult();
            res.BackupFolder = rec.BackupFolder;
            res.ModCount = orderedEnabled.Count;

            foreach (ModInfo mod in orderedEnabled)
            {
                List<string> files = FileUtil.ListFilesSafe(mod.FolderPath);
                foreach (string f in files)
                {
                    string rel = FileUtil.RelativeTo(mod.FolderPath, f);
                    if (rel.Length == 0) continue;
                    string target = Path.Combine(gameFull, rel);
                    if (!seen.ContainsKey(rel))
                    {
                        DeployEntry entry = new DeployEntry();
                        entry.Path = rel;
                        if (File.Exists(target))
                        {
                            entry.Kind = "overwritten";
                            string bak = Path.Combine(backupRoot, rel);
                            string bakDir = Path.GetDirectoryName(bak);
                            if (!string.IsNullOrEmpty(bakDir)) Directory.CreateDirectory(bakDir);
                            File.Copy(target, bak, true);
                            res.OverwrittenCount++;
                        }
                        else
                        {
                            entry.Kind = "created";
                            res.CreatedCount++;
                        }
                        seen[rel] = entry;
                        rec.Entries.Add(entry);
                        res.FileCount++;
                    }
                    string targetDir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
                    File.Copy(f, target, true);
                    FileUtil.ClearReadOnly(target);
                    res.Copies++;
                }
            }
            Data.LastDeploy = rec;
            Save();
            return res;
        }

        public bool HasDeployRecord { get { return Data.LastDeploy != null && Data.LastDeploy.Entries != null; } }

        public DeployRecord LastDeploy { get { return Data.LastDeploy; } }

        /// <summary>Mods 模式：把每个已启用 MOD 的整个文件夹复制到 游戏目录\Mods。</summary>
        private DeployResult DeployIntoModsFolder(List<ModInfo> mods, string gameFull)
        {
            string target = ResolveModsTarget(gameFull);
            Directory.CreateDirectory(target);

            DeployRecord rec = Data.LastDeploy;
            if (rec == null || !string.Equals(rec.GameDirectory, gameFull, StringComparison.OrdinalIgnoreCase) || !IsFolderRecord(rec))
            {
                rec = new DeployRecord();
                rec.Time = Json.Now();
                rec.GameDirectory = gameFull;
                rec.BackupFolder = "deploy_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }
            string backupRoot = Path.Combine(BackupPath, rec.BackupFolder);
            Directory.CreateDirectory(backupRoot);

            Dictionary<string, DeployEntry> seen = new Dictionary<string, DeployEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (DeployEntry e in rec.Entries) if (!seen.ContainsKey(e.Path)) seen[e.Path] = e;

            DeployResult res = new DeployResult();
            res.BackupFolder = rec.BackupFolder;
            res.ModCount = mods.Count;
            res.ModsMode = true;
            res.TargetDirectory = target;

            foreach (ModInfo mod in mods)
            {
                string dest = Path.Combine(target, mod.Name);
                if (!seen.ContainsKey(mod.Name))
                {
                    DeployEntry entry = new DeployEntry();
                    entry.Path = mod.Name;
                    if (Directory.Exists(dest))
                    {
                        entry.Kind = "folder-overwritten";
                        FileUtil.MoveDirectory(dest, Path.Combine(backupRoot, mod.Name));
                        res.OverwrittenCount++;
                    }
                    else
                    {
                        entry.Kind = "folder-created";
                        res.CreatedCount++;
                    }
                    seen[mod.Name] = entry;
                    rec.Entries.Add(entry);
                    res.FileCount++;
                }
                else if (Directory.Exists(dest))
                {
                    // 同一次部署里同名文件夹已被前一个 MOD 写过，直接清空重写
                    FileUtil.DeleteDirectorySafe(dest);
                }
                FileUtil.CopyDirectory(mod.FolderPath, dest);
                res.Copies++;
            }
            Data.LastDeploy = rec;
            Save();
            return res;
        }

        private static bool IsFolderRecord(DeployRecord rec)
        {
            foreach (DeployEntry e in rec.Entries)
                if (e.Kind != null && e.Kind.StartsWith("folder-", StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>还原上一次部署：恢复被覆盖的文件，删除本次新增的文件。</summary>
        public string UndoDeploy()
        {
            DeployRecord rec = Data.LastDeploy;
            if (rec == null) throw new InvalidOperationException("没有可还原的部署记录。");
            string backupRoot = Path.Combine(BackupPath, rec.BackupFolder);
            string modsTarget = IsFolderRecord(rec) ? ResolveModsTarget(rec.GameDirectory) : null;
            int restored = 0, removed = 0, failed = 0;
            for (int i = rec.Entries.Count - 1; i >= 0; i--)
            {
                DeployEntry e = rec.Entries[i];
                string target = modsTarget == null ? Path.Combine(rec.GameDirectory, e.Path) : Path.Combine(modsTarget, e.Path);
                try
                {
                    if (e.Kind == "folder-overwritten")
                    {
                        string bak = Path.Combine(backupRoot, e.Path);
                        FileUtil.DeleteDirectorySafe(target);
                        if (Directory.Exists(bak))
                        {
                            FileUtil.MoveDirectory(bak, target);
                            restored++;
                        }
                        else failed++;
                    }
                    else if (e.Kind == "folder-created")
                    {
                        if (Directory.Exists(target)) { FileUtil.DeleteDirectorySafe(target); removed++; }
                    }
                    else if (e.Kind == "overwritten")
                    {
                        string bak = Path.Combine(backupRoot, e.Path);
                        if (File.Exists(bak))
                        {
                            string dir = Path.GetDirectoryName(target);
                            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                            File.Copy(bak, target, true);
                            FileUtil.ClearReadOnly(target);
                            restored++;
                        }
                        else failed++;
                    }
                    else
                    {
                        if (File.Exists(target)) { FileUtil.ClearReadOnly(target); File.Delete(target); removed++; }
                        FileUtil.RemoveEmptyParents(target, rec.GameDirectory);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.Error("还原失败 " + e.Path + "：" + ex.Message);
                }
            }
            string what = modsTarget == null ? "文件" : "MOD 文件夹";
            string msg = "还原完成：恢复" + what + " " + restored + " 个，删除新增" + what + " " + removed + " 个";
            if (failed > 0) msg += "，失败 " + failed + " 个";
            if (failed == 0) FileUtil.DeleteDirectorySafe(backupRoot);
            Data.LastDeploy = null;
            Save();
            return msg;
        }

        // ---------------------------------------------------------- 清单导出/导入

        public Manifest BuildManifest(List<ModInfo> mods)
        {
            Manifest mf = new Manifest();
            mf.ExportedAt = Json.Now();
            mf.WorkspaceName = Name;
            mf.GameDirectory = Data.GameDirectory;
            int order = 0;
            foreach (ModInfo m in mods)
            {
                ManifestMod mm = new ManifestMod();
                mm.Name = m.Name;
                mm.Enabled = m.Enabled;
                mm.Order = m.Enabled ? ++order : 0;
                mm.Meta = m.Meta;
                mf.Mods.Add(mm);
            }
            return mf;
        }

        /// <summary>导入清单：按名称匹配已有 MOD，套用备注/版本并恢复加载顺序。</summary>
        public string ImportManifest(Manifest mf, List<ModInfo> current)
        {
            if (mf == null || mf.Mods == null) throw new InvalidDataException("清单文件内容无效。");
            int applied = 0;
            List<string> missing = new List<string>();
            List<ManifestMod> ordered = new List<ManifestMod>(mf.Mods);
            ordered.Sort(delegate(ManifestMod a, ManifestMod b)
            {
                int oa = a.Order <= 0 ? int.MaxValue : a.Order;
                int ob = b.Order <= 0 ? int.MaxValue : b.Order;
                return oa.CompareTo(ob);
            });
            List<string> newOrder = new List<string>();
            foreach (ManifestMod mm in ordered)
            {
                bool exists = false;
                foreach (ModInfo m in current) if (string.Equals(m.Name, mm.Name, StringComparison.Ordinal)) { exists = true; break; }
                if (!exists) { missing.Add(mm.Name); continue; }
                if (mm.Meta != null)
                {
                    ModMeta meta = GetMeta(mm.Name);
                    meta.Version = mm.Meta.Version;
                    meta.Author = mm.Meta.Author;
                    meta.Note = mm.Meta.Note;
                    if (string.IsNullOrEmpty(meta.InstalledAt)) meta.InstalledAt = mm.Meta.InstalledAt;
                }
                bool isEnabled = false;
                foreach (ModInfo m in current) if (string.Equals(m.Name, mm.Name, StringComparison.Ordinal)) isEnabled = m.Enabled;
                if (mm.Enabled && !isEnabled)
                {
                    try { SetEnabled(mm.Name, true); } catch (Exception ex) { Log.Error("启用失败 " + mm.Name + "：" + ex.Message); }
                }
                if (mm.Enabled) newOrder.Add(mm.Name);
                applied++;
            }
            foreach (string n in Data.Order) if (!newOrder.Contains(n)) newOrder.Add(n);
            Data.Order = newOrder;
            Save();
            string msg = "导入完成：更新 " + applied + " 个 MOD 的信息与顺序。";
            if (missing.Count > 0)
            {
                msg += "\r\n有 " + missing.Count + " 个 MOD 在当前工作区中不存在：" + string.Join("、", missing.ToArray());
            }
            return msg;
        }

        public string RecycleCount()
        {
            try
            {
                int n = Directory.GetDirectories(BackupPath).Length;
                return n.ToString();
            }
            catch (Exception) { return "0"; }
        }
    }
}
