using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Data;
using System.ComponentModel;
using Microsoft.Win32;
using NAudio.Wave;
using System.Collections.ObjectModel;

using AiMusicWorkstation.Desktop.Services;
using AiMusicWorkstation.Desktop.Models;

namespace AiMusicWorkstation.Desktop
{
    public partial class MainWindow : Window
    {
        private PythonBridge _pythonBridge = new PythonBridge();
        private StemPlayer _player = new StemPlayer();
        private LibraryManager _library = new LibraryManager();
        private SmartImporter _importer = new SmartImporter();

        private DispatcherTimer _timelineTimer;
        private bool _isDraggingTimeline = false;
        private bool _isTimerUpdate = false;      // NY — ersätter _isClickingTimeline
        private bool _isTransposing = false;

        private double _currentPrimaryBpm = 0;
        private double _currentAltBpm = 0;
        private bool _showingAltBpm = false;
        private string _originalKey = "--";
        private int _currentSemitones = 0;

        private static readonly string[] NoteNames =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "Bb", "B" };

        private ObservableCollection<LyricSegment> _currentLyrics = new ObservableCollection<LyricSegment>();
        private List<ChordEvent> _currentChords = new List<ChordEvent>();

        public MainWindow()
        {
            InitializeComponent();

            Closing += (s, e) => _player.Dispose();

            _player.PlaybackStopped += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_isTransposing) return;
                    PlayPauseBtn.Content = "▶";
                    _timelineTimer.Stop();
                    _isTimerUpdate = true;
                    TimelineSlider.Value = 0;
                    _isTimerUpdate = false;
                });
            };

            _timelineTimer = new DispatcherTimer();
            _timelineTimer.Interval = TimeSpan.FromMilliseconds(50);
            _timelineTimer.Tick += Timer_Tick;

            BpmText.MouseDown += BpmText_MouseDown;
            RefreshLibrary();
            UpdateMixerUIState();
        }

        // --- BPM TOGGLE ---
        private void BpmText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentPrimaryBpm <= 0) return;
            _showingAltBpm = !_showingAltBpm;
            double val = _showingAltBpm ? _currentAltBpm : _currentPrimaryBpm;
            BpmText.Text = val.ToString("F0");
            StatusLabel.Text = _showingAltBpm ? "Showing Alternative BPM" : "Showing Primary BPM";
        }

        // --- ANALYS & LADDNING ---
        private async Task AnalyzeAndLoadSong(string inputPath, string customTitle = null, string spotifyUrl = null, string artistName = null)
        {
            Stop_Click(null, null);

            _currentLyrics = new ObservableCollection<LyricSegment>();
            _currentChords = new List<ChordEvent>();
            LyricsList.ItemsSource = _currentLyrics;
            CurrentChordText.Text = "";
            NextChordsText.Text = "";
            ChordDiagramHost.Child = null;
            BpmText.Text = "--";
            KeyText.Text = "--";
            _originalKey = "--";
            _currentSemitones = 0;
            TransposeLabel.Text = "0";

            StatusLabel.Text = "Separating Stems (AI)... This may take a minute.";
            AnalysisProgress.Visibility = Visibility.Visible;

            try
            {
                string fileForPython = inputPath;
                string pathForPlayer = inputPath;

                if (Directory.Exists(inputPath))
                {
                    string vocals = Path.Combine(inputPath, "vocals.mp3");
                    string other = Path.Combine(inputPath, "other.mp3");
                    if (File.Exists(vocals)) fileForPython = vocals;
                    else if (File.Exists(other)) fileForPython = other;
                    else
                    {
                        var firstFile = Directory.GetFiles(inputPath, "*.mp3").FirstOrDefault()
                                        ?? Directory.GetFiles(inputPath, "*.wav").FirstOrDefault();
                        if (firstFile != null) fileForPython = firstFile;
                    }
                }

                string jsonResponse = await _pythonBridge.RunAnalysisAsync(fileForPython, useCloud: true);

                int jsonStartIndex = jsonResponse.IndexOf('{');
                if (jsonStartIndex == -1)
                {
                    StatusLabel.Text = "Analysis Error.";
                    MessageBox.Show($"Python gav inget giltigt svar:\n{jsonResponse}");
                    return;
                }

                string cleanJson = jsonResponse.Substring(jsonStartIndex);
                AnalysisResult analysisData = null;

                try
                {
                    analysisData = JsonSerializer.Deserialize<AnalysisResult>(cleanJson);
                }
                catch (Exception ex)
                {
                    StatusLabel.Text = "Data Format Error.";
                    MessageBox.Show($"Kunde inte läsa datan från Python.\nFel: {ex.Message}");
                    return;
                }

                if (analysisData == null || analysisData.Status != "success")
                {
                    StatusLabel.Text = "Analysis failed.";
                    string errorMsg = analysisData?.Message ?? "Okänt fel";
                    MessageBox.Show($"AI-motorn rapporterade ett fel:\n\n{errorMsg}");
                    return;
                }

                _currentLyrics = new ObservableCollection<LyricSegment>(
                    analysisData.Lyrics ?? new List<LyricSegment>());
                _currentChords = analysisData.Chords ?? new List<ChordEvent>();
                LyricsList.ItemsSource = _currentLyrics;

                if (analysisData.Bpm > 0)
                {
                    _currentPrimaryBpm = analysisData.Bpm;
                    _currentAltBpm = _currentPrimaryBpm > 100 ? _currentPrimaryBpm / 2 : _currentPrimaryBpm * 2;
                    BpmText.Text = _currentPrimaryBpm.ToString("F0");
                }

                string detectedKey = !string.IsNullOrEmpty(analysisData.Key) ? analysisData.Key : "--";
                KeyText.Text = detectedKey;
                _originalKey = detectedKey;

                bool isSpotify = !string.IsNullOrEmpty(spotifyUrl);
                OfficialBadge.Visibility = isSpotify ? Visibility.Visible : Visibility.Collapsed;
                AiBadge.Visibility = !isSpotify ? Visibility.Visible : Visibility.Collapsed;

                if (!string.IsNullOrEmpty(analysisData.StemsPath) && Directory.Exists(analysisData.StemsPath))
                    pathForPlayer = analysisData.StemsPath;

                _player.LoadStems(pathForPlayer);
                TimelineSlider.Maximum = _player.TotalTime.TotalSeconds;
                TotalTimeText.Text = _player.TotalTime.ToString(@"mm\:ss");

                SaveLyricsAndChords(pathForPlayer, _currentLyrics.ToList(), _currentChords);

                bool alreadyExists = _library.Projects.Any(p => p.StemsPath == pathForPlayer);
                if (!alreadyExists)
                {
                    string title = customTitle ?? Path.GetFileNameWithoutExtension(inputPath);
                    string artist = artistName ?? "Unknown Artist";

                    if (string.IsNullOrEmpty(artistName) && title.Contains(" - "))
                    {
                        var parts = title.Split(new[] { " - " }, 2, StringSplitOptions.None);
                        artist = parts[0].Trim();
                        title = parts[1].Trim();
                    }

                    _library.AddProject(new SongProject
                    {
                        Title = title,
                        Artist = artist,
                        Bpm = _currentPrimaryBpm,
                        Key = detectedKey,
                        StemsPath = pathForPlayer,
                        Duration = _player.TotalTime,
                        GroupName = "General",
                        DateAdded = DateTime.Now,
                        IsOfficialData = isSpotify
                    });
                    RefreshLibrary();
                }

                HideImportSection();
                StatusLabel.Text = "Ready to Mix! 🎚️";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Critical Error: " + ex.Message);
                StatusLabel.Text = "Failed.";
            }
            finally
            {
                AnalysisProgress.Visibility = Visibility.Hidden;
                UpdateMixerUIState();
            }
        }

        // --- SESSION: SPARA & LADDA LYRICS/CHORDS ---
        private void SaveLyricsAndChords(string stemsPath, List<LyricSegment> lyrics, List<ChordEvent> chords)
        {
            try
            {
                string dir = Directory.Exists(stemsPath) ? stemsPath : Path.GetDirectoryName(stemsPath);
                var data = new { lyrics, chords };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(dir, "session.json"), json);
            }
            catch { }
        }

        private (List<LyricSegment> lyrics, List<ChordEvent> chords) LoadLyricsAndChords(string stemsPath)
        {
            try
            {
                string dir = Directory.Exists(stemsPath) ? stemsPath : Path.GetDirectoryName(stemsPath);
                string file = Path.Combine(dir, "session.json");
                if (!File.Exists(file)) return (new List<LyricSegment>(), new List<ChordEvent>());

                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var lyrics = JsonSerializer.Deserialize<List<LyricSegment>>(
                    doc.RootElement.GetProperty("lyrics").GetRawText()) ?? new List<LyricSegment>();
                var chords = JsonSerializer.Deserialize<List<ChordEvent>>(
                    doc.RootElement.GetProperty("chords").GetRawText()) ?? new List<ChordEvent>();

                return (lyrics, chords);
            }
            catch { return (new List<LyricSegment>(), new List<ChordEvent>()); }
        }

        private void UpdateMixerUIState()
        {
            VolDrums.IsEnabled = VolBass.IsEnabled = VolOther.IsEnabled =
                VolVocals.IsEnabled = SliderMaster.IsEnabled = true;
            VolDrums.Opacity = VolBass.Opacity = VolOther.Opacity = VolVocals.Opacity = 1.0;
        }

        // --- IMPORT VISA/DÖLJ ---
        private void HideImportSection()
        {
            ImportSection.Visibility = Visibility.Collapsed;
            ShowImportBtn.Visibility = Visibility.Visible;
        }

        private void ShowImport_Click(object sender, RoutedEventArgs e)
        {
            ImportSection.Visibility = Visibility.Visible;
            ShowImportBtn.Visibility = Visibility.Collapsed;
        }

        // --- TIMELINE ---
        private void Timeline_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDraggingTimeline = true;
        }

        private void Timeline_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDraggingTimeline = false;
            if (_player != null)
                _player.CurrentTime = TimeSpan.FromSeconds(TimelineSlider.Value);
            UpdateTimerText();
        }

        private void Timeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isTimerUpdate) return;

            if (_isDraggingTimeline)
            {
                CurrentTimeText.Text = TimeSpan.FromSeconds(e.NewValue).ToString(@"mm\:ss");
            }
            else
            {
                // Klick via IsMoveToPointEnabled
                if (_player != null && _player.TotalTime.TotalSeconds > 0)
                    _player.CurrentTime = TimeSpan.FromSeconds(e.NewValue);
                UpdateTimerText();
            }
        }

        // --- SEEK ---
        private void SeekBack_Click(object sender, RoutedEventArgs e)
        {
            var t = _player.CurrentTime - TimeSpan.FromSeconds(10);
            _player.CurrentTime = t < TimeSpan.Zero ? TimeSpan.Zero : t;
            _isTimerUpdate = true;
            TimelineSlider.Value = _player.CurrentTime.TotalSeconds;
            _isTimerUpdate = false;
            UpdateTimerText();
        }

        private void SeekForward_Click(object sender, RoutedEventArgs e)
        {
            var t = _player.CurrentTime + TimeSpan.FromSeconds(10);
            _player.CurrentTime = t > _player.TotalTime ? _player.TotalTime : t;
            _isTimerUpdate = true;
            TimelineSlider.Value = _player.CurrentTime.TotalSeconds;
            _isTimerUpdate = false;
            UpdateTimerText();
        }

        // --- TIMER TICK ---
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_player.IsPlaying || _isDraggingTimeline) return;

            _isTimerUpdate = true;
            TimelineSlider.Value = _player.CurrentTime.TotalSeconds;
            _isTimerUpdate = false;
            UpdateTimerText();

            double t = _player.CurrentTime.TotalSeconds;

            if (_currentLyrics != null && _currentLyrics.Any())
            {
                var activeLine = _currentLyrics.FirstOrDefault(l => t >= l.Start && t <= l.End);
                if (activeLine != null && !activeLine.IsActive)
                {
                    foreach (var line in _currentLyrics) line.IsActive = false;
                    activeLine.IsActive = true;

                    // Auto-scrolla till aktiv rad
                    LyricsList.ScrollIntoView(activeLine);
                }
            }

            if (_currentChords != null && _currentChords.Any())
            {
                var activeChord = _currentChords.LastOrDefault(c => c.Time <= t);
                if (activeChord != null && CurrentChordText.Text != activeChord.Chord)
                {
                    CurrentChordText.Text = activeChord.Chord;
                    ChordDiagramHost.Child = (UIElement)ChordDiagramRenderer.Render(activeChord.Chord, 100);

                    var upcoming = _currentChords
                        .Where(c => c.Time > t)
                        .Take(3)
                        .Select(c => c.Chord);
                    NextChordsText.Text = string.Join("  →  ", upcoming);
                }
            }
        }

        private void UpdateTimerText()
        {
            CurrentTimeText.Text = _player.CurrentTime.ToString(@"mm\:ss");
            TotalTimeText.Text = _player.TotalTime.ToString(@"mm\:ss");
        }

        // --- VOLYM ---
        private void VolumeInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { ApplyManualVolume(sender as TextBox); Keyboard.ClearFocus(); }
        }

        private void VolumeInput_LostFocus(object sender, RoutedEventArgs e) => ApplyManualVolume(sender as TextBox);

        private void ApplyManualVolume(TextBox tb)
        {
            if (tb == null || tb.Tag == null) return;
            if (double.TryParse(tb.Text, out double val))
            {
                val = Math.Max(0, Math.Min(100, val));
                float norm = (float)(val / 100.0);
                string tag = tb.Tag.ToString();

                if (tag == "master") { SliderMaster.Value = norm; _player.SetMasterVolume(norm); }
                else if (tag == "drums") { VolDrums.Value = norm; _player.SetVolume(tag, norm); }
                else if (tag == "bass") { VolBass.Value = norm; _player.SetVolume(tag, norm); }
                else if (tag == "other") { VolOther.Value = norm; _player.SetVolume(tag, norm); }
                else if (tag == "vocals") { VolVocals.Value = norm; _player.SetVolume(tag, norm); }

                tb.Text = val.ToString("F0");
            }
            else { SyncUIFromSliders(); }
        }

        private void SyncUIFromSliders()
        {
            if (InputMasterVol != null) InputMasterVol.Text = (SliderMaster.Value * 100).ToString("F0");
            if (InputDrums != null) InputDrums.Text = (VolDrums.Value * 100).ToString("F0");
            if (InputBass != null) InputBass.Text = (VolBass.Value * 100).ToString("F0");
            if (InputOther != null) InputOther.Text = (VolOther.Value * 100).ToString("F0");
            if (InputVocals != null) InputVocals.Text = (VolVocals.Value * 100).ToString("F0");
        }

        // --- TRANSPONERING ---
        private void TransposeUp_Click(object sender, RoutedEventArgs e) => SetTranspose(_currentSemitones + 1);
        private void TransposeDown_Click(object sender, RoutedEventArgs e) => SetTranspose(_currentSemitones - 1);
        private void TransposeReset_Click(object sender, RoutedEventArgs e) => SetTranspose(0);

        private void SetTranspose(int semitones)
        {
            _currentSemitones = Math.Clamp(semitones, -12, 12);
            TransposeLabel.Text = _currentSemitones.ToString("+0;-0;0");

            _isTransposing = true;
            _player.SemitoneShift = _currentSemitones;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (s, e) =>
            {
                _isTransposing = false;
                timer.Stop();
                if (_player.IsPlaying && !_timelineTimer.IsEnabled)
                    _timelineTimer.Start();
            };
            timer.Start();

            if (!string.IsNullOrEmpty(_originalKey) && _originalKey != "--")
            {
                string transposedKey = TransposeKey(_originalKey, _currentSemitones);
                KeyText.Text = transposedKey;

                if (ProjectList.SelectedItem is SongProject activeProject)
                {
                    activeProject.Key = transposedKey;
                    _library.SaveLibrary();
                    RefreshLibrary();
                }

                StatusLabel.Text = _currentSemitones == 0
                    ? $"Original key: {_originalKey}"
                    : $"Transposed {(_currentSemitones > 0 ? "+" : "")}{_currentSemitones} st  ({_originalKey} → {transposedKey})";
            }
        }

        private string TransposeKey(string key, int semitones)
        {
            bool isMinor = key.EndsWith("m");
            string root = isMinor ? key[..^1] : key;
            int idx = Array.IndexOf(NoteNames, root);
            if (idx == -1) return key;
            int newIdx = ((idx + semitones) % 12 + 12) % 12;
            return NoteNames[newIdx] + (isMinor ? "m" : "");
        }

        // --- MASTER & STEM VOLYM ---
        private void MasterVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_player != null)
            {
                _player.SetMasterVolume((float)e.NewValue);
                if (InputMasterVol != null) InputMasterVol.Text = (e.NewValue * 100).ToString("F0");
            }
        }

        private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_player != null && sender is Slider s && s.Tag != null)
            {
                _player.SetVolume(s.Tag.ToString(), (float)e.NewValue);
                UpdateVolumeUI(s.Tag.ToString(), (float)e.NewValue);
            }
        }

        private void VolumeSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider s && s.Tag != null)
            {
                string tag = s.Tag.ToString();
                float val = (float)s.Value;
                if (tag == "master")
                {
                    _player.SetMasterVolume(val);
                    if (InputMasterVol != null) InputMasterVol.Text = (val * 100).ToString("F0");
                }
                else { _player.SetVolume(tag, val); UpdateVolumeUI(tag, val); }
            }
        }

        private void UpdateVolumeUI(string tag, float vol)
        {
            string v = ((int)(vol * 100)).ToString();
            if (tag == "drums" && InputDrums != null) InputDrums.Text = v;
            else if (tag == "bass" && InputBass != null) InputBass.Text = v;
            else if (tag == "other" && InputOther != null) InputOther.Text = v;
            else if (tag == "vocals" && InputVocals != null) InputVocals.Text = v;
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton b && b.Tag != null)
                _player.SetMute(b.Tag.ToString(), b.IsChecked == true);
        }

        private void Solo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton b && b.Tag != null)
                _player.SetSolo(b.Tag.ToString(), b.IsChecked == true);
        }

        // --- TRANSPORT ---
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_player.IsPlaying)
            {
                _player.Pause();
                PlayPauseBtn.Content = "▶";
                _timelineTimer.Stop();
            }
            else
            {
                _player.Play();
                PlayPauseBtn.Content = "⏸";
                _timelineTimer.Start();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _player.Stop();
            PlayPauseBtn.Content = "▶";
            _timelineTimer.Stop();
            _isTimerUpdate = true;
            TimelineSlider.Value = 0;
            _isTimerUpdate = false;
            UpdateTimerText();
        }

        private void Loop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton b) _player.IsLooping = b.IsChecked == true;
        }

        // --- LIBRARY ---
        private void RefreshLibrary()
        {
            if (_library?.Projects == null || ProjectList == null ||
                SearchBox == null || SortCombo == null) return;

            var filtered = _library.Projects.AsEnumerable();
            string search = SearchBox.Text.ToLower();

            if (!string.IsNullOrWhiteSpace(search))
                filtered = filtered.Where(p =>
                    p.Title.ToLower().Contains(search) ||
                    (p.Artist != null && p.Artist.ToLower().Contains(search)));

            if (SortCombo.SelectedItem is ComboBoxItem sortItem)
            {
                switch (sortItem.Content.ToString())
                {
                    case "Latest": filtered = filtered.OrderByDescending(p => p.DateAdded); break;
                    case "A-Z": filtered = filtered.OrderBy(p => p.Title); break;
                    case "BPM": filtered = filtered.OrderBy(p => p.Bpm); break;
                }
            }

            ICollectionView view = CollectionViewSource.GetDefaultView(filtered.ToList());
            if (view != null)
            {
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(new PropertyGroupDescription("GroupName"));
                ProjectList.ItemsSource = view;
            }
        }

        private async void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectList.SelectedItem is SongProject p)
            {
                if (Directory.Exists(p.StemsPath) || File.Exists(p.StemsPath))
                {
                    _player.Stop();
                    _player.LoadStems(p.StemsPath);

                    BpmText.Text = p.Bpm.ToString("F0");
                    _currentPrimaryBpm = p.Bpm;
                    _currentAltBpm = _currentPrimaryBpm > 100 ? _currentPrimaryBpm / 2 : _currentPrimaryBpm * 2;
                    _showingAltBpm = false;
                    KeyText.Text = p.Key;
                    _originalKey = p.Key;
                    _currentSemitones = 0;
                    TransposeLabel.Text = "0";

                    var (lyrics, chords) = LoadLyricsAndChords(p.StemsPath);
                    _currentLyrics = new ObservableCollection<LyricSegment>(lyrics);
                    _currentChords = chords;
                    LyricsList.ItemsSource = _currentLyrics.Any() ? _currentLyrics : null;

                    if (_currentChords.Any())
                    {
                        CurrentChordText.Text = _currentChords[0].Chord;
                        ChordDiagramHost.Child = (UIElement)ChordDiagramRenderer.Render(_currentChords[0].Chord, 100);
                        var upcoming = _currentChords.Skip(1).Take(3).Select(c => c.Chord);
                        NextChordsText.Text = string.Join("  →  ", upcoming);
                    }
                    else
                    {
                        CurrentChordText.Text = "";
                        NextChordsText.Text = "";
                        ChordDiagramHost.Child = null;
                    }

                    HideImportSection();

                    StatusLabel.Text = $"Loaded: {p.Artist} – {p.Title}";
                    OfficialBadge.Visibility = p.IsOfficialData ? Visibility.Visible : Visibility.Collapsed;
                    AiBadge.Visibility = !p.IsOfficialData ? Visibility.Visible : Visibility.Collapsed;

                    _isTimerUpdate = true;
                    TimelineSlider.Maximum = _player.TotalTime.TotalSeconds;
                    TimelineSlider.Value = 0;
                    _isTimerUpdate = false;
                    UpdateTimerText();
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshLibrary();
        private void Filter_Changed(object sender, SelectionChangedEventArgs e) => RefreshLibrary();

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is SongProject p)
            {
                var input = new InputWindow("Rename:", p.Title);
                if (input.ShowDialog() == true)
                { p.Title = input.Answer; _library.SaveLibrary(); RefreshLibrary(); }
            }
        }

        private void MoveToGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is SongProject p)
            {
                var input = new InputWindow("Enter Folder:", p.GroupName);
                if (input.ShowDialog() == true)
                { p.GroupName = input.Answer; _library.SaveLibrary(); RefreshLibrary(); }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is SongProject p)
            {
                if (MessageBox.Show($"Delete {p.Title}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                { _library.Projects.Remove(p); _library.SaveLibrary(); RefreshLibrary(); }
            }
        }

        // --- IMPORT / DOWNLOAD ---
        private async void YoutubeDownload_Click(object sender, RoutedEventArgs e)
        {
            string url = YoutubeLinkBox.Text;
            if (string.IsNullOrWhiteSpace(url) || url.Contains("Paste")) return;
            try
            {
                var result = await _importer.DownloadSongAsync(url, new Progress<string>(s => StatusLabel.Text = s));
                string spotifyUrlOrId = !string.IsNullOrEmpty(result.SpotifyId) ? result.SpotifyId : url;
                await AnalyzeAndLoadSong(result.FilePath, result.Title, spotifyUrlOrId, result.Artist);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Audio|*.mp3;*.wav;*.m4a" };
            if (ofd.ShowDialog() == true) await AnalyzeAndLoadSong(ofd.FileName);
        }

        private void ExportMix_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_player.CurrentStemsPath)) return;
            var sfd = new SaveFileDialog { Filter = "WAV Audio|*.wav", FileName = "MyRemix.wav" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    StatusLabel.Text = "Exporting... 💾";
                    _player.ExportMix(sfd.FileName,
                        (float)VolDrums.Value, (float)VolBass.Value,
                        (float)VolOther.Value, (float)VolVocals.Value);
                    MessageBox.Show("Saved!");
                }
                finally { StatusLabel.Text = "Ready"; }
            }
        }

        private void YoutubeBox_GotFocus(object sender, RoutedEventArgs e)
        { if (YoutubeLinkBox.Text.Contains("Paste")) YoutubeLinkBox.Text = ""; }

        private void YoutubeBox_LostFocus(object sender, RoutedEventArgs e)
        { if (string.IsNullOrWhiteSpace(YoutubeLinkBox.Text)) YoutubeLinkBox.Text = "Paste YouTube or Spotify Link here..."; }

        private void LyricsLine_Click(object sender, SelectionChangedEventArgs e)
        {
            if (LyricsList.SelectedItem is LyricSegment line)
            {
                _isTimerUpdate = true;
                _player.CurrentTime = TimeSpan.FromSeconds(line.Start);
                TimelineSlider.Value = line.Start;
                _isTimerUpdate = false;
                UpdateTimerText();

                // Deselecta direkt så highlight-logiken i timern fortsätter fungera
                LyricsList.SelectedItem = null;
            }
        }

        // --- VISA/DÖLJ LYRICS & ACKORD ---
        private void ToggleChords_Click(object sender, RoutedEventArgs e)
        {
            bool show = ToggleChordsBtn.IsChecked == true;
            ChordPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ChordColumn.Width = show ? new GridLength(190) : new GridLength(0);
        }

        private void ToggleLyrics_Click(object sender, RoutedEventArgs e)
        {
            LyricsPanel.Visibility = ToggleLyricsBtn.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
