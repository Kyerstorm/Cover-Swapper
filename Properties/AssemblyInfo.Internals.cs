using System.Runtime.CompilerServices;

// Lets the test project exercise internal infrastructure (e.g. the raw
// SteamGridDB HTTP client) directly with fakes, without making plumbing
// that should stay an implementation detail part of the public API.
[assembly: InternalsVisibleTo("PluginCoverShuffle.Tests")]
