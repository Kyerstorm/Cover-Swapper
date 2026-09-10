using System;
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

        /// <summary>Imports the file at <paramref name="filePath"/> for the game. Returns null on success, or a user-facing error message.</summary>
        public string AddFromFile(Guid gameId, string filePath)
        {
            var searchResult = _provider.SearchAsync(new CoverSearchRequest { GameId = gameId, LocalFilePath = filePath }).GetAwaiter().GetResult();
            if (!searchResult.Success || searchResult.Assets.Count == 0)
            {
                return searchResult.ErrorMessage ?? "Could not read the selected file.";
            }

            var downloadResult = _provider.DownloadAsync(searchResult.Assets[0]).GetAwaiter().GetResult();
            if (!downloadResult.Success)
            {
                return downloadResult.ErrorMessage;
            }

            var importResult = _importService.Import(gameId, new CoverAsset
            {
                Source = searchResult.Assets[0].Source,
                SourceId = searchResult.Assets[0].SourceId,
                FilePath = downloadResult.LocalFilePath
            });

            return importResult.IsSuccess ? null : importResult.Message;
        }
    }
}
