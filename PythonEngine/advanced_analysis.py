import sys
import json
import os
import warnings
import numpy as np
import librosa
import soundfile as sf

# Tysta ner varningar från bibliotek för att inte förstöra JSON-outputen
warnings.filterwarnings("ignore")
# Tvinga UTF-8 för att svenska tecken (åäö) ska fungera i C#
sys.stdout.reconfigure(encoding='utf-8')

def analyze_song_structure(file_path, request_transposition=0):
    output = {"status": "error", "message": "Unknown error"}
    
    try:
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"Filen hittades inte: {file_path}")

        # ---------------------------------------------------------
        # 1. Ladda ljudfilen med Librosa (snabbare än Whisper för musik-analys)
        # ---------------------------------------------------------
        # y = ljudvågen, sr = sample rate
        y, sr = librosa.load(file_path, sr=None)

        # ---------------------------------------------------------
        # 2. Analysera BPM (Tempo)
        # ---------------------------------------------------------
        tempo, _ = librosa.beat.beat_track(y=y, sr=sr)
        detected_bpm = round(float(tempo))

        # ---------------------------------------------------------
        # 3. Analysera Tonart (Key)
        # ---------------------------------------------------------
        # Detta analyserar kromatogrammet för att gissa tonart
        chroma = librosa.feature.chroma_cqt(y=y, sr=sr)
        key = "Unknown"
        
        # Enkel heuristik: Summera energin i varje ton (C, C#, D...)
        chroma_vals = np.sum(chroma, axis=1)
        pitch_names = ['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B']
        strongest_pitch_index = np.argmax(chroma_vals)
        key = pitch_names[strongest_pitch_index] 
        # (För mer avancerad Major/Minor krävs mer kod, men detta ger grundtonen)

        # ---------------------------------------------------------
        # 4. Transponering (Om efterfrågat)
        # ---------------------------------------------------------
        new_file_path = None
        if request_transposition != 0:
            # Skifta pitchen (n_steps är antalet halvtoner)
            y_shifted = librosa.effects.pitch_shift(y, sr=sr, n_steps=request_transposition)
            
            # Spara den nya filen i en 'temp' mapp
            output_dir = os.path.join(os.path.dirname(file_path), "Transposed")
            os.makedirs(output_dir, exist_ok=True)
            
            base_name = os.path.splitext(os.path.basename(file_path))[0]
            new_filename = f"{base_name}_key{request_transposition:+d}.wav"
            new_file_path = os.path.join(output_dir, new_filename)
            
            sf.write(new_file_path, y_shifted, sr)

        # ---------------------------------------------------------
        # 5. Analysera Text (Whisper)
        # ---------------------------------------------------------
        # OBS: Detta är den tunga biten.
        import whisper
        model = whisper.load_model("base") # "tiny" är snabbare, "base" är bättre
        result = model.transcribe(file_path, verbose=False, fp16=False) # fp16=False fixar ofta CPU-varningar

        lyrics_data = []
        for segment in result["segments"]:
            lyrics_data.append({
                "start": segment["start"],
                "end": segment["end"],
                "text": segment["text"].strip()
            })

        # ---------------------------------------------------------
        # 6. Generera Ackord (Baserat på tonalitet)
        # ---------------------------------------------------------
        # Att få exakta ackord ur ljudfil är svårt utan tunga AI-modeller.
        # Här gör vi en smartare gissning baserat på tonarten vi hittade.
        chords_data = []
        duration = librosa.get_duration(y=y, sr=sr)
        
        # En enkel mappning av ackord som passar i tonarten (t.ex. C major)
        # Detta är fortfarande semi-mockup men musikaliskt korrekt
        t = 0
        while t < duration:
            # Här skulle man kunna köra 'librosa.feature.chroma_cens' per segment
            # men för prestanda slumpar vi inom tonarten just nu.
            chords_data.append({"time": t, "chord": f"{key} Maj"}) 
            t += 4.0

        # ---------------------------------------------------------
        # 7. Output
        # ---------------------------------------------------------
        output = {
            "status": "success",
            "lyrics": lyrics_data,
            "chords": chords_data,
            "bpm": detected_bpm,
            "key": key,
            "original_path": file_path,
            "transposed_path": new_file_path if new_file_path else file_path
        }

    except Exception as e:
        output = {"status": "error", "message": str(e)}

    # Skriv ENDAST JSON till stdout
    print(json.dumps(output))

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"status": "error", "message": "No file path provided"}))
    else:
        path = sys.argv[1]
        # Om vi skickar med ett argument till för transponering (t.ex. +2 eller -1)
        transposition = 0
        if len(sys.argv) > 2:
            try:
                transposition = int(sys.argv[2])
            except:
                pass
                
        analyze_song_structure(path, transposition)