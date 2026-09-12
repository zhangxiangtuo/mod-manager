using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ModManager.Tests
{
    /// <summary>核心逻辑自动化测试（不依赖界面）。</summary>
    public static class CoreTests
    {
        private static int _pass;
        private static int _fail;
        private static string _root;
        private static Logger _log = new Logger();

        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; }
            catch (Exception) { }
            _root = Path.Combine(Path.GetTempPath(), "modmgr_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Console.WriteLine("TEST ROOT: " + _root);
            try
            {
                TestInstallZip();
                TestEnableDisable();
                TestOrderAndRename();
                TestConflicts();
                TestDeployAndUndo();
                TestModsModeDeploy();
                TestImportFromGameMods();
                TestBatchImport();
                TestDeleteAndBackup();
                TestManifest();
                TestZipSlip();
                TestJunkCleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FATAL] " + ex);
                _fail++;
            }
            Console.WriteLine();
            Console.WriteLine("PASSED: " + _pass + "   FAILED: " + _fail);
            return _fail == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ 工具

        private static void Check(bool condition, string name, string detail)
        {
            if (condition)
            {
                _pass++;
                Console.WriteLine("[PASS] " + name);
            }
            else
            {
                _fail++;
                Console.WriteLine("[FAIL] " + name + "  -> " + detail);
            }
        }

        private static void Write(string path, string text)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static string MakeZip(string zipPath, Dictionary<string, string> entries)
        {
            string dir = Path.GetDirectoryName(zipPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (FileStream fs = File.Create(zipPath))
            using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (KeyValuePair<string, string> kv in entries)
                {
                    ZipArchiveEntry e = zip.CreateEntry(kv.Key);
                    using (StreamWriter w = new StreamWriter(e.Open(), new UTF8Encoding(false)))
                    {
                        w.Write(kv.Value);
                    }
                }
            }
            return zipPath;
        }

        private static ModWorkspace NewWorkspace(string name)
        {
            string dir = Path.Combine(_root, name);
            return ModWorkspace.Open(dir, _log);
        }

        private static ModInfo Find(List<ModInfo> list, string name)
        {
            foreach (ModInfo m in list) if (m.Name == name) return m;
            return null;
        }

        // ------------------------------------------------------------------ 用例

        private static void TestInstallZip()
        {
            ModWorkspace ws = NewWorkspace("ws_install");
            Dictionary<string, string> files = new Dictionary<string, string>();
            files["MyMod/data/textures/a.dds"] = "AAA";
            files["MyMod/data/textures/b.dds"] = "BBB";
            files["MyMod/readme.txt"] = "hello";
            files["__MACOSX/._junk"] = "junk";
            files["MyMod/.DS_Store"] = "junk";
            string zip = MakeZip(Path.Combine(_root, "zips", "MyMod_v1.zip"), files);

            string name = ws.Install(zip);
            Check(name == "MyMod_v1", "install_returns_archive_name", "actual=" + name);
            Check(Directory.Exists(Path.Combine(ws.ModsPath, name, "data", "textures")),
                "install_unwrapped_single_folder", "data/textures 目录不存在");
            Check(File.Exists(Path.Combine(ws.ModsPath, name, "data", "textures", "a.dds")),
                "install_keeps_payload", "a.dds 丢失");
            Check(!File.Exists(Path.Combine(ws.ModsPath, name, ".DS_Store")),
                "install_strips_junk_files", ".DS_Store 未清理");
            Check(!Directory.Exists(Path.Combine(ws.ModsPath, name, "__MACOSX")),
                "install_strips_junk_dirs", "__MACOSX 未清理");

            List<ModInfo> mods = ws.Scan();
            ModInfo m = Find(mods, name);
            Check(m != null && m.Enabled && m.FileCount == 3,
                "install_scanned_and_enabled", m == null ? "未扫描到" : ("count=" + m.FileCount));

            // 同名 MOD 再装一次应自动重命名
            string name2 = ws.Install(zip);
            Check(name2 == "MyMod_v1 (2)", "install_avoids_name_clash", "actual=" + name2);
        }

        private static void TestEnableDisable()
        {
            ModWorkspace ws = NewWorkspace("ws_toggle");
            Dictionary<string, string> files = new Dictionary<string, string>();
            files["A/one.txt"] = "1";
            string zip = MakeZip(Path.Combine(_root, "zips", "A.zip"), files);
            string name = ws.Install(zip);
            ws.SetEnabled(name, false);
            Check(!Directory.Exists(Path.Combine(ws.ModsPath, name)) && Directory.Exists(Path.Combine(ws.DisabledPath, name)),
                "disable_moves_to_disabled", "目录没有移动");
            List<ModInfo> mods = ws.Scan();
            Check(Find(mods, name).Enabled == false, "disable_reflected_in_scan", "Enabled 仍为 true");
            ws.SetEnabled(name, true);
            Check(Directory.Exists(Path.Combine(ws.ModsPath, name)), "enable_moves_back", "目录没有移回 mods");
            // 重复启用应当是幂等的
            bool threw = false;
            try { ws.SetEnabled(name, true); } catch (Exception) { threw = true; }
            Check(!threw, "enable_is_idempotent", "重复启用抛异常");
        }

        private static void TestOrderAndRename()
        {
            ModWorkspace ws = NewWorkspace("ws_order");
            for (int i = 1; i <= 3; i++)
            {
                Dictionary<string, string> f = new Dictionary<string, string>();
                f["M" + i + "/file.txt"] = "x";
                ws.Install(MakeZip(Path.Combine(_root, "zips", "M" + i + ".zip"), f));
            }
            List<ModInfo> mods = ws.Scan();
            string first = mods[0].Name;
            string second = mods[1].Name;
            ws.MoveOrder(second, -1);
            ModWorkspace reopened = ModWorkspace.Open(ws.Root, _log);
            List<ModInfo> after = reopened.Scan();
            Check(after[0].Name == second && after[1].Name == first, "move_order_changes_sequence",
                "顺序为 " + after[0].Name + "," + after[1].Name);

            reopened.Rename(second, "重命名后的MOD");
            List<ModInfo> renamed = reopened.Scan();
            Check(Find(renamed, "重命名后的MOD") != null, "rename_mod_folder", "重命名后找不到");
            Check(renamed[0].Name == "重命名后的MOD", "rename_keeps_order", "顺序丢失：" + renamed[0].Name);
        }

        private static void TestConflicts()
        {
            ModWorkspace ws = NewWorkspace("ws_conflict");
            Dictionary<string, string> a = new Dictionary<string, string>();
            a["data/textures/shared.dds"] = "A";
            a["data/onlyA.txt"] = "A";
            Dictionary<string, string> b = new Dictionary<string, string>();
            b["data/textures/shared.dds"] = "B";
            b["data/onlyB.txt"] = "B";
            string na = ws.Install(MakeZip(Path.Combine(_root, "zips", "ModA.zip"), a));
            string nb = ws.Install(MakeZip(Path.Combine(_root, "zips", "ModB.zip"), b));
            List<ModInfo> mods = ws.Scan();
            List<ConflictGroup> groups = ws.FindConflicts(mods, mods);
            Check(groups.Count == 1, "conflict_detects_single_group", "组数=" + groups.Count);
            if (groups.Count == 1)
            {
                Check(groups[0].RelativePath == "data\\textures\\shared.dds",
                    "conflict_reports_relative_path", "路径=" + groups[0].RelativePath);
                Check(groups[0].Mods.Count == 2, "conflict_lists_both_mods", "数量=" + groups[0].Mods.Count);
                Check(groups[0].Winner == nb, "conflict_winner_is_last_loaded", "生效=" + groups[0].Winner);
            }
            Check(Find(mods, na).ConflictPaths.Count == 1 && Find(mods, nb).ConflictPaths.Count == 1,
                "conflict_flags_both_mods", "标记数量不正确");
            // 禁用一个后不应再有冲突
            ws.SetEnabled(nb, false);
            List<ModInfo> mods2 = ws.Scan();
            List<ModInfo> enabled = new List<ModInfo>();
            foreach (ModInfo m in mods2) if (m.Enabled) enabled.Add(m);
            Check(ws.FindConflicts(mods2, enabled).Count == 0, "conflict_ignores_disabled_mods", "禁用后仍有冲突");
        }

        private static void TestDeployAndUndo()
        {
            ModWorkspace ws = NewWorkspace("ws_deploy");
            Dictionary<string, string> a = new Dictionary<string, string>();
            a["data/textures/shared.dds"] = "FROM_MOD_A";
            a["data/newfile.txt"] = "NEW";
            string na = ws.Install(MakeZip(Path.Combine(_root, "zips", "DeployA.zip"), a));
            Dictionary<string, string> b = new Dictionary<string, string>();
            b["data/textures/shared.dds"] = "FROM_MOD_B";
            string nb = ws.Install(MakeZip(Path.Combine(_root, "zips", "DeployB.zip"), b));

            string game = Path.Combine(_root, "game");
            Write(Path.Combine(game, "data", "textures", "shared.dds"), "ORIGINAL");
            Write(Path.Combine(game, "game.exe"), "exe");

            List<ModInfo> mods = ws.Scan();
            List<ModInfo> enabled = new List<ModInfo>();
            foreach (ModInfo m in mods) if (m.Enabled) enabled.Add(m);

            ModWorkspace.DeployResult res = ws.Deploy(enabled, game);
            Check(res.Copies == 3, "deploy_copies_every_file", "复制次数=" + res.Copies);
            Check(res.FileCount == 2, "deploy_counts_unique_targets", "目标文件数=" + res.FileCount);
            Check(res.OverwrittenCount == 1, "deploy_counts_overwritten", "覆盖=" + res.OverwrittenCount);
            // A 新增 1 个文件，B 只覆盖 A 已写过的同名文件，因此新增合计 1 个
            Check(res.CreatedCount == 1, "deploy_counts_created", "新增=" + res.CreatedCount);
            Check(ws.LastDeploy.Entries.Count == 2, "deploy_record_has_one_entry_per_path", "记录数=" + ws.LastDeploy.Entries.Count);
            Check(File.ReadAllText(Path.Combine(game, "data", "textures", "shared.dds")) == "FROM_MOD_B",
                "deploy_last_mod_wins", "内容=" + File.ReadAllText(Path.Combine(game, "data", "textures", "shared.dds")));
            Check(File.ReadAllText(Path.Combine(game, "game.exe")) == "exe", "deploy_untouched_files_intact", "game.exe 被破坏");

            // 再部署一次（叠加），仍然能还原到最初的原始状态
            ws.Deploy(enabled, game);
            string msg = ws.UndoDeploy();
            Check(File.ReadAllText(Path.Combine(game, "data", "textures", "shared.dds")) == "ORIGINAL",
                "undo_restores_original", "内容=" + File.ReadAllText(Path.Combine(game, "data", "textures", "shared.dds")));
            Check(!File.Exists(Path.Combine(game, "data", "newfile.txt")), "undo_removes_created_files", "newfile.txt 仍存在");
            Check(!Directory.Exists(Path.Combine(game, "data", "textures", "nope")), "undo_ok", msg);
            Check(!ws.HasDeployRecord, "undo_clears_record", "记录未清除");
        }

        /// <summary>星露谷 / SMAPI 模式：整个 MOD 文件夹复制到 游戏目录\Mods。</summary>
        private static void TestModsModeDeploy()
        {
            ModWorkspace ws = NewWorkspace("ws_mods_mode");
            ws.Data.DeployMode = "mods";
            ws.Data.ModsFolderName = "Mods";
            ws.Save();

            Dictionary<string, string> a = new Dictionary<string, string>();
            a["manifest.json"] = "{\"Name\":\"ModA\"}";
            a["ModA.dll"] = "binary";
            Dictionary<string, string> b = new Dictionary<string, string>();
            b["manifest.json"] = "{\"Name\":\"ModB\"}";
            b["content.json"] = "[]";
            string na = ws.Install(MakeZip(Path.Combine(_root, "zips", "SmapiA.zip"), a));
            string nb = ws.Install(MakeZip(Path.Combine(_root, "zips", "SmapiB.zip"), b));

            string game = Path.Combine(_root, "stardew");
            Directory.CreateDirectory(Path.Combine(game, "Content"));
            Write(Path.Combine(game, "Stardew Valley.exe"), "exe");
            // Mods 目录里已经有一个同名 MOD（模拟玩家手动装过的），部署时应被整体备份
            Write(Path.Combine(game, "Mods", na, "manifest.json"), "{\"Name\":\"OLD\"}");
            Write(Path.Combine(game, "Mods", na, "old.dll"), "old");
            // 还有一个与本工具无关的 MOD，部署不应动它
            Write(Path.Combine(game, "Mods", "ConsoleCommands", "manifest.json"), "{}");

            List<ModInfo> mods = ws.Scan();
            List<ModInfo> enabled = new List<ModInfo>();
            foreach (ModInfo m in mods) if (m.Enabled) enabled.Add(m);
            Check(ws.ResolveModsTarget(game) == Path.Combine(game, "Mods"), "mods_target_is_mods_folder",
                "目标=" + ws.ResolveModsTarget(game));

            ModWorkspace.DeployResult res = ws.Deploy(enabled, game);
            Check(res.ModsMode, "mods_mode_flag", "未使用 Mods 模式");
            Check(res.FileCount == 2, "mods_deploy_counts_folders", "文件夹数=" + res.FileCount);
            Check(res.OverwrittenCount == 1, "mods_deploy_detects_overwrite", "覆盖=" + res.OverwrittenCount);
            Check(res.CreatedCount == 1, "mods_deploy_detects_new", "新增=" + res.CreatedCount);
            Check(File.Exists(Path.Combine(game, "Mods", na, "ModA.dll")),
                "mods_deploy_copies_mod_folder", "ModA.dll 没有复制过去");
            Check(!File.Exists(Path.Combine(game, "Mods", na, "old.dll")),
                "mods_deploy_replaces_old_folder", "旧文件仍然存在");
            Check(File.Exists(Path.Combine(game, "Mods", "ConsoleCommands", "manifest.json")),
                "mods_deploy_leaves_other_mods", "无关 MOD 被破坏");
            Check(File.Exists(Path.Combine(game, "Stardew Valley.exe")), "mods_deploy_game_root_intact", "游戏根目录被改动");
            Check(ws.CheckModsMode(mods).Count == 0, "mods_check_manifest_ok", "误报缺少 manifest.json");

            // 还原：被覆盖的文件夹恢复成原样，新增的文件夹删除
            ws.UndoDeploy();
            Check(File.Exists(Path.Combine(game, "Mods", na, "old.dll")),
                "mods_undo_restores_folder", "原有文件夹没有恢复");
            Check(!Directory.Exists(Path.Combine(game, "Mods", nb)), "mods_undo_removes_new_folder", "新增文件夹没有删除");
            Check(File.Exists(Path.Combine(game, "Mods", "ConsoleCommands", "manifest.json")),
                "mods_undo_keeps_other_mods", "无关 MOD 被误删");
        }

        /// <summary>把游戏 Mods 目录里已有的 MOD 导入工作区纳管。</summary>
        private static void TestImportFromGameMods()
        {
            ModWorkspace ws = NewWorkspace("ws_import");
            ws.Data.DeployMode = "mods";
            ws.Save();
            string game = Path.Combine(_root, "stardew_import");
            Write(Path.Combine(game, "Mods", "自动门", "manifest.json"), "{\"Name\":\"AutoGate\"}");
            Write(Path.Combine(game, "Mods", "自动门", "AutoGate.dll"), "x");
            Write(Path.Combine(game, "Mods", "ContentPatcher", "manifest.json"), "{}");

            Check(ws.ScanGameMods(game).Count == 2, "scan_game_mods", "扫描到的数量不对");
            int skipped;
            int imported = ws.ImportFromGameMods(game, out skipped);
            Check(imported == 2 && skipped == 0, "import_game_mods", "导入=" + imported + " 跳过=" + skipped);
            Check(File.Exists(Path.Combine(ws.ModsPath, "自动门", "AutoGate.dll")),
                "import_copies_content", "MOD 内容没有复制进工作区");
            Check(File.Exists(Path.Combine(game, "Mods", "自动门", "AutoGate.dll")),
                "import_keeps_original", "游戏里的原 MOD 被移动了");
            // 再导入一次应全部跳过
            imported = ws.ImportFromGameMods(game, out skipped);
            Check(imported == 0 && skipped == 2, "import_is_idempotent", "重复导入=" + imported);
            Check(ws.Scan().Count == 2, "import_indexes_mods", "工作区 MOD 数不对");
        }

        /// <summary>批量导入：分类文件夹里的一堆 MOD、一堆压缩包都要能识别。</summary>
        private static void TestBatchImport()
        {
            ModWorkspace ws = NewWorkspace("ws_batch");
            string src = Path.Combine(_root, "import_src");
            Write(Path.Combine(src, "宠物", "ModA", "manifest.json"), "{\"Name\":\"艾尔的马匹\",\"Version\":\"1.2.0\"}");
            Write(Path.Combine(src, "宠物", "ModA", "ModA.dll"), "a");
            Write(Path.Combine(src, "美化", "子分类", "ModB", "manifest.json"), "{\"Name\":\"美化包B\"}");
            Write(Path.Combine(src, "美化", "子分类", "ModB", "content.json"), "[]");
            Dictionary<string, string> zf = new Dictionary<string, string>();
            zf["ModC/manifest.json"] = "{\"Name\":\"ModC\"}";
            zf["ModC/ModC.dll"] = "c";
            MakeZip(Path.Combine(src, "压缩包", "ModC.zip"), zf);

            ModWorkspace.BatchPlan plan = ws.ScanBatch(src);
            Check(plan.Entries.Count == 3, "batch_scan_finds_all", "找到=" + plan.Entries.Count);
            Check(plan.ToInstallCount == 3, "batch_all_are_new", "待装=" + plan.ToInstallCount);
            bool nameFromManifest = false, foundNested = false, foundZip = false;
            foreach (ModWorkspace.BatchEntry e in plan.Entries)
            {
                if (e.Name == "艾尔的马匹") nameFromManifest = true;
                if (e.Name == "美化包B") foundNested = true;
                if (e.IsArchive) foundZip = true;
            }
            Check(nameFromManifest, "batch_uses_manifest_name", "没有优先使用 manifest 里的名字");
            Check(foundNested, "batch_finds_nested_mod", "深层嵌套的 MOD 没找到");
            Check(foundZip, "batch_includes_archive", "压缩包没被识别");
            Check(plan.TotalSize > 0, "batch_computes_size", "体积没算出来");

            List<string> installed = new List<string>();
            List<string> failed = new List<string>();
            int ok = ws.ExecuteBatch(plan, installed, failed);
            Check(ok == 3 && failed.Count == 0, "batch_install_success", "成功=" + ok + " 失败=" + failed.Count);
            Check(File.Exists(Path.Combine(ws.ModsPath, "艾尔的马匹", "ModA.dll")), "batch_installed_folder_mod", "文件夹 MOD 没装上");
            Check(File.Exists(Path.Combine(ws.ModsPath, "美化包B", "content.json")), "batch_installed_nested_mod", "嵌套 MOD 没装上");
            Check(File.Exists(Path.Combine(ws.ModsPath, "ModC", "ModC.dll")), "batch_installed_archive_mod", "压缩包 MOD 没装好");
            Check(ws.GetMeta("艾尔的马匹").Version == "1.2.0", "batch_records_manifest_version", "manifest 版本没记录");
            Check(File.Exists(Path.Combine(src, "宠物", "ModA", "ModA.dll")), "batch_keeps_source_files", "源文件夹被动过了");

            // 再扫一次：应全部识别为「已存在」，重复执行不会产生副本
            ModWorkspace.BatchPlan again = ws.ScanBatch(src);
            Check(again.ExistsCount == 3 && again.ToInstallCount == 0, "batch_detects_existing", "已存在=" + again.ExistsCount);
            List<string> i2 = new List<string>();
            List<string> f2 = new List<string>();
            Check(ws.ExecuteBatch(again, i2, f2) == 0, "batch_second_run_noop", "重复导入产生了重复 MOD");
            Check(ws.Scan().Count == 3, "batch_no_duplicates", "工作区 MOD 数=" + ws.Scan().Count);

            // 通用游戏回退：没有 manifest 也没有压缩包 → 一级子目录当作 MOD
            string generic = Path.Combine(_root, "generic_src");
            Write(Path.Combine(generic, "纹理包", "a.dds"), "x");
            Write(Path.Combine(generic, "界面包", "b.dds"), "x");
            Check(ws.ScanBatch(generic).Entries.Count == 2, "batch_generic_fallback", "通用回退失败");

            // 同一个 MOD 在两处出现（UniqueID 相同）→ 只装一次；
            // 两个不同 MOD 用了同一个显示名 → 都装，但名字要区分开
            string dup = Path.Combine(_root, "dup_src");
            Write(Path.Combine(dup, "A", "manifest.json"), "{\"Name\":\"同名MOD\",\"UniqueID\":\"Test.Same\"}");
            Write(Path.Combine(dup, "B", "manifest.json"), "{\"Name\":\"同名MOD\",\"UniqueID\":\"Test.Same\"}");
            Write(Path.Combine(dup, "C", "manifest.json"), "{\"Name\":\"同名MOD\",\"UniqueID\":\"Test.Other\"}");
            ModWorkspace.BatchPlan dp = ws.ScanBatch(dup);
            ModWorkspace.BatchEntry ea = null, eb = null, ec = null;
            foreach (ModWorkspace.BatchEntry x in dp.Entries)
            {
                string nm = Path.GetFileName(x.SourcePath);
                if (nm == "A") ea = x;
                else if (nm == "B") eb = x;
                else if (nm == "C") ec = x;
            }
            Check(dp.Entries.Count == 3, "batch_dup_scan_finds_three", "找到=" + dp.Entries.Count);
            Check(dp.ToInstallCount == 2, "batch_dedupes_by_uniqueid", "待装=" + dp.ToInstallCount);
            Check(ea != null && eb != null && ec != null && ea.SkipReason == null && eb.SkipReason != null && ec.SkipReason == null,
                "batch_marks_only_the_duplicate", "重复项的标记不对");
            Check(ec != null && ea != null && ec.Name != ea.Name, "batch_disambiguates_same_name",
                "两个同名 MOD 没有区分：" + (ea == null ? "?" : ea.Name) + " / " + (ec == null ? "?" : ec.Name));
        }

        private static void TestDeleteAndBackup()
        {
            ModWorkspace ws = NewWorkspace("ws_delete");
            Dictionary<string, string> f = new Dictionary<string, string>();
            f["X/file.txt"] = "x";
            string name = ws.Install(MakeZip(Path.Combine(_root, "zips", "X.zip"), f));
            string dest = ws.Delete(name);
            Check(!Directory.Exists(Path.Combine(ws.ModsPath, name)), "delete_removes_mod", "MOD 仍在 mods 中");
            Check(Directory.Exists(dest) && File.Exists(Path.Combine(dest, "file.txt")), "delete_moves_to_backup", "备份不存在");
            List<ModInfo> mods = ws.Scan();
            Check(Find(mods, name) == null, "delete_removes_from_index", "索引中仍存在");
        }

        private static void TestManifest()
        {
            ModWorkspace ws = NewWorkspace("ws_manifest");
            Dictionary<string, string> f1 = new Dictionary<string, string>();
            f1["P/file.txt"] = "1";
            Dictionary<string, string> f2 = new Dictionary<string, string>();
            f2["Q/file.txt"] = "2";
            string a = ws.Install(MakeZip(Path.Combine(_root, "zips", "P.zip"), f1));
            string b = ws.Install(MakeZip(Path.Combine(_root, "zips", "Q.zip"), f2));
            ws.SetEnabled(b, false);
            ws.GetMeta(a).Version = "2.5";
            ws.GetMeta(a).Note = "测试备注";
            ws.Save();

            string mfPath = Path.Combine(_root, "manifest.json");
            Json.Save(mfPath, ws.BuildManifest(ws.Scan()));
            Manifest loaded = Json.Load<Manifest>(mfPath);
            Check(loaded != null && loaded.Mods.Count == 2, "manifest_export_import", "读回的 MOD 数不对");

            ModWorkspace ws2 = NewWorkspace("ws_manifest2");
            ws2.Install(MakeZip(Path.Combine(_root, "zips", "P2.zip"), f1));   // 名称不同，应报告缺失
            string msg = ws2.ImportManifest(loaded, ws2.Scan());
            Check(msg.Contains("不存在"), "manifest_reports_missing", msg);
        }

        private static void TestZipSlip()
        {
            ModWorkspace ws = NewWorkspace("ws_zipslip");
            Dictionary<string, string> evil = new Dictionary<string, string>();
            evil["../evil.txt"] = "hacked";
            string zip = MakeZip(Path.Combine(_root, "zips", "evil.zip"), evil);
            bool blocked = false;
            try { ws.Install(zip); }
            catch (Exception) { blocked = true; }
            Check(blocked, "zip_slip_blocked", "危险压缩包没有被拦截");
            Check(!File.Exists(Path.Combine(ws.Root, "evil.txt")), "zip_slip_no_file_written", "恶意文件被写出");
        }

        private static void TestJunkCleanup()
        {
            ModWorkspace ws = NewWorkspace("ws_nested");
            Dictionary<string, string> f = new Dictionary<string, string>();
            f["outer/mods/inner/data/a.txt"] = "a";
            string name = ws.Install(MakeZip(Path.Combine(_root, "zips", "Nested.zip"), f));
            // 外层 outer 会被自动去掉，剩下 mods/inner/data/a.txt
            Check(File.Exists(Path.Combine(ws.ModsPath, name, "mods", "inner", "data", "a.txt")),
                "install_unwrap_only_single_shell", "结构不符合预期");
            bool flat = ws.FlattenOneLevel(name);
            Check(flat && File.Exists(Path.Combine(ws.ModsPath, name, "inner", "data", "a.txt")),
                "flatten_one_level", "展开一层失败");
        }
    }
}
