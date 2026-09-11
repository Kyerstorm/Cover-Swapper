using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// Backs the "Add Local Covers" dialog: turns dropped/picked files into
    /// previewed candidates (extension, size, duplicate, and dimension
    /// checks) and commits the valid ones. Contains no WPF dependency beyond
    /// <see cref="ObservableObject"/>, so it is testable without a real
    /// window.
    /// </summary>
    public class AddLocalCoversViewModel : ObservableObject
    {
        private readonly Guid _gameId;
        private readonly ICoverShuffleRepository _repository;
        private readonly LocalFileCoverAddService _addService;
        private readonly ICoverShuffleLogger _logger;

        public Guid GameId => _gameId;

        public ObservableCollection<LocalFileImportCandidate> Candidates { get; } = new ObservableCollection<LocalFileImportCandidate>();

        public bool HasCandidates => Candidates.Count > 0;

        private bool _isDragActive;

        public bool IsDragActive
        {
            get => _isDragActive;
            set => SetValue(ref _isDragActive, value);
        }

        private bool _isImporting;

        public bool IsImporting
        {
            get => _isImporting;
            set => SetValue(ref _isImporting, value, nameof(IsImporting), nameof(CanImportSelected));
        }

        private string _statusMessage;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetValue(ref _statusMessage, value);
        }

        private int _remainingCapacity;

        public int RemainingCapacity
        {
            get => _remainingCapacity;
            set => SetValue(ref _remainingCapacity, value, nameof(RemainingCapacity), nameof(CapacityText));
        }

        private bool _isAtCoverLimit;

        public bool IsAtCoverLimit
        {
            get => _isAtCoverLimit;
            set => SetValue(ref _isAtCoverLimit, value);
        }

        public string CapacityText
        {
            get
            {
                var used = CoverLimitPolicy.MaxCoversPerGame - RemainingCapacity;
                return $"{used} of {CoverLimitPolicy.MaxCoversPerGame} covers used";
            }
        }

        public bool CanImportSelected => !IsImporting && Candidates.Any(c => c.IsSelected && c.Status == LocalFileCandidateStatus.Valid);

        public AddLocalCoversViewModel(Guid gameId, ICoverShuffleRepository repository, LocalFileCoverAddService addService, ICoverShuffleLogger logger)
        {
            _gameId = gameId;
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _addService = addService ?? throw new ArgumentNullException(nameof(addService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Candidates.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasCandidates));

            RefreshCapacityState();
        }

        /// <summary>
        /// Adds each file as a new preview candidate and validates it. Used
        /// identically by drag & drop and the Browse Files picker, so both
        /// get exactly the same feedback.
        /// </summary>
        public void AddCandidateFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
            {
                return;
            }

            var existingHashes = new HashSet<string>(
                _repository.GetCovers(_gameId).Select(c => c.Hash),
                StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Candidates.Any(c => string.Equals(c.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var candidate = new LocalFileImportCandidate(filePath);
                candidate.PropertyChanged += OnCandidatePropertyChanged;
                Candidates.Add(candidate);
                ValidateCandidate(candidate, existingHashes);
            }

            RefreshCapacityState();
        }

        public void RemoveCandidate(LocalFileImportCandidate candidate)
        {
            if (candidate == null)
            {
                return;
            }

            candidate.PropertyChanged -= OnCandidatePropertyChanged;
            Candidates.Remove(candidate);
            RefreshCapacityState();
        }

        private void OnCandidatePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalFileImportCandidate.IsSelected))
            {
                RefreshCapacityState();
            }
        }

        private readonly Dictionary<LocalFileImportCandidate, string> _hashesByCandidate = new Dictionary<LocalFileImportCandidate, string>();

        private void ValidateCandidate(LocalFileImportCandidate candidate, HashSet<string> existingHashes)
        {
            var extension = Path.GetExtension(candidate.FilePath).ToLowerInvariant();
            if (Array.IndexOf(CoverImportPolicy.AllowedExtensions, extension) < 0)
            {
                SetInvalid(candidate, "Unsupported file type.");
                return;
            }

            if (!File.Exists(candidate.FilePath))
            {
                SetInvalid(candidate, "File could not be found.");
                return;
            }

            var fileInfo = new FileInfo(candidate.FilePath);
            if (fileInfo.Length > CoverImportPolicy.MaxFileSizeBytes)
            {
                var limitMb = CoverImportPolicy.MaxFileSizeBytes / (1024 * 1024);
                candidate.Status = LocalFileCandidateStatus.TooLarge;
                candidate.StatusMessage = $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB (limit {limitMb} MB).";
                return;
            }

            int width, height;
            try
            {
                using (var image = Image.FromFile(candidate.FilePath))
                {
                    width = image.Width;
                    height = image.Height;
                }
            }
            catch (Exception)
            {
                SetInvalid(candidate, "Not a valid image.");
                return;
            }

            candidate.WillBeResized = width > CoverImportPolicy.MaxImageDimensionPixels || height > CoverImportPolicy.MaxImageDimensionPixels;

            var hash = CoverHashUtility.ComputeHash(candidate.FilePath);
            _hashesByCandidate[candidate] = hash;

            if (existingHashes.Contains(hash))
            {
                candidate.Status = LocalFileCandidateStatus.DuplicateOfExisting;
                candidate.StatusMessage = "This image has already been added to this game's covers.";
                return;
            }

            var duplicateInBatch = Candidates
                .Where(c => c != candidate && _hashesByCandidate.TryGetValue(c, out var otherHash) && string.Equals(otherHash, hash, StringComparison.OrdinalIgnoreCase))
                .Any();
            if (duplicateInBatch)
            {
                candidate.Status = LocalFileCandidateStatus.DuplicateInBatch;
                candidate.StatusMessage = "This image was already selected above.";
                return;
            }

            candidate.Status = LocalFileCandidateStatus.Valid;
            candidate.StatusMessage = null;
            candidate.IsSelected = true;
        }

        private static void SetInvalid(LocalFileImportCandidate candidate, string message)
        {
            candidate.Status = LocalFileCandidateStatus.Invalid;
            candidate.StatusMessage = message;
        }

        private void RefreshCapacityState()
        {
            var existingCount = _repository.GetCovers(_gameId).Count;
            RemainingCapacity = Math.Max(0, CoverLimitPolicy.MaxCoversPerGame - existingCount);
            IsAtCoverLimit = RemainingCapacity <= 0;

            var selectedInOrder = Candidates
                .Where(c => c.IsSelected && (c.Status == LocalFileCandidateStatus.Valid || c.Status == LocalFileCandidateStatus.WillExceedLimit))
                .ToList();

            for (var i = 0; i < selectedInOrder.Count; i++)
            {
                var candidate = selectedInOrder[i];
                if (i < RemainingCapacity)
                {
                    if (candidate.Status == LocalFileCandidateStatus.WillExceedLimit)
                    {
                        candidate.Status = LocalFileCandidateStatus.Valid;
                        candidate.StatusMessage = null;
                    }
                }
                else
                {
                    candidate.Status = LocalFileCandidateStatus.WillExceedLimit;
                    candidate.StatusMessage = "Not enough cover slots remain - remove another file or deselect this one.";
                }
            }

            OnPropertyChanged(nameof(CanImportSelected));
        }

        /// <summary>Commits every selected, valid candidate, one file at a time, updating each candidate's status as results return.</summary>
        public async Task ImportSelectedAsync()
        {
            if (IsImporting)
            {
                return;
            }

            var toImport = Candidates.Where(c => c.IsSelected && c.Status == LocalFileCandidateStatus.Valid).ToList();
            if (toImport.Count == 0)
            {
                return;
            }

            IsImporting = true;
            StatusMessage = "Importing...";

            try
            {
                var outcomes = await Task.Run(() => _addService.AddManyFromFiles(_gameId, toImport.Select(c => c.FilePath))).ConfigureAwait(true);

                var successCount = 0;
                foreach (var outcome in outcomes)
                {
                    var candidate = toImport.FirstOrDefault(c => string.Equals(c.FilePath, outcome.FilePath, StringComparison.OrdinalIgnoreCase));
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (outcome.Result.IsSuccess)
                    {
                        candidate.Status = LocalFileCandidateStatus.Imported;
                        candidate.StatusMessage = null;
                        successCount++;
                    }
                    else
                    {
                        candidate.Status = LocalFileCandidateStatus.ImportFailed;
                        candidate.StatusMessage = outcome.Result.Message;
                    }
                }

                StatusMessage = successCount == toImport.Count
                    ? $"Imported {successCount} of {toImport.Count} cover(s)."
                    : $"Imported {successCount} of {toImport.Count} cover(s); see individual files for details.";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Importing local cover files failed unexpectedly.");
                StatusMessage = "Something went wrong importing these files.";
            }
            finally
            {
                IsImporting = false;
                RefreshCapacityState();
            }
        }
    }
}
