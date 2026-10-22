# Silent Hill: Downpour - Arabic translation: restore the original game files.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$m = Get-Content -Raw -Encoding UTF8 -Path (Join-Path $here "patch\manifest.json") | ConvertFrom-Json
$title = "Silent Hill: Downpour - Arabic Translation"
function Show-Msg([string]$ar, [string]$en, [string]$icon = "Information") {
    if ($env:SHDAR_QUIET) { Write-Output "[$icon] $en"; return }   # scripted test runs
    $enL = ($en -split "`r`n" | ForEach-Object { [string][char]0x202A + $_ + [string][char]0x202C }) -join "`r`n"
    $opts = [System.Windows.Forms.MessageBoxOptions]::RtlReading -bor [System.Windows.Forms.MessageBoxOptions]::RightAlign
    [void][System.Windows.Forms.MessageBox]::Show("$ar`r`n`r`n$enL", $title, "OK", $icon, "Button1", $opts)
}
function Test-Port([string]$dir) { return ($dir -and (Test-Path (Join-Path $dir "assets\SHGame\Xbox360TOC.txt"))) }
if (Get-Process -Name downpour, PlayDownpour -ErrorAction SilentlyContinue) {
    Show-Msg "أغلق اللعبة أولًا ثم حاول مرة أخرى." "Close the game first, then try again." "Warning"; exit 1 }
$port = $env:SHDAR_PORT
$saved = Join-Path $here "game_folder.txt"
if (-not $port -and (Test-Path $saved)) { $port = (Get-Content -Raw -Encoding UTF8 $saved).Trim() }
if (-not (Test-Port $port)) { $port = Split-Path -Parent $here }
if (-not (Test-Port $port)) {
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = "اختر مجلد البورت`r`nChoose the port folder"
    if ($dlg.ShowDialog() -ne "OK" -or -not (Test-Port $dlg.SelectedPath)) { exit 1 }
    $port = $dlg.SelectedPath
}
$bak = Join-Path $port "DownpourArabic-backup"
if (-not (Test-Path $bak)) {
    Show-Msg "ما لقيت النسخة الاحتياطية (DownpourArabic-backup). لإرجاع الملفات الأصلية ثبّت اللعبة في البورت من جديد." "No backup found (DownpourArabic-backup). To get the original files back, install the game in the port again." "Warning"; exit 1 }
foreach ($f in $m.files) {
    $b = Join-Path $bak $f.path
    if (Test-Path $b) { Copy-Item $b (Join-Path (Join-Path $port "assets") $f.path) -Force }
}
Remove-Item -Recurse -Force $bak
Show-Msg "تمت إزالة التعريب وعادت ملفات اللعبة الأصلية." "The Arabic translation was removed and the original game files are back."
