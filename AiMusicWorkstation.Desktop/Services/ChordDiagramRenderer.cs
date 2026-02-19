using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AiMusicWorkstation.Desktop.Services
{
    public static class ChordDiagramRenderer
    {
        // Chord-data: [string 6..1][fret positions], -1 = muted, 0 = open
        private static readonly Dictionary<string, int[]> ChordFrets = new()
        {
            { "C",   new[] { -1, 3, 2, 0, 1, 0 } },
            { "Cm",  new[] { -1, 3, 5, 5, 4, 3 } },
            { "D",   new[] { -1, -1, 0, 2, 3, 2 } },
            { "Dm",  new[] { -1, -1, 0, 2, 3, 1 } },
            { "E",   new[] { 0, 2, 2, 1, 0, 0 } },
            { "Em",  new[] { 0, 2, 2, 0, 0, 0 } },
            { "F",   new[] { 1, 1, 2, 3, 3, 1 } },
            { "Fm",  new[] { 1, 1, 1, 3, 3, 1 } },
            { "G",   new[] { 3, 2, 0, 0, 0, 3 } },
            { "Gm",  new[] { 3, 1, 0, 0, 3, 3 } },
            { "A",   new[] { -1, 0, 2, 2, 2, 0 } },
            { "Am",  new[] { -1, 0, 2, 2, 1, 0 } },
            { "Bb",  new[] { -1, 1, 3, 3, 3, 1 } },
            { "B",   new[] { -1, 2, 4, 4, 4, 2 } },
            { "C#",  new[] { -1, 4, 3, 1, 2, 1 } },
            { "D#",  new[] { -1, -1, 1, 3, 4, 3 } },
            { "F#",  new[] { 2, 2, 3, 4, 4, 2 } },
            { "G#",  new[] { 4, 3, 1, 1, 1, 4 } },
            { "A#",  new[] { -1, 1, 3, 3, 3, 1 } },
        };

        public static UIElement Render(string chordName, double size = 120)
        {
            string key = chordName.Replace(" Maj", "").Replace(" maj", "").Trim();
            if (!ChordFrets.ContainsKey(key)) key = "C";

            int[] frets = ChordFrets[key];

            double cellW = size / 7.0;
            double cellH = size / 6.0;
            int numFrets = 5;
            int numStrings = 6;

            var canvas = new Canvas
            {
                Width = size,
                Height = size,        // <-- var size + 20, nu bara size
                Background = Brushes.Transparent
            };

            // BORTTAGET: Chord name label

            double offsetX = cellW * 0.5;
            double offsetY = 4;       // <-- var 20, lite padding kvar för X/O ovanför

            // Rita strängar (vertikala)
            for (int s = 0; s < numStrings; s++)
            {
                double x = offsetX + s * cellW;
                var line = new Line
                {
                    X1 = x,
                    Y1 = offsetY,
                    X2 = x,
                    Y2 = offsetY + numFrets * cellH,
                    Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);
            }

            // Rita bands (horisontella)
            for (int f = 0; f <= numFrets; f++)
            {
                double y = offsetY + f * cellH;
                var line = new Line
                {
                    X1 = offsetX,
                    Y1 = y,
                    X2 = offsetX + (numStrings - 1) * cellW,
                    Y2 = y,
                    Stroke = f == 0
                        ? new SolidColorBrush(Color.FromRgb(180, 180, 180))
                        : new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    StrokeThickness = f == 0 ? 3 : 1
                };
                canvas.Children.Add(line);
            }

            // Rita fingrar / X / O
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
