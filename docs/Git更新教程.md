# 用 Git 手动更新项目（新手版）

仓库地址：https://github.com/zhangxiangtuo/mod-manager
本地目录：`D:\AI\星露谷工具`

---

## 一、一次性准备（已经帮你做好了 ✅）

```powershell
winget install Git.Git                     # 1. 安装 Git for Windows（已装好）
git config --global user.name "你的名字"    # 2. 提交记录里显示的名字（已设置）
git config --global user.email "你的邮箱"   # 3. 邮箱，建议用 GitHub 的 noreply 邮箱（已设置）
git config --global http.proxy http://127.0.0.1:7892    # 4. 走本机代理才能连上 GitHub（已设置）
```

检查是否配好：

```powershell
git config --global --list
```

---

## 二、每次改完东西，只需要 4 条命令

打开「Windows 终端 / PowerShell」，依次输入：

```powershell
cd D:\AI\星露谷工具              # ① 进入项目目录（每次新开终端都要先做这步）

git status                       # ② 看看改了哪些文件（红色=还没暂存，绿色=已暂存）

git add -A                       # ③ 把所有改动加入"待提交"（只想提交某个文件就写 git add src\MainForm.cs）

git commit -m "说明这次改了什么"   # ④ 提交到本地仓库（引号里写清楚改了啥）

git push                         # ⑤ 推送到 GitHub ← 这一步之后网页上就能看到了
```

推送完打开 https://github.com/zhangxiangtuo/mod-manager/commits/main 就能看到你的提交。

> 提示：`git push` 正常情况下不会再问账号密码（已经用 gh 登录并保存了凭据）。
> 如果弹出登录窗口，按提示授权一次即可。

---

## 三、更省事的方式

项目根目录里有一个 **`更新到GitHub.ps1`**：右键 →「使用 PowerShell 运行」，
它会自动帮你做完 ③④⑤（会问你这次改了什么），适合不想记命令的时候用。

---

## 四、常用命令速查

| 我想… | 命令 |
| --- | --- |
| 看当前改了什么 | `git status` |
| 看具体改了哪几行 | `git diff` |
| 看最近 10 次提交 | `git log --oneline -10` |
| 看某次提交的内容 | `git show HEAD` |
| 只提交某一个文件 | `git add src\MainForm.cs` |
| 提交说明写错了，改一下 | `git commit --amend -m "新的说明"` |
| 后悔最后一次提交（保留改动，重新改） | `git reset --soft HEAD~1` |
| 放弃还没提交的修改（⚠️ 会丢失） | `git checkout -- .` |
| 把远端的改动同步下来 | `git pull` |
| 本地和远端差多少 | `git status -sb` |

---

## 五、常见问题

**1. `git push` 卡住 / 报 timeout、连接失败？**
代理软件没开。打开你的代理（Clash 等）再重试。如果换了代理端口：

```powershell
git config --global http.proxy http://127.0.0.1:新端口
```

想临时不用代理：`git config --global --unset http.proxy`

**2. 提示 `nothing to commit, working tree clean`？**
说明没有改动，或者你忘了先改文件。这是正常的。

**3. 提示要输入用户名/密码？**
用户名填 GitHub 用户名；密码**不是登录密码**，要填 Personal Access Token
（GitHub → Settings → Developer settings → Personal access tokens 生成，勾选 `repo` 权限）。

**4. commit 之后想撤销？**

```powershell
git reset --soft HEAD~1     # 撤销提交，但改动还留在文件里
git reset --hard HEAD~1     # ⚠️ 撤销提交并且丢弃改动（危险）
```

**5. push 被拒绝（rejected / non-fast-forward）？**
说明远端有你本地没有的提交（比如在网页上改过文件）。先执行：

```powershell
git pull --rebase
git push
```

**6. 想看清楚这次到底改了哪些文件？**

```powershell
git status          # 概要
git diff --stat     # 每个文件改了几行
```

---

## 六、完整例子

假设你改了 `src\MainForm.cs` 里的按钮文字，想发布出去：

```powershell
cd D:\AI\星露谷工具
git status                                  # 看到 src/MainForm.cs 是红色（已修改）
git add -A
git commit -m "调整主界面按钮文字"
git push
```

完成 ✅