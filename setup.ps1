Param(
    [string]$PythonExe = "python"
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pythonRoot = Join-Path $root "PythonEngine"
$venvPath = Join-Path $pythonRoot "venv"
$requirements = Join-Path $pythonRoot "requirements.txt"

if (-not (Test-Path $venvPath)) {
    Write-Host "Creating Python venv at $venvPath"
    & $PythonExe -m venv $venvPath
}

$pip = Join-Path $venvPath "Scripts\pip.exe"
if (-not (Test-Path $pip)) {
    throw "pip not found in $venvPath. Ensure Python is installed and venv creation succeeded."
}

if (Test-Path $requirements) {
    Write-Host "Installing Python dependencies from $requirements"
    & $pip install -r $requirements
} else {
    Write-Warning "requirements.txt not found at $requirements. Skipping dependency install."
}

Write-Host "Setup complete."
