import uvicorn
from dotenv import load_dotenv
from pathlib import Path
from functools import lru_cache
from contextlib import asynccontextmanager
import os
import re
import time
import json
import shutil
import warnings
import sys
import subprocess
import traceback
import uuid

import librosa
import numpy as np
import soundfile as sf

from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel

warnings.filterwarnings("ignore")

BASE_DIR = Path(__file__).resolve().parent
# Load environment variables from both root and local .env if available
load_dotenv(dotenv_path=BASE_DIR.parent / ".env")
load_dotenv(dotenv_path=BASE_DIR / ".env")

UPLOAD_DIR = os.path.join(BASE_DIR, "temp_uploads")
OUT_DIR = os.path.join(BASE_DIR, "separated")
os.makedirs(UPLOAD_DIR, exist_ok=True)
os.makedirs(OUT_DIR, exist_ok=True)
os.environ["PATH"] += os.pathsep + str(BASE_DIR)

NOTES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B']
DEMUCS_MODEL = "htdemucs"
DEMUCS_TIMEOUT_SECONDS = 900

AUDIO_LOAD_SR = 22050
AUDIO_LOAD_MONO = True
AUDIO_ANALYZE_DURATION = 45
AUDIO_CHORDS_DURATION = 45
WHISPER_SR = 16000

# --- CORS middleware for web frontend support ---
origins = [
    "https://ai-music-workstation.vercel.app",
    "http://localhost:5173",
]

@asynccontextmanager
async def lifespan(app: FastAPI):
    ensure_runtime_dirs()
    print("🚀 AI Music Engine starting up...", flush=True)
    print(f"📁 BASE_DIR: {BASE_DIR}", flush=True)
    print(f"📁 UPLOAD_DIR: {UPLOAD_DIR}", flush=True)
    print(f"📁 OUT_DIR: {OUT_DIR}", flush=True)
    print(f"🔑 GOOGLE_API_KEY present: {bool(os.getenv('GOOGLE_API_KEY'))}", flush=True)
    print(f"🔑 GROQ_API_KEY present: {bool(os.getenv('GROQ_API_KEY'))}", flush=True)
    yield
    print("🛑 AI Music Engine shutting down...", flush=True)

app = FastAPI(title="AI Music Engine", lifespan=lifespan)
app.add_middleware(
    CORSMiddleware,
    allow_origins=origins,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.mount("/audio", StaticFiles(directory=OUT_DIR), name="audio")

def ensure_runtime_dirs():
    os.makedirs(UPLOAD_DIR, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)

def now():
    return time.time()

def elapsed(start):
    return round(time.time() - start, 2)

def log_step(message: str):
    print(message, flush=True)

def sanitize_filename(filename: str) -> str:
    if not filename:
        filename = "upload.mp3"
    filename = os.path.basename(filename)
    filename = re.sub(r"[^A-Za-z0-9._-]", "_", filename)
    name, ext = os.path.splitext(filename)
    if not ext:
        ext = ".mp3"
    unique = uuid.uuid4().hex[:8]
    return f"{name}_{unique}{ext}"

def cleanup_file(path: str):
    try:
        if path and os.path.exists(path):
            os.remove(path)
    except Exception:
        pass

def require_file(path: str, label: str):
    if not os.path.exists(path):
        raise RuntimeError(f"{label} saknas: {path}")

@app.get("/health")
async def health():
    return {"status": "ok"}

@app.get("/debug/env")
async def debug_env():
    return {
        "status": "ok",
        "google_api_key_present": bool(os.getenv("GOOGLE_API_KEY")),
        "groq_api_key_present": bool(os.getenv("GROQ_API_KEY")),
        "base_dir": str(BASE_DIR),
        "upload_dir_exists": os.path.exists(UPLOAD_DIR),
        "out_dir_exists": os.path.exists(OUT_DIR),
        "audio_load_sr": AUDIO_LOAD_SR,
        "audio_analyze_duration": AUDIO_ANALYZE_DURATION,
        "audio_chords_duration": AUDIO_CHORDS_DURATION
    }

@lru_cache(maxsize=1)
def get_gemini_client():
    from google import genai as google_genai
    api_key = os.getenv("GOOGLE_API_KEY")
    if not api_key:
        raise RuntimeError("GOOGLE_API_KEY saknas.")
    return google_genai.Client(api_key=api_key)

@lru_cache(maxsize=1)
def get_groq_client():
    from groq import Groq
    api_key = os.getenv("GROQ_API_KEY")
    if not api_key:
        raise RuntimeError("GROQ_API_KEY saknas.")
    log_step("✅ Groq Whisper Large v3 redo")
    return Groq(api_key=api_key)

def prepare_vocals_for_whisper(vocals_path: str):
    try:
        y, sr = librosa.load(
            vocals_path,
            sr=WHISPER_SR,
            mono=True,
            duration=min(AUDIO_ANALYZE_DURATION, 30)
        )
        y = y / (np.max(np.abs(y)) + 1e-6)
        temp_path = vocals_path.replace(".mp3", "_clean.wav")
        sf.write(temp_path, y, sr)
        return temp_path
    except Exception as e:
        log_step(f"⚠️ prepare_vocals_for_whisper fallback: {e}")
        return vocals_path

def transcribe_with_groq(audio_path: str):
    groq_client = get_groq_client()
    with open(audio_path, "rb") as f:
        transcription = groq_client.audio.transcriptions.create(
            file=(os.path.basename(audio_path), f),
            model="whisper-large-v3",
            response_format="verbose_json",
            language="en"
        )
    segments = []
    if hasattr(transcription, "segments") and transcription.segments:
        for s in transcription.segments:
            segments.append({
                "start": s.start,
                "end": s.end,
                "text": s.text.strip()
            })
    else:
        segments.append({
            "start": 0.0,
            "end": 0.0,
            "text": transcription.text.strip() if hasattr(transcription, "text") else ""
        })
    return segments

def detect_bpm_robust(y, sr):
    duration = librosa.get_duration(y=y, sr=sr)
    segments = [
        y[int(sr * duration * 0.10): int(sr * duration * 0.35)],
        y[int(sr * duration * 0.35): int(sr * duration * 0.65)],
        y[int(sr * duration * 0.65): int(sr * duration * 0.90)],
    ]
    bpms = []
    for seg in segments:
        if len(seg) > sr * 5:
            t, _ = librosa.beat.beat_track(y=seg, sr=sr)
            bpms.append(float(np.atleast_1d(t)[0]))
    if not bpms:
        t, _ = librosa.beat.beat_track(y=y, sr=sr)
        bpm = float(np.atleast_1d(t)[0])
    else:
        bpm = float(np.median(bpms))
    while bpm > 140:
        bpm /= 2
    while bpm < 60:
        bpm *= 2
    return round(bpm, 1)

def detect_time_signature(y, sr, bpm):
    try:
        hop_length = 512
        onset_env = librosa.onset.onset_strength(y=y, sr=sr, hop_length=hop_length)
        ac = librosa.autocorrelate(onset_env, max_size=len(onset_env) // 2)
        beat_frames = int(round(60.0 * sr / (bpm * hop_length)))
        score_3 = float(ac[beat_frames * 3]) if beat_frames * 3 < len(ac) else 0
        score_4 = float(ac[beat_frames * 4]) if beat_frames * 4 < len(ac) else 0
        score_6 = float(ac[beat_frames * 6]) if beat_frames * 6 < len(ac) else 0
        best = max(score_3, score_4, score_6)
        margin = 0.15
        if best == score_3 and score_3 > score_4 * (1 + margin):
            return 3
        if best == score_6 and score_6 > score_4 * (1 + margin):
            return 6
        return 4
    except Exception as e:
        log_step(f"⚠️ detect_time_signature fallback to 4/4: {e}")
        return 4

def detect_key(y, sr):
    y_harmonic = librosa.effects.harmonic(y, margin=4)
    chroma = librosa.feature.chroma_cqt(y=y_harmonic, sr=sr)
    chroma_avg = np.mean(chroma, axis=1)
    major_profile = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88]
    minor_profile = [6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17]
    def correlations(profile):
        return [np.corrcoef(chroma_avg, np.roll(profile, i))[0, 1] for i in range(12)]
    major_corrs = correlations(major_profile)
    minor_corrs = correlations(minor_profile)
    if max(major_corrs) > max(minor_corrs):
        return NOTES[np.argmax(major_corrs)]
    return NOTES[np.argmax(minor_corrs)] + "m"

def get_chords(file_path: str):
    y, sr = librosa.load(
        file_path,
        sr=AUDIO_LOAD_SR,
        mono=AUDIO_LOAD_MONO,
        duration=AUDIO_CHORDS_DURATION
    )
    y_harmonic = librosa.effects.harmonic(y, margin=4)
    chroma = librosa.feature.chroma_cqt(
        y=y_harmonic,
        sr=sr,
        hop_length=512,
        bins_per_octave=36
    )
    def classify_chord(c):
        best_score = -1
        best_chord = "C"
        for root in range(12):
            candidates = {
                NOTES[root]: c[root % 12] + c[(root + 4) % 12] + c[(root + 7) % 12],
                NOTES[root] + "m": c[root % 12] + c[(root + 3) % 12] + c[(root + 7) % 12],
                NOTES[root] + "7": c[root % 12] + c[(root + 4) % 12] + c[(root + 7) % 12] + c[(root + 10) % 12] * 0.8,
                NOTES[root] + "maj7": c[root % 12] + c[(root + 4) % 12] + c[(root + 7) % 12] + c[(root + 11) % 12] * 0.8,
                NOTES[root] + "m7": c[root % 12] + c[(root + 3) % 12] + c[(root + 7) % 12] + c[(root + 10) % 12] * 0.8,
                NOTES[root] + "sus2": c[root % 12] + c[(root + 2) % 12] + c[(root + 7) % 12],
                NOTES[root] + "sus4": c[root % 12] + c[(root + 5) % 12] + c[(root + 7) % 12],
            }
            for chord_name, score in candidates.items():
                if score > best_score:
                    best_score = score
                    best_chord = chord_name
        return best_chord
    chords = []
    step = max(1, int(2.0 * sr / 512))
    prev_chord = None
    for i in range(0, chroma.shape[1], step):
        chord = classify_chord(chroma[:, i])
        timestamp = float(i * 512 / sr)
        if chord != prev_chord:
            chords.append({"time": timestamp, "chord": chord})
            prev_chord = chord
    return chords

def run_demucs(file_path: str):
    cmd = [
        sys.executable,
        "-m",
        "demucs",
        "-n",
        DEMUCS_MODEL,
        "--mp3",
        "-o",
        OUT_DIR,
        file_path,
    ]
    log_step(f"🎚️ Running Demucs: {' '.join(cmd)}")
    start = now()
    result = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        check=True,
        timeout=DEMUCS_TIMEOUT_SECONDS
    )
    log_step(f"✅ Demucs klart på {elapsed(start)}s")
    if result.stdout:
        log_step("📤 Demucs stdout:")
        log_step(result.stdout)
    if result.stderr:
        log_step("📥 Demucs stderr:")
        log_step(result.stderr)
    folder_name = os.path.splitext(os.path.basename(file_path))[0]
    stems_folder = os.path.join(OUT_DIR, DEMUCS_MODEL, folder_name)
    drums_path = os.path.join(stems_folder, "drums.mp3")
    bass_path = os.path.join(stems_folder, "bass.mp3")
    other_path = os.path.join(stems_folder, "other.mp3")
    vocals_path = os.path.join(stems_folder, "vocals.mp3")
    require_file(drums_path, "drums.mp3")
    require_file(bass_path, "bass.mp3")
    require_file(other_path, "other.mp3")
    require_file(vocals_path, "vocals.mp3")
    return stems_folder, drums_path, bass_path, other_path, vocals_path

@app.post("/analyze-only")
async def analyze_only(file: UploadFile = File(...)):
    total_start = now()
    file_path = None
    try:
        ensure_runtime_dirs()
        if not file.filename:
            raise HTTPException(status_code=400, detail="Ingen fil skickades.")
        log_step("📥 /analyze-only request mottagen")
        safe_filename = sanitize_filename(file.filename)
        file_path = os.path.join(UPLOAD_DIR, safe_filename)
        # Limit file size to prevent OOM in cloud
        contents = await file.read()
        if len(contents) > 6 * 1024 * 1024:
            log_step("❌ File too large for analyze-only (max 6MB)")
            raise HTTPException(status_code=413, detail="File too large (max 6MB allowed on free hosting).")
        with open(file_path, "wb") as f:
            f.write(contents)
        log_step(f"💾 Fil sparad: {file_path} ({elapsed(total_start)}s)")
        load_start = now()
        y_audio, sr = librosa.load(
            file_path,
            sr=AUDIO_LOAD_SR,
            mono=AUDIO_LOAD_MONO,
            duration=AUDIO_ANALYZE_DURATION
        )
        log_step(f"🎵 Audio laddad ({elapsed(load_start)}s), samples={len(y_audio)}, sr={sr}")
        bpm_start = now()
        bpm = detect_bpm_robust(y_audio, sr)
        time_signature = detect_time_signature(y_audio, sr, bpm)
        log_step(f"🥁 BPM: {bpm}, Taktart: {time_signature}/4 ({elapsed(bpm_start)}s)")
        key_start = now()
        key = detect_key(y_audio, sr)
        log_step(f"🎹 Tonart: {key} ({elapsed(key_start)}s)")
        chords_start = now()
        chords = get_chords(file_path)
        log_step(f"🎸 Ackord: {len(chords)} detekterade ({elapsed(chords_start)}s)")
        log_step(f"✅ /analyze-only klar på {elapsed(total_start)}s")
        return {
            "status": "success",
            "bpm": bpm,
            "key": key,
            "time_signature": time_signature,
            "chords": chords,
            "lyrics": [],
            "stems_path": "",
            "original_path": file_path,
            "duration_seconds": elapsed(total_start),
            "analysis_window_seconds": AUDIO_ANALYZE_DURATION
        }
    except HTTPException:
        raise
    except Exception as e:
        print("\n============== Traceback ==============")
        print(traceback.format_exc())
        print("============== End Traceback ==============")
        raise HTTPException(status_code=500, detail=f"Analyze-only failed: {str(e)}")
    finally:
        try:
            await file.close()
        except Exception:
            pass


@app.post("/analyze")
async def analyze(file: UploadFile = File(...)):
    """Full analysis: Demucs stem separation + Groq Whisper lyrics + BPM/key/chords."""
    total_start = now()
    file_path = None
    stems_folder = None
    try:
        ensure_runtime_dirs()
        if not file.filename:
            raise HTTPException(status_code=400, detail="Ingen fil skickades.")
        log_step("📥 /analyze request mottagen")
        safe_filename = sanitize_filename(file.filename)
        file_path = os.path.join(UPLOAD_DIR, safe_filename)
        contents = await file.read()
        with open(file_path, "wb") as f:
            f.write(contents)
        log_step(f"💾 Fil sparad: {file_path} ({elapsed(total_start)}s)")

        load_start = now()
        y_audio, sr = librosa.load(
            file_path,
            sr=AUDIO_LOAD_SR,
            mono=AUDIO_LOAD_MONO,
            duration=AUDIO_ANALYZE_DURATION
        )
        log_step(f"🎵 Audio laddad ({elapsed(load_start)}s)")

        bpm = detect_bpm_robust(y_audio, sr)
        time_signature = detect_time_signature(y_audio, sr, bpm)
        log_step(f"🥁 BPM: {bpm}, Taktart: {time_signature}/4")

        key = detect_key(y_audio, sr)
        log_step(f"🎹 Tonart: {key}")

        chords = get_chords(file_path)
        log_step(f"🎸 Ackord: {len(chords)} detekterade")

        demucs_start = now()
        stems_folder, drums_path, bass_path, other_path, vocals_path = run_demucs(file_path)
        log_step(f"🎚️ Demucs klart ({elapsed(demucs_start)}s)")

        lyrics = []
        try:
            clean_vocals = prepare_vocals_for_whisper(vocals_path)
            lyrics = transcribe_with_groq(clean_vocals)
            if clean_vocals != vocals_path:
                cleanup_file(clean_vocals)
            log_step(f"🗣️ Lyrics transkriberade: {len(lyrics)} segment")
        except Exception as e:
            log_step(f"⚠️ Lyrics-transkription misslyckades: {e}")

        log_step(f"✅ /analyze klar på {elapsed(total_start)}s")
        return {
            "status": "success",
            "bpm": bpm,
            "key": key,
            "time_signature": time_signature,
            "chords": chords,
            "lyrics": lyrics,
            "stems_path": stems_folder or "",
            "original_path": file_path,
            "duration_seconds": elapsed(total_start),
            "analysis_window_seconds": AUDIO_ANALYZE_DURATION
        }
    except HTTPException:
        raise
    except Exception as e:
        print("\n============== Traceback ==============")
        print(traceback.format_exc())
        print("============== End Traceback ==============")
        raise HTTPException(status_code=500, detail=f"Analyze failed: {str(e)}")
    finally:
        try:
            await file.close()
        except Exception:
            pass


class StructureRequest(BaseModel):
    artist: str
    title: str
    duration: float


@app.post("/structure")
async def structure(request: StructureRequest):
    """Song structure analysis via Gemini 2.5 Flash."""
    try:
        gemini = get_gemini_client()
        prompt = (
            f"Analyze the song structure of '{request.title}' by '{request.artist}' "
            f"(duration: {request.duration:.1f} seconds). "
            "Return a JSON array of sections with fields: "
            "'label' (e.g. Intro, Verse, Chorus, Bridge, Outro), "
            "'start' (seconds, float), 'end' (seconds, float). "
            "Only return valid JSON, no extra text."
        )
        response = gemini.models.generate_content(
            model="gemini-2.5-flash",
            contents=prompt
        )
        raw = response.text.strip()
        # Strip markdown code fences if present
        if raw.startswith("```"):
            raw = re.sub(r"^```[a-z]*\n?", "", raw)
            raw = re.sub(r"\n?```$", "", raw)
        sections = json.loads(raw)
        return {"status": "success", "sections": sections}
    except Exception as e:
        log_step(f"⚠️ /structure misslyckades: {e}")
        raise HTTPException(status_code=500, detail=f"Structure analysis failed: {str(e)}")

class ImportUrlRequest(BaseModel):
    url: str

@app.post("/import-url")
async def import_url(request: ImportUrlRequest):
    """Download audio from YouTube/Spotify URL via yt-dlp, then run full analysis."""
    total_start = now()
    file_path = None
    try:
        ensure_runtime_dirs()
        log_step(f"📥 /import-url request: {request.url}")

        url = request.url
        is_spotify = False
        spotify_query = None

        if "spotify.com" in url.lower() and "/track/" in url.lower():
            is_spotify = True
            log_step("🟢 Spotify track URL detected. Fetching track metadata...")
            # Extract track ID
            track_id_match = re.search(r"track/([a-zA-Z0-9]{22})", url)
            if track_id_match:
                track_id = track_id_match.group(1)
            else:
                clean_url = url.split("?")[0]
                track_id = clean_url.split("/")[-1]
            
            # Scrape Spotify page for metadata
            try:
                import urllib.request
                from html import unescape
                req = urllib.request.Request(
                    f"https://open.spotify.com/track/{track_id}",
                    headers={"User-Agent": "Mozilla/5.0"}
                )
                with urllib.request.urlopen(req, timeout=10) as response:
                    html = response.read().decode("utf-8")
                
                title_match = re.search(r"<title>(.*?)</title>", html, re.IGNORECASE)
                if title_match:
                    page_title = unescape(title_match.group(1))
                    # Expected format: "Face Down - song and lyrics by The Red Jumpsuit Apparatus | Spotify"
                    match = re.search(r"^(.*?) - (?:song and lyrics|song|single|EP|album) by (.*?) \| Spotify$", page_title, re.IGNORECASE)
                    if match:
                        title = match.group(1).strip()
                        artist = match.group(2).strip()
                        spotify_query = f"{artist} - {title} audio"
                        log_step(f"✅ Scraped Spotify Metadata: '{artist}' - '{title}'")
                
                # Fallback to OpenGraph metadata if page title format differs
                if not spotify_query:
                    og_title = None
                    title_meta = re.search(r'<meta[^>]+property=["\']og:title["\'][^>]+content=["\'](.*?)["\']', html)
                    if not title_meta:
                        title_meta = re.search(r'<meta[^>]+content=["\'](.*?)["\'][^>]+property=["\']og:title["\']', html)
                    if title_meta:
                        og_title = unescape(title_meta.group(1))

                    og_desc = None
                    desc_meta = re.search(r'<meta[^>]+property=["\']og:description["\'][^>]+content=["\'](.*?)["\']', html)
                    if not desc_meta:
                        desc_meta = re.search(r'<meta[^>]+content=["\'](.*?)["\'][^>]+property=["\']og:description["\']', html)
                    if desc_meta:
                        og_desc = unescape(desc_meta.group(1))

                    if og_title and og_desc:
                        parts = og_desc.split(" · ")
                        if len(parts) >= 1:
                            artist = parts[0].strip()
                            title = og_title.strip()
                            spotify_query = f"{artist} - {title} audio"
                            log_step(f"✅ Scraped Spotify OG Metadata: '{artist}' - '{title}'")
            except Exception as e:
                log_step(f"⚠️ Spotify metadata scraping failed: {e}")

            if not spotify_query:
                raise RuntimeError("Could not resolve Spotify track metadata.")

        # 1. Download with yt-dlp (run as a python module to guarantee environment safety)
        unique_id = uuid.uuid4().hex[:8]
        output_template = os.path.join(UPLOAD_DIR, f"dl_{unique_id}.%(ext)s")
        download_target = f"ytsearch1:{spotify_query}" if is_spotify else url
        cmd = [
            sys.executable,
            "-m",
            "yt_dlp",
            "--no-playlist",
            "--js-runtimes", "node",
            "-x",  # extract audio
            "--audio-format", "mp3",
            "--audio-quality", "0",
            "-o", output_template,
        ]
        
        # Check if local cookies.txt exists in PythonEngine directory to bypass YouTube bot detection on Azure VM
        cookies_path = os.path.join(BASE_DIR, "cookies.txt")
        if os.path.exists(cookies_path):
            cmd.extend(["--cookies", cookies_path])
            
        cmd.append(download_target)
        log_step(f"⬇️ Running yt-dlp: {' '.join(cmd)}")
        dl_start = now()
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=300
        )
        if result.returncode != 0:
            log_step(f"❌ yt-dlp failed: {result.stderr}")
            raise RuntimeError(f"yt-dlp failed: {result.stderr[:500]}")
        log_step(f"✅ yt-dlp done ({elapsed(dl_start)}s)")

        # Find the downloaded file
        import glob
        candidates = glob.glob(os.path.join(UPLOAD_DIR, f"dl_{unique_id}.*"))
        if not candidates:
            raise RuntimeError("yt-dlp produced no output file")
        file_path = candidates[0]
        log_step(f"📁 Downloaded file: {file_path}")

        # 2. Run full analysis (same as /analyze)
        load_start = now()
        y_audio, sr = librosa.load(
            file_path,
            sr=AUDIO_LOAD_SR,
            mono=AUDIO_LOAD_MONO,
            duration=AUDIO_ANALYZE_DURATION
        )
        log_step(f"🎵 Audio loaded ({elapsed(load_start)}s)")

        bpm = detect_bpm_robust(y_audio, sr)
        time_signature = detect_time_signature(y_audio, sr, bpm)
        log_step(f"🥁 BPM: {bpm}, Time sig: {time_signature}/4")

        key = detect_key(y_audio, sr)
        log_step(f"🎹 Key: {key}")

        chords = get_chords(file_path)
        log_step(f"🎸 Chords: {len(chords)} detected")

        demucs_start = now()
        stems_folder, drums_path, bass_path, other_path, vocals_path = run_demucs(file_path)
        log_step(f"🎚️ Demucs done ({elapsed(demucs_start)}s)")

        lyrics = []
        try:
            clean_vocals = prepare_vocals_for_whisper(vocals_path)
            lyrics = transcribe_with_groq(clean_vocals)
            if clean_vocals != vocals_path:
                cleanup_file(clean_vocals)
            log_step(f"🗣️ Lyrics: {len(lyrics)} segments")
        except Exception as e:
            log_step(f"⚠️ Lyrics transcription failed: {e}")

        # Cleanup downloaded file
        cleanup_file(file_path)

        log_step(f"✅ /import-url complete in {elapsed(total_start)}s")
        return {
            "status": "success",
            "bpm": bpm,
            "key": key,
            "time_signature": time_signature,
            "chords": chords,
            "lyrics": lyrics,
            "stems_path": stems_folder or "",
            "original_path": "",
            "duration_seconds": elapsed(total_start),
            "analysis_window_seconds": AUDIO_ANALYZE_DURATION
        }
    except Exception as e:
        print("\n============== Traceback ==============")
        print(traceback.format_exc())
        print("============== End Traceback ==============")
        if file_path:
            cleanup_file(file_path)
        raise HTTPException(status_code=500, detail=f"Import-url failed: {str(e)}")


if __name__ == "__main__":
    port = int(os.environ.get("PORT", 10000))
    uvicorn.run("main:app", host="0.0.0.0", port=port)
