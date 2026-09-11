using System;
using System.Collections.Generic;
using System.Linq;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Providers;

namespace PluginCoverShuffle.Services
{
    /// <summary>
    /// Runs the "user already picked a local image file, now import it" flow
    /// shared by the game right-click menu and the per-game Cover Shuffle
    /// panel, so the two entry points can't drift out of sync.
    /// </summary>
    public class LocalFileCoverAddService
    {
        private readonly LocalFileCoverProvider _provider = new LocalFileCoverProvider();
        private readonly CoverImportService _importService;

        public LocalFileCoverAddService(CoverImportService importService)
        {
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        }

        /// <summary>Imports the file at <paramref name="filePath"/> for the game.</summary>
        public CoverImportResult AddFromFile(Guid gameId, string filePath)
        {
            var searchResult = _provider.SearchAsync(new CoverSearchRequest { GameId = gameId, LocalFilePath = filePath }).GetAwaiter().GetResult();
            if (!searchResult.Success || searchResult.Assets.Count == 0)
            {
                return CoverImportResult.Failed(CoverImportStatus.SourceFileMissing, searchResult.ErrorMessage ?? "Could not read the selected file.");
            }

            var downloadResult = _provider.DownloadAsync(searchResult.Assets[0]).GetAwaiter().GetResult();
            if (!downloadResult.Success)
            {
                return CoverImportResult.Failed(CoverImportStatus.SourceFileMissing, downloadResult.ErrorMessage);
            }

            return _importService.Import(gameId, new CoverAsset
            {
                Source = searchResult.Assets[0].Source,
                SourceId = searchResult.Assets[0].SourceId,
                FilePath = downloadResult.LocalFilePath
            });
        }

        /// <summary>Replaces an existing cover's file in place, preserving its history.</summary>
        public CoverImportResult ReplaceFromFile(Guid gameId, Guid coverId, string filePath)
        {
            var searchResult = _provider.SearchAsync(new CoverSearchRequest { GameId = gameId, LocalFilePath = filePath }).GetAwaiter().GetResult();
            if (!searchResult.Success || searchResult.Assets.Count == 0)
            {
                return CoverImportResult.Failed(CoverImportStatus.SourceFileMissing, searchResult.ErrorMessage ?? "Could not read the selected file.");
            }

            var downloadResult = _provider.DownloadAsync(searchResult.Assets[0]).GetAwaiter().GetResult();
            if (!downloadResult.Success)
            {
                return CoverImportResult.Failed(CoverImportStatus.SourceFileMissing, downloadResult.ErrorMessage);
            }

            return _importService.ReplaceFile(gameId, coverId, downloadResult.LocalFilePath);
        }

        /// <summary>
        /// Imports each file in order via <see cref="AddFromFile"/>, never
        /// stopping early - even once the cover limit is hit, remaining
        /// files are still attempted (and correctly fail with
        /// <see cref="CoverImportStatus.CoverLimitExceeded"/>) so every file
        /// gets an honest, individual outcome.
        /// </summary>
        public IReadOnlyList<LocalFileImportOutcome> AddManyFromFiles(Guid gameId, IEnumerable<string> filePaths)
        {
            return filePaths.Select(filePath => new LocalFileImportOutcome(filePath, AddFromFile(gameId, filePath))).ToList();
        }
    }

    /// <summary>One file's outcome from <see cref="LocalFileCoverAddService.AddManyFromFiles"/>.</summary>
    public class LocalFileImportOutcome
    {
        public string FilePath { get; }
        public CoverImportResult Result { get; }

        public LocalFileImportOutcome(string filePath, CoverImportResult result)
        {
            FilePath = filePath;
            Result = result;
        }
    }
}
