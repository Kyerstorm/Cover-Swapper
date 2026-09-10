# Plugin Cover Shuffle — Claude Engineering Ruleset

## 1. Project Identity

Project name: Plugin Cover Shuffle

Target platform: Playnite

Primary language: C#

Primary purpose:

Plugin Cover Shuffle is a Playnite extension that allows users to maintain
a collection of up to 10 cover artworks per game and automatically rotate
the game's Playnite cover according to a configurable schedule.

Users may obtain covers from:

- SteamGridDB
- Playnite metadata/artwork
- Local image files

The plugin must integrate naturally with Playnite Desktop Mode and must
not require a custom Playnite theme.

---

# 2. HARD RULE: PHASED DEVELOPMENT

Development is divided into numbered phases.

Claude MUST work only on the current requested phase.

Do not implement future-phase features unless the current phase explicitly
requires the architectural preparation for them.

If a future feature requires an interface, abstraction, or extension point,
create the minimum required abstraction but do not implement the future
feature.

Example:

CORRECT:
Create `ICoverProvider` during the provider architecture phase.

INCORRECT:
Implement SteamGridDB, IGDB, local files, metadata providers, and provider
selection when only the provider abstraction was requested.

---

# 3. PHASE COMPLETION GATE

A phase is not complete until all of the following are true:

1. The project builds successfully.
2. Existing functionality remains working.
3. New functionality is implemented.
4. Tests relevant to the phase exist and pass.
5. No compiler warnings are introduced unless explicitly justified.
6. No TODO placeholders remain for functionality claimed to be complete.
7. Public APIs and interfaces are documented where appropriate.
8. The implementation follows the architecture defined in this file.
9. No future-phase functionality has been silently added.
10. A concise phase completion report is produced.

The completion report MUST contain:

- What was implemented
- Files added
- Files modified
- Tests added
- Tests executed
- Known limitations
- Anything intentionally deferred to a later phase

---

# 4. DEVELOPMENT PRINCIPLES

## 4.1 Keep the plugin modular

Do not put all functionality into `Plugin.cs`.

The plugin must be separated into logical services.

Expected architectural areas include:

- Plugin lifecycle
- Game integration
- Cover management
- Cover storage
- Cover providers
- Shuffle engine
- Scheduling
- Settings
- Persistence
- UI
- Notifications
- Logging

Exact class names may evolve, but responsibilities must remain separated.

---

## 4.2 Dependency direction

Prefer this general dependency direction:

Playnite Integration
        ↓
Application Services
        ↓
Domain / Core
        ↓
Infrastructure

Provider implementations and persistence should not leak into the core
shuffle logic.

The shuffle engine must not know about SteamGridDB.

The SteamGridDB provider must not know about Playnite UI.

The UI must call application services rather than manipulating persistence
directly.

---

# 5. DOMAIN MODEL

The plugin should conceptually operate around these entities.

## Game

Represents a Playnite game.

The Playnite game ID is the stable external identifier.

Do not duplicate the entire Playnite Game object into plugin storage.

---

## Cover

Represents one stored cover belonging to a game.

Conceptual properties:

- CoverId
- GameId
- Source
- SourceId
- LocalPath
- Hash
- AddedAt
- LastUsedAt
- UsageCount
- IsEnabled

Additional fields may be added when justified.

---

## CoverShuffleSettings

Conceptually contains:

- Enabled
- Interval
- ShuffleMode
- AvoidConsecutiveDuplicates
- ShuffleOnStartup
- ShuffleOnGameLaunch
- NotificationPreference

Per-game settings must be capable of overriding global defaults.

---

## ShuffleState

Conceptually contains:

- CurrentCoverId
- LastShuffleAt
- NextShuffleAt
- ShuffleHistory / cycle information where required

Do not derive scheduling state solely from UI state.

---

# 6. COVER LIMIT

The initial supported maximum is:

10 covers per game.

The limit must be enforced centrally.

Do not scatter `10` throughout the codebase.

Use a single domain/configuration constant or policy.

The architecture should allow the limit to become configurable in the future,
but configurable limits are NOT part of the initial implementation unless
the current phase explicitly requests them.

---

# 7. COVER OWNERSHIP

The plugin must never destructively overwrite a user's original artwork
without being able to restore it.

Before Cover Shuffle takes control of a game's cover, the plugin must retain
the information necessary to restore the previous cover.

Disabling Cover Shuffle must support restoration of the original artwork.

Never assume that a Playnite cover is a simple local file path.

Use Playnite's supported APIs and artwork mechanisms.

---

# 8. RANDOMIZATION RULES

The shuffle system must not simply select a random cover every time.

It must avoid immediate repetition.

Preferred behaviour:

1. Build a shuffle cycle from enabled covers.
2. Randomize the cycle.
3. Consume each cover once.
4. Start a new randomized cycle.
5. Avoid selecting the same cover as the immediately previous cover when
   multiple covers are available.

Example:

Covers:
A B C D E

Cycle:
C A E B D

Next cycle:
D B A E C

The shuffle engine must be deterministic enough to test.

Do not directly instantiate `Random` throughout the application.

Provide a testable randomization abstraction where useful.

---

# 9. SCHEDULING RULES

The scheduler must store actual timestamps.

Do not implement scheduling as:

"Every time Playnite starts, add X hours."

Instead maintain:

- LastShuffleAt
- NextShuffleAt

When Playnite starts:

1. Load persisted state.
2. Determine whether a shuffle is due.
3. Apply the appropriate action.
4. Calculate and persist the next scheduled time.

The system must behave sensibly if Playnite was closed when a shuffle
would have occurred.

Do not silently perform dozens of missed shuffles.

At startup, normally perform the required current shuffle once and advance
the schedule.

---

# 10. TIME

Store timestamps in a consistent representation.

Prefer UTC internally where appropriate.

Convert to local time only for user-facing presentation.

Do not rely on string-formatted dates for scheduling.

---

# 11. PROVIDER ARCHITECTURE

All cover sources must use an abstraction.

Conceptually:

    ICoverProvider

Potential implementations:

    SteamGridDBCoverProvider
    PlayniteMetadataCoverProvider
    LocalFileCoverProvider

The core system must not depend directly on SteamGridDB.

Providers should return normalized cover information.

A provider should not directly modify Playnite games.

---

# 12. STEAMGRIDDB

SteamGridDB is an external provider.

API access must be isolated to the SteamGridDB provider.

Do not scatter HTTP requests across the project.

The provider must support:

- Search
- Cover retrieval
- Source identification
- Downloading artwork where appropriate

API credentials must never be hard-coded.

Do not commit API keys.

Do not log API keys.

Do not include API keys in exception messages.

Network failures must be handled gracefully.

The plugin must remain usable with previously downloaded covers when
SteamGridDB is unavailable.

---

# 13. CACHING

External provider requests should be cached where appropriate.

Do not repeatedly query SteamGridDB for the same information unnecessarily.

Caching must not make the application depend on network availability.

Previously stored covers must continue working offline.

Cache invalidation should be explicit and testable.

---

# 14. STORAGE

Plugin-owned data must be kept separate from Playnite's own metadata.

The plugin should use a dedicated storage area.

Recommended conceptual layout:

CoverShuffle/
    Database / configuration
    Covers/
        <GameId>/
            <CoverId>.<extension>
    Cache/
    Logs/

Do not rely on arbitrary temporary directories for persistent covers.

Do not store binary cover data directly inside configuration JSON unless
there is a compelling reason.

---

# 15. PERSISTENCE

Persistence must survive:

- Playnite restart
- Plugin reload
- Computer restart

The persistence layer must be abstracted from the rest of the application.

Preferred conceptual interface:

    ICoverShuffleRepository

The repository should handle persistence of plugin state.

Do not make UI classes responsible for database or file operations.

---

# 16. PLAYNITE INTEGRATION

Playnite integration should be kept in a dedicated area.

Playnite-specific types should not leak unnecessarily into the domain layer.

The plugin must use Playnite-supported extension APIs.

Avoid reflection unless absolutely necessary.

If Playnite's API provides a supported event or service, use it instead of
polling.

---

# 17. UI PRINCIPLES

The UI should feel like a Playnite extension.

Primary user entry points:

1. Right-click game context menu
2. Cover Shuffle management UI
3. Plugin settings
4. Optional new-game notification/setup

The UI must not directly manipulate database state.

Use services/application-layer methods.

The UI should clearly communicate:

- Whether Cover Shuffle is enabled
- Number of configured covers
- Current interval
- Current cover
- Available actions

---

# 18. RIGHT-CLICK MENU

The plugin should expose actions conceptually equivalent to:

Plugin Cover Shuffle
    Enable Cover Shuffle
    Disable Cover Shuffle
    Shuffle Now
    Manage Covers
    Restore Original Cover

Menu availability should reflect the current game state.

Do not create duplicate menu entries every time Playnite reloads the plugin.

---

# 19. SETTINGS

Global settings should represent defaults.

Per-game configuration should override global defaults where applicable.

Settings must be validated.

Invalid intervals must not crash the plugin.

Settings changes must be persisted.

Do not store transient runtime state as global settings.

---

# 20. NEW GAME DETECTION

New-game automation is optional and configurable.

Supported behaviours should eventually include:

- Do nothing
- Ask user
- Automatically configure

Do not force automatic SteamGridDB requests for every installed game.

Network-heavy behaviour must be opt-in unless explicitly specified otherwise.

---

# 21. ERROR HANDLING

External failures must not crash Playnite.

Examples:

- SteamGridDB unavailable
- Invalid API key
- Cover download failed
- Cover file deleted
- Database unavailable
- Invalid game ID
- Corrupt image
- Provider timeout

Failures should be:

1. Logged
2. Handled gracefully
3. Communicated to the user when appropriate

Do not show technical stack traces to normal users.

---

# 22. LOGGING

Use structured logging where practical.

Log levels should distinguish:

- Debug
- Information
- Warning
- Error

Never log:

- API keys
- Credentials
- Sensitive user data

Debug logging should be useful for diagnosing provider, scheduling, storage,
and Playnite integration problems.

---

# 23. TESTING

Every significant service should be testable independently.

Priority test areas:

- Cover limit
- Duplicate prevention
- Shuffle cycles
- Scheduling
- Missed schedules
- Settings validation
- Repository persistence
- Provider normalization
- Cover restoration
- Game enable/disable state

Tests should not require the live SteamGridDB API.

Use mocks/fakes for network providers.

Do not make tests dependent on the user's actual Playnite installation.

---

# 24. NO LIVE API TESTS BY DEFAULT

Automated tests must not require:

- SteamGridDB API key
- Internet connection
- Real Playnite library

Integration tests requiring external services must be clearly separated
and explicitly opt-in.

---

# 25. FILE NAMING

Use conventional C# naming:

Classes:
PascalCase

Interfaces:
IPascalCase

Methods:
PascalCase

Private fields:
_consistentPrivateFieldStyle

Do not use abbreviations unless they are widely understood.

---

# 26. COMMENTS

Do not comment obvious code.

Comments should explain:

- Why a non-obvious decision exists
- Playnite API workarounds
- Scheduling edge cases
- Persistence compatibility
- Provider-specific behaviour

Prefer clear code over excessive comments.

---

# 27. BACKWARDS COMPATIBILITY

Once persistent data exists, future phases must avoid casually breaking it.

If the storage schema changes:

1. Detect the previous schema version.
2. Migrate it.
3. Test the migration.
4. Do not silently delete user configuration.

Use an explicit schema/data version.

---

# 28. SECURITY

Never:

- Hard-code secrets
- Commit API credentials
- Execute downloaded content
- Trust provider file names blindly
- Construct unsafe file paths from remote values

Downloaded files must be stored using controlled paths.

Sanitize external identifiers before using them in file paths.

---

# 29. PERFORMANCE

Cover changes should not block Playnite's UI unnecessarily.

Network operations must be asynchronous where Playnite APIs and architecture
permit.

Do not repeatedly scan the entire Playnite library when an event-driven
approach is available.

Do not repeatedly download the same cover.

Do not perform expensive image processing on the UI thread.

---

# 30. OFFLINE-FIRST BEHAVIOUR

Once a cover is stored locally, it should not require the provider to be
available for normal shuffling.

The following should work offline:

- Shuffle Now
- Scheduled shuffle
- Cover selection from existing covers
- Enable/disable Cover Shuffle
- Restore original cover

Provider search/download obviously requires the provider to be available.

---

# 31. USER DATA SAFETY

Never delete user covers automatically.

If removing a cover from the shuffle pool:

- Remove it from the pool.
- Do not necessarily delete the physical file immediately.

Physical cleanup should be deliberate and safe.

Never delete Playnite's original artwork as part of normal operation.

---

# 32. ARCHITECTURAL EXTENSIBILITY

The architecture should allow future support for:

- Additional providers
- Weighted covers
- Seasonal covers
- Time-of-day covers
- Launch-based shuffling
- Statistics
- Import/export
- Bulk management
- Configurable cover limits

However, these must not be implemented early merely because the architecture
supports them.

---

# 33. DEFINITION OF DONE

A feature is done only when:

- It works
- It persists correctly
- It handles errors
- It has tests where applicable
- It does not regress previous functionality
- It is integrated into the appropriate UI
- It is documented where necessary

A feature is NOT done if it only has:

- A model
- A UI mock
- A TODO
- A stub method
- A placeholder implementation

unless the current phase explicitly defines it as a stub/extension point.

---

# 34. PHASE DISCIPLINE

Before beginning a phase:

1. Inspect the existing project.
2. Read this CLAUDE.md completely.
3. Read the current phase specification.
4. Determine what already exists.
5. Do not overwrite working code unnecessarily.

During a phase:

1. Make the smallest coherent implementation.
2. Follow existing project conventions where they do not conflict with this
   document.
3. Keep changes scoped to the phase.
4. Test incrementally.

After a phase:

1. Build.
2. Run tests.
3. Review changed files.
4. Check for accidental future features.
5. Produce the phase completion report.
6. Tell the user how they can use the plugin in its current state to test

---

# 35. WHEN REQUIREMENTS ARE AMBIGUOUS

Do not invent major product behaviour silently.

If an ambiguity affects architecture or user behaviour:

- Identify it.
- State the assumption.
- Prefer the smallest reversible decision.
- Continue when the assumption is safe.

Do not block implementation over minor details.

---

# 36. PRIORITY ORDER

When requirements conflict, use this priority:

1. User safety and data preservation
2. Playnite compatibility
3. Correctness
4. Existing user configuration
5. Phase requirements
6. Maintainability
7. Performance
8. Convenience
9. Future extensibility

---

# 37. FINAL RULE

Do not optimize for "how much code can be written."

Optimize for:

- Reliable Playnite integration
- Safe artwork management
- Predictable scheduling
- Clean architecture
- Testability
- Simple user experience

Plugin Cover Shuffle should feel like a small, polished Playnite feature,
not a large external application bolted onto Playnite.