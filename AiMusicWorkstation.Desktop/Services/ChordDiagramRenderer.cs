using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AiMusicWorkstation.Desktop.Services
{
    public static class ChordDiagramRenderer
    {
        // [string 6..1], -1 = muted, 0 = open
        private static readonly Dictionary<string, int[]> ChordFrets = new()
        {
            // === MAJOR ===
            { "C",   new[] { -1, 3, 2, 0, 1, 0 } },
            { "C#",  new[] { -1, 4, 3, 1, 2, 1 } },
            { "D",   new[] { -1, -1, 0, 2, 3, 2 } },
            { "D#",  new[] { -1, -1, 1, 3, 4, 3 } },
            { "E",   new[] { 0, 2, 2, 1, 0, 0 } },
            { "F",   new[] { 1, 1, 2, 3, 3, 1 } },
            { "F#",  new[] { 2, 4, 4, 3, 2, 2 } },
            { "G",   new[] { 3, 2, 0, 0, 0, 3 } },
            { "G#",  new[] { 4, 6, 6, 5, 4, 4 } },
            { "A",   new[] { -1, 0, 2, 2, 2, 0 } },
            { "A#",  new[] { -1, 1, 3, 3, 3, 1 } },
            { "Bb",  new[] { -1, 1, 3, 3, 3, 1 } },
            { "B",   new[] { -1, 2, 4, 4, 4, 2 } },

            // === MINOR ===
            { "Cm",  new[] { -1, 3, 5, 5, 4, 3 } },
            { "C#m", new[] { -1, 4, 6, 6, 5, 4 } },
            { "Dm",  new[] { -1, -1, 0, 2, 3, 1 } },
            { "D#m", new[] { -1, 6, 8, 8, 7, 6 } },
            { "Em",  new[] { 0, 2, 2, 0, 0, 0 } },
            { "Fm",  new[] { 1, 3, 3, 1, 1, 1 } },
            { "F#m", new[] { 2, 4, 4, 2, 2, 2 } },
            { "Gm",  new[] { 3, 5, 5, 3, 3, 3 } },
            { "G#m", new[] { 4, 6, 6, 4, 4, 4 } },
            { "Am",  new[] { -1, 0, 2, 2, 1, 0 } },
            { "A#m", new[] { -1, 1, 3, 3, 2, 1 } },
            { "Bbm", new[] { -1, 1, 3, 3, 2, 1 } },
            { "Bm",  new[] { -1, 2, 4, 4, 3, 2 } },

            // === DOMINANT 7TH ===
            { "C7",  new[] { -1, 3, 2, 3, 1, 0 } },
            { "D7",  new[] { -1, -1, 0, 2, 1, 2 } },
            { "E7",  new[] { 0, 2, 0, 1, 0, 0 } },
            { "F7",  new[] { 1, 1, 2, 1, 3, 1 } },
            { "G7",  new[] { 3, 2, 0, 0, 0, 1 } },
            { "A7",  new[] { -1, 0, 2, 0, 2, 0 } },
            { "B7",  new[] { -1, 2, 1, 2, 0, 2 } },

            // === MINOR 7TH ===
            { "Am7", new[] { -1, 0, 2, 0, 1, 0 } },
            { "Em7", new[] { 0, 2, 2, 0, 3, 0 } },
            { "Dm7", new[] { -1, -1, 0, 2, 1, 1 } },
            { "Bm7", new[] { -1, 2, 4, 2, 3, 2 } },
        };

        public static UIElement? Render(string chordName, double size = 120)
        {
            if (string.IsNullOrEmpty(chordName)) return null;

            string chord = chordName.Trim();

            // Extrahera rot (C, C#, Bb etc.)
            string root;
            string suffix;
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

            // Avgör om minor: "m" direkt efter roten, men INTE "maj"
            bool isMinor = suffix.Length > 0
                && suffix[0] == 'm'
                && !suffix.StartsWith("maj", StringComparison.OrdinalIgnoreCase);

            // Bygg upp lookup-nycklar i prioritetsordning
            string minorKey = root + (isMinor ? "m" : "");
            string majorKey = root;

            // Försök: exakt suffix-match (t.ex. "Am7" → finns i dict)
            if (ChordFrets.ContainsKey(chord)) { }
            // Försök: rot + minor-flagg (t.ex. "C#m7" → "C#m")
            else if (ChordFrets.ContainsKey(minorKey)) chord = minorKey;
            // Försök: bara roten (t.ex. "Cmaj7" → "C")
            else if (ChordFrets.ContainsKey(majorKey)) chord = majorKey;
            // Inget hittades — returnera null, låt anroparen hantera
            else return null;

            int[] frets = ChordFrets[chord];

            double cellW = size / 7.0;
            double cellH = size / 6.0;
            int numFrets = 5;
            int numStrings = 6;

            var canvas = new Canvas
            {
                Width = size,
                Height = size,
                Background = Brushes.Transparent
            };

            double offsetX = cellW * 0.5;
            double offsetY = 4;

            // Strängar (vertikala)
            for (int s = 0; s < numStrings; s++)
            {
                double x = offsetX + s * cellW;
                canvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = offsetY,
                    X2 = x,
                    Y2 = offsetY + numFrets * cellH,
                    Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    StrokeThickness = 1
                });
            }

            // Band (horisontella)
            for (int f = 0; f <= numFrets; f++)
            {
                double y = offsetY + f * cellH;
                canvas.Children.Add(new Line
                {
                    X1 = offsetX,
                    Y1 = y,
                    X2 = offsetX + (numStrings - 1) * cellW,
                    Y2 = y,
                    Stroke = f == 0
                        ? new SolidColorBrush(Color.FromRgb(180, 180, 180))
                        : new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    StrokeThickness = f == 0 ? 3 : 1
                });
            }

            // Fingrar / X / O
            for (int s = 0; s < numStrings; s++)
            {
                int fret = frets[s];
                double x = offsetX + s * cellW;

                if (fret == -1)
                {
                    var tb = new TextBlock
                    {
                        Text = "✕",
                        Foreground = Brushes.IndianRed,
                        FontSize = 10
                    };
                    Canvas.SetLeft(tb, x - 5);
                    Canvas.SetTop(tb, offsetY - 14);
                    canvas.Children.Add(tb);
                }
                else if (fret == 0)
                {
                    var tb = new TextBlock
                    {
                        Text = "○",
                        Foreground = Brushes.Gray,
                        FontSize = 10
                    };
                    Canvas.SetLeft(tb, x - 5);
                    Canvas.SetTop(tb, offsetY - 14);
                    canvas.Children.Add(tb);
                }
                else
                {
                    double y = offsetY + (fret - 0.5) * cellH;
                    var dot = new Ellipse
                    {
                        Width = cellW * 0.75,
                        Height = cellW * 0.75,
                        Fill = new SolidColorBrush(Color.FromRgb(0, 122, 204))
                    };
                    Canvas.SetLeft(dot, x - cellW * 0.375);
                    Canvas.SetTop(dot, y - cellW * 0.375);
                    canvas.Children.Add(dot);
                }
            }

            return canvas;
        }
    }
}