using AiMusicWorkstation.Desktop.Models;
using AiMusicWorkstation.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AiMusicWorkstation.Desktop
{
    public partial class MainWindow : Window
    {
        private List<SongSection> _currentSections = new List<SongSection>();

        private void TabSections_Click(object sender, RoutedEventArgs e)
        {
            TabChordBtn.IsChecked = false;
            TabScaleBtn.IsChecked = false;
            TabSectionsBtn.IsChecked = true;
            ChordView.Visibility = Visibility.Collapsed;
            ScaleView.Visibility = Visibility.Collapsed;
            SectionsView.Visibility = Visibility.Visible;
        }

        private void AddSection_Click(object sender, RoutedEventArgs e)
        {
            double total = _player.TotalTime.TotalSeconds;
            if (total <= 0) { StatusLabel.Text = "Load a song first."; return; }

            string label = (SectionTypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Section";
            int count = _currentSections.Count(s => s.Label.StartsWith(label)) + 1;
            string fullLabel = count == 1 ? label : $"{label} {count}";

            _currentSections.Add(new SongSection
            {
                Label = fullLabel,
                StartTime = _player.CurrentTime.TotalSeconds,
                EndTime = total,
                Color = SectionColors.Get(label)
            });

            _currentSections = _currentSections.OrderBy(s => s.StartTime).ToList();
            for (int i = 0; i < _currentSections.Count - 1; i++)
                _currentSections[i].EndTime = _currentSections[i + 1].StartTime;
            _currentSections[^1].EndTime = total;

            RefreshSectionsList();
            SaveLyricsAndChords(_player.CurrentStemsPath,
                _currentLyrics.ToList(), _currentChords, _currentSections);
        }

        private void RemoveSection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is SongSection sec)
            {
                _currentSections.Remove(sec);
                double total = _player.TotalTime.TotalSeconds;
                var sorted = _currentSections.OrderBy(s => s.StartTime).ToList();
                for (int i = 0; i < sorted.Count - 1; i++)
                    sorted[i].EndTime = sorted[i + 1].StartTime;
                if (sorted.Count > 0) sorted[^1].EndTime = total;
                _currentSections = sorted;

                RefreshSectionsList();
                SaveLyricsAndChords(_player.CurrentStemsPath,
                    _currentLyrics.ToList(), _currentChords, _currentSections);
            }
        }

        private void SectionItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                sender is FrameworkElement fe &&
                fe.DataContext is SongSection section)
            {
                _player.CurrentTime = TimeSpan.FromSeconds(section.StartTime);
                _isTimerUpdate = true;
                TimelineSlider.Value = section.StartTime;
                _isTimerUpdate = false;
                UpdateTimerText();
            }
        }

        private void RefreshSectionsList()
        {
            SectionsList.ItemsSource = null;
            SectionsList.ItemsSource = _currentSections;
        }

        private void HighlightActiveSection(double currentTime)
        {
            foreach (var sec in _currentSections)
            {
                double end = sec.EndTime > 0 ? sec.EndTime : TimelineSlider.Maximum;
                sec.IsActive = currentTime >= sec.StartTime && currentTime < end;
            }
        }
    }
}
