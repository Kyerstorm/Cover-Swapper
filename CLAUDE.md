# Cover Shuffle — Claude Engineering Rules

> **This file is mandatory project guidance. Read it completely before making changes.**
>
> This repository is an existing Playnite extension. The codebase is not a blank
> project and must not be treated as one. The existing implementation is the
> source of truth for what already works.

---

## 1. Project Identity

**Repository:** `Kyerstorm/Cover-Swapper`

**Product:** Cover Shuffle

**Assembly:** `PluginCoverShuffle`

**Namespace:** `PluginCoverShuffle`

**Target:** Playnite Desktop

**Framework:** `net462`

**Language:** C# / latest language version supported by the project

**UI:** WPF

**Current project version:** `1.3.0`

**Primary purpose:**

Cover Shuffle is a Playnite extension that lets users maintain a pool of up to
10 alternate cover artworks for a game and rotate the Playnite cover manually,
on a schedule, or on game launch.

Supported cover sources currently include:

- Local image files
- SteamGridDB
- Artwork already available through Playnite metadata/artwork

The plugin owns copies of managed artwork and keeps enough information to
restore the original Playnite cover.

---

# 2. EXISTING REPOSITORY RULE — HIGHEST IMPORTANCE

This is an existing implementation with substantial functionality already
present.

Claude MUST inspect the repository before implementing any requested feature.

The feature roadmap describes desired outcomes; it is **not** a reason to
rewrite existing working code.

If a requested feature already exists:

1. Locate the implementation.
2. Read it and its tests.
3. Determine whether it actually satisfies the requested behaviour.
4. Keep correct code.
5. Modify only the deficient parts.
6. Add or improve tests where necessary.
7. Avoid architecture churn unless there is a demonstrated problem.

Never assume a feature is missing merely because it appears in a roadmap.

Never replace a functioning implementation with a hypothetical implementation
just because a different design appears cleaner in isolation.

Before changing an area, answer:

- What currently implements this?
- What tests cover it?
- What behaviour does the README promise?
- What is actually missing or broken?
- What is the smallest safe change?

---

# 3. CURRENT REPOSITORY CAPABILITIES

The current project already contains substantial functionality. Treat these as
existing capabilities unless inspection proves otherwise:

- Per-game cover pools
- Maximum of 10 covers per game
- Local cover import
- SteamGridDB cover search/download
- Playnite artwork import
- Cover hashing/validation
- Cover ownership and original-cover restoration
- Persistent plugin-owned data
- Cover enable/disable state
- Randomized no-immediate-repeat cover shuffling
- Persisted shuffle state
- Per-game/global scheduling configuration
- Due-shuffle handling at Playnite startup
- Optional shuffle on game launch
- New-game automation with Do Nothing / Ask / Automatic behaviour
- Configurable notifications
- Cover Shuffle Manager
- Bulk manager operations
- Configuration import/export
- Maintenance scanning and explicit cleanup
- SteamGridDB API-key validation
- Provider abstractions
- Caching abstractions
- Separate tests project
- `.pext` packaging through the project file

Do not reimplement these as if they were missing.

The README is part of the current product contract. If implementation and
README disagree, investigate which is authoritative before changing either.

---

# 4. VERIFIED CURRENT LIMITATIONS

The current repository documentation identifies these limitations. They are
important when planning future work:

1. A due scheduled shuffle is currently applied at Playnite startup rather
   than continuously while Playnite remains open.
2. Playnite Metadata artwork support is currently strongest for artwork that
   already exists as a local file; bare remote URLs are not fully supported.
3. Configuration import/export is designed primarily for the same Playnite
   library and depends on matching Playnite game IDs.

Additional limitations or defects discovered during development must be
recorded rather than silently ignored.

Do not claim a limitation has been fixed unless the implementation and tests
actually prove it.

---

# 5. HARD RULE: PHASED DEVELOPMENT

When the user gives a numbered phase, Claude MUST work only on that phase.

Do not implement unrelated future features.

A phase may introduce the minimum abstraction needed by a future feature, but
must not silently implement that future feature.

For example:

**Correct:**

Create a scheduler abstraction while implementing live scheduling.

**Incorrect:**

While fixing scheduling, also implement seasonal artwork, AI artwork,
statistics, and additional providers.

If the user has not explicitly specified phases, Claude should first identify
the smallest coherent phase rather than turning one request into an unrelated
large rewrite.

---

# 6. PHASE COMPLETION GATE

A phase is complete only when:

1. The project builds successfully.
2. Existing relevant functionality still works.
3. Requested functionality is implemented, not stubbed.
4. Relevant tests exist and pass.
5. No new compiler warnings are introduced without justification.
6. No TODO is used as a substitute for functionality claimed to be complete.
7. Persistent data remains compatible, or migration is implemented.
8. UI changes work with Playnite's supported themes where applicable.
9. No unrelated future feature has been added.
10. The changed code has been reviewed for regressions.
11. The final report clearly states what changed and what remains.

Every phase completion report must contain:

- Summary
- Files added
- Files modified
- Important implementation decisions
- Tests added/changed
- Build result
- Test result
- Known limitations
- Deferred work
- How to manually test the feature in Playnite

---

# 7. DEVELOPMENT PRIORITY ORDER

When requirements conflict, use this order:

1. User data safety
2. Playnite compatibility
3. Correctness
4. Existing user configuration
5. Requested phase
6. Reliability
7. Maintainability
8. Performance
9. UX convenience
10. Future extensibility

Do not sacrifice data safety or Playnite compatibility for visual polish.

---

# 8. ARCHITECTURE PRINCIPLES

The repository already uses separation between Playnite integration,
application/service logic, domain concepts, infrastructure/providers,
persistence, and UI.

Preserve that separation.

Prefer this dependency direction:

```text
Playnite Integration
        ↓
Application Services
        ↓
Domain / Core
        ↓
Infrastructure
```

UI should call application services/view models.

UI should not directly manipulate persistence files.

Domain/shuffle logic must not know about WPF.

Domain/shuffle logic must not know about SteamGridDB.

SteamGridDB code must not know about WPF controls.

Playnite SDK types should not leak into domain logic unless there is a strong
reason.

Do not introduce abstractions simply for theoretical purity. Add them when
they improve testability, separation, or an actual future requirement.

---

# 9. `Plugin.cs` AND LIFECYCLE

`Plugin.cs` is the Playnite integration/composition root, not the place for
business logic.

Keep it responsible for things such as:

- plugin lifecycle
- service composition
- Playnite event registration
- menu registration
- settings integration
- disposal/shutdown

Do not turn it into a god class.

If a new feature requires substantial logic, put that logic in an appropriate
service rather than adding more branches to `Plugin.cs`.

When adding event handlers, ensure they are registered exactly once and are
unregistered/disposed appropriately.

Plugin reload and Playnite shutdown must not leave background workers, timers,
HTTP operations, or event subscriptions running.

---

# 10. DOMAIN MODEL

The conceptual domain includes:

### Game

Represents the Playnite game through its stable Playnite ID.

Do not duplicate the complete Playnite game object in plugin storage.

### Cover

Represents a managed artwork item. Relevant concepts include:

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

Use the existing model before introducing another parallel cover model.

### Cover shuffle settings

Global settings provide defaults. Game-specific settings override them where
appropriate.

### Shuffle state

Contains persisted runtime state such as:

- CurrentCoverId
- LastShuffleAt
- NextShuffleAt
- cycle/order state required by the shuffle implementation

Scheduling state must not be reconstructed from UI state.

---

# 11. COVER LIMIT

The current supported maximum is **10 covers per game**.

The limit must be enforced centrally.

Never scatter literal `10` throughout business logic.

Do not make the limit configurable unless the current phase explicitly calls
for that feature.

Do not bypass the limit through bulk actions, imports, providers, or UI.

All entry points must respect the same policy.

---

# 12. COVER OWNERSHIP AND USER DATA SAFETY

This is one of the most important parts of the project.

Cover Shuffle must never destructively overwrite a user's original artwork
without retaining enough information to restore it.

When Cover Shuffle first takes control of a game's cover, preserve the
original artwork using the existing ownership mechanism.

Disabling Cover Shuffle must not destroy the original artwork.

Removing a cover from the shuffle pool must not automatically delete its
physical file unless the user explicitly performs a safe cleanup action.

Never delete Playnite's original artwork as ordinary plugin housekeeping.

Never assume an artwork reference is merely a simple filesystem path.

Use Playnite-supported artwork APIs/mechanisms.

---

# 13. SHUFFLE ENGINE RULES

The current product behaviour is randomized, no-immediate-repeat shuffling.

A shuffle must not simply do:

```text
current + 1
```

and must not choose a completely unrestricted random item that can repeat
immediately.

The preferred cycle semantics are:

1. Gather enabled covers.
2. Remove invalid/missing candidates.
3. Build a randomized cycle.
4. Consume each cover once.
5. Build another randomized cycle when exhausted.
6. When more than one candidate exists, do not begin the new cycle with the
   cover that was just displayed.

Example:

```text
Pool: A B C D E

Cycle 1: C A E B D
Cycle 2: B D A E C
```

The exact random sequence is not part of the product contract; the invariants
are.

The shuffle engine must be independently testable.

Do not scatter calls to `Random` throughout the application.

If randomness needs to be abstracted, use a small testable abstraction rather
than coupling tests to implementation-specific random values.

Edge cases that must remain correct:

- zero covers
- one cover
- two covers
- disabled covers
- missing files
- removed covers
- newly added covers
- current cover removed
- Playnite restart
- persisted cycle state

---

# 14. SCHEDULING RULES

The scheduler uses actual timestamps.

Persist and reason about:

- LastShuffleAt
- NextShuffleAt

Do not implement schedules as "add the interval whenever Playnite starts".

At startup, due work should normally be applied once rather than replaying
every missed interval.

Do not create duplicate scheduling work when the plugin reloads.

Future live scheduling must be implemented with proper lifecycle/cancellation
handling and must not block Playnite's UI thread.

When a game has no valid enabled covers, the scheduler must skip safely and
must not apply a broken artwork reference.

When an interval changes, the next scheduled time must be recalculated using
well-defined semantics and persisted.

All internal time calculations should use a consistent representation,
preferably UTC, with local conversion only for display.

---

# 15. SETTINGS AND CONFIGURATION INHERITANCE

Global settings are defaults, not necessarily direct commands for every game.

Game-specific configuration must be resolved through one coherent mechanism.

The desired conceptual model is:

```text
Global default
      ↓
Game override, if present
      ↓
Effective settings
```

Where a game can inherit individual settings rather than requiring a complete
copy of global settings.

If the existing implementation still uses a complete per-game snapshot,
do not silently redesign it during an unrelated phase. Introduce proper
inheritance only as part of a configuration-focused change and preserve
existing user behaviour/data through migration or compatibility handling.

Settings must be validated before being applied.

Invalid intervals must not crash the plugin.

Do not store transient runtime state as user settings.

---

# 16. STEAMGRIDDB ARCHITECTURE

SteamGridDB is an external provider.

All API access belongs in the SteamGridDB provider/client layer.

Do not scatter HTTP calls across services or UI code.

The provider should conceptually separate:

```text
Game search/matching
        ↓
Selected SteamGridDB game
        ↓
Artwork retrieval
        ↓
Download/cache
        ↓
Normalized CoverAsset / cover import
```

The UI must not perform raw HTTP calls.

API credentials must never be hard-coded, committed, logged, or included in
exception messages.

Network failures must not crash Playnite.

Previously downloaded/owned covers must continue to work offline.

---

# 17. STEAMGRIDDB MATCHING

Do not silently assume that the first SteamGridDB search result is always the
correct game.

For ambiguous searches, the user should be able to choose the correct game.

Where practical, matching can consider:

- normalized game name
- platform
- release year
- developer/publisher
- known store/source IDs

Do not introduce a complicated scoring system without tests and a product
reason for it.

A high-confidence automatic match may be acceptable, but the user must have
a way to correct an incorrect match.

---

# 18. STEAMGRIDDB NETWORK BEHAVIOUR

Use asynchronous network operations where compatible with Playnite and the
existing architecture.

Handle at least these classes of failure gracefully:

- no API key
- invalid/rejected API key
- timeout
- DNS/network failure
- server error
- rate limiting
- malformed response
- download failure

Transient failures may use bounded retry/backoff where appropriate.

Do not retry authentication failures indefinitely.

Do not make provider failure prevent normal offline cover shuffling.

---

# 19. CACHING

Use the existing cache abstraction rather than inventing a second cache.

Cache external artwork/search information where appropriate.

The cache must be an optimization, not a dependency for ordinary operation.

Previously imported covers must work without network access.

Cache invalidation/cleanup must be explicit and testable.

Never allow unbounded cache growth without a maintenance strategy.

---

# 20. PLAYNITE ARTWORK PROVIDER

Playnite artwork import should normalize available artwork into the plugin's
owned cover representation.

Existing local artwork should be usable offline.

If adding support for remote artwork URLs, ensure the downloaded copy becomes
plugin-owned and is validated before entering the cover pool.

Do not modify Playnite artwork directly from the provider.

---

# 21. LOCAL FILE IMPORT

Local image import must:

- validate the image
- use controlled plugin-owned storage
- avoid unsafe filenames/paths
- detect duplicates where existing infrastructure supports it
- respect the 10-cover limit
- avoid deleting the source file

Future drag-and-drop or multi-select UI should reuse the same import service
rather than bypassing validation/storage.

---

# 22. COVER FILE SAFETY

Remote/source-derived identifiers and filenames must never be trusted as raw
filesystem paths.

Use controlled paths based on stable IDs and validated extensions.

Do not allow path traversal.

Do not execute downloaded files.

Validate image contents before treating them as usable covers.

If a registered cover file is missing:

- do not apply the broken path to Playnite
- surface the problem where appropriate
- allow maintenance/recovery
- preserve the record until the user chooses what to do

---

# 23. PERSISTENCE

Plugin-owned state must survive:

- Playnite restart
- plugin reload
- computer restart

The persistence layer must remain separate from UI and domain logic.

Use the existing repository/persistence abstraction rather than allowing UI
or provider code to edit JSON directly.

Persistent writes must be safe against interruption as far as practical.

Avoid unnecessary full-database rewrites if performance becomes a real issue,
but do not replace the existing persistence system without evidence that it is
necessary.

---

# 24. SCHEMA VERSIONING AND MIGRATION

Persistent data is user data.

Never silently discard it because the model changed.

The repository already has schema-version concepts. Preserve them.

When a schema change is required:

1. Increment the schema version.
2. Implement a deterministic migration.
3. Preserve old data where possible.
4. Test old → new migration.
5. Ensure failed migration does not destroy the original data.
6. Document the migration.

Do not invent destructive migrations.

Future schema changes should move toward an explicit migration pipeline rather
than a collection of ad-hoc compatibility branches.

---

# 25. DATABASE RECOVERY

A corrupt database must not crash Playnite.

Preserve the corrupt source for recovery/diagnostics where the existing
implementation supports this.

If starting with a clean state after corruption, communicate that clearly and
never claim the old data was recovered unless it actually was.

Prefer safe temporary writes and atomic replacement mechanisms supported by
the target runtime.

A future backup/recovery feature must build on the existing persistence model,
not create an unrelated storage system.

---

# 26. UI ARCHITECTURE

WPF UI should use the existing project conventions and Playnite-compatible
resources.

The UI must not directly manipulate persistence.

Prefer:

```text
View
 ↓
ViewModel
 ↓
Application service
 ↓
Repository/provider/storage
```

Do not put network, persistence, or shuffle algorithms into code-behind unless
there is a narrow UI-specific reason.

Keep code-behind small.

---

# 27. PLAYNITE THEME COMPATIBILITY

The UI must work with Playnite themes.

Dark mode must be treated as a first-class supported experience.

Light mode must remain readable and usable.

Do not hard-code a separate black/white palette throughout XAML.

Prefer Playnite/theme resources for:

- backgrounds
- foregrounds
- borders
- accent colours
- buttons
- hover states
- selection states
- disabled states
- focus states

If a custom brush is genuinely necessary, keep it local and document why.

Always test important UI states in both dark and light themes.

---

# 28. STEAMGRIDDB SELECTION UI

The SteamGridDB cover browser is a key user-facing workflow.

The desired interaction is:

```text
Open from Playnite game
        ↓
Game title prefilled
        ↓
Search
        ↓
Select correct SteamGridDB game if ambiguous
        ↓
Browse covers
        ↓
Single click selects a cover
        ↓
Selection is visibly obvious
        ↓
Add Selected Cover
```

The cover card should support, where practical:

- normal state
- hover state
- pressed state
- selected state
- keyboard focus
- disabled/already-added state

The user must receive clear feedback that a cover was selected.

Do not make an image visually appear clickable while routing the actual click
to an unrelated control.

Use accessible names/descriptions for cover cards.

---

# 29. MODERN UI PRINCIPLES

The UI should feel like a modern Playnite extension rather than a generic old
WPF utility.

Prefer:

- clear hierarchy
- restrained spacing
- consistent card sizing
- responsive grids
- clear primary actions
- meaningful empty states
- loading indicators/placeholders
- clear error messages
- fixed/consistent action areas
- obvious selection state
- keyboard navigation

Avoid:

- excessive nested borders
- large unused whitespace
- hard-coded colours
- unexplained icons
- tiny click targets
- controls that look interactive but do nothing
- modal dialogs for operations that can safely happen inline

Do not chase visual trends at the expense of Playnite consistency.

---

# 30. UI STATE REQUIREMENTS

Important asynchronous UI workflows should explicitly represent states such
as:

- idle
- loading
- success/results
- empty
- error
- disabled
- selected

Never leave the user guessing whether a network request is still running.

Prevent accidental duplicate searches/downloads caused by repeated clicks.

Do not freeze the Playnite UI while images or network requests are loading.

---

# 31. RIGHT-CLICK GAME MENU

The game context menu is a primary entry point.

Actions should remain conceptually grouped under Cover Shuffle, including:

- Enable Cover Shuffle
- Disable Cover Shuffle
- Add Cover
- Shuffle Now
- Manage Covers
- Restore Original Cover

Menu state should reflect the selected game's state.

Do not register duplicate menu items after plugin reloads.

Do not expose settings for features that are not actually implemented.

---

# 32. MANAGER

The Cover Shuffle Manager already exists and supports searchable management,
bulk operations, import/export, and maintenance.

Do not replace it with a new manager unless there is a demonstrated usability
or architectural problem.

Future manager improvements should focus on:

- clear enabled/disabled state
- cover count
- current cover
- next shuffle
- effective interval
- search/filtering
- safe bulk actions
- useful empty states
- clear confirmation for destructive maintenance operations

Bulk actions must reuse the same application services/policies as individual
game actions.

---

# 33. NEW GAME AUTOMATION

Current behaviours are:

- Do nothing
- Ask
- Automatic

Automatic preparation should remain lightweight and should not silently
query SteamGridDB for every newly installed game.

Provider/network-heavy automation must remain explicit unless the product
requirements are intentionally changed.

A game should not repeatedly trigger "new game" behaviour after it has already
been seen by Cover Shuffle.

---

# 34. NOTIFICATIONS

Notifications should be useful, not noisy.

Existing notification preferences should be preserved.

Do not show a success notification for every internal operation unless the
user has opted into it.

Errors that require user attention should be communicated clearly while raw
exceptions remain in logs.

---

# 35. ERROR HANDLING

External or malformed input must never crash Playnite where a graceful
recovery is possible.

Examples include:

- provider unavailable
- invalid API key
- image download failure
- deleted cover file
- malformed JSON
- invalid game ID
- corrupt image
- invalid settings
- interrupted import
- missing dependency

The pattern should generally be:

```text
Detect
  ↓
Log useful technical context
  ↓
Recover or fail safely
  ↓
Show a concise user-facing explanation when useful
```

Do not expose stack traces, API credentials, or implementation details to
ordinary users.

---

# 36. LOGGING

Use the existing Playnite/plugin logging conventions.

Useful categories include:

- Debug
- Information
- Warning
- Error

Log enough context to diagnose:

- scheduling
- provider requests/failures
- storage failures
- persistence failures
- Playnite integration failures
- import/export problems

Never log:

- API keys
- credentials
- secrets
- unnecessary sensitive user data

Avoid logging every routine shuffle at a noisy level unless the user has
explicitly enabled useful diagnostic logging.

---

# 37. TESTING STRATEGY

Tests are a first-class part of this project.

The existing separate test project must remain independent of a user's actual
Playnite installation.

Automated tests should not require:

- a real Playnite library
- a live SteamGridDB API key
- an internet connection
- user-specific filesystem state

Use fakes/mocks for external providers and time/randomness where needed.

High-value test areas include:

### Shuffle

- zero covers
- one cover
- multiple covers
- no immediate repeat
- cycle exhaustion
- cycle reshuffle
- disabled covers
- missing files
- persisted state
- pool modification

### Scheduling

- due/not due
- startup handling
- missed intervals
- interval changes
- disabled games
- missing covers
- persisted timestamps

### Configuration

- global defaults
- game overrides
- validation
- reset/inheritance when implemented

### Persistence

- save/load
- corrupt data
- schema version
- migration
- round-trip compatibility

### Providers

- normalization
- authentication failure
- network failure
- malformed response
- duplicate handling
- source identification

### Cover management

- import
- validation
- limit enforcement
- duplicate prevention
- restoration
- missing files

### UI/ViewModels

- search state
- game title autofill
- selection state
- add-button state
- loading/error/empty states
- commands

Do not write tests that merely mirror implementation details. Test observable
behaviour and invariants.

---

# 38. UI TESTING

Unit tests cannot catch every WPF interaction problem.

When changing interactive WPF controls, explicitly verify:

- mouse click
- keyboard focus
- selection
- hover
- enabled/disabled state
- dark theme
- light theme
- resizing
- scrolling

If automated UI testing is impractical, document manual acceptance steps in
the phase completion report.

The screenshot-driven issue of a cover appearing clickable while not responding
to clicks is a reminder that ViewModel tests alone are insufficient.

---

# 39. PERFORMANCE

The plugin must remain lightweight for large Playnite libraries.

Avoid:

- repeated full-library scans when an event-driven solution is available
- repeated provider queries
- repeated downloads
- unnecessary image decoding on the UI thread
- duplicate timers per game where one scheduler can handle the workload
- excessive database rewrites

Do not optimize prematurely. Measure or identify a real bottleneck first.

A large library should not make opening the manager or settings unusably slow.

---

# 40. LIVE SCHEDULER FUTURE REQUIREMENTS

The current documented scheduler is startup-based. When live scheduling is
implemented, it should follow these rules:

- one coherent scheduler service
- cancellation token / deterministic shutdown
- no UI-thread blocking
- no duplicate workers after reload
- persist next-run timestamps
- wake/recalculate when relevant settings change
- handle Playnite sleep/restart sensibly
- never replay a backlog of every missed interval
- skip invalid/missing cover files safely

Prefer a single scheduler/priority mechanism over one uncontrolled timer per
game.

---

# 41. IMPORT / EXPORT

Existing import/export must remain backward compatible.

Imports must validate:

- structure
- version/schema where applicable
- game identity
- cover references
- image files

Never silently overwrite unrelated user data.

Where games cannot be matched, report them clearly.

Future portable backup features should distinguish between:

- configuration export
- complete backup/restore

Do not claim cross-library remapping unless it is actually implemented.

---

# 42. MAINTENANCE AND CLEANUP

Maintenance actions are potentially destructive.

Scanning should not itself delete anything.

Cleanup should:

1. Show what will be affected.
2. Require explicit confirmation.
3. Delete only plugin-owned data that is safe to delete.
4. Never delete Playnite's original artwork as ordinary cleanup.

Potential maintenance categories include:

- orphaned cover files
- missing cover records/files
- stale provider cache
- unused cache
- invalid records

Any new cleanup operation must clearly define ownership before deletion.

---

# 43. BACKUP AND RECOVERY

Before introducing major persistence changes, consider how existing users can
recover their data.

Do not use backup functionality as an excuse to change the storage model
unnecessarily.

If a backup/archive format is introduced, include enough version metadata to
allow future recovery.

---

# 44. DEPENDENCIES AND BUILD

The current project targets `net462` and references:

- PlayniteSDK `6.16.0`
- Newtonsoft.Json `13.0.3`

Playnite SDK is a compile-time dependency and must not be packaged as a second
runtime copy when Playnite supplies it.

The project contains an existing `DeployForPlaynite` build target and a
`PackageExtension` target for producing a `.pext` package.

Do not casually remove or alter these targets.

If dependency versions change:

1. Verify Playnite compatibility.
2. Build the plugin.
3. Test loading behaviour.
4. Check packaging contents.
5. Document the reason.

---

# 45. PACKAGING

The distributable `.pext` must contain only what Playnite needs.

Do not accidentally package:

- tests
- source files
- Playnite SDK runtime copies
- local credentials
- development artifacts
- unnecessary debug files

Before release, inspect the package contents rather than assuming the MSBuild
rule is correct.

---

# 46. VERSIONING

Keep these aligned when releasing:

- project version
- assembly version/file version where appropriate
- `extension.yaml`
- README/release notes where relevant

Use semantic versioning unless project requirements explicitly change it.

A behaviour change that affects persisted data requires additional care even
if the semantic version appears minor.

---

# 47. SECURITY

Never:

- commit API credentials
- hard-code secrets
- log credentials
- trust remote filenames as paths
- execute downloaded files
- write outside controlled plugin storage without explicit need
- expose credentials through UI diagnostics

Treat external provider responses as untrusted input.

Validate lengths, identifiers, URLs, filenames, image data, and other external
values where relevant.

---

# 48. DOCUMENTATION CONTRACT

The README describes user-visible behaviour.

When a feature changes materially, update documentation in the same phase if
appropriate.

Do not document aspirational functionality as if it were implemented.

Known limitations must remain honest.

If a manual setup step is required, document it.

---

# 49. CODE STYLE

Use conventional C# naming:

- Classes: `PascalCase`
- Interfaces: `IPascalCase`
- Methods: `PascalCase`
- Private fields: `_camelCase`
- Local variables: `camelCase`

Follow the existing repository style where it is consistent.

Avoid unnecessary abbreviations.

Prefer small, focused methods.

Avoid deeply nested conditional logic when a clearer service/policy can
express the behaviour.

Do not introduce clever patterns without a concrete benefit.

---

# 50. COMMENTS

Comments should explain **why**, not restate **what** the code obviously does.

Useful comments include:

- Playnite API quirks/workarounds
- persistence compatibility decisions
- scheduling edge cases
- provider-specific behaviour
- security decisions

Do not fill the code with comments that simply narrate each line.

---

# 51. WHEN REQUIREMENTS ARE AMBIGUOUS

Do not silently invent major product behaviour.

If an ambiguity affects architecture, persistence, user data, or visible
behaviour:

1. Identify it.
2. State the assumption.
3. Choose the smallest reversible behaviour if safe.
4. Continue when the decision is low-risk.
5. Ask for clarification when the decision could cause data loss or major
   product divergence.

Do not block on trivial details.

---

# 52. DO NOT OVER-ENGINEER

Avoid introducing:

- unnecessary dependency injection frameworks
- unnecessary databases
- unnecessary event buses
- unnecessary generic repositories
- unnecessary abstractions for one method
- unnecessary third-party UI libraries

The plugin is small and should remain maintainable.

Use the simplest design that preserves testability and future flexibility.

---

# 53. DO NOT REFACTOR FOR STYLE ALONE

A phase should not become an excuse for a repository-wide cleanup.

If you discover unrelated technical debt:

- record it
- fix it only if it blocks the current feature or creates a clear safety risk
- otherwise defer it

Avoid huge diffs that make regressions difficult to identify.

---

# 54. FEATURE ROADMAP / PRODUCT DIRECTION

The following roadmap is guidance for future work, not permission to implement
all of it in one task.

## Phase A — Core Shuffle Reliability

Ensure the current randomized shuffle implementation is fully correct and
well tested.

Focus on:

- shuffle invariants
- cycle persistence
- disabled/missing covers
- manual shuffle semantics
- state recovery

## Phase B — Live Scheduling

Move from startup-only due processing to a reliable live scheduler.

Focus on:

- background scheduling
- lifecycle
- cancellation
- next-run calculation
- sleep/restart behaviour
- no duplicate workers

## Phase C — SteamGridDB 2.0

Improve provider UX and matching.

Focus on:

- multiple game matches
- correct-game selection
- stronger matching
- cover filtering
- rate-limit handling
- caching
- image loading feedback

## Phase D — UI/UX Modernisation

Bring all major plugin windows to a polished Playnite-compatible design.

Focus on:

- dark/light theme compatibility
- modern cover browser
- reliable click selection
- cover preview
- responsive grid
- loading/empty/error states
- keyboard accessibility
- manager improvements

## Phase E — Effective Settings

Improve global/game configuration semantics.

Focus on:

- true inheritance
- per-setting overrides
- reset to global defaults
- effective settings service
- migration of existing configuration

## Phase F — Advanced Cover Management

Potential features:

- per-cover enable/disable
- favourites
- cover ordering
- usage statistics
- least-used shuffle
- weighted shuffle
- missing-cover recovery
- drag/drop local import

Only implement features explicitly selected for the phase.

## Phase G — Playnite Integration Improvements

Potential features:

- improved game-launch shuffle controls
- better new-game workflow
- stronger metadata artwork support
- event-driven integration

## Phase H — Portability and Recovery

Potential features:

- robust import matching
- backup/restore
- migration framework
- recovery tooling

## Phase I — Hardening

Focus on:

- race conditions
- file locking
- plugin reloads
- shutdown
- corrupted data
- missing files
- provider failures
- large libraries
- memory usage
- UI responsiveness

## Phase J — Release Candidate

Focus on:

- CI
- build verification
- package verification
- clean installation
- upgrade testing
- documentation
- release notes
- final UX polish

---

# 55. HIGH-VALUE FUTURE FEATURES

Features that fit the existing product particularly well include:

### Cover preview

Selecting a cover can show a larger preview and metadata before adding it.

### Per-cover enable/disable

Keep 10 covers in the library while choosing which participate in shuffling.

### Favourite covers

Allow users to mark covers as favourites and optionally shuffle only those.

### Usage information

Show usage count and last-used time where useful.

### Least-used shuffle

Prefer covers that have not been shown recently.

### Missing-cover recovery

For provider-backed covers, offer redownload when the local file disappears.

### Drag-and-drop local import

Use the existing validated import pipeline.

### Seasonal cover groups

Possible future extension once the core scheduler and cover model are stable.

### Diagnostics

Provide a safe diagnostic summary containing plugin/version/database/provider
status without exposing secrets.

Do not implement these merely because they appear here.

---

# 56. FEATURES THAT MUST NOT BE ADDED WITHOUT EXPLICIT APPROVAL

Do not introduce major scope changes such as:

- AI-generated artwork
- a separate Windows background application
- a tray application
- a new database technology
- a custom Playnite theme dependency
- an additional online provider
- telemetry/analytics
- account systems
- cloud synchronisation
- automatic network scraping

unless the user explicitly requests them and their architecture/security
implications are considered first.

---

# 57. DEVELOPMENT WORKFLOW

Before coding:

1. Read this entire `CLAUDE.md`.
2. Inspect the repository tree.
3. Read the relevant existing implementation.
4. Read the relevant tests.
5. Read the README section covering the feature.
6. Identify what already exists.
7. Define the smallest coherent change.

During coding:

1. Preserve existing architecture.
2. Reuse existing services/abstractions.
3. Keep changes scoped.
4. Add tests with the implementation.
5. Avoid unrelated formatting churn.
6. Check error handling and persistence.

After coding:

1. Build.
2. Run tests.
3. Inspect the diff.
4. Verify no secrets or development files were introduced.
5. Verify no future-phase feature slipped in.
6. Update README/documentation when required.
7. Produce the completion report.

---

# 58. REQUIRED SELF-REVIEW BEFORE FINAL RESPONSE

Before claiming a phase is complete, Claude must mentally verify:

```text
[ ] Did I inspect the existing implementation first?
[ ] Did I preserve working functionality?
[ ] Did I work only on the requested scope?
[ ] Did I preserve user data?
[ ] Did I preserve schema compatibility?
[ ] Did I handle errors?
[ ] Did I add appropriate tests?
[ ] Did I build the project?
[ ] Did I run the relevant test suite?
[ ] Did I check dark/light UI where applicable?
[ ] Did I check Playnite lifecycle behaviour where applicable?
[ ] Did I inspect the final diff?
[ ] Did I avoid introducing secrets?
[ ] Did I document limitations honestly?
[ ] Did I provide manual Playnite test steps?
```

If any answer is no, do not describe the work as fully complete without
explaining the exception.

---

# 59. FINAL RULE

Optimize for a plugin that users can trust.

The goal is not maximum code or maximum features.

The goal is:

- safe artwork management
- reliable randomisation
- predictable scheduling
- excellent Playnite integration
- polished UI
- offline-capable existing covers
- robust persistence
- clear errors
- maintainable architecture
- useful tests
- easy upgrades

**Inspect first. Change only what is necessary. Test everything important.
Protect user data. Never claim functionality that does not actually work.**
