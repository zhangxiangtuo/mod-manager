# =====================================================================
#  一键把本项目发布到你的 GitHub 仓库
#
#  用法（在项目根目录右键「使用 PowerShell 运行」，或按下面命令执行）：
#     powershell -ExecutionPolicy Bypass -File .\发布到GitHub.ps1
#
#  第一次推送时会弹出 GitHub 登录窗口，按提示在浏览器里登录授权即可。
# =====================================================================
param(
    [string]$RepoUrl = "",                 # 例：https://github.com/你的用户名/mod-manager.git
    [string]$UserName = "",                # 提交记录里的名字
    [string]$UserEmail = ""                # 提交记录里的邮箱
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

function Find-Git {
    $cmd = Get-Command git -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $bundled = Join-Path $env:USERPROFILE ".cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe"
    if (Test-Path $bundled) {
        Write-Host "[提示] 系统里没装 Git，先用 Codex 自带的 Git。" -ForegroundColor Yellow
        Write-Host "       建议以后执行： winget install Git.Git" -ForegroundColor Yellow
        return $bundled
    }
    throw "没有找到 git，请先安装： winget install Git.Git"
}

$git = Find-Git
Write-Host "使用 Git：$git" -ForegroundColor Cyan

# ---------- 1. 提交者信息 ----------
$curName = (& $git config user.name) 2>$null
$curMail = (& $git config user.email) 2>$null
if ($UserName -ne "") { $curName = $UserName }
if ($UserEmail -ne "") { $curMail = $UserEmail }
if ([string]::IsNullOrWhiteSpace($curName)) {
    $curName = Read-Host "请输入你的名字（会写进提交记录，通常填 GitHub 用户名）"
}
if ([string]::IsNullOrWhiteSpace($curMail)) {
    $curMail = Read-Host "请输入你的邮箱（可以填 GitHub 的 noreply 邮箱）"
}
& $git config user.name $curName
& $git config user.email $curMail

# ---------- 2. 初始化仓库 ----------
if (-not (Test-Path (Join-Path $root ".git"))) {
    & $git init -b main | Out-Null
    Write-Host "已初始化本地仓库（分支 main）" -ForegroundColor Green
}

# ---------- 3. 提交 ----------
& $git add -A
$pending = (& $git status --porcelain) 2>$null
if ($pending) {
    & $git commit -m "feat: MOD 管理器 v1.1.0（星露谷 / 通用双模式 MOD 管理工具）" | Out-Null
    Write-Host "已创建提交" -ForegroundColor Green
} else {
    Write-Host "没有新的改动需要提交" -ForegroundColor Yellow
}

# ---------- 4. 远程仓库地址 ----------
if ([string]::IsNullOrWhiteSpace($RepoUrl)) {
    Write-Host ""
    Write-Host "=== 先去 GitHub 建一个空仓库 ===" -ForegroundColor Cyan
    Write-Host "1) 打开 https://github.com/new"
    Write-Host "2) 仓库名建议：mod-manager（或 stardew-mod-manager）"
    Write-Host "3) 选择 Public，不要勾选 Add README / .gitignore / license（本地已经有了）"
    Write-Host "4) 点 Create repository，复制页面上那个 https 地址"
    Write-Host ""
    $RepoUrl = Read-Host "把仓库地址粘贴到这里（形如 https://github.com/用户名/mod-manager.git）"
}
$RepoUrl = $RepoUrl.Trim()
if ($RepoUrl -notmatch "^https?://") { throw "仓库地址看起来不对：$RepoUrl" }

$existing = (& $git remote) 2>$null
if ($existing -contains "origin") {
    & $git remote set-url origin $RepoUrl
} else {
    & $git remote add origin $RepoUrl
}
Write-Host "远程仓库：$RepoUrl" -ForegroundColor Cyan

# ---------- 5. 推送 ----------
Write-Host ""
Write-Host "开始推送。如果弹出 GitHub 登录窗口，请在浏览器里登录并授权。" -ForegroundColor Yellow
& $git push -u origin main

Write-Host ""
Write-Host "完成！仓库地址：" -ForegroundColor Green
$web = $RepoUrl -replace "\.git$", ""
Write-Host $web -ForegroundColor Green
Write-Host ""
Write-Host "之后修改代码再发布，只需要两条命令：" -ForegroundColor Cyan
Write-Host "  git add -A"
Write-Host "  git commit -m `"说明这次改了什么`" ; git push"
