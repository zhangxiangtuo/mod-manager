using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ModManager
{
    /// <summary>文件系统相关的小工具。</summary>
    public static class FileUtil
    {
        private static readonly string[] JunkDirNames = { "__MACOSX", ".git", "$RECYCLE.BIN" };
        private static readonly string[] JunkFileNames = { ".DS_Store", "Thumbs.db", "desktop.ini", "ehthumbs.db" };

        public static bool IsJunkDir(string name)
        {
            foreach (string j in JunkDirNames)
                if (string.Equals(name, j, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool IsJunkFile(string name)
        {
            foreach (string j in JunkFileNames)
                if (string.Equals(name, j, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static List<string> ListFilesSafe(string root)
        {
            List<string> result = new List<string>();
            if (!Directory.Exists(root)) return result;
            try
            {
                foreach (string f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    if (IsJunkFile(Path.GetFileName(f))) continue;
                    result.Add(f);
                }
            }
            catch (Exception)
            {
                // 某些目录可能无权限，忽略掉，尽量返回已枚举到的文件
            }
            return result;
        }

        public static void MeasureDirectory(string root, out int fileCount, out long size)
        {
            fileCount = 0;
            size = 0;
            if (!Directory.Exists(root)) return;
            List<string> files = ListFilesSafe(root);
            fileCount = files.Count;
            foreach (string f in files)
            {
                try { size += new FileInfo(f).Length; }
                catch (Exception) { }
            }
        }

        public static string RelativeTo(string root, string fullPath)
        {
            string r = root;
            if (!r.EndsWith("\\", StringComparison.Ordinal)) r += "\\";
            if (fullPath.StartsWith(r, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(r.Length);
            return fullPath;
        }

        public static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            List<string> files = ListFilesSafe(from);
            foreach (string f in files)
            {
                string rel = RelativeTo(from, f);
                string dest = Path.Combine(to, rel);
                string destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(f, dest, true);
                ClearReadOnly(dest);
            }
            // 保留空目录结构
            foreach (string d in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            {
                string rel = RelativeTo(from, d);
                if (IsJunkDir(Path.GetFileName(d))) continue;
                try { Directory.CreateDirectory(Path.Combine(to, rel)); }
                catch (Exception) { }
            }
        }

        public static void ClearReadOnly(string file)
        {
            try
            {
                FileAttributes a = File.GetAttributes(file);
                if ((a & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    File.SetAttributes(file, a & ~FileAttributes.ReadOnly);
            }
            catch (Exception) { }
        }

        public static void DeleteDirectorySafe(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try
            {
                foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    ClearReadOnly(f);
            }
            catch (Exception) { }
            try { Directory.Delete(dir, true); }
            catch (Exception) { }
        }

        /// <summary>跨盘符也能用的“移动目录”（先复制再删除）。</summary>
        public static void MoveDirectory(string from, string to)
        {
            if (!Directory.Exists(from)) throw new DirectoryNotFoundException("找不到目录：" + from);
            string parent = Path.GetDirectoryName(to);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            try
            {
                Directory.Move(from, to);
            }
            catch (IOException)
            {
                CopyDirectory(from, to);
                DeleteDirectorySafe(from);
            }
        }

        public static void RemoveEmptyParents(string fileOrDir, string stopAt)
        {
            string dir = Directory.Exists(fileOrDir) ? fileOrDir : Path.GetDirectoryName(fileOrDir);
            string stop = stopAt.TrimEnd('\\');
            while (!string.IsNullOrEmpty(dir) && dir.Length > stop.Length &&
                   dir.StartsWith(stop, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                        Directory.Delete(dir, false);
                    else break;
                }
                catch (Exception) { break; }
                dir = Path.GetDirectoryName(dir);
            }
        }

        /// <summary>把任意字符串变成合法的文件夹名。</summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "未命名MOD";
            char[] invalid = Path.GetInvalidFileNameChars();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in name.Trim())
            {
                bool bad = false;
                foreach (char ic in invalid) if (ic == c) { bad = true; break; }
                sb.Append(bad ? '_' : c);
            }
            string s = sb.ToString().Trim().TrimEnd('.');
            if (s.Length == 0) s = "未命名MOD";
            if (s.Length > 80) s = s.Substring(0, 80);
            return s;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.0", CultureInfo.InvariantCulture) + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            double gb = mb / 1024.0;
            return gb.ToString("0.00", CultureInfo.InvariantCulture) + " GB";
        }

        /// <summary>列出目录第一层的内容摘要，用于让用户确认 MOD 结构是否正确。</summary>
        public static string FirstLevelSummary(string path, int maxChars)
        {
            try
            {
                if (!Directory.Exists(path)) return "（无法读取）";
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (string d in Directory.GetDirectories(path))
                {
                    sb.Append(Path.GetFileName(d)).Append("/  ");
                    if (sb.Length > maxChars) break;
                }
                foreach (string f in Directory.GetFiles(path))
                {
                    sb.Append(Path.GetFileName(f)).Append("  ");
                    if (sb.Length > maxChars) break;
                }
                return sb.Length == 0 ? "（空）" : sb.ToString().Trim();
            }
            catch (Exception) { return "（无法读取）"; }
        }
    }
}
