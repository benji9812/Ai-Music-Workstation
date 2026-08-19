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

try:
    import allin1  # pyright: ignore[reportMissingImports]

    ALLIN1_AVAILABLE = True
except ImportError:
    allin1 = None
    ALLIN1_AVAILABLE = False

try:
    import essentia.standard as es  # pyright: ignore[reportMissingImports]

    ESSENTIA_AVAILABLE = True
except ImportError:
    es = None
    ESSENTIA_AVAILABLE = False
import librosa
import numpy as np
import soundfile as sf
import uvicorn
from dotenv import load_dotenv
from fastapi import (
    FastAPI,
    File,
    HTTPException,
    UploadFile,
)
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
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
DEMUCS_MODEL = "htdemucs_6s"
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
async def lifespan(app: FastAPI):  # pyright: ignore[reportUnusedParameter]
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


@app.get("/audio/{filename}")
async def get_audio_upload(filename: str):
    """Serve original audio files from UPLOAD_DIR with range support."""
    file_path = os.path.join(UPLOAD_DIR, filename)
    if not os.path.exists(file_path):
        raise HTTPException(status_code=404, detail="File not found")

    return FileResponse(
        file_path,
        media_type="audio/mpeg",
        headers={"Accept-Ranges": "bytes"},
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
    except Exception as e:
        log_step(f"⚠️  Could not cleanup file {path}: {e}")


def require_file(path: str, label: str):
    if not os.path.exists(path):
        raise RuntimeError(f"{label} is missing: {path}")


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
        raise RuntimeError("GOOGLE_API_KEY is missing.")
    return google_genai.Client(api_key=api_key)


@lru_cache(maxsize=1)
def get_groq_client():
    from groq import Groq

    api_key = os.getenv("GROQ_API_KEY")
    if not api_key:
        raise RuntimeError("GROQ_API_KEY is missing.")
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


def detect_key(file_path: str):
    """Detect key/scale using Essentia KeyExtractor."""
    if ESSENTIA_AVAILABLE:
        try:
            # Load audio using Essentia's MonoLoader
            audio = es.MonoLoader(filename=file_path, sampleRate=44100)()  # pyright: ignore[reportOptionalMemberAccess]
            # Extract key and scale
            key, scale, strength = es.KeyExtractor()(audio)  # pyright: ignore[reportUnusedVariable, reportOptionalMemberAccess]

            # Format to match requested output (e.g. "C major", "C# minor")
            suffix = " minor" if scale == "minor" else " major"
            return f"{key}{suffix}"
        except Exception as e:
            log_step(f"⚠️ Essentia key detection failed: {e}. Falling back to librosa.")

    # Fallback to librosa if essentia is unavailable or fails
    try:
        y, sr = librosa.load(file_path, sr=22050, mono=True)
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
            return [
                np.corrcoef(chroma_avg, np.roll(profile, i))[0, 1] for i in range(12)
            ]

        major_corrs = correlations(major_profile)
        minor_corrs = correlations(minor_profile)
        if max(major_corrs) > max(minor_corrs):
            res = NOTES[np.argmax(major_corrs)] + " major"
        else:
            res = NOTES[np.argmax(minor_corrs)] + " minor"
        return res
    except Exception as e2:
        log_step(f"⚠️ Librosa fallback also failed: {e2}")
        return "C major"


def get_chords(file_path: str):
    """Detect chords using Essentia."""
    if ESSENTIA_AVAILABLE:
        try:
            # Load audio
            audio = es.MonoLoader(filename=file_path, sampleRate=44100)()  # pyright: ignore[reportOptionalMemberAccess]

            # Frame-based processing for HPCP
            hopSize = 2048
            frameSize = 4096

            # Algorithms
            windowing = es.Windowing(type="hann")  # pyright: ignore[reportOptionalMemberAccess]
            spectrum = es.Spectrum()  # pyright: ignore[reportOptionalMemberAccess]
            spectralPeaks = es.SpectralPeaks()  # pyright: ignore[reportOptionalMemberAccess]
            hpcp_calc = es.HPCP()  # pyright: ignore[reportOptionalMemberAccess]
            chords_det = es.ChordsDetection()  # pyright: ignore[reportOptionalMemberAccess]

            hpcps = []
            for frame in es.FrameGenerator(  # pyright: ignore[reportOptionalMemberAccess]
                audio, frameSize=frameSize, hopSize=hopSize, startFromZero=True
            ):
                spec = spectrum(windowing(frame))
                peaks, magnitudes = spectralPeaks(spec)
                hpcp = hpcp_calc(peaks, magnitudes)
                hpcps.append(hpcp)

            # Detect chords
            chords, strength = chords_det(hpcps)  # pyright: ignore[reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable, reportUnusedVariable]

            # Format output
            res = []
            prev_chord = None
            for i, chord in enumerate(chords):
                timestamp = float(i * hopSize / 44100.0)
                # Essentia returns chords like "C", "Cmin", "G#maj", etc.
                # We might want to normalize "min" to "m" and "maj" to ""
                normalized_chord = chord.replace("min", "m").replace("maj", "")

                if normalized_chord != prev_chord:
                    res.append({"time": timestamp, "chord": normalized_chord})
                    prev_chord = normalized_chord
            return res
        except Exception as e:
            log_step(
                f"⚠️ Essentia chord detection failed: {e}. Falling back to librosa."
            )

    # Fallback to current librosa-based logic
    try:
        y, sr = librosa.load(file_path, sr=22050, mono=True)
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
    except Exception as e2:
        log_step(f"⚠️ Librosa chord fallback failed: {e2}")
        return []


def run_demucs(
    file_path: str,
    stems_to_extract: Optional[List[str]] = None,
    job_id: Optional[str] = None,
):
    cmd = [
        sys.executable,
        "-m",
        "demucs",
        "-n",
        DEMUCS_MODEL,
        "--mp3",
        "-o",
        OUT_DIR,
    ]

    # Use --two-stems ONLY if exactly 1 stem is selected
    if stems_to_extract and len(stems_to_extract) == 1:
        cmd.extend(["--two-stems", stems_to_extract[0]])
    # For htdemucs_6s, we run without stems-filter if more than 1 (or all) stems requested.
    # The --stems flag is invalid and has been removed.

    cmd.append(file_path)
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

    log_step(f"✅ Demucs finished in {elapsed(start)}s")
    folder_name = os.path.splitext(os.path.basename(file_path))[0]
    stems_output_dir = os.path.join(OUT_DIR, DEMUCS_MODEL, folder_name)

    extracted_stems = {}

    # Define what we actually WANT to return
    want_stems = (
        stems_to_extract
        if stems_to_extract
        else ["vocals", "drums", "bass", "other", "guitar", "piano"]
    )

    if os.path.isdir(stems_output_dir):
        # First, find and resample all files we want
        for stem_name in want_stems:
            stem_file_name = f"{stem_name}.mp3"
            stem_path = os.path.join(stems_output_dir, stem_file_name)
            if os.path.isfile(stem_path):
                resample_mp3_to_target_sr(stem_path)
                extracted_stems[stem_name] = stem_path
            else:
                log_step(f"⚠️ Warning: Expected stem file not found: {stem_path}")

        # Second, if we had a specific list, delete anything else in that folder
        if stems_to_extract:
            for filename in os.listdir(stems_output_dir):
                stem_name = os.path.splitext(filename)[0]
                if stem_name not in want_stems:
                    cleanup_file(os.path.join(stems_output_dir, filename))

    if not extracted_stems:
        raise RuntimeError("No stems were extracted by Demucs. Check logs for errors.")

    return extracted_stems


@app.post("/analyze-quick")
async def analyze_quick(file: UploadFile = File(...)):
    total_start = now()
    file_path = None
    try:
        ensure_runtime_dirs()
        if not file.filename:
            raise HTTPException(status_code=400, detail="No file was sent.")
        log_step("📥 /analyze-quick request received")
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
        log_step(f"💾 File saved: {file_path} ({elapsed(total_start)}s)")
        load_start = now()
        y_audio, sr = librosa.load(
            file_path,
            sr=AUDIO_LOAD_SR,
            mono=AUDIO_LOAD_MONO,
            duration=AUDIO_ANALYZE_DURATION,
        )
        log_step(
            f"🎵 Audio loaded ({elapsed(load_start)}s), samples={len(y_audio)}, sr={sr}"
        )
        bpm_start = now()
        bpm = detect_bpm_robust(y_audio, sr)
        time_signature = detect_time_signature(y_audio, sr, bpm)
        log_step(
            f"🥁 BPM: {bpm}, Time Signature: {time_signature}/4 ({elapsed(bpm_start)}s)"
        )
        key_start = now()
        key = detect_key(file_path)
        log_step(f"🎹 Key: {key} ({elapsed(key_start)}s)")
        chords_start = now()
        chords = get_chords(file_path)
        log_step(f"🎸 Chords: {len(chords)} detected ({elapsed(chords_start)}s)")
        log_step(f"✅ /analyze-quick finished in {elapsed(total_start)}s")
        return {
            "status": "success",
            "bpm": bpm,
            "key": key,
            "time_signature": time_signature,
            "chords": chords,
            "lyrics": [],
            "stems_path": "",
            "original_path": os.path.basename(file_path),
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
            raise HTTPException(status_code=400, detail="No file was sent.")
        log_step(f"📥 /analyze request received (title={title}, artist={artist})")
        safe_filename = sanitize_filename(file.filename)
        file_path = os.path.join(UPLOAD_DIR, safe_filename)
        contents = await file.read()
        with open(file_path, "wb") as f:
            f.write(contents)
        log_step(f"💾 File saved: {file_path} ({elapsed(total_start)}s)")

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
        log_step(f"🥁 BPM: {bpm}, Time Signature: {time_signature}/4")

        key = detect_key(file_path)
        log_step(f"🎹 Key: {key}")

        chords = get_chords(file_path)
        log_step(f"🎸 Chords: {len(chords)} detected")

        demucs_start = now()
        extracted_stems = run_demucs(file_path)
        stems_folder = (
            os.path.dirname(list(extracted_stems.values())[0])
            if extracted_stems
            else None
        )
        vocals_path = extracted_stems.get("vocals")
        log_step(f"🎚️ Demucs finished ({elapsed(demucs_start)}s)")

        lyrics = []
        # 1. Whisper transcription
        if vocals_path:
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
            "extracted_stems": {
                stem: f"/audio/{os.path.relpath(path, OUT_DIR)}"
                for stem, path in extracted_stems.items()
            },
            "original_path": os.path.basename(file_path),
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

        log_step(f"✅ /analyze finished in {elapsed(total_start)}s")
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


class SeparateStemsRequest(BaseModel):
    stems: List[str]
    file_path: str


@app.post("/separate-stems")
async def separate_stems(request_data: SeparateStemsRequest):
    total_start = now()
    try:
        # 1. Log incoming request details
        log_step("--- Incoming /separate-stems JSON request ---")
        log_step(f"Request data: {request_data}")

        ensure_runtime_dirs()

        audio_file_path = request_data.file_path
        stems_list = request_data.stems

        log_step(f"📥 /separate-stems: Using existing file: {audio_file_path}")
        if not os.path.exists(audio_file_path):
            detail = f"File not found on server path: {audio_file_path}"
            log_step(f"❌ 404 Not Found: {detail}")
            raise HTTPException(status_code=404, detail=detail)

        if not stems_list:
            detail = "No stems specified (stems_list is empty). Required: vocals, drums, etc."
            log_step(f"❌ 400 Bad Request: {detail}")
            raise HTTPException(status_code=400, detail=detail)

        log_step(f"🎚️ Initiating stem separation for: {', '.join(stems_list)}")
        extracted_stems = run_demucs(audio_file_path, stems_to_extract=stems_list)

        result_stems_urls = {
            stem: f"/audio/{os.path.relpath(path, OUT_DIR)}"
            for stem, path in extracted_stems.items()
        }

        log_step(f"✅ /separate-stems finished in {elapsed(total_start)}s")
        return {
            "status": "success",
            "extracted_stems": result_stems_urls,
            "analysis_time_seconds": elapsed(total_start),
        }

    except HTTPException as he:
        # Re-log the specific HTTP error
        log_step(f"⚠️ HTTPException {he.status_code}: {he.detail}")
        raise
    except Exception as e:
        print("\n============== Traceback ==============")
        print(traceback.format_exc())
        print("============== End Traceback ==============")
        error_msg = f"Separate stems failed: {str(e)}"
        log_step(f"❌ Critical Error: {error_msg}")
        raise HTTPException(status_code=500, detail=error_msg)


class StructureRequest(BaseModel):
    artist: str
    title: str
    duration: float
    file_path: Optional[str] = None


@app.post("/structure")
async def structure(request: StructureRequest):
    """Song structure analysis via allin1 (local) with Gemini fallback."""
    sections = []
    used_method = "none"

    actual_path = None
    if request.file_path:
        actual_path = request.file_path
        if not os.path.exists(actual_path):
            # Try in UPLOAD_DIR
            potential_path = os.path.join(
                UPLOAD_DIR, os.path.basename(request.file_path)
            )
            if os.path.exists(potential_path):
                actual_path = potential_path

    # 1. Try allin1 (Local ML)
    if ALLIN1_AVAILABLE and actual_path:

        if os.path.exists(actual_path) and os.path.isfile(actual_path):
            try:
                log_step(f"🧠 Running allin1 analysis on: {actual_path}")
                # allin1.analyze returns a result object
                result = allin1.analyze(actual_path)  # pyright: ignore[reportOptionalMemberAccess]

                # Map allin1 segments to our format
                # Labels mapping: allin1 has many labels, we try to map to our 5 labels
                label_map = {
                    "intro": "Intro",
                    "verse": "Verse",
                    "chorus": "Chorus",
                    "bridge": "Bridge",
                    "outro": "Outro",
                    "solo": "Bridge",  # Map solo to bridge for simplicity
                    "silence": "Intro",  # Map silence to intro if at start, or just keep as is?
                }

                for segment in result.segments:
                    label = segment.label.lower()
                    # Find best match in our labels
                    mapped_label = "Verse"  # Default
                    for k, v in label_map.items():
                        if k in label:
                            mapped_label = v
                            break

                    sections.append(
                        {
                            "label": mapped_label,
                            "start": float(segment.start),
                            "end": float(segment.end),
                        }
                    )

                if sections:
                    used_method = "allin1"
                    log_step("✅ Structure analysis completed via allin1")
                    return {
                        "status": "success",
                        "sections": sections,
                        "method": used_method,
                    }

            except Exception as e:
                log_step(f"⚠️ allin1 analysis failed: {e}. Falling back to Gemini.")

    # 2. Try Gemini (Fallback)
    max_retries = 2
    last_error = None

    for attempt in range(max_retries + 1):
        try:
            gemini = get_gemini_client()
            prompt = (
                f"Analyze the song structure of '{request.title}' by '{request.artist}'.\n"
                f"The total audio duration is exactly {request.duration:.1f} seconds.\n"
                "Provide a list of song sections using ONLY these labels: Intro, Verse, Chorus, Bridge, Outro.\n"
                "CRITICAL:\n"
                f"1. Sections must cover 0.0 to {request.duration:.1f} without gaps.\n"
                f"2. The last section MUST end at {request.duration:.1f}.\n"
                'Return a JSON array: [{"label": "...", "start": 0.0, "end": 10.0}, ...]\n'
                "Only return valid JSON, no extra text."
            )
            
            contents = [prompt]
            gemini_file = None
            if actual_path and os.path.exists(actual_path):
                log_step("⬆️ Uploading audio to Gemini for structure analysis...")
                gemini_file = gemini.files.upload(file=actual_path)
                contents = [gemini_file, prompt]

            response = gemini.models.generate_content(
                model="gemini-1.5-flash",
                contents=contents,
                config={"max_output_tokens": 8192},
            )
            
            if gemini_file:
                try:
                    gemini.files.delete(name=gemini_file.name)
                except Exception as e:
                    log_step(f"⚠️ Failed to delete Gemini file: {e}")

            raw = (response.text or "").strip()
            if not raw:
                raise ValueError("Empty response from Gemini")

            if raw.startswith("```"):
                raw = re.sub(r"^```[a-z]*\n?", "", raw)
                raw = re.sub(r"\n?```$", "", raw)
            raw = raw.strip()

            if not raw.endswith("]"):
                last_brace = raw.rfind("}")
                if last_brace != -1:
                    raw = raw[: last_brace + 1] + "]"
                else:
                    raw += "]"

            try:
                sections = json.loads(raw)
            except json.JSONDecodeError as je:
                log_step(f"❌ JSON Parse Error on attempt {attempt + 1}: {je}")
                last_error = je
                continue

            if isinstance(sections, list) and len(sections) > 0:
                sections.sort(key=lambda x: x.get("start", 0))
                if sections[0].get("start", 0) > 0:
                    sections[0]["start"] = 0.0
                for i in range(len(sections) - 1):
                    current_end = float(sections[i].get("end", 0))
                    next_start = float(sections[i + 1].get("start", 0))
                    if current_end < next_start:
                        sections[i]["end"] = next_start
                    elif current_end > next_start:
                        sections[i + 1]["start"] = current_end
                sections[-1]["end"] = float(request.duration)

                used_method = "gemini_fallback"
                log_step("✅ Structure analysis completed via Gemini fallback")
                return {
                    "status": "success",
                    "sections": sections,
                    "method": used_method,
                }
            else:
                raise ValueError("AI returned an empty or invalid list of sections")
        except Exception as e:
            err_str = str(e)
            is_503 = (
                "503" in err_str
                or "Unavailable" in err_str
                or "overloaded" in err_str.lower()
            )
            if is_503 and attempt < max_retries:
                wait_time = [2, 4][attempt]
                log_step(
                    f"⚠️ Gemini 503 (attempt {attempt + 1}). Retrying in {wait_time}s..."
                )
                time.sleep(wait_time)
                continue
            log_step(f"⚠️ Attempt {attempt + 1} failed: {e}")
            last_error = e
            if attempt < max_retries:
                time.sleep(1)

    # 3. Final Fallback
    log_step(f"❌ All structure analysis attempts failed: {last_error}")
    return {
        "status": "success",
        "sections": [
            {"label": "Full Song", "start": 0.0, "end": float(request.duration)}
        ],
        "method": "final_fallback",
    }


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

        key = detect_key(file_path)
        log_step(f"🎹 Key: {key}")

        chords = get_chords(file_path)
        log_step(f"🎸 Chords: {len(chords)} detected")

        demucs_start = now()
        extracted_stems = run_demucs(file_path)
        stems_folder = (
            os.path.dirname(list(extracted_stems.values())[0])
            if extracted_stems
            else None
        )
        vocals_path = extracted_stems.get("vocals")
        log_step(f"🎚️ Demucs done ({elapsed(demucs_start)}s)")

        lyrics = []
        # 1. Whisper transkription
        if vocals_path:
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
            "extracted_stems": {
                stem: f"/audio/{os.path.relpath(path, OUT_DIR)}"
                for stem, path in extracted_stems.items()
            },
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

            import glob

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
            candidates = glob.glob(os.path.join(UPLOAD_DIR, f"dl_{unique_id}.*"))
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
            key = detect_key(file_path)
            job_update(job_id, "🎸 Detecting chords...", progress=35)
            chords = get_chords(file_path)
            job_update(
                job_id,
                "🎚️ Separating stems (Demucs) — this takes 3-5 min...",
                progress=40,
            )
            extracted_stems = run_demucs(file_path, job_id=job_id)
            stems_folder = (
                os.path.dirname(list(extracted_stems.values())[0])
                if extracted_stems
                else None
            )
            vocals_path = extracted_stems.get("vocals")

            job_update(job_id, "🗣️ Transcribing lyrics (Whisper)...", progress=95)
            lyrics = []
            if vocals_path:
                try:
                    clean_vocals = prepare_vocals_for_whisper(vocals_path)
                    lyrics = transcribe_with_groq(clean_vocals)
                    if clean_vocals != vocals_path:
                        cleanup_file(clean_vocals)
                except Exception as e:
                    log_step(f"⚠️ Lyrics transcription failed: {e}")

            # 2. Quality fixes
            audio_duration = float(librosa.get_duration(y=y_audio, sr=sr))
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
                "extracted_stems": {
                    stem: f"/audio/{os.path.relpath(path, OUT_DIR)}"
                    for stem, path in extracted_stems.items()
                },
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


@app.post("/start-analyze-quick-job")
async def start_analyze_quick_job(request: ImportUrlAsyncRequest):
    """Start an async quick analysis job. Returns job_id immediately."""
    job_id = uuid.uuid4().hex[:12]
    jobs[job_id] = {
        "status": "running",
        "stage": "⏳ Starting quick analysis...",
        "progress": 0,
        "result": None,
        "error": None,
    }

    def run_quick_analysis_job():
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

            import glob

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
            candidates = glob.glob(os.path.join(UPLOAD_DIR, f"dl_{unique_id}.*"))
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
            audio_duration = float(librosa.get_duration(y=y_audio, sr=sr))
            bpm = detect_bpm_robust(y_audio, sr)
            time_signature = detect_time_signature(y_audio, sr, bpm)
            job_update(job_id, f"🥁 BPM: {bpm}  Key: detecting...", progress=25)
            key = detect_key(file_path)
            job_update(job_id, "🎸 Detecting chords...", progress=35)
            chords = get_chords(file_path)

            # NOTE: Lyrics analysis is omitted here for "quick analysis" as it depends on stem separation
            # This aligns with the user's request for "WITHOUT Demucs stem separation"

            result = {
                "status": "success",
                "title": title,
                "artist": artist,
                "bpm": bpm,
                "key": key,
                "time_signature": time_signature,
                "chords": chords,
                "lyrics": [],  # No lyrics in quick analysis
                "stems_path": "",  # No stems in quick analysis
                "extracted_stems": {},  # No stems in quick analysis
                "original_path": os.path.basename(
                    file_path
                ),  # Provide the path to the downloaded file for later stem separation
                "duration_seconds": audio_duration,
                "analysis_window_seconds": AUDIO_ANALYZE_DURATION,
            }
            jobs[job_id]["result"] = result
            jobs[job_id]["status"] = "done"
            jobs[job_id]["stage"] = "✅ Quick Analysis Done!"
            jobs[job_id]["progress"] = 100

            log_step(
                f"✅ Quick analysis job {job_id} complete in {elapsed(total_start)}s"
            )

        except Exception as e:
            log_step(f"❌ Quick analysis job {job_id} failed: {e}")
            if file_path:
                cleanup_file(file_path)
            jobs[job_id]["status"] = "error"
            jobs[job_id]["error"] = str(e)
            jobs[job_id]["stage"] = f"❌ Failed: {str(e)[:100]}"

    t = threading.Thread(target=run_quick_analysis_job, daemon=True)
    t.start()
    return {"job_id": job_id, "status": "started"}


class StartLyricsJobRequest(BaseModel):
    file_path: str


@app.post("/start-lyrics-job")
async def start_lyrics_job(request: StartLyricsJobRequest):
    job_id = uuid.uuid4().hex[:12]
    jobs[job_id] = {
        "status": "running",
        "stage": "⏳ Starting lyrics transcription...",
        "progress": 0,
        "result": None,
        "error": None,
    }

    def run_lyrics_job():
        total_start = now()
        try:
            ensure_runtime_dirs()
            file_path = request.file_path
            if not file_path or not os.path.exists(file_path):
                alt_path = os.path.join(UPLOAD_DIR, os.path.basename(file_path or ""))
                if os.path.exists(alt_path):
                    file_path = alt_path
                else:
                    raise FileNotFoundError(f"Audio file not found: {file_path}")

            job_update(job_id, "🗣️ Transcribing lyrics with Whisper...", progress=10)
            lyrics = transcribe_with_groq(file_path)
            audio_duration = float(librosa.get_duration(path=file_path))
            lyrics = post_process_lyrics(lyrics, audio_duration)
            
            jobs[job_id]["result"] = {"lyrics": lyrics}
            jobs[job_id]["status"] = "done"
            jobs[job_id]["stage"] = "✅ Lyrics transcription complete!"
            jobs[job_id]["progress"] = 100
            
            log_step(f"✅ Lyrics job {job_id} complete in {elapsed(total_start)}s")

        except Exception as e:
            log_step(f"❌ Lyrics job {job_id} failed: {e}")
            jobs[job_id]["status"] = "error"
            jobs[job_id]["error"] = str(e)
            jobs[job_id]["stage"] = f"❌ Failed: {str(e)[:100]}"

    t = threading.Thread(target=run_lyrics_job, daemon=True)
    t.start()
    return {"job_id": job_id, "status": "started"}

@app.post("/start-structure-job")
async def start_structure_job(request: StructureRequest):
    job_id = uuid.uuid4().hex[:12]
    jobs[job_id] = {
        "status": "running",
        "stage": "⏳ Starting structure analysis...",
        "progress": 0,
        "result": None,
        "error": None,
    }

    def run_structure_job():
        total_start = now()
        try:
            job_update(job_id, "🧠 Analyzing song structure...", progress=10)
            
            # Call the async structure function from a new event loop
            import asyncio
            loop = asyncio.new_event_loop()
            asyncio.set_event_loop(loop)
            res = loop.run_until_complete(structure(request))
            loop.close()
            
            jobs[job_id]["result"] = res
            jobs[job_id]["status"] = "done"
            jobs[job_id]["stage"] = "✅ Structure analysis complete!"
            jobs[job_id]["progress"] = 100
            
            log_step(f"✅ Structure job {job_id} complete in {elapsed(total_start)}s")

        except Exception as e:
            log_step(f"❌ Structure job {job_id} failed: {e}")
            jobs[job_id]["status"] = "error"
            jobs[job_id]["error"] = str(e)
            jobs[job_id]["stage"] = f"❌ Failed: {str(e)[:100]}"

    t = threading.Thread(target=run_structure_job, daemon=True)
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
