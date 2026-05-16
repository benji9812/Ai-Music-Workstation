AI Music Workstation: Komplett Deploy-Manual (2026)

Gäller branch: Solution/Remodelling_To_Web
Repo: benji9812/Ai-Music-Workstation

______________________________________
1.  Lokalt - Allt från start till slut
______________________________________

A) Krav (på din maskin/VM):

Python 3.11+
.NET 10 SDK
Node.js (18+) och npm
ffmpeg, Docker (valfritt)
God internetuppkoppling

B) Klona projektet

bash
git clone https://github.com/benji9812/Ai-Music-Workstation.git
cd Ai-Music-Workstation
git checkout Solution/Remodelling_To_Web

C) Python backend

bash
cd PythonEngine
python -m venv venv
# Windows:
venv\Scripts\activate
# Mac/Linux:
source venv/bin/activate

pip install --upgrade pip
pip install -r requirements.txt

# Lägg .env i PythonEngine med:
# GOOGLE_API_KEY=...
# GROQ_API_KEY=...
# (fyll på med secrets du behöver – finns .env.example)
# Starta backend:
uvicorn main:app --host 0.0.0.0 --port 8000
Swagger-UI: http://localhost:8000/docs

D) API Gateway

bash
cd ../AiMusicWorkstation.Api
dotnet restore
dotnet build
dotnet run
# (startar ofta på https://localhost:7107 eller http://localhost:5082)
Scalar API docs: https://localhost:7107/scalar/v1

E) React/Frontend

bash
cd ../AiMusicWorkstation.Web
cp .env.example .env     # editera in rätt VITE_API_URL (från din API-proxy)
npm install
npm run dev
# http://localhost:5173

______________________
2. Deployment på Cloud
______________________

A) Python backend på Azure VM (rekommenderas)

Skapa Azure VM (Ubuntu/Windows), öppna port 8000.
Klona repo, följ steg C ovan.
Kör med screen/tmux/systemd:
bash
nohup uvicorn main:app --host 0.0.0.0 --port 8000 &
Externa anrop: http://<din-azure-vm>:8000/docs

B) API Gateway på Azure (eller Render)

Bygg Docker-image (eller hosta lokalt/hosta i cloud).
Se till att env-vars för Pythons backend (PythonEngine__BaseUrl) mappas rätt mot din backend-Vm:s IP.
Ange din databas-SUPABASE-connection-string.
Kan även deployas direkt till Azure Web App (se README).

C) Frontend på Vercel

Gå till vercel.com
“New Project” → koppla Github-repo.
Framework: Vite
Root Directory: AiMusicWorkstation.Web
Environment: VITE_API_URL=https://din-api-url
Deploy
Kopiera slut-URL och kontrollera CORS

D) Docker - Allt-i-ett
Du kan (på egen server/VM) bygga och köra båda backend och API via Docker:

bash
cd PythonEngine
docker build -t ai-python-engine .
docker run -p 8000:8000 --env-file .env ai-python-engine

cd ../AiMusicWorkstation.Api
docker build -t ai-music-api .
docker run -p 8080:8080 \
  -e DATABASE_URL=... \
  -e PythonEngine__BaseUrl=http://ai-python-engine:8000/ \
  ai-music-api

_________________________________
3.  Secrets/Filer att skapa själv
_________________________________

.env.example finns i varje huvud-mapp med alla "public" env-vars.
Alltid skapa egen .env – lägg aldrig hemliga nycklar i git!

____________
4.  Workflow
____________

Cloud deploy på Vercel = automatisk
GitHub Actions för build/test = automatisk vid push/PR
Desktop/workflows nu ersatt/dokumenterad (se .github/workflows/)
README och AI_MUSIC_WORKSTATION_REFERENCE.md pekar alltid ut exakt deploysteg

______________________________
5.  Skicka till vänner/testare
______________________________

Länka din frontend (Vercel), API (Azure/Render), och backend (Azure VM el liknande)
De kan även köra lokalt – skicka denna manual!
All kod/index finns och är färdig för feedback
