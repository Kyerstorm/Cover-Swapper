using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Infrastructure.Persistence
{
    /// <summary>
    /// File-backed implementation of <see cref="ICoverShuffleRepository"/>.
    /// The entire database is kept in memory and rewritten atomically on
    /// every mutation, which is simple and safe at the data volumes this
    /// plugin deals with (a handful of records per game).
    /// </summary>
    public class CoverShuffleRepository : ICoverShuffleRepository
    {
        public const int CurrentSchemaVersion = 3;

        private readonly string _databaseFilePath;
        private readonly object _syncRoot = new object();
        private readonly PersistedDatabase _database;

        public CoverShuffleRepository(string databaseFilePath)
        {
            if (string.IsNullOrWhiteSpace(databaseFilePath))
            {
                throw new ArgumentException("Database file path must be provided.", nameof(databaseFilePath));
            }

            _databaseFilePath = databaseFilePath;
            _database = Load(databaseFilePath);
        }

        public GameConfiguration GetGameConfiguration(Guid gameId)
        {
            lock (_syncRoot)
            {
                return Clone(_database.GameConfigurations.FirstOrDefault(g => g.GameId == gameId));
            }
        }

        public void SaveGameConfiguration(GameConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            lock (_syncRoot)
            {
                _database.GameConfigurations.RemoveAll(g => g.GameId == configuration.GameId);
                _database.GameConfigurations.Add(Clone(configuration));
                Persist();
            }
        }

        public IReadOnlyList<GameConfiguration> GetAllGameConfigurations()
        {
            lock (_syncRoot)
            {
                return _database.GameConfigurations.Select(Clone).ToList();
            }
        }

        public IReadOnlyList<Guid> GetGameIdsWithCovers()
        {
            lock (_syncRoot)
            {
                return _database.Covers.Select(c => c.GameId).Distinct().ToList();
            }
        }

        public IReadOnlyList<Cover> GetCovers(Guid gameId)
        {
            lock (_syncRoot)
            {
                return _database.Covers.Where(c => c.GameId == gameId).Select(Clone).ToList();
            }
        }

        public Cover GetCover(Guid gameId, Guid coverId)
        {
            lock (_syncRoot)
            {
                return Clone(_database.Covers.FirstOrDefault(c => c.GameId == gameId && c.CoverId == coverId));
            }
        }

        public void AddCover(Cover cover)
        {
            if (cover == null)
            {
                throw new ArgumentNullException(nameof(cover));
            }

            lock (_syncRoot)
            {
                var existingCount = _database.Covers.Count(c => c.GameId == cover.GameId);
                if (existingCount >= CoverLimitPolicy.MaxCoversPerGame)
                {
                    throw new CoverLimitExceededException(cover.GameId, CoverLimitPolicy.MaxCoversPerGame);
                }

                _database.Covers.Add(Clone(cover));
                Persist();
            }
        }

        public void RemoveCover(Guid gameId, Guid coverId)
        {
            lock (_syncRoot)
            {
                _database.Covers.RemoveAll(c => c.GameId == gameId && c.CoverId == coverId);
                Persist();
            }
        }

        public void UpdateCover(Cover cover)
        {
            if (cover == null)
            {
                throw new ArgumentNullException(nameof(cover));
            }

            lock (_syncRoot)
            {
                var index = _database.Covers.FindIndex(c => c.GameId == cover.GameId && c.CoverId == cover.CoverId);
                if (index < 0)
                {
                    throw new InvalidOperationException(
                        $"Cover '{cover.CoverId}' does not exist for game '{cover.GameId}'.");
                }

                _database.Covers[index] = Clone(cover);
                Persist();
            }
        }

        public ShuffleState GetShuffleState(Guid gameId)
        {
            lock (_syncRoot)
            {
                return Clone(_database.ShuffleStates.FirstOrDefault(s => s.GameId == gameId));
            }
        }

        public void SaveShuffleState(ShuffleState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            lock (_syncRoot)
            {
                _database.ShuffleStates.RemoveAll(s => s.GameId == state.GameId);
                _database.ShuffleStates.Add(Clone(state));
                Persist();
            }
        }

        public OriginalArtworkInfo GetOriginalArtwork(Guid gameId)
        {
            lock (_syncRoot)
            {
                return Clone(_database.OriginalArtworkRecords.FirstOrDefault(a => a.GameId == gameId));
            }
        }

        public void SaveOriginalArtwork(OriginalArtworkInfo info)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            lock (_syncRoot)
            {
                _database.OriginalArtworkRecords.RemoveAll(a => a.GameId == info.GameId);
                _database.OriginalArtworkRecords.Add(Clone(info));
                Persist();
            }
        }

        public void ClearOriginalArtwork(Guid gameId)
        {
            lock (_syncRoot)
            {
                _database.OriginalArtworkRecords.RemoveAll(a => a.GameId == gameId);
                Persist();
            }
        }

        private static PersistedDatabase Load(string databaseFilePath)
        {
            if (!File.Exists(databaseFilePath))
            {
                return new PersistedDatabase();
            }

            var json = File.ReadAllText(databaseFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PersistedDatabase();
            }

            PersistedDatabase database;
            try
            {
                database = JsonConvert.DeserializeObject<PersistedDatabase>(json) ?? new PersistedDatabase();
            }
            catch (JsonException)
            {
                // A corrupt file must never take down the whole plugin nor
                // silently vanish: preserve it next to the original so the
                // user can recover data manually, and start fresh in-memory
                // rather than throwing out of the constructor.
                BackUpCorruptFile(databaseFilePath);
                return new PersistedDatabase();
            }

            // This check is the extension point for future migrations
            // (section 27): a newer file than this build understands must
            // fail loudly rather than silently losing data, and older files
            // must be upgraded here rather than discarded.
            //
            // Version 1 -> 2: GameConfiguration.SettingsOverride changed from
            // a complete CoverShuffleSettings snapshot to a sparse
            // GameSettingsOverride (every field nullable = "inherit global").
            // No explicit data transform is needed: every version-1 override
            // was created by cloning every global value at the time, so it
            // already has an explicit value for every field; deserializing
            // that JSON straight into the new nullable-field type preserves
            // every value as an explicit override with identical effective
            // behaviour, and the now-removed SteamGridDbApiKey property (a
            // global-only credential that was never read back per-game) is
            // silently ignored by the deserializer.
            //
            // Version 2 -> 3: ShuffleState gained LastShuffleTrigger, recording
            // whether the current cover came from the randomized engine or a
            // manual "Choose Cover" override. Older files have no such
            // property, and deserializing that missing value into the new
            // enum field defaults it to ShuffleTrigger.Random, which matches
            // the actual historical behaviour (every prior shuffle was
            // engine-selected), so no explicit transform is needed.
            if (database.SchemaVersion > CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Cover Shuffle database schema version {database.SchemaVersion} is newer than the " +
                    $"version {CurrentSchemaVersion} supported by this build.");
            }

            database.SchemaVersion = CurrentSchemaVersion;
            return database;
        }

        private static void BackUpCorruptFile(string databaseFilePath)
        {
            try
            {
                var backupPath = databaseFilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                File.Copy(databaseFilePath, backupPath, overwrite: true);
            }
            catch (IOException)
            {
                // Best-effort only; losing the backup must not prevent
                // recovering with a fresh in-memory database.
            }
        }

        private void Persist()
        {
            var directory = Path.GetDirectoryName(_databaseFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(_database, Formatting.Indented);
            var tempFilePath = _databaseFilePath + ".tmp";
            File.WriteAllText(tempFilePath, json);

            if (File.Exists(_databaseFilePath))
            {
                File.Delete(_databaseFilePath);
            }

            File.Move(tempFilePath, _databaseFilePath);
        }

        private static Cover Clone(Cover source) => DeepClone(source);

        private static GameConfiguration Clone(GameConfiguration source) => DeepClone(source);

        private static ShuffleState Clone(ShuffleState source) => DeepClone(source);

        private static OriginalArtworkInfo Clone(OriginalArtworkInfo source) => DeepClone(source);

        private static T DeepClone<T>(T source) where T : class
        {
            return source == null ? null : JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source));
        }
    }
}
