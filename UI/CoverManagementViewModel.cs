using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Playnite.SDK;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// Backs the "Manage Covers" window: displays a game's covers and lets
    /// the user remove one from the shuffle pool. Contains no WPF
    /// dependencies beyond <see cref="ObservableObject"/>, so it is testable
    /// without instantiating a real window.
    /// </summary>
    public class CoverManagementViewModel : ObservableObject
    {
        private readonly Guid _gameId;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;

        public ObservableCollection<CoverDisplayItem> Covers { get; } = new ObservableCollection<CoverDisplayItem>();

        private string _coverCountText;

        /// <summary>e.g. "3 of 10 covers" — a quick indicator of how close the game is to the pool limit.</summary>
        public string CoverCountText
        {
            get => _coverCountText;
            set => SetValue(ref _coverCountText, value);
        }

        public bool IsEmpty => Covers.Count == 0;

        public CoverManagementViewModel(Guid gameId, ICoverShuffleRepository repository, ICoverStorage storage)
        {
            _gameId = gameId;
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));

            Reload();
        }

        public void Reload()
        {
            Covers.Clear();
            foreach (var cover in _repository.GetCovers(_gameId).OrderBy(c => c.AddedAt))
            {
                Covers.Add(new CoverDisplayItem
                {
                    CoverId = cover.CoverId,
                    AbsoluteImagePath = _storage.GetAbsolutePath(cover.LocalPath),
                    Source = cover.Source,
                    AddedAt = cover.AddedAt,
                    UsageCount = cover.UsageCount
                });
            }

            CoverCountText = $"{Covers.Count} of {CoverLimitPolicy.MaxCoversPerGame} covers";
            OnPropertyChanged(nameof(IsEmpty));
        }

        /// <summary>Removes a cover from the shuffle pool. Never deletes the physical file.</summary>
        public void Remove(Guid coverId)
        {
            _repository.RemoveCover(_gameId, coverId);
            Reload();
        }
    }
}
