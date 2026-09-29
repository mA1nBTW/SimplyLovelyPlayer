using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TagLib;
using System.IO;
using Newtonsoft.Json;
using System.Windows.Data;

namespace SimplyLovelyPlayer
{
    internal class SongsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Song> Songs { get; set; } = new ObservableCollection<Song>();
        public ObservableCollection<Song> LikedSongs { get; set; } = new ObservableCollection<Song>();
        public ICollectionView SongsView { get; }

        public ICommand ReplaceSongsCommand { get; set; }
        public ICommand AddSongsCommand { get; set; }
        public ICommand PlayCommand { get; set; }
        public ICommand PauseCommand { get; set; }
        public ICommand ResumeCommand { get; set; }
        public ICommand StopCommand { get; set; }
        public ICommand NextSongCommand { get; set; }
        public ICommand PreviousSongCommand { get; set; }
        public ICommand ToggleShuffleCommand { get; set; }
        public ICommand ToggleRepeatCommand { get; set; }
        public ICommand RemoveCommand { get; set; }
        public ICommand LikeCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private Song _currentSong;
        public Song CurrentSong
        {
            get => _currentSong;
            set
            {
                if (_currentSong != value)
                {
                    _currentSong = value;
                    OnPropertyChanged(nameof(CurrentSong));
                }
            }
        }

        private bool _isShuffle;
        public bool IsShuffle
        {
            get => _isShuffle;
            set
            {
                _isShuffle = value;
                OnPropertyChanged(nameof(IsShuffle));
            }
        }

        private bool _isRepeat;
        public bool IsRepeat
        {
            get => _isRepeat;
            set
            {
                _isRepeat = value;
                OnPropertyChanged(nameof(IsRepeat));
            }
        }

        private Song _selectedSong;
        public Song SelectedSong
        {
            get => _selectedSong;
            set
            {
                _selectedSong = value;
                OnPropertyChanged(nameof(SelectedSong));
            }
        }

        private TimeSpan _currentPosition;
        public TimeSpan CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (_currentPosition != value)
                {
                    _currentPosition = value;
                    OnPropertyChanged(nameof(CurrentPosition));
                    OnPropertyChanged(nameof(CurrentPositionSeconds));
                    OnPropertyChanged(nameof(CurrentPositionFormatted));
                }
            }
        }

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged();
                SongsView.Refresh();
            }
        }

        public double CurrentPositionSeconds
        {
            get => CurrentPosition.TotalSeconds;
            set
            {
                var newTime = TimeSpan.FromSeconds(value);
                if (CurrentPosition != newTime)
                {
                    mediaElement.Position = newTime;
                    CurrentPosition = newTime;
                }
            }
        }


        private DispatcherTimer _timer;

        private void StartTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };

            _timer.Tick += (s, e) =>
            {
                if (mediaElement.Source != null && mediaElement.NaturalDuration.HasTimeSpan)
                {
                    CurrentPosition = mediaElement.Position;
                }
            };

            _timer.Start();
        }


        public double Progress => CurrentSong != null && CurrentSong.Duration.TotalSeconds > 0
            ? CurrentPosition.TotalSeconds / CurrentSong.Duration.TotalSeconds * 100
            : 0;

        public string CurrentPositionFormatted => $"{CurrentPosition.Minutes:D2}:{CurrentPosition.Seconds:D2}";


        private MediaElement mediaElement;

        public SongsViewModel(MediaElement mediaPlayer)
        {
            mediaElement = mediaPlayer;
            PlayCommand = new RelayCommand(PlaySong);
            PauseCommand = new RelayCommand(PauseSong);
            ReplaceSongsCommand = new RelayCommand(ReplaceSongs);
            AddSongsCommand = new RelayCommand(AddSongs);
            ResumeCommand = new RelayCommand(ResumeSong);
            StopCommand = new RelayCommand(StopSong);
            NextSongCommand = new RelayCommand(NextSong);
            PreviousSongCommand = new RelayCommand(PreviousSong);
            ToggleShuffleCommand = new RelayCommand(ToggleShuffle);
            ToggleRepeatCommand = new RelayCommand(ToggleRepeat);
            RemoveCommand = new RelayCommand(RemoveSong);
            LikeCommand = new RelayCommand(LikeSong);

            SongsView = CollectionViewSource.GetDefaultView(Songs);
            SongsView.Filter = song => FilterSongs(song as Song);

            Songs.CollectionChanged += (s, e) => UpdateSongIndexes();

            mediaElement.MediaEnded += (s, e) => OnSongEnded();
        }

        private bool FilterSongs(Song song)
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return true;
            return song.Title.IndexOf(SearchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LikeSong(object parameter)
        {
            if (parameter is Song song)
            {
                if (song != null)
                {
                    song.isLiked = !song.isLiked;
                    if (song.isLiked && !LikedSongs.Contains(song))
                    {
                        LikedSongs.Add(song);
                    }
                    else if (!song.isLiked && LikedSongs.Contains(song))
                    {
                        LikedSongs.Remove(song);
                    }

                    UpdateSongIndexes();
                    OnPropertyChanged(nameof(song));
                }
            }
        }

        private void ToggleShuffle(object parameter)
        {
            IsShuffle = !IsShuffle;
        }

        private void ToggleRepeat(object parameter)
        {
            IsRepeat = !IsRepeat;
        }

        private void OnSongEnded()
        {
            if (IsRepeat)
            {
                CurrentSong.State = SongState.Stopped;
                PlaySong(CurrentSong);
            }
            else if (IsShuffle)
            {
                CurrentSong.State = SongState.Stopped;
                var random = new Random();
                var nextIndex = random.Next(Songs.Count);
                CurrentSong = Songs[nextIndex];
                PlaySong(CurrentSong);
            }
            else
            {
                NextSong(null);
            }
        }

        private void RemoveSong(object parameter)
        {
            if (parameter is Song song)
            {
                if (song != null)
                {
                    if (song == CurrentSong && CurrentSong.State == SongState.Playing)
                    {
                        if (Songs.Count > 1)
                        {
                            NextSong(null);
                        }
                    }
                    Songs.Remove(song);
                }
            }
        }

        private void PlaySong(object parameter)
        {
            if (parameter is Song song)
            {
                if (CurrentSong != null && CurrentSong.State == SongState.Pause && CurrentSong == song)
                {
                    mediaElement.Play();
                    CurrentSong.State = SongState.Playing;
                    return;
                }
                else if (CurrentSong != null && CurrentSong == song && CurrentSong.State == SongState.Playing)
                {
                    mediaElement.Pause();
                    CurrentSong.State = SongState.Pause;
                    return;
                }

                foreach (var s in Songs)
                {
                    s.State = SongState.Stopped;
                }

                song.State = SongState.Playing;
                CurrentSong = song;
                SelectedSong = CurrentSong;
                CurrentSong.State = SongState.Playing;

                mediaElement.Source = new Uri(song.FilePath);
                mediaElement.Position = TimeSpan.Zero;
                mediaElement.Play();

                StartTimer();

                OnPropertyChanged(nameof(CurrentSong));
            }
        }

        private void PauseSong(object parameter)
        {
            if (CurrentSong != null && CurrentSong.State == SongState.Playing)
            {
                mediaElement.Pause();
                CurrentSong.State = SongState.Pause;
            }
        }

        private void ResumeSong(object parameter = null)
        {
            if (CurrentSong != null)
            {
                if (CurrentSong.State == SongState.Playing)
                {
                    mediaElement.Pause();
                    CurrentSong.State = SongState.Pause;
                }
                else if (CurrentSong.State == SongState.Pause)
                {
                    mediaElement.Play();
                    CurrentSong.State = SongState.Playing; ;
                }
            }
            else
            {
                return;
            }

            OnPropertyChanged(nameof(CurrentSong));
        }

        private void StopSong(object parameter)
        {
            if (CurrentSong != null)
            {
                mediaElement.Stop();
                _timer?.Stop();
                CurrentPosition = TimeSpan.Zero;
                CurrentSong.State = SongState.Stopped;
            }
        }

        private void NextSong(object parameter)
        {
            if (Songs.Count > 0 && Songs.Count != 1)
            {
                int currentIndex = Songs.IndexOf(CurrentSong);
                int nextIndex = (currentIndex + 1) % Songs.Count;
                CurrentSong = Songs[nextIndex];
                OnPropertyChanged(nameof(CurrentSong));
                PlaySong(CurrentSong);
            }
            else if (Songs.Count == 1 && CurrentSong != Songs[0])
            {
                CurrentSong.State = SongState.Stopped;
                CurrentSong = Songs[0];
                OnPropertyChanged(nameof(CurrentSong));
                PlaySong(CurrentSong);
            }
            //else if (Songs.Count == 1)
            //{
            //    CurrentSong.State = SongState.Stopped;
            //    PlaySong(CurrentSong);
            //}
        }

        private void PreviousSong(object parameter)
        {
            if (Songs.Count > 0 && Songs.Count != 1)
            {
                int currentIndex = Songs.IndexOf(CurrentSong);
                int previousIndex = (currentIndex - 1 + Songs.Count) % Songs.Count;
                CurrentSong = Songs[previousIndex];
                OnPropertyChanged(nameof(CurrentSong));
                PlaySong(CurrentSong);
            }
        }


        private void ReplaceSongs(object parameter)
        {
            var files = OpenFileDialogForSongs();
            if (files == null || files.Length == 0)
                return;

            Songs.Clear();
            foreach (var filePath in files)
            {
                Songs.Add(CreateSongFromFile(filePath));
            }
        }

        private void AddSongs(object parameter)
        {
            var files = OpenFileDialogForSongs();
            if (files == null || files.Length == 0)
                return;

            foreach (var filePath in files)
            {
                Songs.Add(CreateSongFromFile(filePath));
            }
        }

        private string[] OpenFileDialogForSongs()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                FileName = "Music",
                DefaultExt = ".mp3",
                Multiselect = true,
                Filter = "MP3 Files (*.mp3)|*.mp3|All Files (*.*)|*.*"
            };

            try
            {
                bool? result = openFileDialog.ShowDialog();
                Console.WriteLine($"Dialog result: {result}");
                return result == true ? openFileDialog.FileNames : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening dialog: {ex.Message}");
                return null;
            }
;
        }

        private Song CreateSongFromFile(string filePath)
        {
            try
            {
                var file = TagLib.File.Create(filePath);

                return new Song
                {
                    Title = string.IsNullOrEmpty(file.Tag.Title) ? System.IO.Path.GetFileNameWithoutExtension(filePath) : file.Tag.Title,
                    FilePath = filePath,
                    Artist = string.Join(", ", file.Tag.Performers ?? new string[] { "Unknown Artist" }),
                    Album = string.IsNullOrEmpty(file.Tag.Album) ? "Unknown Album" : file.Tag.Album,
                    Genre = string.Join(", ", file.Tag.Genres ?? new string[] { "Unknown Genre" }),
                    Duration = file.Properties.Duration
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
                return new Song
                {
                    Title = System.IO.Path.GetFileNameWithoutExtension(filePath),
                    FilePath = filePath,
                    Artist = "Unknown Artist",
                    Album = "Unknown Album",
                    Genre = "Unknown Genre",
                    Duration = TimeSpan.Zero
                };
            }
        }

        private void UpdateSongIndexes()
        {
            for (int i = 0; i < Songs.Count; i++)
            {
                Songs[i].Index = i + 1;
            }
            for (int i = 0; i < LikedSongs.Count; i++)
            {
                LikedSongs[i].LikedIndex = i + 1;
            }
        }

        public void SaveLikedSongs(string filePath)
        {
            for (int i = 0; i < LikedSongs.Count; i++)
            {
                if (LikedSongs[i].State == SongState.Playing) LikedSongs[i].State = SongState.Stopped;
            }
            string json = JsonConvert.SerializeObject(LikedSongs, Formatting.Indented);
            System.IO.File.WriteAllText(filePath, json);
        }

        public void LoadLikedSongs(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                string json = System.IO.File.ReadAllText(filePath);
                var loadedSongs = JsonConvert.DeserializeObject<ObservableCollection<Song>>(json);

                LikedSongs.Clear();
                foreach (var song in loadedSongs)
                {
                    LikedSongs.Add(song);
                }
            }
        }
    }
}
