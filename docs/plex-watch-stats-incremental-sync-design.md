# Plex Watch Stats Incremental Sync Design

## Goal

Replace the current full-history rebuild model for Plex watch stats with a reliable incremental sync that:

- fetches only new or recently-changed Plex history where possible
- makes repeated runs idempotent
- avoids reprocessing the full Plex history on every scheduled run
- keeps rolling aggregates and watch-triggered searches correct
- preserves a safe recovery path when Plex data, matching, or sync state becomes inconsistent

## Current Behavior

The current implementation does a full rebuild on each run:

1. `RefreshPlexSeriesStatsService` fetches all Plex history pages.
2. Every returned watch event is normalized and matched again.
3. Daily aggregates are rebuilt in memory.
4. `PlexSeriesWatchStatisticsRepository.ReplaceForServer()` deletes all buckets for that Plex server and reinserts the entire set.
5. Watch-triggered search evaluates matched events from the full dataset.

This is correct enough for v1, but expensive:

- runtime scales with total Plex history, not recent activity
- matching cost is paid on every run
- database writes rewrite the entire server dataset
- the first run after enabling watch-triggered search can enqueue a large historical backfill

## Reliability Principles

The incremental design should follow these rules:

1. Never advance the sync checkpoint until the run completes successfully.
2. Always use a safety overlap window to protect against ordering shifts and delayed Plex writes.
3. Make duplicate processing harmless through a unique processed-event key.
4. Base watch-triggered search on newly accepted events, not the full matched history.
5. Keep a bounded reconciliation path for recovery and drift correction.

## Proposed Schema Changes

### 1. New table: `PlexWatchStatsSyncState`

One row per Plex server definition.

Suggested columns:

- `Id`
- `PlexServerDefinitionId` `INT NOT NULL`
- `LastSuccessfulViewedAtUtc` `DATETIME NULL`
- `LastSuccessfulEventKey` `TEXT NULL`
- `LastRunStartedAtUtc` `DATETIME NULL`
- `LastRunCompletedAtUtc` `DATETIME NULL`
- `LastRunStatus` `TEXT NULL`
- `LastRunMessage` `TEXT NULL`
- `FullResyncRequired` `BOOLEAN NOT NULL DEFAULT 0`
- `CreatedAtUtc` `DATETIME NOT NULL`
- `UpdatedAtUtc` `DATETIME NOT NULL`

Indexes:

- unique index on `PlexServerDefinitionId`
- optional index on `FullResyncRequired`

Purpose:

- stores the authoritative per-server checkpoint
- separates operational sync state from derived series statistics

### 2. New table: `PlexProcessedWatchEvents`

One row per accepted watch event from Plex.

Suggested columns:

- `Id`
- `PlexServerDefinitionId` `INT NOT NULL`
- `EventKey` `TEXT NOT NULL`
- `ViewedAtUtc` `DATETIME NOT NULL`
- `ViewedOn` `DATETIME NOT NULL`
- `SeriesId` `INT NULL`
- `SeriesTitle` `TEXT NULL`
- `FilePath` `TEXT NULL`
- `Matched` `BOOLEAN NOT NULL`
- `CreatedAtUtc` `DATETIME NOT NULL`

Indexes:

- unique index on `PlexServerDefinitionId, EventKey`
- index on `PlexServerDefinitionId, ViewedAtUtc`
- index on `PlexServerDefinitionId, SeriesId, ViewedOn`
- optional index on `Matched`

Purpose:

- makes overlap-based re-fetch safe
- prevents duplicate bucket increments
- gives a lightweight audit trail for accepted unmatched/matched events

### 3. Keep `PlexSeriesWatchStatistics`

No major schema change required for incremental mode.

Current columns are enough:

- `SeriesId`
- `PlexServerDefinitionId`
- `ViewedOn`
- `ViewCount`
- `LastViewedAtUtc`
- `CreatedAtUtc`
- `UpdatedAtUtc`

Behavior changes from full replace to targeted per-bucket upsert/increment.

### 4. Keep `PlexWatchTriggeredSearchState`

No schema change required for the first incremental pass.

Behavior changes so it is updated only from newly accepted events.

## Event Identity Strategy

The design depends on stable dedupe keys.

### Preferred

Use a Plex-provided stable history identifier if the response contains one.

Candidate fields to add to the history DTOs if present:

- `ratingKey`
- `key`
- `guid`
- a history/session id

If a stable history row id exists, define:

- `EventKey = $"{plexHistoryId}"`

### Fallback

If Plex does not expose a stable event id consistently, compute a deterministic fingerprint from immutable-enough fields:

- `ViewedAtUtc`
- `SeriesTitle`
- `SeriesYear`
- episode/parent indexes if present
- normalized `FilePath`
- user/account id if present

Recommended shape:

- add `SeasonNumber`
- add `EpisodeNumber`
- add `EpisodeTitle`
- add `RatingKey` if available

Then compute:

- `EventKey = SHA256(serverId + viewedAt + ratingKey + accountId + filePath + season + episode)`

If `ratingKey` is available, include it. If not, use the best available fallback mix.

## Fetch Strategy

### Normal incremental run

1. Load `PlexWatchStatsSyncState` for the server.
2. Set `fetchBeforeUtc = LastSuccessfulViewedAtUtc - overlapWindow`.
3. Request Plex history sorted by `viewedAt desc`.
4. Continue paging until:
   - the page is empty, or
   - the oldest item in the current page is older than `fetchBeforeUtc`
5. Normalize the fetched items.
6. For each event:
   - compute `EventKey`
   - skip if already in `PlexProcessedWatchEvents`
   - otherwise accept it for matching and persistence

Recommended overlap:

- default `48 hours`

Why:

- protects against delayed writes and equal-timestamp boundary conditions
- keeps hourly incremental runs bounded

### Full resync / reconciliation run

Run when:

- sync state is missing
- `FullResyncRequired = true`
- schema or matching behavior changed materially
- manual repair is invoked later

Behavior:

- fetch a bounded recovery window first, for example last `90 days`
- only do full-history rebuild if explicitly required

For v2 of this optimization, a weekly reconciliation of the last `30-90 days` is sufficient and far cheaper than a full-history replay every hour.

## Write Path

### New repository/service responsibilities

Introduce:

- `IPlexWatchStatsSyncStateRepository`
- `PlexWatchStatsSyncStateRepository`
- `IPlexProcessedWatchEventRepository`
- `PlexProcessedWatchEventRepository`

#### `IPlexWatchStatsSyncStateRepository`

Operations:

- `GetByServer(int plexServerDefinitionId)`
- `Upsert(PlexWatchStatsSyncState state)`
- `MarkRunStarted(...)`
- `MarkRunSucceeded(...)`
- `MarkRunFailed(...)`

#### `IPlexProcessedWatchEventRepository`

Operations:

- `HashSet<string> GetExistingKeys(int plexServerDefinitionId, IEnumerable<string> eventKeys)`
- `InsertMany(IList<PlexProcessedWatchEvent> events)`
- `GetNewlyMatchedSeriesEvents(...)` optional helper

### Change aggregate writes to per-bucket upsert

Replace the current `ReplaceForServer()` pattern with something like:

- `UpsertBucketDeltas(int plexServerDefinitionId, IList<PlexSeriesWatchStatisticDelta> deltas)`

Where each delta contains:

- `SeriesId`
- `ViewedOn`
- `ViewCountDelta`
- `LastViewedAtUtc`

Repository behavior:

- if bucket exists, increment `ViewCount`
- set `LastViewedAtUtc = MAX(existing, incoming)`
- update `UpdatedAtUtc`
- if bucket does not exist, insert it

This is the key performance win on the write side.

## Watch-Triggered Search Behavior

### Problem in current behavior

The trigger path groups over the full matched history returned in the current run. On the first enabled run, historical viewing causes a mass trigger.

### Proposed behavior

Drive triggering only from newly accepted events in the current incremental run.

Flow:

1. Fetch incremental events.
2. Deduplicate against `PlexProcessedWatchEvents`.
3. Match only new events.
4. Persist processed-event rows.
5. Upsert aggregate deltas.
6. Pass only newly matched events to `PlexWatchTriggeredSearchService.Process()`

Result:

- a newly-enabled server does not need to replay all historical watch activity into searches
- watch-trigger logic naturally reflects recent user behavior

### Optional protection for first enablement

Add one new setting later if needed:

- `SeedWatchTriggerStateOnEnable: bool`

If enabled:

- first run populates `PlexWatchTriggeredSearchState.LastSeenViewedAtUtc`
- but does not queue searches

This is not required to ship incremental sync, but it is the safest operational default.

## Proposed Code Touchpoints

### Fetch / normalize layer

Update:

- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexWatchStatsProxy.cs`
- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexWatchStatsService.cs`
- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexWatchEvent.cs`

Changes:

- extend Plex DTOs with additional identity fields if available
- expose a paged incremental fetch helper instead of only `GetWatchEvents(settings)`
- add normalization support for `EventKey` generation inputs

Suggested API changes:

- `PlexWatchHistoryPage GetHistory(PlexServerSettings settings, int start, int pageSize)`
  remains
- add service-level method:
  - `List<PlexWatchEvent> GetWatchEventsSince(PlexServerSettings settings, PlexWatchStatsFetchCursor cursor, TimeSpan overlap)`

Keep endpoint selection encapsulated in the proxy.

### Matching layer

Update:

- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexSeriesMatchService.cs`

Changes:

- no architectural rewrite required
- keep current GUID/path/title matching
- optionally cache lookups more aggressively within a run
- optionally return match diagnostics so logs can separate `guid`, `path`, `title`, `none`

### Persistence layer

Update:

- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexSeriesWatchStatisticsRepository.cs`
- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexWatchTriggeredSearchStateRepository.cs`
- `src/NzbDrone.Core/Datastore/TableMapping.cs`

Add:

- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexWatchStatsSyncState.cs`
- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexWatchStatsSyncStateRepository.cs`
- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexProcessedWatchEvent.cs`
- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexProcessedWatchEventRepository.cs`

Repository changes:

- remove `ReplaceForServer()` from the hot path
- add batch dedupe lookups by event key
- add targeted bucket upsert/increment operations

### Scheduled job orchestration

Update:

- `src/NzbDrone.Core/Notifications/Plex/WatchStats/RefreshPlexSeriesStatsService.cs`

Refactor flow to:

1. load sync state
2. mark run started
3. fetch incremental events with overlap
4. dedupe against processed-event keys
5. match only newly accepted events
6. insert processed-event rows
7. upsert aggregate bucket deltas
8. run watch-trigger search on newly matched events only
9. mark sync state success with high-water mark
10. on failure, leave previous successful checkpoint unchanged

Suggested internal helpers:

- `FetchIncrementalEvents(...)`
- `DeduplicateEvents(...)`
- `MatchEvents(...)`
- `BuildBucketDeltas(...)`
- `UpdateCheckpoint(...)`

### Watch-trigger service

Update:

- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexWatchTriggeredSearchService.cs`

Changes:

- no large logic change required
- input should become only newly matched events
- current cooldown/state logic can remain

Optional enhancement:

- add a mode to seed `LastSeenViewedAtUtc` without queuing commands for first-run enablement

### Migrations

Add new migrations under:

- `src/NzbDrone.Core/Datastore/Migration`

Suggested migration order:

- `230_add_plex_watch_stats_sync_state`
- `231_add_plex_processed_watch_events`

If desired, a later migration can add optional indexes or cleanup support.

### Stats aggregation and API

No major changes needed in:

- `src/NzbDrone.Core/SeriesStats/SeriesStatisticsService.cs`
- `src/NzbDrone.Core/Notifications/Plex/WatchStats/PlexSeriesWatchStatistic.cs`
- API resources already added for the user-facing stats

The read side should continue to aggregate from `PlexSeriesWatchStatistics`.

## Detailed Incremental Algorithm

For each Plex server:

1. Load settings and sync state.
2. Compute `overlapStartUtc`.
3. Page Plex history newest-first until page exhaustion or crossing `overlapStartUtc`.
4. Normalize each history item into `PlexWatchEvent`.
5. Compute `EventKey` for each normalized event.
6. Query existing keys for this server in batches.
7. Keep only unseen events.
8. Match unseen events to Sonarr series.
9. Insert `PlexProcessedWatchEvents` rows for all unseen events.
   For unmatched events:
   - store `Matched = false`
   - keep basic metadata for diagnostics
10. Build bucket deltas for matched unseen events.
11. Upsert bucket deltas into `PlexSeriesWatchStatistics`.
12. Pass newly matched events to `PlexWatchTriggeredSearchService`.
13. Advance checkpoint to the newest successfully persisted event in the run.
14. Mark sync success.

Failure semantics:

- if any step after fetch fails, do not advance the checkpoint
- already-inserted processed-event rows are acceptable only if the bucket upsert and checkpoint update are in the same transaction boundary

Recommended transaction boundary:

- insert processed events
- upsert bucket deltas
- update sync state success

That keeps incremental progression atomic enough for retry safety.

## Logging and Metrics

Update `RefreshPlexSeriesStatsService` logging to include:

- pages fetched
- events fetched from Plex
- events older than checkpoint window
- duplicate events ignored
- newly accepted events
- matched events
- unmatched events
- malformed events
- buckets inserted
- buckets updated
- triggered series
- duration for:
  - fetch
  - dedupe
  - match
  - persist
  - trigger

This should replace the current single summary line as the main optimization telemetry.

## Backfill and Cleanup

### Initial rollout for existing users

For existing deployments with already-populated `PlexSeriesWatchStatistics`:

- create sync-state row with `FullResyncRequired = true`
- first upgraded run performs one reconciliation run
- after that, switch to incremental mode

### Processed-event table growth

This table will grow continuously. To keep it bounded:

- retain at least the same horizon as any overlap/reconciliation window
- do not aggressively delete recent rows

For a first iteration:

- keep all processed events

For a later housekeeping improvement:

- prune processed events older than `180-365 days` only if corresponding aggregate correctness is unaffected

## Test Plan

### New tests

Add tests for:

- incremental paging stop condition based on checkpoint plus overlap
- dedupe against existing processed-event keys
- bucket upsert/increment semantics
- checkpoint advancement only on success
- checkpoint not advanced on partial failure
- watch-trigger service receiving only newly matched events
- first run with overlap does not double-count when rerun

### Update existing tests

Update:

- `PlexWatchStatsServiceFixture`
- `SeriesStatisticsServiceFixture`
- scheduled job tests for provider selection and failure isolation

Add repository fixtures for:

- `PlexWatchStatsSyncStateRepository`
- `PlexProcessedWatchEventRepository`
- incremental bucket updates in `PlexSeriesWatchStatisticsRepository`

## Implementation Order

1. Add new models and table mappings.
2. Add migrations for sync state and processed events.
3. Extend Plex DTO normalization to support stable event keys.
4. Add sync state and processed-event repositories.
5. Refactor `RefreshPlexSeriesStatsService` to incremental orchestration.
6. Replace aggregate full replace with per-bucket upsert/increment.
7. Change watch-trigger input to newly matched events only.
8. Add logging and tests.
9. Optional: add a bounded reconciliation mode and initial-trigger seeding behavior.

## Expected Outcome

After this design is implemented:

- steady-state runs scale with new Plex watch activity, not entire history size
- duplicate counting is prevented by design
- watch-triggered search becomes event-driven instead of history-replay-driven
- first-run enablement behavior can be controlled safely
- recovery remains possible without making hourly syncs expensive
