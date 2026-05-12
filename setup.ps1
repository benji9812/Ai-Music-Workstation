Param(
    [string]$PythonExe = "python"
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pythonRoot = Join-Path $root "PythonEngine"
$venvPath = Join-Path $pythonRoot "venv"
$requirements = Join-Path $pythonRoot "requirements.txt"
$ffmpegPath = Join-Path $pythonRoot "ffmpeg.exe"

# ── Python venv ────────────────────────────────────────────────────────────────
if (-not (Test-Path $venvPath)) {
    Write-Host "Creating Python venv at $venvPath..."
    & $PythonExe -m venv $venvPath
}

$pip = Join-Path $venvPath "Scripts\pip.exe"
if (-not (Test-Path $pip)) {
    throw "pip not found in $venvPath. Ensure Python is installed and venv creation succeeded."
}

if (Test-Path $requirements) {
    Write-Host "Installing Python dependencies from $requirements..."
    & $pip install -r $requirements
} else {
    Write-Warning "requirements.txt not found at $requirements. Skipping dependency install."
}

# ── ffmpeg ─────────────────────────────────────────────────────────────────────
if (-not (Test-Path $ffmpegPath)) {
    Write-Host "ffmpeg.exe not found. Downloading..."

    $ffmpegZip = Join-Path $env:TEMP "ffmpeg_download.zip"
    $ffmpegTemp = Join-Path $env:TEMP "ffmpeg_extracted"
    $ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"

    try {
        Write-Host "Fetching from $ffmpegUrl..."
        Invoke-WebRequest -Uri $ffmpegUrl -OutFile $ffmpegZip -UseBasicParsing

        Write-Host "Extracting..."
        if (Test-Path $ffmpegTemp) { Remove-Item -Recurse -Force $ffmpegTemp }
        Expand-Archive -Path $ffmpegZip -DestinationPath $ffmpegTemp -Force

        $ffmpegBin = Get-ChildItem -Path $ffmpegTemp -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
        if ($null -eq $ffmpegBin) {
            throw "ffmpeg.exe not found inside the downloaded archive."
        }

        Copy-Item -Path $ffmpegBin.FullName -Destination $ffmpegPath -Force
        Write-Host "ffmpeg installed to $ffmpegPath"
    }
    catch {
        Write-Error "Failed to download or extract ffmpeg: $_"
        Write-Warning "Please download ffmpeg manually from https://ffmpeg.org/download.html and place ffmpeg.exe in $pythonRoot"
    }
    finally {
        if (Test-Path $ffmpegZip)  { Remove-Item -Force $ffmpegZip }
        if (Test-Path $ffmpegTemp) { Remove-Item -Recurse -Force $ffmpegTemp }
    }
} else {
    Write-Host "ffmpeg already present at $ffmpegPath. Skipping download."
}

# ── Done ───────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Setup complete." -ForegroundColor Green