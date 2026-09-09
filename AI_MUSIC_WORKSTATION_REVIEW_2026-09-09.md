# AI Music Workstation — system review

Reviewed 9 September 2026. This is a read-only investigation of application behavior, local source, connected repositories, deployment metadata, and live database configuration. No application fixes, deployments, database changes, or account changes were made. Build outputs and this report were created locally.

## Assessment

The project implements a music analysis and practice workstation: import existing audio, identify musical information, separate instruments, practice with playback aids, and manage a personal library. It is not currently a song-generation system or a full recording/sequencing DAW.

The frontend builds, the deployed landing page loads, the Azure API answers its health check, and the API can reach Python. Those are useful positive signals. However, the library model and live database disagree, the shared application/test project does not compile, and several import, persistence, and mixer contracts contain concrete defects. Successful deployment is therefore not evidence of a working end-to-end workstation.

## Connected system map

| Component | Verified information |
|---|---|
| Local checkout | `C:\Project C.O.D.E\AiMusicWorkstation`; HEAD `f48f68872de51b628132fa94c0e5579d2ce9d733` at review time |
| GitHub | [benji9812/Ai-Music-Workstation](https://github.com/benji9812/Ai-Music-Workstation), public, default branch `dev`; the configured local remote |
| Vercel | `ai_music-workstation_frontend`, team `aimusicworkstation`, Vite, Node 24.x; latest production deployment READY and built from the same HEAD |
| Live frontend | [project-0p9yf.vercel.app](https://project-0p9yf.vercel.app/landing); landing page verified in browser |
| Azure API | `aimusicworkstation-api-e5eyc7b2bgemh5en.swedencentral-01.azurewebsites.net`; `/health` returned HTTP 200 and `healthy` |
| Python connectivity | Azure `/api/analysis/health` returned HTTP 200 and `ok` |
| Supabase | Project `rsynmievakeeccdbczqy`, named Ai Music Workstation, ACTIVE_HEALTHY, eu-north-1, PostgreSQL 17 |
| GitLab | [benji9812-group/Ai-Music-Workstation](https://gitlab.com/benji9812-group/Ai-Music-Workstation), private, default branch `dev`; latest inspected commit `98a00edf` dated 28 May 2026; no pipelines returned |
| Spotify | Connector available, but its tools expose music/library/playback functions rather than developer app configuration. No integration between that connector's account session and the workstation was found in source |
| Azure infrastructure | GitHub workflows describe an App Service deployment for .NET and SSH/systemd deployment of Python to an Azure VM. VM configuration, disks, firewall, and installed models were not directly inspected |
| Render | An older deployment manifest remains, targeting `Solution/Remodelling_To_Web`; no live Render account was inspected |

The latest GitHub Azure API deployment and CodeQL run both succeeded. Open-issue and open-PR searches returned none at inspection time. Some recent Dependabot update jobs failed; these are distinct from the successful API deployment. The GitLab branch appears to be a stale copy, not an actively synchronized deployment source. Its exact mirroring configuration was not inspected.

Latest API deployment: [GitHub Actions run](https://github.com/benji9812/Ai-Music-Workstation/actions/runs/34358617332). Latest CodeQL run: [GitHub Actions run](https://github.com/benji9812/Ai-Music-Workstation/actions/runs/34358617373).

## Architecture and data flow

The React/Vite frontend authenticates directly with Supabase using email and password. Zustand holds session state. API calls generally attach the Supabase access token. The .NET gateway validates issuer, audience, and token lifetime against the project's Supabase Auth endpoint.

The API talks directly to PostgreSQL through Entity Framework/Npgsql, and forwards analysis requests to FastAPI. Audio is stored on the Python server's filesystem, not in the inspected Supabase song rows or a demonstrated Supabase Storage integration. The browser receives audio through an API proxy to Python.

Python combines signal-processing libraries and model services: librosa, optional Essentia, optional allin1, Demucs, Groq Whisper, and Gemini. URL imports use yt-dlp. Jobs run in daemon threads with status held in a process-local dictionary. The browser polls the API, which polls Python.

The Windows WPF application remains in the solution. It has its own playback, local library/session persistence, Python bridge, chord/scale renderers, metronome, and command/query handlers. The web API directly references Infrastructure and Shared, not Application. This explains why API deployment can succeed while tests that reference Application fail to compile.

## Function inventory

| Area | Implemented behavior and limits |
|---|---|
| Entry/account | Landing page, email/password registration and login, display name, email-confirmation messaging, session initialization, sign-out, simple `/landing` and `/app` routing |
| Local import | File selection, upload progress, quick BPM/key/meter/chord analysis, original-file playback; currently a temporary session flow rather than a complete durable save flow |
| URL import | Background quick analysis with progress/status polling; Spotify track metadata resolution and SoundCloud/YouTube search; direct supported URL download through yt-dlp |
| Full analysis | Demucs stem separation plus musical analysis and lyrics; available through backend routes, distinct from the primary quick-import UI flow |
| Stem separation | User-selectable instruments; Python uses `htdemucs_6s`; frontend handles dynamically returned stems, while legacy library reload assumes drums/bass/other/vocals |
| Mixer | Per-stem gain, mute, solo, original-audio channel, master gain; confirmed scaling defect described below |
| Playback | Play/pause, start/seek controls, timeline, repeat, count-in, metronome, displayed duration, synchronized text/chord position |
| Pitch/music theory | Browser Tone.js pitch shifting, semitone controls limited to +/-12, note/chord/key transposition, chord simplification, chord notes, scale derivation and diagrams |
| Lyrics | Groq `whisper-large-v3`, timestamp post-processing, background transcription, synchronized display, API persistence endpoint; legacy .NET service additionally tries LRCLib and Genius |
| Song structure | allin1 when available, Gemini fallback, labeled sections, navigation and manual section editing with frontend time validation |
| Chords | Detection and timed display, diagrams, transposition, API endpoint for saved chord edits |
| Library | List/load projects, edit title/artist, delete, create/delete groups, move songs between groups; model/schema and persistence defects prevent assuming this works reliably |
| Recovery | Admin-only API stem rescan, intended to recover orphaned filesystem folders |
| Export | Offline browser rendering to stereo WAV with mute/solo/gain handling; pitch-shift processing is not present in the export graph |
| Desktop | WPF UI, NAudio-style stem playback/mixing, metronome/count-in, transposition, loop handling, local library/session files, analysis parsing and lyric lookup |
| Telemetry/docs | Vercel Speed Insights in frontend; API OpenAPI and Scalar exposed; basic service health endpoints |

Spotify import is not a download of the Spotify stream. Python reads track metadata and searches for a corresponding recording elsewhere. The selected recording can differ in version, duration, arrangement, or quality. The .NET SmartImporter also has Spotify client-credentials support and metadata-scraping fallback, then searches YouTube. Neither establishes that the desktop Spotify application's login is used by the workstation.

## Highest-priority findings

### 1. Live song schema and Entity Framework model disagree

Confirmed by comparing `Infrastructure/Persistence/AiMusicWorkstationDbContext.cs` and `Domain/Entities/SongProject.cs` with live Supabase column metadata.

The model expects columns including `date_added`, `genre`, `group_name`, `is_official_data`, `original_path`, `sections`, `spotify_id`, and `stems_path`. These were absent from the live `public.songs` table. The live table instead includes `created_at`, `file_url`, and `structure`. There are also type disagreements: model ID is a string while the database ID is UUID; model duration is TimeSpan while the database duration is integer; model time signature is integer while the database column is text. No conversion addressing these differences appears in the current context mapping.

Expected consequence: project materialization and writes fail against this schema, even though a basic connection or row-count check can succeed. This was not tested with a user login or a live write.

Actual read-only counts were 0 songs, 0 groups, and 2 profiles. Table-list row estimates differed from exact counts; exact SQL counts are used here. Migration history contained all seven local migrations plus the `99999999999999_DropLegacyTables` marker. That marker is explicitly inserted by the local RemapSongsSchema migration; it is not evidence of an unknown migration by itself. A fully populated migration history does not establish schema/model agreement.

### 2. Shared application/test build fails

`dotnet test AiMusicWorkstation.Tests/AiMusicWorkstation.Tests.csproj --no-restore` failed during compilation:

- `Application/Handlers/Commands/DeleteProjectCommandHandler.cs:31` and `:38`: CancellationToken supplied where the repository now expects nullable user ID.
- `Application/Handlers/Queries/FilterProjectsQueryHandler.cs:22`: same signature mismatch.

Tests did not execute. The desktop library adapter also exposes the older signatures and deserves reconciliation when restoring the full solution. API-only deployment does not exercise these callers.

### 3. Playback and export gain calculation is wrong

`Web/src/App.tsx:1577` and `:2228` calculate `(volumes[k] ?? 0 / 100) * (masterVol / 100)`. Division applies only to the fallback zero, not the stored 1–100 slider value. For example, stem volume 80 and master 50 yields 40, then clamps to 1, instead of producing gain 0.4. This affects both listening and WAV export. Mute/solo still explicitly zero channels, but ordinary level adjustment is largely defeated.

### 4. Quick import and stem separation disagree about file paths

Quick analysis returns `original_path` as a basename. The UI sends that value as `originalFilePath`; the API forwards it as `file_path`. Python `/separate-stems` checks it directly with `os.path.exists`, without resolving it beneath UPLOAD_DIR. Normally the file resides in the uploads directory, not the process working directory. Lyrics jobs already contain a basename-to-UPLOAD_DIR fallback, showing inconsistent handling.

There is a second contract problem: the API supports sending an uploaded file as multipart to `/separate-stems`, but the Python endpoint only accepts its JSON Pydantic request. That branch is incompatible with the current Python route.

### 5. Job-status polling creates library records indiscriminately

`Api/Controllers/ImportController.cs` saves any completed object result as a SongProject. The same status endpoint is used for import, lyrics, and structure jobs. Results with no title or stems path can therefore produce default placeholder songs. Deduplication only runs when stemsPath is nonempty, so quick jobs can be inserted again on subsequent completed polls. Saving on a GET status request also makes retry behavior unsafe.

The saved project sets OriginalPath to an empty string, losing quick-import source information. Persistence failures are logged and the job result is still returned, making apparent analysis success possible without a usable library entry. The source already contains a TODO recognizing unwanted saving.

### 6. Local quick uploads lack a complete durable project flow

The upload UI calls `/api/import/analyze-quick`, which forwards analysis without creating a project, then sets currentProjectId to null. Stem separation does not complete a durable save either. Furthermore, the upload callback subsequently passes the captured previous currentProjectId into lyrics retrieval; React state updates do not alter that closure. If another project was previously selected, new lyrics can be associated with its ID.

### 7. Quick uploads report processing time as song duration

Python `/analyze-quick` returns `duration_seconds: elapsed(total_start)`, rather than measuring the recording. The URL quick-job path correctly measures audio duration. Browser audio metadata can later repair the displayed duration, but the initial structure request already uses the incorrect returned duration.

### 8. Saved project contracts do not match the frontend

The API exposes SongProject with default camelCase properties such as `originalPath`. The frontend reads `original_path`. The frontend also expects `extracted_stems`, but SongProject does not define or persist this map. Quick-import records have neither a retained original path nor useful stemsPath. Library reload also fetches filesystem analysis and requests structure again instead of consistently restoring all database-saved edits.

## Access and security findings

These are source/configuration findings, not an exploitation exercise.

- Audio proxy access is explicitly anonymous (`AnalysisController.GetAudio`), with no ownership lookup. Python audio routes/static output also lack app-level authentication. Knowing an audio path is sufficient at the application layer. Deployment network restrictions were not inspected.
- Python job IDs are not bound to a user; status results are retrieved by ID. The API authenticates the caller but does not record job ownership, and can save a completed result under the polling caller. Python compute and filesystem endpoints likewise have no authentication middleware in the reviewed source.
- The anonymous `LibraryController.DbCheck` endpoint exposes operational details. Its connection-string parse-error branch returns the raw string, potentially exposing credentials if that error path is reached. Normal parsing masks the password. Avoid returning connection strings and stack traces from a public endpoint.
- Supabase RLS is enabled on all four inspected public tables. Song/group policies use auth.uid ownership predicates. This is a positive control, but direct API database access has its own authorization requirements.
- Authenticated users have INSERT/UPDATE privilege on `profiles.role`, and profile policies permit writing their own row. This makes the role field user-writable absent another restriction such as a trigger. No end-to-end admin escalation was demonstrated; the current API checks JWT roles rather than querying that profile field. It must not be treated as trusted authorization data without restricting writes.
- Repository methods skip their ownership filter when passed null user ID. Controllers return null when subject parsing fails rather than rejecting the request. Normal Supabase subjects are UUIDs; nevertheless the repository interface fails open if called without identity.
- Moving a song to a group validates ownership of the song but not ownership of the target group. The database foreign key only validates group existence.
- API CORS allows every origin. Python's allowed-origin list names an older frontend hostname. Audio usually passes through the API, so this mismatch is not proof that current playback fails, but configuration should be made intentional.
- Supabase reported leaked-password protection disabled. [Supabase remediation](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection).
- The local .NET build reported AngleSharp 1.4.0 affected by a moderate advisory. The advisory identifies 1.5.0 as patched. Actual exploitability in this app was not established. [GitHub advisory](https://github.com/advisories/GHSA-pgww-w46g-26qg).

## Reliability, storage, and analysis limits

- Background jobs live only in memory and daemon threads. Restarting Python loses job status; multiple worker processes would not share that dictionary. No durable queue, bounded worker pool, user quota, or completed-job expiration was found in the job code.
- Local quick uploads use sanitized original filenames rather than per-user unique names. Two uploads with the same name can overwrite the same file.
- The 6 MB quick-upload limit is checked after reading the entire file into memory. It limits accepted files but does not prevent the allocation that the comment says it addresses.
- Heavy analysis runs synchronously inside several async FastAPI endpoints. Blocking CPU/subprocess work can occupy the event loop; background-thread routes improve request responsiveness but do not establish controlled throughput.
- File deletion runs on the .NET host even though audio resides on Python's host in the described deployment. ProjectFileManager also falls back to deleting a parent directory when a requested path is missing, without a configured storage-root boundary. This can both leave remote files orphaned and be unsafe for local/shared-filesystem layouts.
- Full analysis assigns OriginalPath from stemsPath, conflating a source file and stem directory.
- The reanalysis client posts to `/analyze-only`, but that route is absent from current Python source. The README still advertises it.
- Export mixes decoded sources through gain nodes only; it does not include the live Tone.js pitch shift. A transposed preview and exported file can differ.
- Backend musical estimates have fallback values, such as C major when key detection fails and 4 for meter fallback. These should not be mistaken for verified metadata. Accuracy has not been benchmarked on recordings in this review.
- The Gemini fallback uses `gemini-1.5-flash` in source, while the README says Gemini 2.5 Flash. Runtime model availability and account quota were not verified.
- Supabase advisors reported three unindexed foreign keys, five RLS auth-initplan findings, and ten overlapping-policy findings (the latter span role/action combinations, not ten distinct tables). [Foreign-key indexes](https://supabase.com/docs/guides/database/database-linter?lint=0001_unindexed_foreign_keys), [RLS evaluation](https://supabase.com/docs/guides/database/database-linter?lint=0003_auth_rls_initplan), [overlapping policies](https://supabase.com/docs/guides/database/database-linter?lint=0006_multiple_permissive_policies).

## Maintainability and deployment

The main React App.tsx exceeds 3,400 lines, combining authentication routing, jobs, networking, playback, theory, editing, library, and markup. Python main.py exceeds 2,000 lines and repeats URL/Spotify/download logic across import paths. Desktop and web retain different representations and services. These are concrete places where fixes can diverge or fail to propagate.

The frontend has Vitest and Testing Library dependencies, but its test script only prints that no tests are configured. The inspected xUnit suite focuses on ImportSongCommandHandler. No Python test files were present in the source inventory. There is no demonstrated regression coverage for audio mixing, user isolation, library reload, job idempotency, or the browser-to-Python contracts.

GitHub contains a .NET CI workflow and separate Azure deploy workflows. The frontend web-deploy.yml is comments/triggers without jobs, so it is not a working frontend quality gate. Vercel deploys independently through its GitHub integration. API startup automatically attempts migrations and logs failures while continuing, which can leave a nominally healthy service with unusable persistence.

Python requirements contain a large pinned dependency list plus unpinned Essentia. The README recommends Python 3.11+ and Node 18+, while the Vercel project uses Node 24.x and the local observed Node is 22.17.0. A clean dependency install was not attempted, so compatibility/reproducibility from those setup instructions is unverified. Several Python packages/models are optional in source but appear in installation requirements.

Two modifications existed before review: the API project gained a UserSecretsId, and App.tsx gained playback of the original URL-imported track. These edits were preserved. The latter is not included in the deployed HEAD and materially changes behavior: the committed version clears audio after URL quick import, while the local version loads the original recording immediately.

## Verification results and boundaries

| Check | Result |
|---|---|
| Frontend TypeScript compilation | Passed using installed compiler directly |
| Frontend Vite production build | Passed; installed Vite reported 8.0.13 |
| Standard npm launcher | Failed: npm-cli.js missing at a roaming npm location; direct installed tools worked |
| Frontend lint | Failed: one no-async-promise-executor error at App.tsx:1170; unused catch-variable warnings at 1024 and 1104 |
| .NET tests | Did not execute because Application compilation failed with three CS1503 errors |
| Python files | Both parsed successfully with Python AST; this is syntax verification, not a runtime/dependency/model test |
| Deployed frontend | Public landing page loaded, with login/register and feature descriptions |
| Deployed API health | HTTP 200, healthy |
| API-to-Python health | HTTP 200, ok |
| Supabase | Live schema, exact aggregate counts, migrations, RLS policies, selected privileges, security and performance advisors inspected |

No login credentials were entered, no account was created, and no recording was uploaded or sent to Groq/Gemini. Authenticated end-to-end behavior, actual stem quality, timing/latency, export listening quality, backup/restore, VM capacity, billing/quotas, private network controls, and all historical commits were not verified. Health responses do not test database model compatibility or successful AI inference. This is a broad system review with focused source tracing, not a claim that every line or every historical artifact was exhaustively audited.

The report does not reproduce secrets. A separate raw lint result file, review-eslint-results.json, contains lint findings only. Any .codex environment metadata appearing during the session was not intentionally authored as part of the application review.

## Suggested repair order

1. Reconcile the live schema, domain model, serialization contract, and source/stem storage identifiers. Verify save and reload against a non-production test database.
2. Fix repository callers so the test/application/desktop build is usable; establish tests for current web/API paths.
3. Make jobs typed, user-owned, and idempotent; separate status reads from project creation and persist original file references.
4. Correct mixer gain, song duration, stem-path resolution, and multipart/JSON mismatch; verify using a known short audio fixture.
5. Restrict anonymous diagnostics/audio access appropriately, protect profile roles, and validate target group ownership and storage roots.
6. Add durable jobs/storage lifecycle, regression coverage, and deployment gates; then reconcile documentation and the stale GitLab/Render configuration.

These are proposed changes only. Nothing in this review has been fixed or deployed.
