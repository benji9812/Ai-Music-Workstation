# AI Music Workstation

## Overview

AI Music Workstation is a cloud/web-based music analysis app. Upload an MP3 and get back BPM, key, time signature, chords, lyrics, and song structure — powered by Demucs, Groq Whisper, and Google Gemini.

**Stack:** Python FastAPI → ASP.NET Core 10 API Gateway → React/Vite frontend (Vercel)

---

## Repository Layout

```
AiMusicWorkstation.Api/       ASP.NET Core 10 – API gateway/proxy
AiMusicWorkstation.Application/  Business logic
AiMusicWorkstation.Domain/    Domain models
AiMusicWorkstation.Infrastructure/  EF Core, ExternalServices (PythonEngineClient)
AiMusicWorkstation.Shared/    Shared DTOs
AiMusicWorkstation.Tests/     xUnit tests
AiMusicWorkstation.Web/       React + Vite + TypeScript frontend
AiMusicWorkstation.Desktop/   Legacy WPF app (kept for reference)
PythonEngine/                 FastAPI music analysis backend
```

---

## Quick Start (Local)

### Prerequisites
- Python 3.11+, ffmpeg
- .NET 10 SDK
- Node.js 18+ and npm

### 1. Python Backend

```bash
cd PythonEngine
python -m venv venv
source venv/bin/activate      # Windows: venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env           # fill in GOOGLE_API_KEY and GROQ_API_KEY
uvicorn main:app --host 0.0.0.0 --port 8000
# Swagger UI: http://localhost:8000/docs
```

### 2. API Gateway

```bash
cd AiMusicWorkstation.Api
dotnet restore
dotnet run
# Scalar UI: https://localhost:7107/scalar/v1
# Set DATABASE_URL or ConnectionStrings:DefaultConnection for Supabase
```

### 3. React Frontend

```bash
cd AiMusicWorkstation.Web
cp .env.example .env           # set VITE_API_URL=http://localhost:5082
npm install
npm run dev
# http://localhost:5173
```

Or use the all-in-one setup script (Windows PowerShell):

```powershell
.\setup-all.ps1
```

---

## Environment Variables

### PythonEngine/.env
```
GOOGLE_API_KEY=your_google_gemini_api_key
GROQ_API_KEY=your_groq_api_key
```

### AiMusicWorkstation.Web/.env
```
VITE_API_URL=https://your-api-gateway-url
```

### AiMusicWorkstation.Api (environment / appsettings)
```
DATABASE_URL=postgresql://...           # Supabase connection string
PythonEngine__BaseUrl=https://...       # URL of the deployed Python backend
```

> **Never commit real secrets.** Use `.env.example` files as templates.

---

## API Endpoints

### PythonEngine (FastAPI) – port 8000
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| POST | `/analyze` | Full analysis: Demucs + Groq + BPM/key/chords |
| POST | `/analyze-only` | Fast analysis: BPM/key/chords only (no Demucs) |
| POST | `/structure` | Song structure via Gemini 2.5 Flash |

### AiMusicWorkstation.Api (ASP.NET Core) – port 8080
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| POST | `/api/analysis/analyze` | Proxies to Python `/analyze` |
| POST | `/api/analysis/analyze-only` | Proxies to Python `/analyze-only` |
| POST | `/api/analysis/structure` | Proxies to Python `/structure` |
| GET | `/scalar/v1` | Scalar API documentation UI |

---

## GitHub Actions

- **`.github/workflows/ci.yml`** — builds and tests .NET on push/PR to `dev`/`main`
- **`.github/workflows/web-deploy.yml`** — documents the Vercel watch path (Vercel auto-deploys)

---

## Deployment

See [`DEPLOYMENT_INSTRUCTIONS.md`](./DEPLOYMENT_INSTRUCTIONS.md) for full cloud deployment steps (Render, Azure, Vercel, Supabase, Docker).

