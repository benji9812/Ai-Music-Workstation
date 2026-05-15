import uvicorn
from dotenv import load_dotenv
from pathlib import Path
import os
from fastapi import FastAPI, UploadFile, File
from pydantic import BaseModel
import shutil
import warnings
import sys
import subprocess
import json
import traceback
import librosa
import numpy as np
import soundfile as sf

warnings.filterwarnings("ignore")

# ✅ BASE_DIR och load_dotenv() FÖRE allt som behöver env-variabler
BASE_DIR = Path(__file__).resolve().parent
load_dotenv(dotenv_path=BASE_DIR.parent / ".env")

UPLOAD_DIR = os.path.join(BASE_DIR, "temp_uploads")
OUT_DIR    = os.path.join(BASE_DIR, "separated")
os.environ["PATH"] += os.pathsep + str(BASE_DIR)

app = FastAPI(title="AI Music Engine")

# -------------------------------------------------------
# ENDPOINT: /health
# -------------------------------------------------------
@app.get("/health")
async def health():
    return {"status": "ok"}

os.makedirs(UPLOAD_DIR, exist_ok=True)
os.makedirs(OUT_DIR, exist_ok=True)

NOTES = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B']
_gemini_client = None
_groq_client = None

def get_gemini_client():
    global _gemini_client
    if _gemini_client is None:
        from google import genai as google_genai

        api_key = os.getenv("GOOGLE_API_KEY")
        if not api_key:
            raise RuntimeError("GOOGLE_API_KEY saknas — kontrollera att .env finns i AiMusicWorkstation/ och innehåller nyckeln.")
        _gemini_client = google_genai.Client(api_key=api_key)
    return _gemini_client

def get_groq_client():
    global _groq_client
    if _groq_client is None:
        from groq import Groq

        api_key = os.getenv("GROQ_API_KEY")
        if not api_key:
            raise RuntimeError("GROQ_API_KEY saknas — lägg till den i .env och som Railway-variabel.")
        _groq_client = Groq(api_key=api_key)
        print("✅ Groq Whisper Large v3 redo (API-baserad, ingen lokal modell)")
    return _groq_client

# -------------------------------------------------------
# HJÄLPFUNKTION: Hitta stems-mapp via drums.mp3-storlek
# -------------------------------------------------------
def find_stems_folder_by_drums(drums_upload_path):
    uploaded_size = os.path.getsize(drums_upload_path)
    htdemucs_dir  = os.path.join(OUT_DIR, "htdemucs")
    if not os.path.exists(htdemucs_dir):
        return None
    for song_folder in os.listdir(htdemucs_dir):
        folder_path = os.path.join(htdemucs_dir, song_folder)
        candidate   = os.path.join(folder_path, "drums.mp3")
        if os.path.exists(candidate) and os.path.getsize(candidate) == uploaded_size:
            return folder_path
    return None

# -------------------------------------------------------
# HJÄLPFUNKTION: Förbered vocals för Whisper
# -------------------------------------------------------
def prepare_vocals_for_whisper(vocals_path):
    try:
        y, sr = librosa.load(vocals_path, sr=16000, mono=True)
        y = y / (np.max(np.abs(y)) + 1e-6)
        temp_path = vocals_path.replace(".mp3", "_clean.wav")
        sf.write(temp_path, y, sr)
        return temp_path
    except:
        return vocals_path

# -------------------------------------------------------
# HJÄLPFUNKTION: Transkribera med Groq Whisper Large v3
# -------------------------------------------------------
def transcribe_with_groq(audio_path):
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
                "end":   s.end,
                "text":  s.text.strip()
            })
    else:
        segments.append({
            "start": 0.0,
            "end":   0.0,
            "text":  transcription.text.strip() if hasattr(transcription, "text") else ""
        })
    return segments

# -------------------------------------------------------
# BPM — Robust via median av tre segment på drums.mp3
# -------------------------------------------------------
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
    while bpm > 140: bpm /= 2
    while bpm < 60:  bpm *= 2
    return round(bpm, 1)

# -------------------------------------------------------
# TAKTART — via autocorrelation på drums.mp3
# -------------------------------------------------------
def detect_time_signature(y, sr, bpm):
    try:
        hop_length = 512
        onset_env  = librosa.onset.onset_strength(y=y, sr=sr, hop_length=hop_length)
        ac         = librosa.autocorrelate(onset_env, max_size=len(onset_env) // 2)
        beat_frames = int(round(60.0 * sr / (bpm * hop_length)))
        score_3 = float(ac[beat_frames * 3]) if beat_frames * 3 < len(ac) else 0
        score_4 = float(ac[beat_frames * 4]) if beat_frames * 4 < len(ac) else 0
        score_6 = float(ac[beat_frames * 6]) if beat_frames * 6 < len(ac) else 0
        best   = max(score_3, score_4, score_6)
        margin = 0.15
        if best == score_3 and score_3 > score_4 * (1 + margin): return 3
        if best == score_6 and score_6 > score_4 * (1 + margin): return 6
        return 4
    except:
        return 4

# -------------------------------------------------------
# TONART — Krumhansl-Schmuckler på bass.mp3
# -------------------------------------------------------
def detect_key(y, sr):
    y_harmonic  = librosa.effects.harmonic(y, margin=4)
    chroma      = librosa.feature.chroma_cqt(y=y_harmonic, sr=sr)
    chroma_avg  = np.mean(chroma, axis=1)
    major_profile = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88]
    minor_profile = [6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17]

    def correlations(profile):
        return [np.corrcoef(chroma_avg, np.roll(profile, i))[0, 1] for i in range(12)]

    major_corrs = correlations(major_profile)
    minor_corrs = correlations(minor_profile)
    if max(major_corrs) > max(minor_corrs):
        return NOTES[np.argmax(major_corrs)]
    else:
        return NOTES[np.argmax(minor_corrs)] + "m"

# -------------------------------------------------------
# ACKORD — på other.mp3 (gitarr/piano/synth)
# -------------------------------------------------------
def get_chords(file_path):
    y, sr      = librosa.load(file_path, duration=180)
    y_harmonic = librosa.effects.harmonic(y, margin=4)
    chroma     = librosa.feature.chroma_cqt(
        y=y_harmonic, sr=sr, hop_length=512, bins_per_octave=36)

    def classify_chord(c):
        best_score = -1
        best_chord = "C"
        for root in range(12):
            candidates = {
                NOTES[root]:          c[root%12] + c[(root+4)%12] + c[(root+7)%12],
                NOTES[root] + "m":    c[root%12] + c[(root+3)%12] + c[(root+7)%12],
                NOTES[root] + "7":    c[root%12] + c[(root+4)%12] + c[(root+7)%12] + c[(root+10)%12] * 0.8,
                NOTES[root] + "maj7": c[root%12] + c[(root+4)%12] + c[(root+7)%12] + c[(root+11)%12] * 0.8,
                NOTES[root] + "m7":   c[root%12] + c[(root+3)%12] + c[(root+7)%12] + c[(root+10)%12] * 0.8,
                NOTES[root] + "sus2": c[root%12] + c[(root+2)%12] + c[(root+7)%12],
                NOTES[root] + "sus4": c[root%12] + c[(root+5)%12] + c[(root+7)%12],
            }
            for chord_name, score in candidates.items():
                if score > best_score:
                    best_score = score
                    best_chord = chord_name
        return best_chord

    chords     = []
    step       = max(1, int(2.0 * sr / 512))
    prev_chord = None

    for i in range(0, chroma.shape[1], step):
        chord = classify_chord(chroma[:, i])
        time  = float(i * 512 / sr)
        if chord != prev_chord:
            chords.append({"time": time, "chord": chord})
            prev_chord = chord

    return chords

# -------------------------------------------------------
# ENDPOINT: /analyze-only — Refresh utan Whisper/Demucs
# -------------------------------------------------------
@app.post("/analyze-only")
async def analyze_only(file: UploadFile = File(...)):
    try:
        safe_filename = "".join([c for c in file.filename
            if c.isalnum() or c in ('.', '-', '_')]).strip()
        file_path = os.path.join(UPLOAD_DIR, safe_filename)
        with open(file_path, "wb") as f:
            shutil.copyfileobj(file.file, f)

        y_drums, sr    = librosa.load(file_path, sr=None)
        bpm            = detect_bpm_robust(y_drums, sr)
        time_signature = detect_time_signature(y_drums, sr, bpm)
        print(f"🥁 BPM: {bpm}, Taktart: {time_signature}/4")

        stems_folder = find_stems_folder_by_drums(file_path)
        bass_path  = os.path.join(stems_folder, "bass.mp3")  if stems_folder else None
        other_path = os.path.join(stems_folder, "other.mp3") if stems_folder else None

        if bass_path and os.path.exists(bass_path):
            y_harm, sr_harm = librosa.load(bass_path, sr=None)
            key = detect_key(y_harm, sr_harm)
        else:
            key = detect_key(y_drums, sr)
        print(f"🎹 Tonart: {key}")

        if other_path and os.path.exists(other_path):
            chords = get_chords(other_path)
        elif bass_path and os.path.exists(bass_path):
            chords = get_chords(bass_path)
        else:
            chords = get_chords(file_path)
        print(f"🎸 Ackord: {len(chords)} detekterade")

        return {
            "status":         "success",
            "bpm":            bpm,
            "key":            key,
            "time_signature": time_signature,
            "chords":         chords,
            "lyrics":         [],
            "stems_path":     stems_folder or "",
            "original_path":  file_path
        }
    except Exception as e:
        traceback.print_exc()
        return {"status": "error", "message": str(e)}

# -------------------------------------------------------
# ENDPOINT: /analyze — Full analys med Demucs + Groq Whisper
# -------------------------------------------------------
@app.post("/analyze")
async def analyze_audio(file: UploadFile = File(...)):
    try:
        safe_filename = "".join([c for c in file.filename
            if c.isalnum() or c in ('.', '-', '_')]).strip()
        file_path = os.path.join(UPLOAD_DIR, safe_filename)
        with open(file_path, "wb") as f:
            shutil.copyfileobj(file.file, f)

        # 1. Stem-separation med Demucs
        cmd = [sys.executable, "-m", "demucs", "-n", "htdemucs", "--mp3", "-o", OUT_DIR, file_path]
        subprocess.run(cmd, text=True)

        folder_name  = os.path.splitext(safe_filename)[0]
        stems_folder = os.path.join(OUT_DIR, "htdemucs", folder_name)

        drums_path  = os.path.join(stems_folder, "drums.mp3")
        bass_path   = os.path.join(stems_folder, "bass.mp3")
        other_path  = os.path.join(stems_folder, "other.mp3")
        vocals_path = os.path.join(stems_folder, "vocals.mp3")

        # 2. BPM + Taktart → drums.mp3
        y_drums, sr_drums = librosa.load(drums_path, sr=None)
        bpm            = detect_bpm_robust(y_drums, sr_drums)
        time_signature = detect_time_signature(y_drums, sr_drums, bpm)
        print(f"🥁 BPM: {bpm}, Taktart: {time_signature}/4")

        # 3. Tonart → bass.mp3
        harm_src = bass_path if os.path.exists(bass_path) else (other_path if os.path.exists(other_path) else file_path)
        y_harm, sr_harm = librosa.load(harm_src, sr=None)
        key = detect_key(y_harm, sr_harm)
        print(f"🎹 Tonart: {key} (källa: {os.path.basename(harm_src)})")

        # 4. Ackord → other.mp3
        chord_src = other_path if os.path.exists(other_path) else (bass_path if os.path.exists(bass_path) else file_path)
        chords = get_chords(chord_src)
        print(f"🎸 Ackord: {len(chords)} detekterade (källa: {os.path.basename(chord_src)})")

        # 5. Lyrics → vocals.mp3 via Groq Whisper Large v3
        whisper_src = prepare_vocals_for_whisper(vocals_path) if os.path.exists(vocals_path) else file_path
        lyrics = transcribe_with_groq(whisper_src)
        print(f"🎤 Lyrics: {len(lyrics)} segment")

        return {
            "status":         "success",
            "bpm":            bpm,
            "key":            key,
            "time_signature": time_signature,
            "lyrics":         lyrics,
            "chords":         chords,
            "stems_path":     stems_folder,
            "original_path":  file_path
        }

    except Exception as e:
        traceback.print_exc()
        return {"status": "error", "message": str(e)}

# -------------------------------------------------------
# ENDPOINT: /structure — Sektion-detektering via Gemini
# -------------------------------------------------------
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
        raw   = response.text.strip()
        start = raw.find("[")
        end   = raw.rfind("]") + 1
        if start == -1 or end == 0:
            return {"status": "error", "message": "No JSON in response"}

        sections = json.loads(raw[start:end])
        result   = []
        for i, sec in enumerate(sections):
            end_time = sections[i + 1]["start"] if i + 1 < len(sections) else req.duration
            result.append({
                "label": sec["label"],
                "start": float(sec["start"]),
                "end":   float(end_time),
                "color": ""
            })
        return {"status": "success", "sections": result}

    except Exception as e:
        return {"status": "error", "message": str(e)}

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8000)
