namespace AiMusicWorkstation.Shared.Helpers
{
    public static class MusicTheoryHelper
    {
        public static readonly string[] NoteNames =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "Bb", "B" };

        public static string TransposeKey(string key, int semitones)
        {
            bool isMinor = key.EndsWith("m");
            string root = isMinor ? key[..^1] : key;
            int idx = Array.IndexOf(NoteNames, root);
            if (idx == -1) return key;
            int newIdx = ((idx + semitones) % 12 + 12) % 12;
            return NoteNames[newIdx] + (isMinor ? "m" : "");
        }

        public static string TransposeChord(string chord, int semitones)
        {
            if (semitones == 0 || string.IsNullOrEmpty(chord)) return chord;

            string root = "";
            string suffix = "";

            if (chord.Length >= 2 && (chord[1] == '#' || chord[1] == 'b'))
            {
                root = chord[..2];
                suffix = chord[2..];
            }
            else
            {
                root = chord[..1];
                suffix = chord[1..];
            }

            int idx = Array.IndexOf(NoteNames, root);
            if (idx == -1) return chord;
            int newIdx = ((idx + semitones) % 12 + 12) % 12;
            return NoteNames[newIdx] + suffix;
        }
    }
}