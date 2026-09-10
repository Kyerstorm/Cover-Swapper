using System;

namespace PluginCoverShuffle.Domain.Providers
{
    /// <summary>
    /// Input to <see cref="ICoverProvider.Search"/>. Not every provider uses
    /// every field: <see cref="LocalFilePath"/> is only meaningful for
    /// <see cref="Infrastructure.Providers.LocalFileCoverProvider"/>, while a
    /// future network provider would use <see cref="Query"/> instead.
    /// </summary>
    public class CoverSearchRequest
    {
        /// <summary>The Playnite game the search is being performed for.</summary>
        public Guid GameId { get; set; }

        /// <summary>Free-text search term, for providers that search by name.</summary>
        public string Query { get; set; }

        /// <summary>A file path already chosen by the user, for local-file import.</summary>
        public string LocalFilePath { get; set; }
    }
}
