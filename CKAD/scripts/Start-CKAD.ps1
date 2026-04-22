#Requires -Version 5.1
<#
.SYNOPSIS
    Launches Windows Terminal in WSL Ubuntu and boots the CKAD practice environment.

.DESCRIPTION
    Opens a new Windows Terminal tab running Ubuntu (WSL), then sources the
    ckad-up.sh bootstrap so kubectl/minikube/namespace/alias are all ready.

    You land in an interactive bash shell at ~ with:
      - minikube profile 'ckad' running
      - kubectl context = ckad, default namespace = practice
      - alias k, $do, $now, tab-completion

.PARAMETER Reset
    If set, deletes and recreates the minikube profile (clean slate).

.PARAMETER Distro
    WSL distro to use. Default: Ubuntu.

.EXAMPLE
    .\Start-CKAD.ps1

.EXAMPLE
    .\Start-CKAD.ps1 -Reset

.NOTES
    Prereqs (install once):
      - WSL2 + Ubuntu      (wsl --install -d Ubuntu)
      - Windows Terminal   (from Microsoft Store; command: wt)
      - Docker Desktop with WSL integration enabled
      - kubectl + minikube installed inside Ubuntu
#>

[CmdletBinding()]
param(
    [switch] $Reset,
    [string] $Distro = 'Ubuntu'
)

$ErrorActionPreference = 'Stop'

# Resolve repo-relative path to the bootstrap script, then translate to WSL form.
$repoRoot   = Split-Path -Parent $PSScriptRoot          # ...\CKAD
$upScriptWin = Join-Path $repoRoot 'scripts\ckad-up.sh' # ...\CKAD\scripts\ckad-up.sh

if (-not (Test-Path $upScriptWin)) {
    throw "ckad-up.sh not found at $upScriptWin"
}

# Convert Windows path to WSL path via wslpath.
$upScriptWsl = (& wsl.exe -d $Distro -- wslpath -a ((Resolve-Path $upScriptWin).Path -replace '\\','/')).Trim()
if (-not $upScriptWsl) {
    throw "wslpath translation failed. Is the '$Distro' distro installed? Try: wsl -l -v"
}

# Build the bash command. We `source` so shell helpers persist in the interactive shell.
$resetExport = if ($Reset.IsPresent) { 'export CKAD_RESET=1; ' } else { '' }
$bashCmd = "cd ~ && $resetExport" + "source '$upScriptWsl' && exec bash"

# Prefer Windows Terminal (wt) if available; fall back to plain wsl.exe.
$wt = Get-Command wt.exe -ErrorAction SilentlyContinue
if ($wt) {
    Write-Host "Launching Windows Terminal → WSL ($Distro)..." -ForegroundColor Cyan
    Start-Process -FilePath 'wt.exe' -ArgumentList @(
        '-w', '0',                      # reuse current window if any
        'new-tab',
        '--title', 'CKAD',
        '--profile', $Distro,
        'bash', '-lc', $bashCmd
    )
} else {
    Write-Host "Windows Terminal not found. Launching plain WSL..." -ForegroundColor Yellow
    Start-Process -FilePath 'wsl.exe' -ArgumentList @(
        '-d', $Distro,
        '--cd', '~',
        '--', 'bash', '-lc', $bashCmd
    )
}
