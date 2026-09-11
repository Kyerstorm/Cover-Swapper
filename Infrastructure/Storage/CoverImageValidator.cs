using System;
using System.IO;

namespace PluginCoverShuffle.Infrastructure.Storage
{
    /// <summary>
    /// Cheap corruption probe shared by every caller that needs to know
    /// whether an existing cover file can actually be decoded as an image,
    /// without paying for a full pixel decode. Extracted (Stage 3) from the
    /// Manage Covers per-cover probe so the Cover Shuffle Manager's game-list
    /// aggregation can flag the same real condition ("Needs Attention")
    /// without a second, potentially drifting implementation.
    /// </summary>
    public static class CoverImageValidator
    {
        /// <summary>
        /// Reads only the image header (no full pixel decode) to check the
        /// file is a readable image. Assumes the caller has already verified
        /// the file exists - a missing file is a distinct condition
        /// ("missing", not "corrupt").
        /// </summary>
        public static bool IsImageHeaderReadable(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return false;
            }

            try
            {
                using (var stream = File.OpenRead(absolutePath))
                using (System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
