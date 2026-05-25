import json
import os
import re
import subprocess
import sys
import threading
import time
import traceback
import uuid
import warnings
from contextlib import asynccontextmanager
from functools import lru_cache
from pathlib import Path
from typing import List, Optional

import librosa
import numpy as np
import soundfile as sf
import uvicorn
from dotenv import load_dotenv
from fastapi import FastAPI, File, HTTPException, UploadFile
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

NOTES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "Bb", "B"]
DEMUCS_MODEL = "htdemucs"
DEMUCS_TIMEOUT_SECONDS = 900
TARGET_AUDIO_SR = 44100

AUDIO_LOAD_SR = 22050
AUDIO_LOAD_MONO = True
AUDIO_ANALYZE_DURATION = None
AUDIO_CHORDS_DURATION = None
WHISPER_SR = 16000

COMMON_FALSE_POSITIVES = {
    "you",
    "i",
    "the",
    "a",
    "it",
    "me",
    "my",
    "in",
    "to",
    "is",
    "on",
    "that",
    "this",
    "for",
}

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

# --- Background job tracking ---
jobs: dict = {}  # job_id -> { status, stage, progress, result, error }


def job_update(job_id: str, stage: str, progress: int = -1, status: str = "running"):
    if job_id in jobs:
        jobs[job_id]["stage"] = stage
        jobs[job_id]["status"] = status
        if progress >= 0:
            jobs[job_id]["progress"] = progress
        print(f"[job:{job_id}] {stage}", flush=True)


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


def resample_mp3_to_target_sr(path: str, target_sr: int = TARGET_AUDIO_SR):
    temp_path = f"{path}.tmp.mp3"
    cmd = [
        "ffmpeg",
        "-y",
        "-i",
        path,
        "-ar",
        str(target_sr),
        "-ac",
        "2",
        "-codec:a",
        "libmp3lame",
        "-q:a",
        "2",
        temp_path,
    ]
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
        if result.returncode != 0:
            log_step(f"⚠️ ffmpeg resample failed: {result.stderr[:200]}")
            raise RuntimeError("ffmpeg resample failed")
        os.replace(temp_path, path)
        log_step(f"✅ Resampled to {target_sr} Hz: {os.path.basename(path)}")
        return
    except FileNotFoundError:
        log_step("⚠️ ffmpeg not found; falling back to librosa")
    except Exception as e:
        log_step(f"⚠️ ffmpeg resample error: {e}")
    finally:
        cleanup_file(temp_path)

    temp_wav = f"{path}.tmp.wav"
    temp_mp3 = f"{path}.tmp_fallback.mp3"
    try:
        y, _ = librosa.load(path, sr=target_sr, mono=False)
        if y.ndim == 1:
            y = np.expand_dims(y, axis=0)
        y = np.transpose(y, (1, 0))
        sf.write(temp_wav, y, target_sr)
        fallback_cmd = [
            "ffmpeg",
            "-y",
            "-i",
            temp_wav,
            "-ar",
            str(target_sr),
            "-ac",
            "2",
            "-codec:a",
            "libmp3lame",
            "-q:a",
            "2",
            temp_mp3,
        ]
        result = subprocess.run(
            fallback_cmd, capture_output=True, text=True, timeout=120
        )
        if result.returncode != 0:
            log_step(f"⚠️ librosa fallback encode failed: {result.stderr[:200]}")
            return
        os.replace(temp_mp3, path)
        log_step(
            f"✅ Resampled (librosa fallback) to {target_sr} Hz: {os.path.basename(path)}"
        )
    except FileNotFoundError:
        log_step("⚠️ ffmpeg not found for fallback encode; skipping resample")
    except Exception as e:
        log_step(f"⚠️ librosa fallback error: {e}")
    finally:
        cleanup_file(temp_wav)
        cleanup_file(temp_mp3)


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
        "audio_chords_duration": AUDIO_CHORDS_DURATION,
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
            duration=AUDIO_ANALYZE_DURATION,
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
            language="en",
        )
    segments = []

    # Robustly convert response to dict/object access
    if isinstance(transcription, dict):
        raw_segments = transcription.get("segments")
        raw_text = transcription.get("text", "")
    else:
        raw_segments = getattr(transcription, "segments", None)
        raw_text = getattr(transcription, "text", "")

    if raw_segments:
        for s in raw_segments:
            if isinstance(s, dict):
                start = s.get("start", 0.0)
                end = s.get("end", 0.0)
                text = s.get("text", "").strip()
            else:
                start = getattr(s, "start", 0.0)
                end = getattr(s, "end", 0.0)
                text = getattr(s, "text", "").strip()
            segments.append({"start": start, "end": end, "text": text})
    else:
        segments.append(
            {"start": 0.0, "end": 0.0, "text": raw_text.strip() if raw_text else ""}
        )
    return segments


def post_process_lyrics(lyrics: List[dict], audio_duration: float):
    """Validate and clean lyrics."""
    if not lyrics:
        return []

    # a. Remove lines where text is a single common false-positive word
    processed = []
    for seg in lyrics:
        text = seg["text"].strip().lower()
        if text in COMMON_FALSE_POSITIVES or not text:
            continue
        processed.append(seg)

    if not processed:
        return []

    # b. Ensure first lyric line starts at or before first vocal occurrence (handled by sorted start times)
    processed.sort(key=lambda x: x["start"])

    # c. Trim trailing lines after song effectively ended (last 2 seconds)
    max_time = audio_duration - 2.0
    processed = [s for s in processed if s["start"] < max_time]

    return processed


def detect_bpm_robust(y, sr):
    duration = librosa.get_duration(y=y, sr=sr)
    segments = [
        y[int(sr * duration * 0.10) : int(sr * duration * 0.35)],
        y[int(sr * duration * 0.35) : int(sr * duration * 0.65)],
        y[int(sr * duration * 0.65) : int(sr * duration * 0.90)],
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
    major_profile = [
        6.35,
        2.23,
        3.48,
        2.33,
        4.38,
        4.09,
        2.52,
        5.19,
        2.39,
        3.66,
        2.29,
        2.88,
    ]
    minor_profile = [
        6.33,
        2.68,
        3.52,
        5.38,
        2.60,
        3.53,
        2.54,
        4.75,
        3.98,
        2.69,
        3.34,
        3.17,
    ]

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
        duration=AUDIO_CHORDS_DURATION,
    )
    y_harmonic = librosa.effects.harmonic(y, margin=4)
    chroma = librosa.feature.chroma_cqt(
        y=y_harmonic, sr=sr, hop_length=512, bins_per_octave=36
    )

    def classify_chord(c):
        best_score = -1
        best_chord = "C"
        for root in range(12):
            candidates = {
                NOTES[root]: c[root % 12] + c[(root + 4) % 12] + c[(root + 7) % 12],
                NOTES[root] + "m": c[root % 12]
                + c[(root + 3) % 12]
                + c[(root + 7) % 12],
                NOTES[root] + "7": c[root % 12]
                + c[(root + 4) % 12]
                + c[(root + 7) % 12]
                + c[(root + 10) % 12] * 0.8,
                NOTES[root] + "maj7": c[root % 12]
                + c[(root + 4) % 12]
                + c[(root + 7) % 12]
                + c[(root + 11) % 12] * 0.8,
                NOTES[root] + "m7": c[root % 12]
                + c[(root + 3) % 12]
                + c[(root + 7) % 12]
                + c[(root + 10) % 12] * 0.8,
                NOTES[root] + "sus2": c[root % 12]
                + c[(root + 2) % 12]
                + c[(root + 7) % 12],
                NOTES[root] + "sus4": c[root % 12]
                + c[(root + 5) % 12]
                + c[(root + 7) % 12],
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


def run_demucs(file_path: str, job_id: Optional[str] = None):
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

    process = subprocess.Popen(
        cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1
    )

    stderr_lines = []
    assert process.stdout is not None
    for line in process.stdout:
        line = line.rstrip()
        stderr_lines.append(line)
        log_step(line)
        # Parse tqdm progress: looks like " 45%|████ | 86.8/193.0 [...]"
        pct_match = re.search(r"(\d{1,3})%\|", line)
        if pct_match and job_id:
            pct = int(pct_match.group(1))
            job_update(job_id, f"🎚️ Separating stems... {pct}%", progress=pct)

    process.wait(timeout=DEMUCS_TIMEOUT_SECONDS)
    if process.returncode != 0:
        raise subprocess.CalledProcessError(
            process.returncode, cmd, "\n".join(stderr_lines)
        )

    log_step(f"✅ Demucs klart på {elapsed(start)}s")
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
    resample_mp3_to_target_sr(drums_path)
    resample_mp3_to_target_sr(bass_path)
    resample_mp3_to_target_sr(other_path)
    resample_mp3_to_target_sr(vocals_path)
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
            raise HTTPException(
                status_code=413,
                detail="File too large (max 6MB allowed on free hosting).",
            )
        with open(file_path, "wb") as f:
            f.write(contents)
        log_step(f"💾 Fil sparad: {file_path} ({elapsed(total_start)}s)")
        load_start = now()
        y_audio, sr = librosa.load(
            file_path,
            sr=AUDIO_LOAD_SR,
            mono=AUDIO_LOAD_MONO,
            duration=AUDIO_ANALYZE_DURATION,
        )
        log_step(
            f"🎵 Audio laddad ({elapsed(load_start)}s), samples={len(y_audio)}, sr={sr}"
        )
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
            "analysis_window_seconds": AUDIO_ANALYZE_DURATION,
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
async def analyze(
    file: UploadFile = File(...),
    title: Optional[str] = None,
    artist: Optional[str] = None,
):
    """Full analysis: Demucs stem separation + Whisper lyrics + BPM/key/chords."""
    total_start = now()
    file_path = None
    stems_folder = None
    try:
        ensure_runtime_dirs()
        if not file.filename:
            raise HTTPException(status_code=400, detail="Ingen fil skickades.")
        log_step(f"📥 /analyze request mottagen (title={title}, artist={artist})")
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
            duration=AUDIO_ANALYZE_DURATION,
        )
        audio_duration = float(librosa.get_duration(y=y_audio, sr=sr))
        log_step(
            f"🎵 Audio loaded ({elapsed(load_start)}s), duration: {audio_duration:.2f}s"
        )

        bpm = detect_bpm_robust(y_audio, sr)
        time_signature = detect_time_signature(y_audio, sr, bpm)
        log_step(f"🥁 BPM: {bpm}, Taktart: {time_signature}/4")

        key = detect_key(y_audio, sr)
        log_step(f"🎹 Tonart: {key}")

        chords = get_chords(file_path)
        log_step(f"🎸 Ackord: {len(chords)} detekterade")

        demucs_start = now()
        stems_folder, drums_path, bass_path, other_path, vocals_path = run_demucs(
            file_path
        )
        log_step(f"🎚️ Demucs klart ({elapsed(demucs_start)}s)")

        lyrics = []
        # 1. Whisper transkription (Nu den enda källan i Python-motorn)
        try:
            clean_vocals = prepare_vocals_for_whisper(vocals_path)
            lyrics = transcribe_with_groq(clean_vocals)
            if clean_vocals != vocals_path:
                cleanup_file(clean_vocals)
            log_step(f"🗣️ Lyrics (Whisper): {len(lyrics)} segments")
        except Exception as e:
            log_step(f"⚠️ Lyrics-transkription misslyckades: {e}")

        # 2. Quality fixes
        lyrics = post_process_lyrics(lyrics, audio_duration)

        result = {
            "status": "success",
            "title": title or "",
            "artist": artist or "",
            "bpm": bpm,
            "key": key,
            "time_signature": time_signature,
            "chords": chords,
            "lyrics": lyrics,
            "stems_path": stems_folder or "",
            "original_path": file_path,
            "duration_seconds": audio_duration,
            "analysis_time_seconds": elapsed(total_start),
            "analysis_window_seconds": AUDIO_ANALYZE_DURATION,
        }

        if stems_folder and os.path.isdir(stems_folder):
            json_path = os.path.join(stems_folder, "analysis.json")
            try:
                with open(json_path, "w") as f:
                    json.dump(result, f, indent=2)
                log_step(f"💾 Analysis JSON saved to {json_path}")
            except Exception as e:
                log_step(f"⚠️ Could not save analysis JSON: {e}")

        log_step(f"✅ /analyze klar på {elapsed(total_start)}s")
        return result
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
            f"Analyze the song structure of '{request.title}' by '{request.artist}'.\n"
            f"The total audio duration is exactly {request.duration:.1f} seconds.\n"
            "Your task is to provide a complete list of song sections (e.g., Intro, Verse, Chorus, Bridge, Outro).\n"
            "CRITICAL REQUIREMENTS:\n"
            f"1. The sections MUST cover the ENTIRE duration from 0.0 to {request.duration:.1f} seconds.\n"
            "2. There must be no gaps between sections.\n"
            f"3. The last section's end time MUST be exactly {request.duration:.1f}.\n"
            "Return a JSON array of objects with fields: "
            "'label' (string), 'start' (float), 'end' (float).\n"
            "Only return valid JSON, no extra text."
        )
        response = gemini.models.generate_content(
            model="gemini-2.5-flash",
            contents=prompt,
            config={"max_output_tokens": 2048},
        )
        raw = (response.text or "").strip()
        # Strip markdown code fences if present
        if raw.startswith("```"):
            raw = re.sub(r"^```[a-z]*\n?", "", raw)
            raw = re.sub(r"\n?```$", "", raw)

        sections = json.loads(raw)

        # Post-processing to ensure full duration coverage
        if isinstance(sections, list) and len(sections) > 0:
            # Sort by start time
            sections.sort(key=lambda x: x.get("start", 0))

            # Ensure the first section starts at 0
            if sections[0].get("start", 0) > 0:
                sections[0]["start"] = 0.0

            # Ensure no gaps and logical flow
            for i in range(len(sections) - 1):
                current_end = sections[i].get("end", 0)
                next_start = sections[i + 1].get("start", 0)

                # If there's a gap, close it
                if current_end < next_start:
                    sections[i]["end"] = next_start
                # If they overlap significantly or end > next start, adjust
                elif current_end > next_start:
                    sections[i + 1]["start"] = current_end

            # Ensure it reaches the end
            last_section = sections[-1]
            if last_section.get("end", 0) < request.duration - 0.5:
                log_step(
                    f"📏 Extending last section from {last_section.get('end')} to {request.duration}"
                )
                last_section["end"] = round(request.duration, 2)
            elif last_section.get("end", 0) > request.duration + 0.5:
                last_section["end"] = round(request.duration, 2)

        return {"status": "success", "sections": sections}
    except Exception as e:
        log_step(f"⚠️ /structure failed: {e}")
        raise HTTPException(
            status_code=500, detail=f"Structure analysis failed: {str(e)}"
        )


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
        title = "Unknown Track"
        artist = "Unknown Artist"

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
                    headers={"User-Agent": "Mozilla/5.0"},
                )
                with urllib.request.urlopen(req, timeout=10) as response:
                    html = response.read().decode("utf-8")

                title_match = re.search(r"<title>(.*?)</title>", html, re.IGNORECASE)
                if title_match:
                    page_title = unescape(title_match.group(1))
                    # Expected format: "Face Down - song and lyrics by The Red Jumpsuit Apparatus | Spotify"
                    match = re.search(
                        r"^(.*?) - (?:song and lyrics|song|single|EP|album) by (.*?) \| Spotify$",
                        page_title,
                        re.IGNORECASE,
                    )
                    if match:
                        title = match.group(1).strip()
                        artist = match.group(2).strip()
                        spotify_query = f"{artist} - {title} audio"
                        log_step(f"✅ Scraped Spotify Metadata: '{artist}' - '{title}'")

                # Fallback to OpenGraph metadata if page title format differs
                if not spotify_query:
                    og_title = None
                    title_meta = re.search(
                        r'<meta[^>]+property=["\']og:title["\'][^>]+content=["\'](.*?)["\']',
                        html,
                    )
                    if not title_meta:
                        title_meta = re.search(
                            r'<meta[^>]+content=["\'](.*?)["\'][^>]+property=["\']og:title["\']',
                            html,
                        )
                    if title_meta:
                        og_title = unescape(title_meta.group(1))

                    og_desc = None
                    desc_meta = re.search(
                        r'<meta[^>]+property=["\']og:description["\'][^>]+content=["\'](.*?)["\']',
                        html,
                    )
                    if not desc_meta:
                        desc_meta = re.search(
                            r'<meta[^>]+content=["\'](.*?)["\'][^>]+property=["\']og:description["\']',
                            html,
                        )
                    if desc_meta:
                        og_desc = unescape(desc_meta.group(1))

                    if og_title and og_desc:
                        parts = og_desc.split(" · ")
                        if len(parts) >= 1:
                            artist = parts[0].strip()
                            title = og_title.strip()
                            spotify_query = f"{artist} - {title} audio"
                            log_step(
                                f"✅ Scraped Spotify OG Metadata: '{artist}' - '{title}'"
                            )
            except Exception as e:
                log_step(f"⚠️ Spotify metadata scraping failed: {e}")

            if not spotify_query:
                raise RuntimeError("Could not resolve Spotify track metadata.")
        else:
            # Try to get title from YouTube URL
            try:
                cookies_path = os.path.join(BASE_DIR, "cookies.txt")
                info_cmd = [
                    sys.executable,
                    "-m",
                    "yt_dlp",
                    "--get-title",
                    "--no-playlist",
                ]
                if os.path.exists(cookies_path):
                    cookies_age_days = (
                        time.time() - os.path.getmtime(cookies_path)
                    ) / 86400
                    if cookies_age_days < 7:
                        info_cmd.extend(["--cookies", cookies_path])
                info_cmd.append(url)

                title_result = subprocess.run(
                    info_cmd, capture_output=True, text=True, timeout=20
                )
                if title_result.returncode == 0 and title_result.stdout:
                    raw_title = title_result.stdout.strip()
                    if raw_title:
                        # Try to parse artist from title if it has " - "
                        if " - " in raw_title:
                            parts = raw_title.split(" - ", 2)
                            artist = parts[0].strip()
                            title = parts[1].strip()
                        else:
                            title = raw_title
            except Exception as e:
                log_step(f"⚠️ Could not get YouTube title: {e}")

        # 1. Download with yt-dlp
        import glob

        unique_id = uuid.uuid4().hex[:8]
        output_template = os.path.join(UPLOAD_DIR, f"dl_{unique_id}.%(ext)s")

        def build_yt_dlp_cmd(target: str, use_cookies: bool = True) -> list:
            base = [
                sys.executable,
                "-m",
                "yt_dlp",
                "--no-playlist",
                "--extractor-retries",
                "2",
                "--socket-timeout",
                "30",
                "-x",
                "--audio-format",
                "mp3",
                "--audio-quality",
                "0",
                "-o",
                output_template,
            ]
            if use_cookies:
                cookies_path = os.path.join(BASE_DIR, "cookies.txt")
                if os.path.exists(cookies_path):
                    cookies_age_days = (
                        time.time() - os.path.getmtime(cookies_path)
                    ) / 86400
                    if cookies_age_days < 7:
                        base.extend(["--cookies", cookies_path])
                        log_step(
                            f"🍪 Using cookies.txt (age: {cookies_age_days:.1f} days)"
                        )
            base.append(target)
            return base

        dl_start = now()
        file_path = None

        if is_spotify:
            # SoundCloud search — no bot detection on datacenter IPs, no auth needed
            sc_target = f"scsearch1:{spotify_query}"
            log_step(f"🎵 Searching SoundCloud: {spotify_query}")
            cmd = build_yt_dlp_cmd(sc_target, use_cookies=False)
            log_step(f"⬇️ Running yt-dlp (SoundCloud): {' '.join(cmd)}")
            result = subprocess.run(cmd, capture_output=True, text=True, timeout=300)
            if result.returncode != 0:
                log_step(
                    f"⚠️ SoundCloud search failed, trying YouTube: {result.stderr[:300]}"
                )
                yt_target = f"ytsearch1:{spotify_query}"
                cmd = build_yt_dlp_cmd(yt_target, use_cookies=True)
                log_step(f"⬇️ Fallback to YouTube: {' '.join(cmd)}")
                result = subprocess.run(
                    cmd, capture_output=True, text=True, timeout=300
                )
                if result.returncode != 0:
                    log_step(f"❌ Both SoundCloud and YouTube failed: {result.stderr}")
                    raise RuntimeError(
                        f"Download failed (SoundCloud + YouTube both blocked): {result.stderr[:400]}"
                    )
        else:
            # Direct YouTube URL: try YouTube first, fall back to SoundCloud title search
            log_step(f"⬇️ Attempting direct YouTube download: {url}")
            cmd = build_yt_dlp_cmd(url, use_cookies=True)
            log_step(f"⬇️ Running yt-dlp (YouTube direct): {' '.join(cmd)}")
            result = subprocess.run(cmd, capture_output=True, text=True, timeout=300)
            if result.returncode != 0 and (
                "Sign in" in result.stderr or "bot" in result.stderr.lower()
            ):
                log_step(
                    "⚠️ YouTube bot detection triggered — falling back to SoundCloud search"
                )
                sc_query = f"{artist} - {title}" if title != "Unknown Track" else url
                sc_target = f"scsearch1:{sc_query}"
                cmd = build_yt_dlp_cmd(sc_target, use_cookies=False)
                log_step(f"⬇️ Fallback SoundCloud search: {sc_query}")
                result = subprocess.run(
                    cmd, capture_output=True, text=True, timeout=300
                )
                if result.returncode != 0:
                    log_step(f"❌ SoundCloud fallback also failed: {result.stderr}")
                    raise RuntimeError(f"Download failed: {result.stderr[:400]}")
            elif result.returncode != 0:
                log_step(f"❌ yt-dlp failed: {result.stderr}")
                raise RuntimeError(f"yt-dlp failed: {result.stderr[:500]}")

        log_step(f"✅ yt-dlp done ({elapsed(dl_start)}s)")

        # Find the downloaded file
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
            duration=AUDIO_ANALYZE_DURATION,
        )
        audio_duration = float(librosa.get_duration(y=y_audio, sr=sr))
        log_step(
            f"🎵 Audio loaded ({elapsed(load_start)}s), duration: {audio_duration:.2f}s"
        )

        bpm = detect_bpm_robust(y_audio, sr)
        time_signature = detect_time_signature(y_audio, sr, bpm)
        log_step(f"🥁 BPM: {bpm}, Time sig: {time_signature}/4")

        key = detect_key(y_audio, sr)
        log_step(f"🎹 Key: {key}")

        chords = get_chords(file_path)
        log_step(f"🎸 Chords: {len(chords)} detected")

        demucs_start = now()
        stems_folder, drums_path, bass_path, other_path, vocals_path = run_demucs(
            file_path
        )
        log_step(f"🎚️ Demucs done ({elapsed(demucs_start)}s)")

        lyrics = []
        # 1. Whisper transkription
        try:
            clean_vocals = prepare_vocals_for_whisper(vocals_path)
            lyrics = transcribe_with_groq(clean_vocals)
            if clean_vocals != vocals_path:
                cleanup_file(clean_vocals)
            log_step(f"🗣️ Lyrics (Whisper): {len(lyrics)} segments")
        except Exception as e:
            log_step(f"⚠️ Lyrics transcription failed: {e}")

        # 2. Quality fixes
        lyrics = post_process_lyrics(lyrics, audio_duration)

        # Cleanup downloaded file
        cleanup_file(file_path)

        result = {
            "status": "success",
            "title": title,
            "artist": artist,
            "bpm": bpm,
            "key": key,
            "time_signature": time_signature,
            "chords": chords,
            "lyrics": lyrics,
            "stems_path": stems_folder or "",
            "original_path": "",
            "duration_seconds": audio_duration,
            "analysis_time_seconds": elapsed(total_start),
            "analysis_window_seconds": AUDIO_ANALYZE_DURATION,
        }

        if stems_folder and os.path.isdir(stems_folder):
            json_path = os.path.join(stems_folder, "analysis.json")
            try:
                with open(json_path, "w") as f:
                    json.dump(result, f, indent=2)
                log_step(f"💾 Analysis JSON saved to {json_path}")
            except Exception as e:
                log_step(f"⚠️ Could not save analysis JSON: {e}")

        log_step(f"✅ /import-url complete in {elapsed(total_start)}s")
        return result
    except Exception as e:
        print("\n============== Traceback ==============")
        print(traceback.format_exc())
        print("============== End Traceback ==============")
        if file_path:
            cleanup_file(file_path)
        raise HTTPException(status_code=500, detail=f"Import-url failed: {str(e)}")


class ImportUrlAsyncRequest(BaseModel):
    url: str


@app.post("/import-url-async")
async def import_url_async(request: ImportUrlAsyncRequest):
    """Start an async import job. Returns job_id immediately."""
    job_id = uuid.uuid4().hex[:12]
    jobs[job_id] = {
        "status": "running",
        "stage": "⏳ Starting...",
        "progress": 0,
        "result": None,
        "error": None,
    }

    def run_job():
        # We reuse the same logic as import_url but update job state
        file_path = None
        total_start = now()
        try:
            ensure_runtime_dirs()
            url = request.url
            is_spotify = False
            spotify_query = None
            title = "Unknown Track"
            artist = "Unknown Artist"

            job_update(job_id, "🔍 Fetching track metadata...", progress=2)

            if "spotify.com" in url.lower() and "/track/" in url.lower():
                is_spotify = True
                track_id_match = re.search(r"track/([a-zA-Z0-9]{22})", url)
                if track_id_match:
                    track_id = track_id_match.group(1)
                else:
                    clean_url = url.split("?")[0]
                    track_id = clean_url.split("/")[-1]
                try:
                    import urllib.request
                    from html import unescape

                    req = urllib.request.Request(
                        f"https://open.spotify.com/track/{track_id}",
                        headers={"User-Agent": "Mozilla/5.0"},
                    )
                    with urllib.request.urlopen(req, timeout=10) as response:
                        html = response.read().decode("utf-8")
                    title_match = re.search(
                        r"<title>(.*?)</title>", html, re.IGNORECASE
                    )
                    if title_match:
                        page_title = unescape(title_match.group(1))
                        match = re.search(
                            r"^(.*?) - (?:song and lyrics|song|single|EP|album) by (.*?) \| Spotify$",
                            page_title,
                            re.IGNORECASE,
                        )
                        if match:
                            title = match.group(1).strip()
                            artist = match.group(2).strip()
                            spotify_query = f"{artist} - {title} audio"
                    if not spotify_query:
                        og_title_m = re.search(
                            r'<meta[^>]+property=["\']og:title["\'][^>]+content=["\'](.*?)["\']',
                            html,
                        )
                        og_desc_m = re.search(
                            r'<meta[^>]+property=["\']og:description["\'][^>]+content=["\'](.*?)["\']',
                            html,
                        )
                        if og_title_m and og_desc_m:
                            parts = unescape(og_desc_m.group(1)).split(" · ")
                            artist = parts[0].strip()
                            title = unescape(og_title_m.group(1)).strip()
                            spotify_query = f"{artist} - {title} audio"
                except Exception as e:
                    log_step(f"⚠️ Spotify metadata scraping failed: {e}")
                if not spotify_query:
                    jobs[job_id]["status"] = "error"
                    jobs[job_id]["error"] = "Could not resolve Spotify track metadata."
                    return

            job_update(job_id, f"⬇️ Downloading: {artist} - {title}...", progress=8)

            import glob as glob_mod

            unique_id = uuid.uuid4().hex[:8]
            output_template = os.path.join(UPLOAD_DIR, f"dl_{unique_id}.%(ext)s")

            def build_cmd(target, use_cookies=True):
                c = [
                    sys.executable,
                    "-m",
                    "yt_dlp",
                    "--no-playlist",
                    "--extractor-retries",
                    "2",
                    "--socket-timeout",
                    "30",
                    "-x",
                    "--audio-format",
                    "mp3",
                    "--audio-quality",
                    "0",
                    "-o",
                    output_template,
                ]
                if use_cookies:
                    cp = os.path.join(BASE_DIR, "cookies.txt")
                    if (
                        os.path.exists(cp)
                        and (time.time() - os.path.getmtime(cp)) / 86400 < 7
                    ):
                        c.extend(["--cookies", cp])
                c.append(target)
                return c

            dl_start = now()
            if is_spotify:
                sc_target = f"scsearch1:{spotify_query}"
                r = subprocess.run(
                    build_cmd(sc_target, False),
                    capture_output=True,
                    text=True,
                    timeout=300,
                )
                if r.returncode != 0:
                    r = subprocess.run(
                        build_cmd(f"ytsearch1:{spotify_query}"),
                        capture_output=True,
                        text=True,
                        timeout=300,
                    )
                    if r.returncode != 0:
                        raise RuntimeError(f"Download failed: {r.stderr[:300]}")
            else:
                r = subprocess.run(
                    build_cmd(url), capture_output=True, text=True, timeout=300
                )
                if r.returncode != 0 and (
                    "Sign in" in r.stderr or "bot" in r.stderr.lower()
                ):
                    sc_q = f"{artist} - {title}" if title != "Unknown Track" else url
                    r = subprocess.run(
                        build_cmd(f"scsearch1:{sc_q}", False),
                        capture_output=True,
                        text=True,
                        timeout=300,
                    )
                    if r.returncode != 0:
                        raise RuntimeError(f"Download failed: {r.stderr[:300]}")
                elif r.returncode != 0:
                    raise RuntimeError(f"yt-dlp failed: {r.stderr[:400]}")

            log_step(f"✅ Download done ({elapsed(dl_start)}s)")
            candidates = glob_mod.glob(os.path.join(UPLOAD_DIR, f"dl_{unique_id}.*"))
            if not candidates:
                raise RuntimeError("yt-dlp produced no output file")
            file_path = candidates[0]

            job_update(job_id, "🎵 Analyzing audio (BPM, Key, Chords)...", progress=15)
            y_audio, sr = librosa.load(
                file_path,
                sr=AUDIO_LOAD_SR,
                mono=AUDIO_LOAD_MONO,
                duration=AUDIO_ANALYZE_DURATION,
            )
            bpm = detect_bpm_robust(y_audio, sr)
            time_signature = detect_time_signature(y_audio, sr, bpm)
            job_update(job_id, f"🥁 BPM: {bpm}  Key: detecting...", progress=25)
            key = detect_key(y_audio, sr)
            job_update(job_id, "🎸 Detecting chords...", progress=35)
            chords = get_chords(file_path)
            job_update(
                job_id,
                "🎚️ Separating stems (Demucs) — this takes 3-5 min...",
                progress=40,
            )
            stems_folder, drums_path, bass_path, other_path, vocals_path = run_demucs(
                file_path, job_id
            )

            job_update(job_id, "🗣️ Transcribing lyrics (Whisper)...", progress=95)
            lyrics = []
            try:
                clean_vocals = prepare_vocals_for_whisper(vocals_path)
                lyrics = transcribe_with_groq(clean_vocals)
                if clean_vocals != vocals_path:
                    cleanup_file(clean_vocals)
            except Exception as e:
                log_step(f"⚠️ Lyrics transcription failed: {e}")

            # 2. Quality fixes
            audio_duration = float(librosa.get_duration(path=vocals_path))
            lyrics = post_process_lyrics(lyrics, audio_duration)

            cleanup_file(file_path)

            result = {
                "status": "success",
                "title": title,
                "artist": artist,
                "bpm": bpm,
                "key": key,
                "time_signature": time_signature,
                "chords": chords,
                "lyrics": lyrics,
                "stems_path": stems_folder or "",
                "original_path": "",
                "duration_seconds": elapsed(total_start),
                "analysis_window_seconds": AUDIO_ANALYZE_DURATION,
            }
            jobs[job_id]["result"] = result
            jobs[job_id]["status"] = "done"
            jobs[job_id]["stage"] = "✅ Done!"
            jobs[job_id]["progress"] = 100

            if stems_folder and os.path.isdir(stems_folder):
                json_path = os.path.join(stems_folder, "analysis.json")
                try:
                    with open(json_path, "w") as f:
                        json.dump(result, f, indent=2)
                    log_step(f"💾 Analysis JSON saved for job {job_id}")
                except Exception as e:
                    log_step(f"⚠️ Could not save analysis JSON for job {job_id}: {e}")

            log_step(f"✅ Job {job_id} complete in {elapsed(total_start)}s")

        except Exception as e:
            log_step(f"❌ Job {job_id} failed: {e}")
            if file_path:
                cleanup_file(file_path)
            jobs[job_id]["status"] = "error"
            jobs[job_id]["error"] = str(e)
            jobs[job_id]["stage"] = f"❌ Failed: {str(e)[:100]}"

    t = threading.Thread(target=run_job, daemon=True)
    t.start()
    return {"job_id": job_id, "status": "started"}


@app.get("/job-status/{job_id}")
async def job_status(job_id: str):
    """Poll status of a background import job."""
    if job_id not in jobs:
        raise HTTPException(status_code=404, detail="Job not found")
    job = jobs[job_id]
    return {
        "job_id": job_id,
        "status": job["status"],
        "stage": job["stage"],
        "progress": job.get("progress", 0),
        "result": job.get("result"),
        "error": job.get("error"),
    }


@app.get("/rescan-stems")
async def rescan_stems():
    """Scan the stems output directory for all valid stem folders."""
    stems_base = os.path.join(OUT_DIR, DEMUCS_MODEL)
    if not os.path.isdir(stems_base):
        return {"stems": []}

    found = []
    for folder_name in os.listdir(stems_base):
        folder_path = os.path.join(stems_base, folder_name)
        if not os.path.isdir(folder_path):
            continue
        drums = os.path.join(folder_path, "drums.mp3")
        bass = os.path.join(folder_path, "bass.mp3")
        other = os.path.join(folder_path, "other.mp3")
        vocals = os.path.join(folder_path, "vocals.mp3")
        if all(os.path.isfile(f) for f in [drums, bass, other, vocals]):
            found.append(
                {
                    "folder_name": folder_name,
                    "stems_path": folder_path,
                    "title": folder_name.replace("dl_", "").replace("_", " "),
                }
            )

    log_step(f"🔍 Rescan found {len(found)} stem folders in {stems_base}")
    return {"stems": found}


@app.get("/analysis-data/{model}/{folder_name}")
async def get_analysis_data(model: str, folder_name: str):
    """Retrieve the analysis.json file for a given separated stem folder."""
    stems_folder = os.path.join(OUT_DIR, model, folder_name)
    json_path = os.path.join(stems_folder, "analysis.json")
    if not os.path.isfile(json_path):
        raise HTTPException(status_code=404, detail="Analysis data not found.")
    try:
        with open(json_path, "r") as f:
            data = json.load(f)
        return data
    except Exception as e:
        raise HTTPException(
            status_code=500, detail=f"Failed to read analysis data: {e}"
        )


if __name__ == "__main__":
    port = int(os.environ.get("PORT", 10000))
    uvicorn.run("main:app", host="0.0.0.0", port=port)
