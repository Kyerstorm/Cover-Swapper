using System.Collections.Generic;

namespace PluginCoverShuffle.Domain.Providers
{
    /// <summary>Result of an <see cref="ICoverProvider"/> search.</summary>
    public class CoverSearchResult
    {
        public bool Success { get; set; }

        /// <summary>User-facing explanation when <see cref="Success"/> is false.</summary>
        public string ErrorMessage { get; set; }

        public List<CoverAsset> Assets { get; set; } = new List<CoverAsset>();

        /// <summary>
        /// True when the search query matched more than one game and the
        /// provider needs the caller to disambiguate via
        /// <see cref="CoverGameMatch"/> before it will return covers. When
        /// true, <see cref="Assets"/> is empty and <see cref="GameMatches"/>
        /// holds the candidates.
        /// </summary>
        public bool RequiresGameSelection { get; set; }

        public List<CoverGameMatch> GameMatches { get; set; } = new List<CoverGameMatch>();

        public static CoverSearchResult Succeeded(params CoverAsset[] assets)
        {
            return new CoverSearchResult { Success = true, Assets = new List<CoverAsset>(assets) };
        }

        public static CoverSearchResult Failed(string errorMessage)
        {
            return new CoverSearchResult { Success = false, ErrorMessage = errorMessage };
        }

        public static CoverSearchResult NeedsGameSelection(IEnumerable<CoverGameMatch> matches)
        {
            return new CoverSearchResult { Success = true, RequiresGameSelection = true, GameMatches = new List<CoverGameMatch>(matches) };
        }
    }
}
