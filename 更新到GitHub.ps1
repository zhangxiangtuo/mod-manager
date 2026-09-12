# 一键把改动提交并推送到 GitHub
# 用法：右键本文件 →「使用 PowerShell 运行」，或：
#   powershell -ExecutionPolicy Bypass -File .\更新到GitHub.ps1
param([string]$Message = "")

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# 找 git（装了 Git for Windows 就有；否则退回 Codex 自带的）
$git = "git"
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    $fallback = Join-Path $env:ProgramFiles "Git\cmd\git.exe"
    if (Test-Path $fallback) { $git = $fallback }
    else { throw "没有找到 git，请先安装： winget install Git.Git" }
}

Write-Host "=== 当前改动 ===" -ForegroundColor Cyan
& $git status -s
$changed = (& $git status --porcelain)
if (-not $changed) {
    Write-Host "没有需要提交的改动。" -ForegroundColor Yellow
    Start-Sleep -Seconds 3
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Message)) {
    $Message = Read-Host "这次改了什么？（会作为提交说明）"
    if ([string]::IsNullOrWhiteSpace($Message)) { $Message = "更新" }
}

Write-Host "=== 提交并推送 ===" -ForegroundColor Cyan
& $git add -A
& $git commit -m $Message
& $git push

Write-Host ""
Write-Host "完成！提交记录： https://github.com/zhangxiangtuo/mod-manager/commits/main" -ForegroundColor Green
Write-Host "（如果上面出现 timeout / 连接失败，请先打开代理软件再重试）" -ForegroundColor Yellow