using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AiMusicWorkstation.Desktop.Services
{
    public static class ScaleDiagramRenderer
    {
        private static readonly string[] NoteNames =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "Bb", "B" };

        // Durskala: W W H W W W H (hela/halva steg)
        private static readonly int[] MajorIntervals = { 0, 2, 4, 5, 7, 9, 11 };

        // Pentatonisk durskala: W W WH W WH
        private static readonly int[] PentatonicIntervals = { 0, 2, 4, 7, 9 };

        // Öppna strängar i standard-stämning (E A D G B E), MIDI-noter
        private static readonly int[] OpenStrings = { 4, 9, 14, 19, 23, 28 }; // E2 A2 D3 G3 B3 E4

        public static UIElement Render(string key, bool pentatonic = false, double width = 180, int numFrets = 12)
        {
            bool isMinor = key.EndsWith("m");
            string rootName = isMinor ? key[..^1] : key;
            int rootNote = System.Array.IndexOf(NoteNames, rootName);
            if (rootNote == -1) rootNote = 0;

            // Välj intervaller — moll pentatonisk om isMinor
            int[] intervals = pentatonic
                ? (isMinor
                    ? new[] { 0, 3, 5, 7, 10 }   // Moll pentatonisk
                    : PentatonicIntervals)          // Dur pentatonisk
                : (isMinor
                    ? new[] { 0, 2, 3, 5, 7, 8, 10 } // Naturlig mollskala
                    : MajorIntervals);               // Durskala

            // Bygg HashSet med skalans toner
            var scaleNotes = new HashSet<int>();
            foreach (var i in intervals)
                scaleNotes.Add((rootNote + i) % 12);

            int numStrings = 6;
            double cellW = (width - 30) / numFrets;
            double cellH = 18;
            double offsetX = 28; // Plats för strängetikett
            double offsetY = 8;
            double totalHeight = offsetY + numStrings * cellH + 10;

            var canvas = new Canvas
            {
                Width = width,
                Height = totalHeight,
                Background = Brushes.Transparent
            };

            // Strängetiketter (E A D G B E)
            string[] stringLabels = { "E", "A", "D", "G", "B", "e" };
            for (int s = 0; s < numStrings; s++)
            {
                double y = offsetY + s * cellH + cellH / 2;
                var label = new TextBlock
                {
                    Text = stringLabels[s],
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    FontSize = 9,
                    Width = 20,
                    TextAlignment = System.Windows.TextAlignment.Right
                };
                Canvas.SetLeft(label, 4);
                Canvas.SetTop(label, y - 7);
                canvas.Children.Add(label);

                // Stränglinjer
                var line = new Line
                {
                    X1 = offsetX,
                    Y1 = y,
                    X2 = offsetX + numFrets * cellW,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);
            }

            // Bandmarkeringar (3, 5, 7, 9, 12)
            int[] markers = { 3, 5, 7, 9, 12 };
            foreach (var m in markers)
            {
                if (m > numFrets) break;
                double x = offsetX + (m - 0.5) * cellW;
                var dot = new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Fill = new SolidColorBrush(Color.FromRgb(60, 60, 60))
                };
                Canvas.SetLeft(dot, x - 2.5);
                Canvas.SetTop(dot, totalHeight - 10);
                canvas.Children.Add(dot);
            }

            // Vertikala bandlinjer
            for (int f = 0; f <= numFrets; f++)
            {
                double x = offsetX + f * cellW;
                var line = new Line
                {
                    X1 = x,
                    Y1 = offsetY,
                    X2 = x,
                    Y2 = offsetY + numStrings * cellH,
                    Stroke = f == 0
                        ? new SolidColorBrush(Color.FromRgb(180, 180, 180))
                        : new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                    StrokeThickness = f == 0 ? 2.5 : 0.8
                };
                canvas.Children.Add(line);
            }

            // Rita skalatoner
            for (int s = 0; s < numStrings; s++)
            {
                int openNote = OpenStrings[s] % 12;
                double y = offsetY + s * cellH + cellH / 2;

                for (int f = 0; f <= numFrets; f++)
                {
                    int note = (openNote + f) % 12;
                    if (!scaleNotes.Contains(note)) continue;

                    bool isRoot = note == rootNote;
                    double x = offsetX + f * cellW;

                    // Öppen sträng (band 0)
                    if (f == 0)
                    {
                        var circle = new Ellipse
                        {
                            Width = 12,
                            Height = 12,
                            Fill = isRoot
                                ? new SolidColorBrush(Color.FromRgb(0, 180, 120))
                                : new SolidColorBrush(Color.FromRgb(0, 100, 200)),
                            Stroke = Brushes.White,
                            StrokeThickness = 1
                        };
                        Canvas.SetLeft(circle, x - 14);
                        Canvas.SetTop(circle, y - 6);
                        canvas.Children.Add(circle);
                    }
                    else
                    {
                        var dot = new Ellipse
                        {
                            Width = isRoot ? 13 : 10,
                            Height = isRoot ? 13 : 10,
                            Fill = isRoot
                                ? new SolidColorBrush(Color.FromRgb(0, 200, 120))
                                : new SolidColorBrush(Color.FromRgb(0, 122, 204))
                        };
                        Canvas.SetLeft(dot, x - (isRoot ? 6.5 : 5) - cellW / 2);
                        Canvas.SetTop(dot, y - (isRoot ? 6.5 : 5));
                        canvas.Children.Add(dot);

                        // Notnamn inuti pricken
                        var label = new TextBlock
                        {
                            Text = NoteNames[note],
                            Foreground = Brushes.White,
                            FontSize = isRoot ? 7 : 6.5,
                            FontWeight = isRoot ? FontWeights.Bold : FontWeights.Normal,
                            TextAlignment = System.Windows.TextAlignment.Center,
                            Width = isRoot ? 13 : 10
                        };
                        Canvas.SetLeft(label, x - (isRoot ? 6.5 : 5) - cellW / 2);
                        Canvas.SetTop(label, y - (isRoot ? 4 : 3.5));
                        canvas.Children.Add(label);
                    }
                }
            }

            return canvas;
        }
    }
}
