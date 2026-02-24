using AiMusicWorkstation.Desktop.Models;
using AiMusicWorkstation.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AiMusicWorkstation.Desktop
{
    public partial class MainWindow : Window
    {
        private List<SongSection> _currentSections = new List<SongSection>();

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

        private void ClearSections()
        {
            _currentSections.Clear();
            RefreshSectionsList();
        }

        private void SectionsEdit_Click(object sender, RoutedEventArgs e)
        {
            bool isEdit = SectionsEditBtn.IsChecked == true;
            SectionsEditBar.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SectionItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 &&
                sender is FrameworkElement fe &&
                fe.DataContext is SongSection section)
            {
                var dialog = new EditSectionWindow(section.Label, section.StartTime)
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    section.Label = dialog.SectionName;
                    section.StartTime = dialog.SectionTime;
                    section.Color = SectionColors.Get(dialog.SectionName);

                    double total = _player.TotalTime.TotalSeconds;
                    _currentSections = _currentSections
                        .OrderBy(s => s.StartTime).ToList();

                    for (int i = 0; i < _currentSections.Count - 1; i++)
                        _currentSections[i].EndTime = _currentSections[i + 1].StartTime;
                    _currentSections[^1].EndTime = total;

                    RefreshSectionsList();
                    SaveLyricsAndChords(_player.CurrentStemsPath,
                        _currentLyrics.ToList(), _currentChords, _currentSections);
                }

                e.Handled = true;
            }
        }

        private async void AutoDetectStructure_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectList.SelectedItem is not SongProject p)
            { StatusLabel.Text = "Load a song first."; return; }

            double duration = _player.TotalTime.TotalSeconds;
            if (duration <= 0)
            { StatusLabel.Text = "Load a song first."; return; }

            StatusLabel.Text = "🤖 Fetching structure from AI...";
            AutoDetectBtn.IsEnabled = false;

            try
            {
                string json = await _pythonBridge.GetStructureAsync(p.Artist, p.Title, duration);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.GetProperty("status").GetString() != "success")
                {
                    string msg = doc.RootElement.TryGetProperty("message", out var m)
                        ? m.GetString() : "Unknown error";
                    StatusLabel.Text = $"AI: {msg}";
                    return;
                }

                var sectionsEl = doc.RootElement.GetProperty("sections");
                var newSections = new List<SongSection>();

                foreach (var s in sectionsEl.EnumerateArray())
                {
                    string label = s.GetProperty("label").GetString() ?? "Section";
                    newSections.Add(new SongSection
                    {
                        Label = label,
                        StartTime = s.GetProperty("start").GetDouble(),
                        EndTime = s.GetProperty("end").GetDouble(),
                        Color = SectionColors.Get(label)
                    });
                }

                _currentSections = newSections;
                RefreshSectionsList();
                SaveLyricsAndChords(_player.CurrentStemsPath,
                    _currentLyrics.ToList(), _currentChords, _currentSections);

                StatusLabel.Text = $"✅ Structure loaded: {_currentSections.Count} sections";
            }
            catch (Exception ex) { StatusLabel.Text = $"Error: {ex.Message}"; }
            finally { AutoDetectBtn.IsEnabled = true; }
        }
    }
}
