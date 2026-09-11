using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PluginCoverShuffle.Playnite.Integration;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// Backs the bulk-shuffle progress dialog: runs a <see cref="BulkShuffleService"/>
    /// operation off the UI thread, reports live progress, and then shows a
    /// structured completion summary. Contains no WPF dependency beyond
    /// <see cref="ObservableObject"/>, so it is testable without a window.
    /// The actual shuffle loop lives entirely in <see cref="BulkShuffleService"/>;
    /// this class only drives it and shapes its result for display.
    /// </summary>
    public class BulkShuffleProgressViewModel : ObservableObject
    {
        private readonly BulkShuffleService _service;
        private readonly IReadOnlyList<Guid> _gameIds;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public string Title { get; }

        private bool _isRunning = true;

        public bool IsRunning
        {
            get => _isRunning;
            private set => SetValue(ref _isRunning, value, nameof(IsRunning), nameof(IsComplete), nameof(CanCancel));
        }

        public bool IsComplete => !IsRunning;

        public bool CanCancel => IsRunning;

        private int _completed;

        public int Completed
        {
            get => _completed;
            private set => SetValue(ref _completed, value, nameof(Completed), nameof(ProgressText), nameof(ProgressPercent));
        }

        private int _total;

        public int Total
        {
            get => _total;
            private set => SetValue(ref _total, value, nameof(Total), nameof(ProgressText), nameof(ProgressPercent));
        }

        public string ProgressText => $"{Completed} / {Total}";

        public double ProgressPercent => Total == 0 ? 0 : Math.Min(100.0, Completed * 100.0 / Total);

        public BulkShuffleResult Result { get; private set; }

        public bool HasFailures => Result != null && Result.Failures.Count > 0;

        private bool _showDetails;

        public bool ShowDetails
        {
            get => _showDetails;
            set => SetValue(ref _showDetails, value);
        }

        public string DetailsText => Result == null
            ? null
            : string.Join(Environment.NewLine, Result.Failures.Select(f => $"{f.GameName}: {f.Message}"));

        public string SummaryText
        {
            get
            {
                if (Result == null)
                {
                    return null;
                }

                var lines = new List<string>
                {
                    Result.WasCancelled ? "Shuffle cancelled" : "Shuffle complete",
                    $"✓ {Result.Shuffled} game(s) shuffled"
                };

                if (Result.SkippedDisabled > 0)
                {
                    lines.Add($"○ {Result.SkippedDisabled} skipped — Cover Shuffle disabled");
                }

                if (Result.SkippedNoCovers > 0)
                {
                    lines.Add($"○ {Result.SkippedNoCovers} skipped — no usable covers");
                }

                if (Result.SkippedNotInstalled > 0)
                {
                    lines.Add($"○ {Result.SkippedNotInstalled} skipped — not installed");
                }

                if (Result.Failed > 0)
                {
                    lines.Add($"✕ {Result.Failed} failed");
                }

                return string.Join(Environment.NewLine, lines);
            }
        }

        /// <param name="gameIds">The exact games to shuffle, or null to shuffle every installed game Cover Shuffle manages (<see cref="BulkShuffleService.ShuffleInstalledGames"/>).</param>
        public BulkShuffleProgressViewModel(BulkShuffleService service, IReadOnlyList<Guid> gameIds, string title)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _gameIds = gameIds;
            Title = title;
        }

        /// <summary>
        /// Runs the bulk shuffle on a background thread so the Manager window
        /// never freezes, reporting progress back to whatever thread called
        /// this (the <see cref="Progress{T}"/> capture makes that safe to
        /// call directly from a WPF event handler).
        /// </summary>
        public async Task RunAsync()
        {
            var progress = new Progress<BulkShuffleProgress>(p =>
            {
                Total = p.Total;
                Completed = p.Completed;
            });

            var token = _cts.Token;
            Result = await Task.Run(() => _gameIds != null
                ? _service.ShuffleGames(_gameIds, progress, token)
                : _service.ShuffleInstalledGames(progress, token)).ConfigureAwait(true);

            IsRunning = false;
            OnPropertyChanged(nameof(HasFailures));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(DetailsText));
        }

        /// <summary>
        /// Requests cancellation. <see cref="BulkShuffleService"/> checks this
        /// between games (never mid-game), so whatever game is currently
        /// being shuffled always finishes cleanly rather than being left
        /// half-applied.
        /// </summary>
        public void Cancel() => _cts.Cancel();
    }
}
