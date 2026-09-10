namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>Outcome of <see cref="PlayniteCoverService.ShuffleToNextCover"/>.</summary>
    public class ShuffleResult
    {
        public bool Success { get; }

        /// <summary>User-facing explanation; set only when <see cref="Success"/> is false.</summary>
        public string Message { get; }

        private ShuffleResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static ShuffleResult Ok() => new ShuffleResult(true, null);

        public static ShuffleResult Failed(string message) => new ShuffleResult(false, message);
    }
}
