# AI Music Workstation — Deployment Instructions

> Branch: `Solution/Remodelling_To_Web` | Repo: `benji9812/Ai-Music-Workstation`

---

## 1. Local Development

### Prerequisites
- Python 3.11+ and ffmpeg
- .NET 10 SDK
- Node.js 18+ and npm
- (Optional) Docker

### Clone the repository
```bash
git clone https://github.com/benji9812/Ai-Music-Workstation.git
cd Ai-Music-Workstation
git checkout Solution/Remodelling_To_Web
```

### A. Python Backend
```bash
cd PythonEngine
python -m venv venv
source venv/bin/activate        # Windows: venv\Scripts\activate
pip install --upgrade pip
pip install -r requirements.txt

# Create .env from the example, then fill in your API keys
cp .env.example .env

uvicorn main:app --host 0.0.0.0 --port 8000
# Swagger UI: http://localhost:8000/docs
```

`PythonEngine/.env` must contain:
```
GOOGLE_API_KEY=<your Gemini API key>
GROQ_API_KEY=<your Groq API key>
```

### B. API Gateway (.NET)
```bash
cd AiMusicWorkstation.Api
dotnet restore
dotnet build
dotnet run
# Scalar UI: https://localhost:7107/scalar/v1
```

For local dev with a database, use `dotnet user-secrets` (never commit real credentials):
```bash
cd AiMusicWorkstation.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "postgresql://user:pass@host:5432/db"
```

### C. React Frontend
```bash
cd AiMusicWorkstation.Web
cp .env.example .env            # edit VITE_API_URL to point to your API gateway
npm install
npm run dev
# http://localhost:5173
```

`AiMusicWorkstation.Web/.env` must contain:
```
VITE_API_URL=http://localhost:5082
```

### Automated setup (Windows PowerShell)
```powershell
.\setup-all.ps1
```

---

## 2. Cloud Deployment

### A. PythonEngine on Render (recommended free tier)

1. Go to [render.com](https://render.com) → **New** → **Web Service**
2. Connect `benji9812/Ai-Music-Workstation`
3. Settings:
   - **Branch**: `Solution/Remodelling_To_Web`
   - **Root Directory**: `PythonEngine`
   - **Runtime**: Docker
   - **Dockerfile path**: `./Dockerfile`
   - **Health Check Path**: `/health`
4. **Environment Variables** (set in Render dashboard — never in git):
   - `GOOGLE_API_KEY` = your Gemini key
   - `GROQ_API_KEY` = your Groq key
5. Deploy → note your URL: `https://ai-music-python-engine-XXXX.onrender.com`
6. Verify: `https://ai-music-python-engine-XXXX.onrender.com/docs`

> The `render.yaml` at the repo root pre-configures both services for Render.
> After connecting the repo in Render, use **"Use render.yaml"** to apply it.

### B. PythonEngine on Azure VM (alternative)

1. Create an Ubuntu VM on Azure, open inbound port 8000
2. SSH in, clone repo, follow the local Python setup steps above
3. Run persistently with systemd or screen:
   ```bash
   nohup uvicorn main:app --host 0.0.0.0 --port 8000 &
   ```
4. External URL: `http://<vm-public-ip>:8000`

### C. AiMusicWorkstation.Api on Render

1. **New** → **Web Service** → same repo
2. Settings:
   - **Root Directory**: `.` (repo root)
   - **Runtime**: Docker
   - **Dockerfile path**: `AiMusicWorkstation.Api/Dockerfile`
   - **Health Check Path**: `/health`
3. **Environment Variables**:
   - `DATABASE_URL` = your Supabase postgresql:// URI
   - `PythonEngine__BaseUrl` = `https://ai-music-python-engine-XXXX.onrender.com/`
4. Deploy → note your URL: `https://ai-music-api-XXXX.onrender.com`
5. Verify: `https://ai-music-api-XXXX.onrender.com/scalar/v1`

### D. AiMusicWorkstation.Api via Docker (any host)

```bash
# Build (from repo root)
docker build -t ai-music-api -f AiMusicWorkstation.Api/Dockerfile .

# Run
docker run -p 8080:8080 \
  -e DATABASE_URL="postgresql://user:pass@host:5432/db" \
  -e PythonEngine__BaseUrl="https://your-python-engine.onrender.com/" \
  ai-music-api
```

### E. React Frontend on Vercel

1. Go to [vercel.com](https://vercel.com) → **New Project** → import `benji9812/Ai-Music-Workstation`
2. Settings:
   - **Framework Preset**: Vite
   - **Root Directory**: `AiMusicWorkstation.Web`
3. **Environment Variables** (in Vercel project settings):
   - `VITE_API_URL` = `https://ai-music-api-XXXX.onrender.com`
4. Deploy → `https://ai-music-workstation.vercel.app`

> Vercel auto-deploys on every push to the configured branch.
> No GitHub Actions step is needed for Vercel deploys.

---

## 3. Database (Supabase)

1. Create a project at [supabase.com](https://supabase.com)
2. Copy the connection string from **Settings → Database → Connection string → URI**
3. Supply it to the API via `DATABASE_URL` environment variable
4. Run EF Core migrations:
   ```bash
   cd AiMusicWorkstation.Api
   dotnet ef database update
   ```

---

## 4. Required Secrets Summary

| Secret | Where to set | Description |
|--------|-------------|-------------|
| `GOOGLE_API_KEY` | Render/Azure env var | Google Gemini API key |
| `GROQ_API_KEY` | Render/Azure env var | Groq Whisper API key |
| `DATABASE_URL` | Render/Azure env var | Supabase PostgreSQL URI |
| `PythonEngine__BaseUrl` | Render/Azure env var | URL of deployed Python backend |
| `VITE_API_URL` | Vercel env var | URL of deployed API gateway |

> All secrets are configured in the cloud dashboard — **nothing secret is ever committed to git**.
> Use the `.env.example` files in `PythonEngine/` and `AiMusicWorkstation.Web/` as templates.

---

## 5. GitHub Actions

- **`ci.yml`**: Builds and tests the .NET solution on every push/PR to `dev`/`main`
- **`web-deploy.yml`**: Documents the Vercel watch path (`AiMusicWorkstation.Web/**`); Vercel handles actual deploys

---

## 6. Sharing with Teammates

- Frontend (Vercel): `https://ai-music-workstation.vercel.app`
- API docs (Scalar): `https://ai-music-api-XXXX.onrender.com/scalar/v1`
- Python docs (Swagger): `https://ai-music-python-engine-XXXX.onrender.com/docs`
- For local setup: share this file and `README.md`
