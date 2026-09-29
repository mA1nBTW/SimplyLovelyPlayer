using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SimplyLovelyPlayer
{
    public enum SongState
    {
        Unset,
        Playing,
        Pause,
        Stopped
    }
    internal class Song : INotifyPropertyChanged
    {
        public string Artist {  get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public string FilePath { get; set; }
        public TimeSpan Duration { get; set; }

        public string TitleFormatted => $"Title: {Title}";
        public string ArtistFormatted => $"Artist: {Artist}";
        public string DurationFormatted => $"{Duration.Minutes}:{Duration.Seconds:D2}";

        private int _index;
        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged(nameof(Index));
                }
            }
        }

        private int _likedIndex;
        public int LikedIndex
        {
            get => _likedIndex;
            set
            {
                if (_likedIndex != value)
                {
                    _likedIndex = value;
                    OnPropertyChanged(nameof(LikedIndex));
                }
            }
        }

        private SongState _State;
        public SongState State
        {
            get => _State;
            set
            {
                if (_State != value)
                {
                    _State = value;
                    OnPropertyChanged(nameof(State));
                }
            }
        }

        private bool _isLiked;
        public bool isLiked
        {
            get => _isLiked;
            set
            {
                if (_isLiked != value)
                {
                    _isLiked = value;
                    OnPropertyChanged(nameof(isLiked)); // Уведомляем об изменении
                }
            }
        }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is Song other)
            {
                return this.FilePath == other.FilePath;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return this.FilePath.GetHashCode();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
