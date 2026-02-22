import uvicorn
from fastapi import FastAPI, UploadFile, File
import os
import shutil
import warnings
import whisper
import sys
import subprocess
import json
import traceback
import librosa
import numpy as np
import soundfile as sf

warnings.filterwarnings("ignore")

app = FastAPI(title="AI Music Engine")

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
UPLOAD_DIR = os.path.join(BASE_DIR, "temp_uploads")
OUT_DIR = os.path.join(BASE_DIR, "separated")
os.environ["PATH"] += os.pathsep + BASE_DIR

os.makedirs(UPLOAD_DIR, exist_ok=True)
os.makedirs(OUT_DIR, exist_ok=True)

print("⏳ Laddar Whisper Medium (bättre precision)...")
whisper_model = whisper.load_model("medium")

# -------------------------------------------------------
# HJÄLPFUNKTION: Välj bästa stem för analys
# -------------------------------------------------------
def get_best_analysis_source(stems_folder, original_path):
    drums = os.path.join(stems_folder, "drums.mp3")
    if os.path.exists(drums):
        return drums
    return original_path  # Originalet är bättre än other/vocals

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
# BPM — Robust detektering via median av tre segment
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
            bpms.append(float(t))

    if not bpms:
        t, _ = librosa.beat.beat_track(y=y, sr=sr)
        bpm = float(t)
    else:
        bpm = float(np.median(bpms))

    while bpm > 160: bpm /= 2
    while bpm < 60:  bpm *= 2

    return round(bpm, 1)

# -------------------------------------------------------
# TAKTART — via autocorrelation
# -------------------------------------------------------
def detect_time_signature(y, sr, bpm):
    try:
        onset_env = librosa.onset.onset_strength(y=y, sr=sr)
        ac = librosa.autocorrelate(onset_env, max_size=sr // 2)

        hop_length = 512
        beat_frames = int(round(sr * 60.0 / (bpm * hop_length)))

        score_3 = ac[beat_frames * 3] if beat_frames * 3 < len(ac) else 0
        score_4 = ac[beat_frames * 4] if beat_frames * 4 < len(ac) else 0
        score_6 = ac[beat_frames * 6] if beat_frames * 6 < len(ac) else 0

        best = max(score_3, score_4, score_6)
        if best == score_6: return 6
        if best == score_3: return 3
        return 4
    except:
        return 4

# -------------------------------------------------------
# TONART — Krumhansl-Schmuckler
# -------------------------------------------------------
def detect_key(y, sr):
    chroma = librosa.feature.chroma_cqt(y=y, sr=sr)
    chroma_avg = np.mean(chroma, axis=1)

    major_profile = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88]
    minor_profile = [6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17]
    notes = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B']

    def correlations(profile):
        return [np.corrcoef(chroma_avg, np.roll(profile, i))[0, 1] for i in range(12)]

    major_corrs = correlations(major_profile)
    minor_corrs = correlations(minor_profile)

    if max(major_corrs) > max(minor_corrs):
        return notes[np.argmax(major_corrs)]
    else:
        return notes[np.argmax(minor_corrs)] + "m"

# -------------------------------------------------------
# ACKORD — Förbättrad Librosa med harmonisk isolering
# -------------------------------------------------------
def get_chords(file_path):
    y, sr = librosa.load(file_path, duration=180)

    # Isolera harmoniska frekvenser — filtrerar bort trummor/perkussion
    y_harmonic = librosa.effects.harmonic(y, margin=4)
    chroma = librosa.feature.chroma_cqt(
        y=y_harmonic, sr=sr, hop_length=512, bins_per_octave=36)

    notes = ['C','C#','D','D#','E','F','F#','G','G#','A','Bb','B']

    def classify_chord(c):
        best_score = -1
        best_chord = "C"
        for root in range(12):
            major = c[root%12] + c[(root+4)%12] + c[(root+7)%12]
            minor = c[root%12] + c[(root+3)%12] + c[(root+7)%12]
            dom7  = c[root%12] + c[(root+4)%12] + c[(root+7)%12] + c[(root+10)%12] * 0.5
            if major > best_score:
                best_score = major
                best_chord = notes[root]
            if minor > best_score:
                best_score = minor
                best_chord = notes[root] + "m"
            if dom7 > best_score:
                best_score = dom7
                best_chord = notes[root] + "7"
        return best_chord

    chords = []
    step = max(1, int(2.0 * sr / 512))
    prev_chord = None

    for i in range(0, chroma.shape[1], step):
        chord = classify_chord(chroma[:, i])
        time = float(i * 512 / sr)
        if chord != prev_chord:
            chords.append({"time": time, "chord": chord})
            prev_chord = chord

    return chords

# -------------------------------------------------------
# FASTAPI ENDPOINT
# -------------------------------------------------------
@app.post("/analyze-only")
async def analyze_only(file: UploadFile = File(...)):
    try:
        safe_filename = "".join([c for c in file.filename
            if c.isalnum() or c in ('.', '-', '_')]).strip()
        file_path = os.path.join(UPLOAD_DIR, safe_filename)
        with open(file_path, "wb") as f:
            shutil.copyfileobj(file.file, f)

        y, sr = librosa.load(file_path, sr=None)
        bpm = detect_bpm_robust(y, sr)
        time_signature = detect_time_signature(y, sr, bpm)
        y_harmonic = librosa.effects.harmonic(y, margin=4)
        key = detect_key(y_harmonic, sr)
        chords = get_chords(file_path)

        return {
            "status": "success",
            "bpm": bpm,
            "key": key,
            "time_signature": time_signature,
            "chords": chords,
            "lyrics": [],        # Hoppar över Whisper vid refresh
            "stems_path": "",
            "original_path": file_path
        }
    except Exception as e:
        return {"status": "error", "message": str(e)}

@app.post("/analyze")
async def analyze_audio(file: UploadFile = File(...)):
    try:
        safe_filename = "".join([c for c in file.filename
            if c.isalnum() or c in ('.', '-', '_')]).strip()
        file_path = os.path.join(UPLOAD_DIR, safe_filename)
        with open(file_path, "wb") as f:
            shutil.copyfileobj(file.file, f)

        # 1. Separera stems
        cmd = [sys.executable, "-m", "demucs", "-n", "htdemucs", "--mp3", "-o", OUT_DIR, file_path]
        subprocess.run(cmd, text=True)

        folder_name = os.path.splitext(safe_filename)[0]
        stems_folder = os.path.join(OUT_DIR, "htdemucs", folder_name)

        # 2. Välj bästa källa för musikanalys
        analysis_src = get_best_analysis_source(stems_folder, file_path)
        print(f"🎵 Analyserar med: {os.path.basename(analysis_src)}")

        y, sr = librosa.load(analysis_src, sr=None)

        # 3. BPM
        bpm = detect_bpm_robust(y, sr)
        print(f"🥁 BPM: {bpm}")

        # 4. Taktart
        time_signature = detect_time_signature(y, sr, bpm)
        print(f"🎼 Taktart: {time_signature}/4")

        # 5. Tonart — kör på harmonic-isolerad version för bättre precision
        y_harmonic = librosa.effects.harmonic(y, margin=4)
        key = detect_key(y_harmonic, sr)
        print(f"🎹 Tonart: {key}")

        # 6. Ackord
        chords = get_chords(analysis_src)
        print(f"🎸 Ackord: {len(chords)} detekterade")

        # 7. Lyrics via Whisper
        vocals_path = os.path.join(stems_folder, "vocals.mp3")
        if os.path.exists(vocals_path):
            clean_vocals = prepare_vocals_for_whisper(vocals_path)
        else:
            clean_vocals = file_path

        text_result = whisper_model.transcribe(
            clean_vocals,
            fp16=False,
            language="en",
            task="transcribe",
            beam_size=5,
            best_of=5
        )
        lyrics = [
            {
                "start": s["start"],
                "end":   s["end"],
                "text":  s["text"].strip()
            }
            for s in text_result["segments"]
        ]
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

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8000)
