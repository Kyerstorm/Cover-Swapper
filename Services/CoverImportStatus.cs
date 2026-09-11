namespace PluginCoverShuffle.Services
{
    public enum CoverImportStatus
    {
        Success,
        DuplicateCover,
        InvalidImage,
        SourceFileMissing,
        CoverLimitExceeded,
        CoverNotFound,
        FileTooLarge
    }
}
