# AI Music Workstation – Projektöversikt & Referens
> Skapad: 2026-05-14 | Branch: `Solution/Remodelling_To_Web` | Repo: `benji9812/Ai-Music-Workstation`

---

## 1. Vad systemet gör

AI Music Workstation är ett system som tar en MP3-fil och returnerar:
- **BPM** (tempoanalys via librosa på trumstam)
- **Taktart** (3/4, 4/4, 6/8 via autocorrelation)
- **Tonart** (Krumhansl-Schmuckler på basstam)
- **Ackord** (chroma-analys på `other`-stam)
- **Lyrics** (Groq Whisper Large v3 på vokalstam)
- **Låtstruktur** (Intro/Verse/Chorus/Bridge/Outro via Gemini 2.5 Flash)

Stem-separationen görs med **Demucs htdemucs-modellen** som delar upp låten i:
`drums.mp3`, `bass.mp3`, `other.mp3`, `vocals.mp3`

---

## 2. Repo-struktur (`dev`-branchen)

Ai-Music-Workstation/
├── .github/workflows/
│ ├── ci.yml # Build + test på push till dev/main
│ └── desktop-build.yml # Bygger WPF-app, skapar GitHub Prerelease (zip)
├── AiMusicWorkstation.Api/ # ASP.NET Core 10 – gateway/proxy mot Python
│ ├── Controllers/
│ │ └── AnalysisController.cs # Alla endpoints: health, analyze, analyze-only, structure
│ ├── Program.cs # Startup: Npgsql, PythonEngineClient, Scalar UI, PORT-binding
│ ├── appsettings.json # PythonEngine.BaseUrl = http://127.0.0.1:8000/
│ ├── appsettings.Development.json
│ └── Dockerfile # .NET 10, multi-stage build
├── AiMusicWorkstation.Application/ # Business logic, use cases
├── AiMusicWorkstation.Domain/ # Domänmodeller
├── AiMusicWorkstation.Infrastructure/ # Persistence (EF Core + Npgsql), ExternalServices
│ └── ExternalServices/
│ ├── PythonEngineClient.cs # HttpClient mot PythonEngine
│ └── PythonEngineManager.cs # Hanterar anrop
├── AiMusicWorkstation.Desktop/ # WPF-app (kommer att ersättas med React-webb)
├── AiMusicWorkstation.Shared/ # DTOs, delade modeller
├── AiMusicWorkstation.Tests/ # xUnit-tester
├── PythonEngine/ # FastAPI – musikanalys
│ ├── main.py # Alla endpoints + Demucs + Groq + Gemini-logik
│ ├── requirements.txt # Inkl. demucs, torch, librosa, groq, google-genai m.fl.
│ └── Dockerfile # python:3.11-slim + ffmpeg + libsndfile + pip install
├── AiMusicWorkstation.slnx # Solution-fil
├── setup.ps1 # Setup-skript (PowerShell)
└── README.md

---

## 3. API-endpoints

### PythonEngine (FastAPI) – port 8000

| Metod | Endpoint | Beskrivning |
|-------|----------|-------------|
| GET | `/health` | Returnerar `{"status": "ok"}` |
| POST | `/analyze` | **Full analys**: Demucs + Groq Whisper + BPM/tonart/ackord |
| POST | `/analyze-only` | **Snabb analys**: BPM/tonart/ackord direkt från uppladdad fil, **ingen Demucs** |
| POST | `/structure` | Låtstruktur via Gemini 2.5 Flash (tar `artist`, `title`, `duration`) |

### AiMusicWorkstation.Api (ASP.NET Core) – port 8080/7107

| Metod | Endpoint | Beskrivning |
|-------|----------|-------------|
| GET | `/health` | Proxar till PythonEngine health |
| POST | `/api/analysis/analyze` | Proxar till `/analyze` |
| POST | `/api/analysis/analyze-only` | Proxar till `/analyze-only` |
| POST | `/api/analysis/structure` | Proxar till `/structure` |
| GET | `/scalar/v1` | Scalar UI (API-dokumentation, alla miljöer) |

---

## 4. Miljövariabler som krävs

### PythonEngine (Railway/Render/lokalt)
GOOGLE_API_KEY=<din Gemini API-nyckel>
GROQ_API_KEY=<din Groq API-nyckel>

### AiMusicWorkstation.Api
Välj ett av dessa för Supabase:
DATABASE_URL=postgresql://user:pass@host:5432/db
SUPABASE_CONNECTION_STRING=Host=...;Port=5432;Database=...;Username=...;Password=...;Ssl Mode=Require

URL till PythonEngine (sätts som env-var i produktion):
PythonEngine__BaseUrl=https://din-python-engine.onrender.com/

---

## 5. GitHub Actions Workflows

### `ci.yml` – Körs vid push/PR till `dev` och `main`
- Bygger och testar hela .NET-lösningen
- `dotnet restore → dotnet build → dotnet test`
- Kör på `ubuntu-latest`

### `desktop-build.yml` – Körs vid push till `dev`
- Triggas av ändringar i: `Desktop/`, `Application/`, `Domain/`, `Infrastructure/`, `Shared/`
- Bygger WPF-app: `dotnet publish ... -r win-x64 --self-contained true`
- Skapar zip: `AiMusicWorkstation.Desktop-win-x64-beta-<run_number>.zip`
- Skapar GitHub Prerelease automatiskt

> **OBS**: När Desktop ersätts med React-webb ska `desktop-build.yml` antingen tas bort eller uppdateras.

---

## 6. Dockerfiles

### PythonEngine/Dockerfile
```dockerfile
FROM python:3.11-slim
RUN apt-get update && apt-get install -y ffmpeg libsndfile1 build-essential
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .
RUN mkdir -p temp_uploads separated
EXPOSE 8000
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
```
> **Känt problem**: Demucs finns nu i `requirements.txt` men har inte bekräftats installeras korrekt i Railway. Testa med Render istället.

### AiMusicWorkstation.Api/Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Multi-stage: sdk:10.0 för build, aspnet:10.0 för runtime
# Exponerar port 8080
# ENTRYPOINT: dotnet AiMusicWorkstation.Api.dll
```

---

## 7. Nuvarande hosting-status (2026-05-14)

| Tjänst | Platform | URL | Status |
|--------|----------|-----|--------|
| `python_engine` | Railway (TRIAL SLUT) | `python-engine-production-628a.up.railway.app` | ❌ Trial maxad |
| `api` | Railway (TRIAL SLUT) | `ai-music-workstation-production.up.railway.app` | ❌ Trial maxad |

**Beslut**: Migrera till **Render** (riktig free tier, ingen trial).

---

## 8. Migrationsplan: Railway → Render + React-webb


### Steg 1: Deploya PythonEngine på Render

1. Gå till [render.com](https://render.com) → skapa konto
2. "New" → "Web Service" → koppla `benji9812/Ai-Music-Workstation`
3. Inställningar:
   - **Branch**: `dev`
   - **Root Directory**: `PythonEngine`
   - **Build Command**: `pip install --no-cache-dir -r requirements.txt`
   - **Start Command**: `uvicorn main:app --host 0.0.0.0 --port $PORT`
   - **Instance Type**: Free
4. Environment Variables:
   - `GOOGLE_API_KEY` = din nyckel
   - `GROQ_API_KEY` = din nyckel
5. Deploy → du får URL: `https://python-engine-XXX.onrender.com`
6. Testa: `https://python-engine-XXX.onrender.com/docs`

### Steg 2: Deploya AiMusicWorkstation.Api på Render

1. "New" → "Web Service" → samma repo
2. Inställningar:
   - **Branch**: `dev`
   - **Root Directory**: `.` (roten)
   - **Build Command**: `dotnet publish AiMusicWorkstation.Api/AiMusicWorkstation.Api.csproj -c Release -o out`
   - **Start Command**: `dotnet out/AiMusicWorkstation.Api.dll`
   - **Instance Type**: Free
3. Environment Variables:
   - `DATABASE_URL` = din Supabase connection string
   - `PythonEngine__BaseUrl` = `https://python-engine-XXX.onrender.com/`
4. Deploy → du får URL: `https://ai-music-api-XXX.onrender.com`
5. Testa: `https://ai-music-api-XXX.onrender.com/scalar/v1`

> **Alternativ för API**: Kör API:t via **Docker** på Render (välj "Docker" istället för manuell build/start – Render hittar Dockerfile automatiskt).

### Steg 3: Ersätt WPF-Desktop med React-webb

**Ny mapp i repo**: `AiMusicWorkstation.Web/`

Tech stack:
- **React + Vite** (snabb setup)
- **Hosting**: Vercel (gratis, automatisk deploy vid push)

Minsta flöde att bygga:
[Filuppladdning MP3]
→ POST /api/analysis/analyze (till Render API)
→ Visa: BPM, Taktart, Tonart, Ackord, Lyrics, Sektioner

text

Setup:
```bash
npm create vite@latest AiMusicWorkstation.Web -- --template react-ts
cd AiMusicWorkstation.Web
npm install
```

**CORS måste läggas till i Program.cs** (saknas idag!):
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
        policy.WithOrigins("https://din-app.vercel.app")
              .AllowAnyMethod()
              .AllowAnyHeader());
});
app.UseCors("AllowWeb"); // Lägg FÖRE app.UseAuthorization()
```

Deploy React på Vercel:
1. [vercel.com](https://vercel.com) → "New Project" → koppla repo
2. Framework: Vite
3. Root Directory: `AiMusicWorkstation.Web`
4. Environment Variable: `VITE_API_URL=https://ai-music-api-XXX.onrender.com`
5. Deploy → `https://din-app.vercel.app`

### Steg 4: Uppdatera GitHub Actions

Ta bort eller inaktivera `desktop-build.yml` när Desktop är borttagen.

Lägg till ny workflow för React (valfritt):
```yaml
name: web-deploy
on:
  push:
    branches: [dev]
    paths:
      - "AiMusicWorkstation.Web/**"
# Vercel hanterar deploy automatiskt via sin GitHub-integration
# Denna workflow behövs egentligen inte om Vercel-integration är aktiv
```

---

## 9. Känt problem: Demucs i container

**Symptom**: `No module named demucs` i Railway-loggar trots att `demucs` finns i `requirements.txt`

**Möjliga orsaker**:
- Railway använde cached Docker-image utan att installera om
- Fel branch kopplad till Railway-servicen
- Railway trial tog slut innan ny build slutfördes

**Lösning**: Render bygger från scratch vid varje deploy → bör lösa problemet automatiskt.

**Guard att lägga in i `main.py`** (efter `subprocess.run`):
```python
if result.returncode != 0:
    return {
        "status": "error",
        "message": "Demucs failed. Check that 'demucs' is installed in the container."
    }

if not os.path.exists(drums_path):
    return {
        "status": "error", 
        "message": f"Demucs did not produce stems at {drums_path}"
    }
```

---

## 10. Databas: Supabase

- ORM: Entity Framework Core med Npgsql-provider
- Connection string-prioritet i `Program.cs`:
  1. `ConnectionStrings:DefaultConnection` (appsettings)
  2. `DATABASE_URL` (env-var)
  3. `SUPABASE_CONNECTION_STRING` (env-var)
- `NormalizeConnectionString()` konverterar `postgresql://` URI till Npgsql-format automatiskt

---

## 11. Lokalt köra utan hosting

```bash
# Terminal 1: Python-engine
cd PythonEngine
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8000

# Terminal 2: .NET API
cd AiMusicWorkstation.Api
dotnet run

# Swagger UI:
# http://localhost:8000/docs   (Python)
# https://localhost:7107/scalar/v1  (API)
```

Eller med Docker:
```bash
docker build -t ai-python-engine ./PythonEngine
docker run -p 8000:8000 \
  -e GOOGLE_API_KEY=xxx \
  -e GROQ_API_KEY=xxx \
  ai-python-engine
```

---

## 12. Att göra – checklista

### Infrastruktur
- [ ] Deploya PythonEngine på Render
- [ ] Verifiera att Demucs installeras (kolla build-loggen i Render)
- [ ] Testa `/analyze` med en hel MP3-fil via Swagger på Render
- [ ] Deploya AiMusicWorkstation.

# AI Music Workstation – Kom igång & Referens (2026)

> **Branch:** `Solution/Remodelling_To_Web`  
> **Repo:** `benji9812/Ai-Music-Workstation`

---

## 1. LOKAL UPPSTART (Windows/macOS/Linux)

### **A. Krav**
- Python 3.11+
- .NET 10 SDK
- Node.js + npm
- ffmpeg
- (Windows: Powershell v5+, Linux/Mac: Bash)

### **B. CLI: Automatiserad setup**

#### **Linux/macOS/WSL**
```bash
./setup-all.sh
```
#### **Windows Powershell**
```powershell
.\setup-all.ps1
```
> *Dessa skript hanterar:*
> - Python venv, pip install
> - .env/autofyll (kopierar .env.example där det saknas, men du måste fylla i secrets)
> - Startar backend (Uvicorn/FastAPI) i nytt fönster
> - Startar React-webb i nytt fönster
> - .NET API startas manuellt/lämnas till IDE

### **C. Manuella steg**
- Om `.env` saknas efter setup: Kopiera .env.example → .env och fyll i API-nycklar.
- För .NET API:  
  ```powershell
  cd AiMusicWorkstation.Api
  dotnet restore
  dotnet build
  dotnet run
  ```

---

## 2. LOKALA ADRESSER

| Tjänst            | URL                              |
|-------------------|----------------------------------|
| Python backend    | http://localhost:8000/docs       |
| API gateway (.NET)| https://localhost:7107/scalar/v1 |
| Webb (React)      | http://localhost:5173            |

---

## 3. PRODUKTIONSDEPLOY (kortfattat)

Se `DEPLOYMENT_INSTRUCTIONS.md` för utförlig handledning:
- **Backend på Azure VM** (föredras):  
  PythonEngine
- **API Gateway på Azure/Render:**  
  AiMusicWorkstation.Api (Docker eller hostad)
- **Webb på Vercel:**  
  AiMusicWorkstation.Web (VITE_API_URL mot din prod-API)
  
---

## 4. WORKFLOWS (GitHub Actions)

- **.github/workflows/ci.yml**: Bygger & testar .NET
- **.github/workflows/web-deploy.yml**: Dokumenterar watch-path för Vercel; ingen build/zip
- **Legacy:** desktop-build.yml (tas snart bort)

---

## 5. SECRET FILER
- PythonEngine/.env – **INTE i git**, behöver:
  ```
  GOOGLE_API_KEY=din_key
  GROQ_API_KEY=din_key
  ```
- AiMusicWorkstation.Web/.env – **INTE i git**, bara:
  ```
  VITE_API_URL=https://ai-music-api-XXX.onrender.com
  ```

- **Exemplen finns som `.env.example` i respektive mapp**

---

## 6. FÖR AGENTEN / CONTEXT-FILER
Se alltid:  
- README.md, AI_MUSIC_WORKSTATION_REFERENCE.md (denna fil)  
- CLI-skript (`setup-all.sh`, `setup-all.ps1`)

---

### **Vid frågor, kolla AI_MUSIC_WORKSTATION_REFERENCE.md och DEPLOYMENT_INSTRUCTIONS.md först! Hör annars av dig till repoägaren.**
:)
