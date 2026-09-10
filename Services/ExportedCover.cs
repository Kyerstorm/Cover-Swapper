using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Services
{
    /// <summary>A cover's metadata plus where its image file lives within the export bundle.</summary>
    public class ExportedCover
    {
        public Cover Cover { get; set; }

        /// <summary>Path to the image file, relative to the export folder (e.g. "Covers/&lt;gameId&gt;/&lt;file&gt;").</summary>
        public string RelativeFilePath { get; set; }
    }
}
