import sys
import json
import os
import warnings
import librosa
import soundfile as sf

warnings.filterwarnings("ignore")
# Safely call reconfigure if available (avoids static type checker error on TextIO)
_reconf = getattr(sys.stdout, "reconfigure", None)
if callable(_reconf):
    _reconf(encoding='utf-8')

# -------------------------------------------------------
# Transponering — pitch shift via librosa
# Anropas som: python advanced_analysis.py <filväg> <halvtoner>
# Exempel: python advanced_analysis.py song.wav +2
# -------------------------------------------------------
def transpose_audio(file_path, semitones):
    output = {"status": "error", "message": "Unknown error"}
    try:
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"Filen hittades inte: {file_path}")

        y, sr = librosa.load(file_path, sr=None)
        y_shifted = librosa.effects.pitch_shift(y, sr=sr, n_steps=semitones)

        output_dir  = os.path.join(os.path.dirname(file_path), "Transposed")
        os.makedirs(output_dir, exist_ok=True)

        base_name    = os.path.splitext(os.path.basename(file_path))[0]
        new_filename = f"{base_name}_key{semitones:+d}.wav"
        new_path     = os.path.join(output_dir, new_filename)
        sf.write(new_path, y_shifted, sr)

        output = {
            "status":           "success",
            "original_path":    file_path,
            "transposed_path":  new_path,
            "semitones":        semitones
        }
    except Exception as e:
        output = {"status": "error", "message": str(e)}

    print(json.dumps(output))

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"status": "error", "message": "Användning: python advanced_analysis.py <filväg> [halvtoner]"}))
        sys.exit(1)

    path       = sys.argv[1]
    semitones  = 0
    if len(sys.argv) > 2:
        try:
            semitones = int(sys.argv[2])
        except ValueError:
            pass

    transpose_audio(path, semitones)