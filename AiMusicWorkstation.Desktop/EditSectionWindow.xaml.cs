using System.Windows;

namespace AiMusicWorkstation.Desktop
{
    public partial class EditSectionWindow : Window
    {
        public string SectionName { get; private set; }
        public double SectionTime { get; private set; }

        public EditSectionWindow(string currentName, double currentTime)
        {
            InitializeComponent();
            NameBox.Text = currentName;
            var ts = System.TimeSpan.FromSeconds(currentTime);
            TimeBox.Text = ts.ToString(@"m\:ss");
            NameBox.Focus();
            NameBox.SelectAll();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Name cannot be empty.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseTime(TimeBox.Text, out double seconds))
            {
                MessageBox.Show("Invalid time format. Use m:ss (e.g. 1:30).",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SectionName = NameBox.Text.Trim();
            SectionTime = seconds;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private bool TryParseTime(string input, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            input = input.Trim();

            // Försök m:ss och mm:ss
            var formats = new[] { @"m\:ss", @"mm\:ss", @"m\:s", @"h\:mm\:ss" };
            foreach (var fmt in formats)
            {
                if (TimeSpan.TryParseExact(input, fmt, null, out var ts))
                {
                    seconds = ts.TotalSeconds;
                    return true;
                }
            }

            // Försök standard TimeSpan-parsning som fallback
            if (TimeSpan.TryParse(input, out var ts2))
            {
                seconds = ts2.TotalSeconds;
                return true;
            }

            // Bara sekunder som sista fallback
            if (double.TryParse(input, out double raw))
            {
                seconds = raw;
                return true;
            }

            return false;
        }

    }
}
