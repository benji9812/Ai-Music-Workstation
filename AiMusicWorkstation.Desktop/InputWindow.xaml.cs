using System.Windows;

namespace AiMusicWorkstation.Desktop
{
    public partial class InputWindow : Window
    {
        public string Answer { get; private set; }

        public InputWindow(string prompt, string defaultText = "")
        {
            InitializeComponent();
            PromptText.Text = prompt;
            InputBox.Text = defaultText;
            InputBox.Focus();
            InputBox.SelectAll();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Answer = InputBox.Text;
            DialogResult = true;
        }
    }
}