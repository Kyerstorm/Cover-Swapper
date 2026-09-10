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

        public static CoverSearchResult Succeeded(params CoverAsset[] assets)
        {
            return new CoverSearchResult { Success = true, Assets = new List<CoverAsset>(assets) };
        }

        public static CoverSearchResult Failed(string errorMessage)
        {
            return new CoverSearchResult { Success = false, ErrorMessage = errorMessage };
        }
    }
}
