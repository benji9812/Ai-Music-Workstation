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
from pydantic import BaseModel

warnings.filterwarnings("ignore")

BASE_DIR = Path(__file__).resolve().parent
load_dotenv(dotenv_path=BASE_DIR.parent / ".env")

UPLOAD_DIR = os.path.join(BASE_DIR, "temp_uploads")
OUT_DIR = os.path.join(BASE_DIR, "separated")
os.environ["PATH"] += os.pathsep + str(BASE_DIR)

NOTES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B']
DEMUCS_MODEL = "htdemucs"
DEMUCS_TIMEOUT_SECONDS = 900

@asynccontextmanager
async def lifespan(app: FastAPI):
    ensure_runtime_dirs()
    print("🚀 AI Music Engine starting up...")
    print(f"📁 BASE_DIR: {BASE_DIR}")
    print(f"📁 UPLOAD_DIR: {UPLOAD_DIR}")
    print(f"📁 OUT_DIR: {OUT_DIR}")
    print(f"🔑 GOOGLE_API_KEY present: {bool(os.getenv('GOOGLE_API_KEY'))}")
    print(f"🔑 GROQ_API_KEY present: {bool(os.getenv('GROQ_API_KEY'))}")
    yield
    print("🛑 AI Music Engine shutting down...")

app = FastAPI(title="AI Music Engine", lifespan=lifespan)

def ensure_runtime_dirs():
    os.makedirs(UPLOAD_DIR, exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)

def now():
    return time.time()

def elapsed(start):
    return round(time.time() - start, 2)

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
    print("✅ Groq Whisper Large v3 redo")
    return Groq(api_key=api_key)

def find_stems_folder_by_drums(drums_upload_path: str):
    uploaded_size = os.path.getsize(drums_upload_path)
    htdemucs_dir = os.path.join(OUT_DIR, DEMUCS_MODEL)
    if not os.path.exists(htdemucs_dir):
        return None

    for song_folder in os.listdir(htdemucs_dir):
        folder_path = os.path.join(htdemucs_dir, song_folder)
        candidate = os.path.join(folder_path, "drums.mp3")
        if os.path.exists(candidate) and os.path.getsize(candidate) == uploaded_size:
            return folder_path
    return None

def prepare_vocals_for_whisper(vocals_path: str):
    try:
        y, sr = librosa.load(vocals_path, sr=16000, mono=True)
        y = y / (np.max(np.abs(y)) + 1e-6)
        temp_path = vocals_path.replace(".mp3", "_clean.wav")
        sf.write(temp_path, y, sr)
        return temp_path
    except Exception as e:
        print(f"⚠️ prepare_vocals_for_whisper fallback: {e}")
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
        print(f"⚠️ detect_time_signature fallback to 4/4: {e}")
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

def get_chords(file_path):
    y, sr = librosa.load(file_path, duration=180)
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

    print(f"🎚️ Running Demucs: {' '.join(cmd)}")
    start = now()

    result = subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        check=True,
        timeout=DEMUCS_TIMEOUT_SECONDS
    )

    print(f"✅ Demucs klart på {elapsed(start)}s")
    if result.stdout:
        print("📤 Demucs stdout:")
        print(result.stdout)
    if result.stderr:
        print("📥 Demucs stderr:")
        print(result.stderr)

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

        safe_filename = sanitize_filename(file.filename)
        file_path = os.path.join(UPLOAD_DIR, safe_filename)

        save_start = now()
        with open(file_path, "wb") as f:
            shutil.copyfileobj(file.file, f)
        print(f"💾 Fil sparad: {file_path} ({elapsed(save_start)}s)")

        analyze_start = now()
        y_audio, sr = librosa.load(file_path, sr=None)
        bpm = detect_bpm_robust(y_audio, sr)
        time_signature = detect_time_signature(y_audio, sr, bpm)
        print(f"🥁 BPM: {bpm}, Taktart: {time_signature}/4 ({elapsed(analyze_start)}s)")

        key_start = now()
        key = detect_key(y_audio, sr)
        print(f"🎹 Tonart: {key} ({elapsed(key_start)}s)")

        chords_start = now()
        chords = get_chords(file_path)
        print(f"🎸 Ackord: {len(chords)} detekterade ({elapsed(chords_start)}s)")

        print(f"✅ /analyze-only klar på {elapsed(total_start)}s")

        return {
            "status": "success",
            "bpm": bpm,
            "key": key,
            "time_signature": time_signature,
            "chords": chords,
            "lyrics": [],
            "stems_path": "",
            "original_path": file_path,
            "duration_seconds": elapsed(total_start)
        }

    except HTTPException:
        raise
    except Exception as e:
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=f"Analyze-only failed: {str(e)}")
    finally:
        try:
            await file.close()
        except Exception:
            pass

@app.post("/analyze")
async def analyze_audio(file: UploadFile = File(...)):
    total_start = now()
    file_path = None
    whisper_src = None

    try:
        ensure_runtime_dirs()

        if not file.filename:
            raise HTTPException(status_code=400, detail="Ingen fil skickades.")

        safe_filename = sanitize_filename(file.filename)
        file_path = os.path.join(UPLOAD_DIR, safe_filename)

        save_start = now()
        with open(file_path, "wb") as f:
            shutil.copyfileobj(file.file, f)
        print(f"💾 Fil sparad: {file_path} ({elapsed(save_start)}s)")

        demucs_start = now()
        stems_folder, drums_path, bass_path, other_path, vocals_path = run_demucs(file_path)
        print(f"🎚️ Demucs-steg klart ({elapsed(demucs_start)}s)")

        bpm_start = now()
        y_drums, sr_drums = librosa.load(drums_path, sr=None)
        bpm = detect_bpm_robust(y_drums, sr_drums)
        time_signature = detect_time_signature(y_drums, sr_drums, bpm)
        print(f"🥁 BPM: {bpm}, Taktart: {time_signature}/4 ({elapsed(bpm_start)}s)")

        key_start = now()
        y_harm, sr_harm = librosa.load(bass_path, sr=None)
        key = detect_key(y_harm, sr_harm)
        print(f"🎹 Tonart: {key} ({elapsed(key_start)}s)")

        chords_start = now()
        chords = get_chords(other_path)
        print(f"🎸 Ackord: {len(chords)} detekterade ({elapsed(chords_start)}s)")

        lyrics_start = now()
        whisper_src = prepare_vocals_for_whisper(vocals_path)
        lyrics = transcribe_with_groq(whisper_src)
        print(f"🎤 Lyrics: {len(lyrics)} segment ({elapsed(lyrics_start)}s)")

        print(f"✅ /analyze klar på {elapsed(total_start)}s")

        return {
            "status": "success",
            "bpm": bpm,
            "key": key,
            "time_signature": time_signature,
            "lyrics": lyrics,
            "chords": chords,
            "stems_path": stems_folder,
            "original_path": file_path,
            "duration_seconds": elapsed(total_start)
        }

    except subprocess.TimeoutExpired as e:
        traceback.print_exc()
        raise HTTPException(status_code=504, detail=f"Demucs timeout efter {DEMUCS_TIMEOUT_SECONDS}s")
    except subprocess.CalledProcessError as e:
        print("❌ Demucs process failed")
        print(f"Return code: {e.returncode}")
        print(f"STDOUT:\n{e.stdout}")
        print(f"STDERR:\n{e.stderr}")
        traceback.print_exc()
        raise HTTPException(status_code=500, detail="Demucs failed. Kontrollera Render-loggarna.")
    except HTTPException:
        raise
    except Exception as e:
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=f"Analyze failed: {str(e)}")
    finally:
        try:
            await file.close()
        except Exception:
            pass

        if whisper_src and whisper_src.endswith("_clean.wav"):
            cleanup_file(whisper_src)

class StructureRequest(BaseModel):
    artist: str
    title: str
    duration: float

@app.post("/structure")
async def get_structure(req: StructureRequest):
    try:
        gemini_client = get_gemini_client()

        prompt = (
            f"You are a music analyst. List the ACTUAL song structure for '{req.artist} - {req.title}' "
            f"with total duration {int(req.duration)} seconds.\n\n"
            "STRICT RULES:\n"
            "- Only include sections that ACTUALLY EXIST in this specific song\n"
            "- Do NOT add Bridge, Solo or Instrumental unless they genuinely appear\n"
            "- Timestamps must be realistic and span the full duration evenly\n"
            "- Most pop/rock songs follow: Intro → Verse → Pre → Chorus → Verse → Pre → Chorus → Bridge/Solo → Chorus → Outro\n"
            "- Return ONLY a raw JSON array, no markdown, no explanation\n\n"
            "Format: [{\"label\": \"Intro\", \"start\": 0}, {\"label\": \"Verse\", \"start\": 14}]\n"
            "Allowed labels: Intro, Verse, Pre, Chorus, Bridge, Solo, Instrumental, Outro, Break\n"
            "First section must start at 0. All start times are integers in seconds."
        )

        response = gemini_client.models.generate_content(
            model="gemini-2.5-flash",
            contents=prompt
        )

        raw = response.text.strip()
        start_idx = raw.find("[")
        end_idx = raw.rfind("]") + 1

        if start_idx == -1 or end_idx == 0:
            raise HTTPException(status_code=500, detail="No JSON in Gemini response")

        sections = json.loads(raw[start_idx:end_idx])

        result = []
        for i, sec in enumerate(sections):
            end_time = sections[i + 1]["start"] if i + 1 < len(sections) else req.duration
            result.append({
                "label": sec["label"],
                "start": float(sec["start"]),
                "end": float(end_time),
                "color": ""
            })

        return {
            "status": "success",
            "sections": result
        }

    except HTTPException:
        raise
    except Exception as e:
        traceback.print_exc()
        raise HTTPException(status_code=500, detail=f"Structure failed: {str(e)}")

if __name__ == "__main__":
    port = int(os.environ.get("PORT", 8000))
    uvicorn.run("main:app", host="0.0.0.0", port=port)
