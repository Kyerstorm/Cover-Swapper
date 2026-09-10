namespace PluginCoverShuffle.Domain.Providers
{
    /// <summary>
    /// One candidate game returned by a provider's name search, normalized
    /// away from any provider-specific search DTO. Used to let the user pick
    /// which game they meant before covers are fetched, so a provider never
    /// silently guesses (e.g. "Fallout" matching "Fallout 3" instead of the
    /// original 1997 game).
    /// </summary>
    public class CoverGameMatch
    {
        /// <summary>The game's identifier within the provider's own system.</summary>
        public string ProviderGameId { get; set; }

        /// <summary>Display name as returned by the provider.</summary>
        public string Name { get; set; }
    }
}
