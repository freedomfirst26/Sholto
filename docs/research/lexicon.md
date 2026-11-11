# Lexicon DJ as a library manager for Sholto

Research for todo C169, 2026-11-11. Method: read the public lexicondj.com pages and the
published OpenAPI file by plain HTTP fetch. Nothing was installed, bought or run, so
anything that needs a licence or a running Lexicon is marked **not verified**.

## 1. What Lexicon is

Lexicon DJ is a desktop library manager made by Rekordcloud (one developer). It is not a
player-first DJ app: it sits beside Rekordbox, Serato, Traktor and the rest and does
the library chores (conversion, sync, cleanup, duplicates, cue generation, backups).
Runs on macOS, Windows and Linux (x86_64 `.deb` for Ubuntu 22.04+/Debian 12+, `.rpm`
for Fedora/openSUSE/RHEL 10; no ARM). It is closed source, commercial software.
Sources: <https://www.lexicondj.com/about>, <https://www.lexicondj.com/features>,
<https://www.lexicondj.com/manual/linux>.

Plans (<https://www.lexicondj.com/pricing>): Free (full library conversion, all
supported apps), Essential (track and playlist editing, mobile app), Ultimate (cue
generation, duplicates, watch folder, cloud backup). The page also shows $10.49 /
$20.99 and $249 / $499 figures; which belongs to which plan and period could not be
read from the fetched HTML, so prices are **not verified**.

## 2. Which DJ software it syncs

Import from and sync to (<https://www.lexicondj.com/manual/sync>): Rekordbox 5/6/7,
Serato, Traktor, VirtualDJ, Engine DJ, djay Pro, Apple Music / iTunes, Pioneer USB
(OneLibrary). It also imports and exports `.m3u(8)` playlists
(<https://www.lexicondj.com/manual/import-m3u>) and imports playlists from Spotify,
Beatport and Tidal. Sholto is not in the list and there is no way to add a target
besides plugins (below).

On Linux most of those apps have no native version, so Lexicon only reaches them
through a Wine prefix or a folder the user picks; the Linux-native paths are Pioneer
USB, Engine DJ on USB, Rekordbox XML and iTunes XML
(<https://www.lexicondj.com/manual/linux>).

## 3. Library storage and export formats

- **Storage:** Lexicon keeps its own SQLite database (tags, cues, beatgrids, playlists,
  settings). The developer page says it can be queried directly but advises against it
  (locking while Lexicon runs, you must join the data yourself). Its file location was
  not found in the public docs: **not verified**.
  Source: <https://www.lexicondj.com/docs/developers/api>.
- **Backups:** a ZIP of tags, playlists and settings, written to `Documents/Lexicon/Backups`
  (<https://www.lexicondj.com/manual/database-backup>). The ZIP's internal layout is not
  documented: **not verified**.
- **Export:** there is no neutral file format of Lexicon's own. "Export" means syncing
  into a target app's library: Rekordbox XML (official, slow, limited) or direct write
  to the Rekordbox 6/7 database, Serato crates, Traktor NML, Engine DJ and so on, plus
  M3U for playlists (<https://www.lexicondj.com/manual/sync-rekordbox-xml>).
- **Conversion limits:** e.g. no coloured cues in Traktor, 128 beatgrid markers max in
  Serato (<https://www.lexicondj.com/manual/library-conversion-limitations>).

## 4. Local API (the one real integration surface)

REST, JSON, OpenAPI 3.0 spec at <https://www.lexicondj.com/developer/api-docs.yaml>
(read in full for this note, 27 operations). Default `http://localhost:48624/v1`, off by
default, listens on all interfaces. Since v1.12 an app pairs with
`POST /v1/auth/pair`; the user approves in a popup and the app stores a bearer token.

- **Tracks:** `GET/PATCH /track`, `GET/POST/DELETE /tracks`, `GET /search/tracks`,
  `PATCH /track/location`. A track has title, artist, album, label, key, BPM,
  rating, colour, tags, file location, plus nested **cuepoints** (position, name,
  colour) and **tempomarkers** (beatgrid: start time in seconds, BPM).
- **Playlists:** `GET/POST/PATCH /playlist`, `/playlists`, `/playlist-by-path`,
  `/playlist-tracks`.
- **Custom tags:** `/tag`, `/tags`, `/tag-category`.
- **Control and now-playing:** `POST /control`, `GET /playing`, `GET /queue`.
- Read and write both exist, so a tool can pull a library out or push edits in.

**Licence limit:** the manual says the Local API needs **Essential or Ultimate**, so it
is paid
(<https://www.lexicondj.com/manual/local-api>). The pricing table lists "Local API for
developers" without a clear per-plan marker in the fetched HTML; the manual wins.
Whether the free tier can use it was **not verified** without a licence.

**Plugins:** Lexicon runs JavaScript plugins from `Documents/Lexicon/Plugins`
(<https://www.lexicondj.com/manual/plugins>). The developer page for them returned 404
under the guessed URL, so what a plugin can write to is **not verified**.

## 5. Can Sholto read from or sync with it?

| Route | Works today | Cost / catch |
|---|---|---|
| Local API (HTTP) | Yes, in principle | Paid plan, Lexicon must be running, user must enable it and approve the app |
| Direct SQLite read | Probably | Undocumented schema, location unknown, vendor discourages it |
| M3U export from Lexicon | Yes | Playlists only, no cues, beatgrids or tags |
| Rekordbox XML export | Yes (Linux-native) | Needs a Rekordbox XML reader in Sholto; XML carries cues and beatgrids |
| Sync target "Sholto" | No | Not offered, closed app, no extension point beyond plugins |

Sholto's own data (Sholto.App.Library, Sholto.App.Storage) keys tracks by file path and
holds analysis (BPM, beats, key, stems), grid adjustments, markers, tags and crates.
Everything except analysis has a Lexicon counterpart (cues, playlists, tags), and
Lexicon has no stems or per-track phrase sections.

**Smallest integration:** a one-way importer, Lexicon to Sholto, over the Local API: pair
once, `GET /tracks` with cuepoints and tempomarkers, match to Sholto tracks by file
path (`Track.Path` in Storage), then create markers (`Marker.PositionSecs`), crates
from playlists, and tags from custom tags. No write-back. A lower-effort fallback with
no licence is reading an M3U export into crates. Both fit behind `ITrackCatalog` /
`ICrateService` / `IMarkerService` without touching decks. Path differences between
Lexicon (possibly the user's Windows paths) and the Linux mount would need a mapping.

## 6. Does Lexicon replace Sholto's own library management?

No.

- Sholto's library is not a catalogue for its own sake: it is the store behind live
  features (analysis cache, grid edits, markers, search ranked against the other deck,
  tags and crates driven from the controller and keyboard). Those need to be in-process,
  instant and offline; a REST hop to a separate paid app that must be running does not
  fit a gig laptop that is "local library, offline analysis, no cloud" by design.
- Lexicon cannot hold Sholto's analysis (stems, phrase sections, grid adjustments), and
  Sholto is not a sync target.
- What Lexicon is good at (conversion between DJ apps, duplicate finding, tag and genre
  cleanup, moving/renaming files, backups) is real library hygiene that Sholto does
  not have and should not rebuild. It is a sensible companion for users who already
  have a Rekordbox/Serato/Traktor library.
- It does run on Linux, so a Sholto user can use it side by side.

## Recommendation

Do not adopt Lexicon as Sholto's library layer; keep Sholto.App.Library/Storage, and
if wanted later build a one-way Lexicon Local API (or M3U) importer for cues, playlists and tags.
