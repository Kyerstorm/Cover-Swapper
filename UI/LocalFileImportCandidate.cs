using System.Collections.Generic;
using System.IO;

namespace PluginCoverShuffle.UI
{
    public enum LocalFileCandidateStatus
    {
        Pending,
        Valid,
        Invalid,
        TooLarge,
        DuplicateOfExisting,
        DuplicateInBatch,
        WillExceedLimit,
        Imported,
        ImportFailed
    }

    /// <summary>
    /// One file dropped/picked into the "Add Local Covers" dialog, with the
    /// preview status computed by <see cref="AddLocalCoversViewModel"/>.
    /// </summary>
    public class LocalFileImportCandidate : ObservableObject
    {
        public string FilePath { get; }

        public string FileName => Path.GetFileName(FilePath);

        /// <summary>
        /// Source for the preview &lt;Image&gt;: either the file path itself
        /// (WPF decodes PNG/JPG/BMP/GIF directly) or a pre-rendered in-memory
        /// <see cref="System.Windows.Media.Imaging.BitmapImage"/> for WEBP
        /// files, since WPF's built-in decoder has the same WEBP-codec
        /// uncertainty as GDI+.
        /// </summary>
        private object _thumbnailSource;

        public object ThumbnailSource
        {
            get => _thumbnailSource;
            set => SetValue(ref _thumbnailSource, value);
        }

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetValue(ref _isSelected, value);
        }

        private LocalFileCandidateStatus _status = LocalFileCandidateStatus.Pending;

        public LocalFileCandidateStatus Status
        {
            get => _status;
            set => SetValue(ref _status, value, nameof(Status), nameof(CanImport), nameof(StatusBadgeText));
        }

        private string _statusMessage;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetValue(ref _statusMessage, value);
        }

        private bool _willBeConverted;

        /// <summary>True when this file will be re-encoded to PNG on import (WEBP source).</summary>
        public bool WillBeConverted
        {
            get => _willBeConverted;
            set => SetValue(ref _willBeConverted, value);
        }

        private bool _willBeResized;

        /// <summary>True when this file's pixel dimensions exceed the policy max and will be downscaled on import.</summary>
        public bool WillBeResized
        {
            get => _willBeResized;
            set => SetValue(ref _willBeResized, value);
        }

        /// <summary>Whether this candidate is eligible to be committed by "Import Selected".</summary>
        public bool CanImport => Status == LocalFileCandidateStatus.Valid;

        public string StatusBadgeText
        {
            get
            {
                switch (Status)
                {
                    case LocalFileCandidateStatus.Valid:
                        return WillBeConverted && WillBeResized ? "Will be converted & resized"
                            : WillBeConverted ? "Will be converted to PNG"
                            : WillBeResized ? "Will be resized"
                            : "Ready";
                    case LocalFileCandidateStatus.Invalid:
                        return "Not a valid image";
                    case LocalFileCandidateStatus.TooLarge:
                        return "File too large";
                    case LocalFileCandidateStatus.DuplicateOfExisting:
                        return "Already added";
                    case LocalFileCandidateStatus.DuplicateInBatch:
                        return "Duplicate in selection";
                    case LocalFileCandidateStatus.WillExceedLimit:
                        return "Exceeds cover limit";
                    case LocalFileCandidateStatus.Imported:
                        return "Imported";
                    case LocalFileCandidateStatus.ImportFailed:
                        return "Import failed";
                    default:
                        return string.Empty;
                }
            }
        }

        public string AccessibleDescription => $"{FileName}, {StatusBadgeText}";

        public LocalFileImportCandidate(string filePath)
        {
            FilePath = filePath;
            ThumbnailSource = filePath;
        }
    }
}
