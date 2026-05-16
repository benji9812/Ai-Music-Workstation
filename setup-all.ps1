# AI Music Workstation - Komplett lokal setup för Windows/Powershell
# Körs från repo-roten

Write-Host "=== AI Music Workstation: Full Setup & Local Start (Windows) ===" -ForegroundColor Cyan

# PYTHON BACKEND
Write-Host "`n>> PythonEngine: Skapar virtuell miljö och installerar requirements..."
Set-Location PythonEngine

if (-not (Test-Path "venv")) {
    python -m venv venv
}
& venv\Scripts\Activate.ps1

pip install --upgrade pip
pip install -r requirements.txt

if (-not (Test-Path ".env")) {
    if (Test-Path ".env.example") {
        Copy-Item ".env.example" ".env"
        Write-Host "Kopierade .env.example till .env – fyll i GOOGLE/GROQ API-nycklar!"
    } else {
        Write-Warning "Saknar .env och .env.example! Skapa PythonEngine/.env manuellt."
    }
}

Write-Host "Startar AI Python backend i nytt fönster..."
Start-Process powershell -ArgumentList "venv\Scripts\Activate.ps1; uvicorn main:app --host 0.0.0.0 --port 8000"
Set-Location ..

# API GATEWAY (valfritt)
if (Test-Path "AiMusicWorkstation.Api") {
    Write-Host "`n>> .NET API finns, starta separat i din IDE eller ny terminal:"
    Write-Host "   cd AiMusicWorkstation.Api"
    Write-Host "   dotnet restore"
    Write-Host "   dotnet build"
    Write-Host "   dotnet run"
}

# REACT FRONTEND
Write-Host "`n>> AiMusicWorkstation.Web: Installerar och startar web frontend..."
Set-Location AiMusicWorkstation.Web

if (-not (Test-Path ".env")) {
    if (Test-Path ".env.example") {
        Copy-Item ".env.example" ".env"
        Write-Host "Fyllde på .env från .env.example."
    }
    Write-Host "Kom ihåg att ange rätt VITE_API_URL i .env (t.ex. http://localhost:8000 eller din api-url)!"
}

npm install

Write-Host "Startar web frontend i nytt fönster..."
Start-Process powershell -ArgumentList "npm run dev"

Set-Location ..

Write-Host "=== KLART ===" -ForegroundColor Green
Write-Host "`nWebb-UI:        http://localhost:5173"
Write-Host "Python backend: http://localhost:8000/docs"
Write-Host "`nStäng fönster/process för att avsluta."