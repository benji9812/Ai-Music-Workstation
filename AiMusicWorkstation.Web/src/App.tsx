import * as React from "react";
import { PitchShift, getContext, start as toneStart } from "tone";
import { SpeedInsights } from "@vercel/speed-insights/react";
import "./index.css";
import { ChordDiagram } from "./ChordDiagram";

type LyricSegment = { start: number; end: number; text: string };
type ChordEntry = { time: number; chord: string };
type Section = { label: string; start: number; end: number };

type AnalysisSource = "spotify" | "analysis";

type AnalysisResult = {
  bpm?: number;
  bpmSource?: AnalysisSource;
  key?: string;
  keySource?: AnalysisSource;
  chords?: ChordEntry[];
  time_signature?: number;
  timeSigSource?: AnalysisSource;
  lyrics?: LyricSegment[];
  stems_path?: string;
  title?: string;
  artist?: string;
  error?: string;
};

type StructureResult = {
  sections?: Section[];
  error?: string;
};

type EditableSection = {
  id: string;
  label: string;
  start: string;
  end: string;
};

type SongProject = {
  id: string;
  title: string;
  artist: string;
  genre: string;
  bpm: number;
  key: string;
  stemsPath: string;
};

const formatTime = (sec: number) => {
  if (!Number.isFinite(sec) || sec < 0) return "00:00";
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
};

const parseTimeInput = (value: string) => {
  const match = value.trim().match(/^(\d+):([0-5]\d)$/);
  if (!match) return null;
  const minutes = Number(match[1]);
  const seconds = Number(match[2]);
  return minutes * 60 + seconds;
};

const toEditableSections = (sections: Section[]) =>
  sections.map((sec, idx) => ({
    id: `${sec.label}-${sec.start}-${sec.end}-${idx}`,
    label: sec.label,
    start: formatTime(sec.start),
    end: formatTime(sec.end),
  }));

const validateStructureEdits = (sections: EditableSection[]) => {
  const parsed = sections.map((sec, idx) => {
    const start = parseTimeInput(sec.start);
    const end = parseTimeInput(sec.end);
    return {
      idx,
      label: sec.label.trim() || `Section ${idx + 1}`,
      start,
      end,
    };
  });

  for (const sec of parsed) {
    if (sec.start === null || sec.end === null) {
      return { error: `Invalid time for "${sec.label}". Use MM:SS.` };
    }
    if (sec.start < 0 || sec.end <= sec.start) {
      return {
        error: `Section "${sec.label}" must have a valid start/end time.`,
      };
    }
  }

  const sorted = [...parsed].sort((a, b) => (a.start ?? 0) - (b.start ?? 0));
  for (let i = 1; i < sorted.length; i++) {
    if ((sorted[i].start ?? 0) < (sorted[i - 1].end ?? 0)) {
      return {
        error: `Sections overlap (${sorted[i - 1].label} / ${sorted[i].label}).`,
      };
    }
  }

  return {
    sections: parsed.map((sec) => ({
      label: sec.label,
      start: sec.start ?? 0,
      end: sec.end ?? 0,
    })),
  };
};

type SongStructurePanelProps = {
  structure: StructureResult | null;
  isStructureLoading: boolean;
  onJumpToTime: (time: number) => void;
  onSaveSections: (sections: Section[]) => Promise<void>;
};

function SongStructurePanel({
  structure,
  isStructureLoading,
  onJumpToTime,
  onSaveSections,
}: SongStructurePanelProps) {
  const [isEditing, setIsEditing] = React.useState(false);
  const [draftSections, setDraftSections] = React.useState<EditableSection[]>([]);
  const [editError, setEditError] = React.useState<string | null>(null);
  const [isSaving, setIsSaving] = React.useState(false);

  React.useEffect(() => {
    if (!isEditing) {
      setDraftSections(toEditableSections(structure?.sections ?? []));
    }
  }, [structure, isEditing]);

  const toggleEdit = () => {
    if (isEditing) {
      setIsEditing(false);
      setEditError(null);
      setDraftSections(toEditableSections(structure?.sections ?? []));
    } else {
      setIsEditing(true);
      setEditError(null);
      setDraftSections(toEditableSections(structure?.sections ?? []));
    }
  };

  const updateDraft = (index: number, patch: Partial<EditableSection>) => {
    setDraftSections((prev) =>
      prev.map((sec, idx) => (idx === index ? { ...sec, ...patch } : sec)),
    );
  };

  const handleDelete = (index: number) => {
    setDraftSections((prev) => prev.filter((_, idx) => idx !== index));
  };

  const handleAddSection = () => {
    setDraftSections((prev) => {
      const lastEnd =
        prev.length > 0 ? parseTimeInput(prev[prev.length - 1].end) ?? 0 : 0;
      const start = lastEnd;
      const end = lastEnd + 10;
      return [
        ...prev,
        {
          id: `new-${Date.now()}-${prev.length}`,
          label: `Section ${prev.length + 1}`,
          start: formatTime(start),
          end: formatTime(end),
        },
      ];
    });
  };

  const handleSave = async () => {
    setEditError(null);
    const validation = validateStructureEdits(draftSections);
    if (!validation.sections) {
      setEditError(validation.error ?? "Invalid structure data.");
      return;
    }
    setIsSaving(true);
    try {
      await onSaveSections(validation.sections);
      setIsEditing(false);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="glass-panel" style={{ flex: 1 }}>
      <div className="panel-header">
        <span className="section-title" style={{ margin: 0 }}>
          SONG STRUCTURE
        </span>
        <div className="flex items-center gap-1">
          {isEditing && (
            <button
              className="btn-primary"
              style={{ padding: "4px 10px", fontSize: "10px" }}
              onClick={handleSave}
              disabled={isSaving}
            >
              {isSaving ? "Saving…" : "Save"}
            </button>
          )}
          <button
            className="toggle-btn"
            data-active={isEditing ? "true" : "false"}
            onClick={toggleEdit}
            style={{ padding: "4px 10px", fontSize: "10px" }}
          >
            {isEditing ? "Done" : "Edit"}
          </button>
        </div>
      </div>
      <div style={{ overflowY: "auto", flex: 1, paddingRight: "5px" }}>
        {isStructureLoading ? (
          // Skeleton rows while structure is being fetched
          <div className="structure-skeleton">
            {["60%", "45%", "70%", "50%", "65%", "40%"].map((w, i) => (
              <div key={i} className="structure-skeleton-row">
                <div className="skeleton-bar" style={{ width: w }} />
                <div className="skeleton-bar" style={{ width: "28px" }} />
              </div>
            ))}
          </div>
        ) : isEditing ? (
          <>
            {draftSections.length === 0 ? (
              <div
                style={{
                  fontSize: "12px",
                  color: "#777",
                  textAlign: "center",
                  marginTop: "16px",
                }}
              >
                No sections yet
              </div>
            ) : (
              draftSections.map((sec, idx) => (
                <div
                  key={sec.id}
                  className="glass-panel-inner mb-1 flex flex-col gap-1"
                >
                  <div className="flex items-center gap-1 justify-between">
                    <input
                      className="input-dark"
                      style={{ fontSize: "12px", padding: "6px 8px" }}
                      value={sec.label}
                      onChange={(e) =>
                        updateDraft(idx, { label: e.target.value })
                      }
                      aria-label={`Section ${idx + 1} name`}
                    />
                    <button
                      className="btn-icon"
                      style={{ fontSize: "14px", color: "#ff4444" }}
                      onClick={() => handleDelete(idx)}
                      aria-label={`Delete section ${idx + 1}`}
                    >
                      🗑
                    </button>
                  </div>
                  <div className="flex items-center gap-1">
                    <input
                      className="input-dark"
                      style={{ width: "72px", fontSize: "11px", padding: "6px" }}
                      value={sec.start}
                      onChange={(e) =>
                        updateDraft(idx, { start: e.target.value })
                      }
                      placeholder="MM:SS"
                      aria-label={`Section ${idx + 1} start time`}
                    />
                    <span style={{ fontSize: "10px", color: "#777" }}>to</span>
                    <input
                      className="input-dark"
                      style={{ width: "72px", fontSize: "11px", padding: "6px" }}
                      value={sec.end}
                      onChange={(e) => updateDraft(idx, { end: e.target.value })}
                      placeholder="MM:SS"
                      aria-label={`Section ${idx + 1} end time`}
                    />
                  </div>
                </div>
              ))
            )}
            <button
              className="btn-primary mt-2"
              style={{ width: "100%" }}
              onClick={handleAddSection}
            >
              + Add Section
            </button>
            {editError && (
              <div
                style={{
                  color: "#ff6666",
                  fontSize: "11px",
                  marginTop: "6px",
                  textAlign: "center",
                }}
              >
                ⚠️ {editError}
              </div>
            )}
          </>
        ) : structure?.sections ? (
          structure.sections.map((sec: Section, idx: number) => (
            <div
              key={idx}
              className="glass-panel-inner mb-1 flex justify-between items-center cursor-pointer hover:border-cyan"
              onClick={() => onJumpToTime(parseFloat(String(sec.start)))}
            >
              <span
                style={{
                  color: "white",
                  fontWeight: "bold",
                  fontSize: "12px",
                }}
              >
                {sec.label}
              </span>
              <span style={{ color: "var(--neon-cyan)", fontSize: "10px" }}>
                {formatTime(sec.start)}
              </span>
            </div>
          ))
        ) : (
          <div
            style={{
              fontSize: "12px",
              color: "#555",
              textAlign: "center",
              marginTop: "20px",
            }}
          >
            No structure data
          </div>
        )}
      </div>
    </div>
  );
}

const API_URL =
  import.meta.env.VITE_API_URL ||
  "https://aimusicworkstation-api-e5eyc7b2bgemh5en.swedencentral-01.azurewebsites.net";

export const getActiveLyricIndex = (
  lyrics: LyricSegment[],
  current: number,
) => {
  let activeIdx = lyrics.findIndex(
    (l) => l.start <= current && l.end >= current,
  );
  if (
    activeIdx === -1 &&
    lyrics.length > 0 &&
    current > lyrics[lyrics.length - 1].end
  ) {
    activeIdx = lyrics.length - 1;
  }
  return activeIdx;
};

export const onImportSuccess = (refreshLibrary: () => void) => {
  refreshLibrary();
};

const renderSourceBadge = (source?: AnalysisSource) => {
  if (!source) return null;
  const isSpotify = source === "spotify";
  return (
    <span
      className={`source-badge ${
        isSpotify ? "source-badge--spotify" : "source-badge--analysis"
      }`}
    >
      {isSpotify ? "Spotify" : "AI"}
    </span>
  );
};

// ─── Transpose utilities ────────────────────────────────────────────────────
const ENHARMONIC_MAP: Record<string, string> = {
  Db: "C#",
  Eb: "D#",
  Gb: "F#",
  Ab: "G#",
  Bb: "A#",
  Cb: "B",
  Fb: "E",
  "E#": "F",
  "B#": "C",
};
const TRANSPOSE_SCALE = [
  "C",
  "C#",
  "D",
  "D#",
  "E",
  "F",
  "F#",
  "G",
  "G#",
  "A",
  "A#",
  "B",
];
function transposeNote(note: string, semitones: number): string {
  if (semitones === 0) return note;
  const n = ENHARMONIC_MAP[note] ?? note;
  const idx = TRANSPOSE_SCALE.indexOf(n);
  if (idx === -1) return note;
  return TRANSPOSE_SCALE[(((idx + semitones) % 12) + 12) % 12];
}
function transposeChord(chord: string, semitones: number): string {
  if (semitones === 0) return chord;
  const { root, quality } = parseChord(chord);
  return transposeNote(root, semitones) + quality;
}
function transposeKey(key: string, semitones: number): string {
  if (!key || semitones === 0) return key;
  const m = key.match(/^([A-G][#b]?)(.*)?$/);
  if (!m) return key;
  return transposeNote(m[1], semitones) + (m[2] ?? "");
}
// ────────────────────────────────────────────────────────────────────────────

// ─── Scale utilities ───────────────────────────────────────────────────────
const MAJOR_SCALE_INTERVALS = [0, 2, 4, 5, 7, 9, 11];
const MINOR_SCALE_INTERVALS = [0, 2, 3, 5, 7, 8, 10];

/**
 * Returns the 7 note names for a given root + scale type, shifted by semitoneOffset.
 * semitoneOffset is applied to the root so the scale tracks transpose.
 */
function getScale(
  root: string,
  type: "major" | "minor",
  semitoneOffset: number,
): string[] {
  const intervals =
    type === "major" ? MAJOR_SCALE_INTERVALS : MINOR_SCALE_INTERVALS;
  const normalised = ENHARMONIC_MAP[root] ?? root;
  const rootIdx = TRANSPOSE_SCALE.indexOf(normalised);
  if (rootIdx === -1) return [];
  const shiftedRoot = (((rootIdx + semitoneOffset) % 12) + 12) % 12;
  return intervals.map((iv) => TRANSPOSE_SCALE[(shiftedRoot + iv) % 12]);
}

/** Parse a key string like "Am", "F# minor", "C major", "Bb" etc. */
function parseKey(key: string): { root: string; type: "major" | "minor" } {
  if (!key) return { root: "C", type: "major" };
  const m = key.match(/^([A-G][#b]?)(.*)/);
  if (!m) return { root: "C", type: "major" };
  const root = m[1];
  const rest = m[2].toLowerCase().trim();
  // "m", "min", "minor", or starts with "m " → minor  (exclude "maj"/"major")
  const isMinor =
    (rest === "m" ||
      rest === "min" ||
      rest.startsWith("minor") ||
      rest.startsWith("m ")) &&
    !rest.startsWith("maj");
  return { root, type: isMinor ? "minor" : "major" };
}

/** Returns the TRANSPOSE_SCALE note names that make up a chord (triad/7th). */
function getChordNotes(chord: string): string[] {
  const { root, quality } = parseChord(chord);
  const normalised = ENHARMONIC_MAP[root] ?? root;
  const rootIdx = TRANSPOSE_SCALE.indexOf(normalised);
  if (rootIdx === -1) return [];
  const q = quality.toLowerCase();
  let intervals: number[];
  if (q === "" || q === "maj") {
    intervals = [0, 4, 7];
  } else if (q === "m" || q === "min") {
    intervals = [0, 3, 7];
  } else if (q === "7") {
    intervals = [0, 4, 7, 10];
  } else if (q === "maj7") {
    intervals = [0, 4, 7, 11];
  } else if (q === "m7" || q === "min7") {
    intervals = [0, 3, 7, 10];
  } else if (q === "dim" || q === "°") {
    intervals = [0, 3, 6];
  } else if (q === "aug" || q === "+") {
    intervals = [0, 4, 8];
  } else if (q === "sus2") {
    intervals = [0, 2, 7];
  } else if (q === "sus4") {
    intervals = [0, 5, 7];
  } else {
    intervals = [0, 4, 7]; // unknown → treat as major triad
  }
  return intervals.map((iv) => TRANSPOSE_SCALE[(rootIdx + iv) % 12]);
}
// ────────────────────────────────────────────────────────────────────────────

function parseChord(chord: string): { root: string; quality: string } {
  const match = chord.match(/^([A-G](?:#|b)?)(.*)?$/);
  if (!match) return { root: chord, quality: "" };
  return { root: match[1], quality: match[2] || "" };
}
// ChordDiagram is now imported from ./ChordDiagram (svguitar-based)

// Web Audio Context for Metronome beep
const audioCtx = new (
  window.AudioContext || (window as any).webkitAudioContext
)();
const playClick = () => {
  if (audioCtx.state === "suspended") audioCtx.resume();
  const osc = audioCtx.createOscillator();
  const gain = audioCtx.createGain();
  osc.connect(gain);
  gain.connect(audioCtx.destination);
  osc.frequency.value = 1000;
  gain.gain.setValueAtTime(1, audioCtx.currentTime);
  gain.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.1);
  osc.start();
  osc.stop(audioCtx.currentTime + 0.1);
};

/**
 * Schedules a single metronome click at an exact Web Audio clock time.
 * Accent beat (beat 1 of each measure) uses a higher pitch (1200 Hz).
 * All other beats use 880 Hz. Duration is 50 ms.
 */
const scheduleMetroClick = (startTime: number, isAccent: boolean) => {
  const osc = audioCtx.createOscillator();
  const gain = audioCtx.createGain();
  osc.connect(gain);
  gain.connect(audioCtx.destination);
  osc.frequency.value = isAccent ? 1200 : 880;
  gain.gain.setValueAtTime(isAccent ? 0.8 : 0.5, startTime);
  gain.gain.exponentialRampToValueAtTime(0.001, startTime + 0.05);
  osc.start(startTime);
  osc.stop(startTime + 0.05);
};

// ── WAV encoder ──────────────────────────────────────────────────────────
// Converts a rendered AudioBuffer into a 16-bit PCM WAV ArrayBuffer.
function encodeWav(buffer: AudioBuffer): ArrayBuffer {
  const numChannels = buffer.numberOfChannels;
  const sampleRate = buffer.sampleRate;
  const numFrames = buffer.length;
  const bytesPerSample = 2; // 16-bit
  const dataLength = numFrames * numChannels * bytesPerSample;
  const wavBuf = new ArrayBuffer(44 + dataLength);
  const view = new DataView(wavBuf);

  const str = (offset: number, s: string) => {
    for (let i = 0; i < s.length; i++)
      view.setUint8(offset + i, s.charCodeAt(i));
  };

  str(0, "RIFF");
  view.setUint32(4, 36 + dataLength, true);
  str(8, "WAVE");
  str(12, "fmt ");
  view.setUint32(16, 16, true); // PCM chunk size
  view.setUint16(20, 1, true); // PCM format
  view.setUint16(22, numChannels, true);
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * numChannels * bytesPerSample, true);
  view.setUint16(32, numChannels * bytesPerSample, true);
  view.setUint16(34, 16, true);
  str(36, "data");
  view.setUint32(40, dataLength, true);

  let offset = 44;
  for (let i = 0; i < numFrames; i++) {
    for (let ch = 0; ch < numChannels; ch++) {
      const sample = Math.max(-1, Math.min(1, buffer.getChannelData(ch)[i]));
      view.setInt16(
        offset,
        sample < 0 ? sample * 0x8000 : sample * 0x7fff,
        true,
      );
      offset += 2;
    }
  }
  return wavBuf;
}
// ─────────────────────────────────────────────────────────────────────────

export const __testHooks = {
  refreshLibrary: () => {},
  setStructure: (_structure: StructureResult | null) => {},
  setDuration: (_duration: number) => {},
  setDurationReady: (_ready: boolean) => {},
  setResult: (_result: AnalysisResult | null) => {},
  getCurrentTime: () => 0,
};

const { useState, useRef, useEffect } = React;

export default function App() {
  const [activeTab, setActiveTab] = useState<"chord" | "scale">("chord");

  // Audio playback states
  const [isPlaying, setIsPlaying] = useState(false);
  const [isCountingIn, setIsCountingIn] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [durationReady, setDurationReady] = useState(false);

  const [metronomeEnabled, setMetronomeEnabled] = useState(false);
  const [countInEnabled, setCountInEnabled] = useState(false);
  const [repeatEnabled, setRepeatEnabled] = useState(false);
  const [transposeEnabled, setTransposeEnabled] = useState(false);
  const [transposeSteps, setTransposeSteps] = useState(0);
  const [masterVol, setMasterVol] = useState(100);

  // Mixer states
  const [volumes, setVolumes] = useState({
    drums: 80,
    bass: 80,
    other: 80,
    vocals: 80,
  });
  const [mutes, setMutes] = useState({
    drums: false,
    bass: false,
    other: false,
    vocals: false,
  });
  const [solos, setSolos] = useState({
    drums: false,
    bass: false,
    other: false,
    vocals: false,
  });

  // Panel toggles
  const [showChords, setShowChords] = useState(true);
  const [showLyrics, setShowLyrics] = useState(true);
  const [showStructure, setShowStructure] = useState(true);
  const [showLibrary, setShowLibrary] = useState(true);

  // API Integration States
  const [result, setResult] = useState<AnalysisResult | null>(null);
  const [structure, setStructure] = useState<StructureResult | null>(null);
  const [loading, setLoading] = useState(false);
  // Tracks the async structure fetch independently so the Song Structure panel
  // can show a skeleton and the global "ready" state waits for it.
  const [isStructureLoading, setIsStructureLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [nowPlaying, setNowPlaying] = useState<{
    title: string;
    artist: string;
  } | null>(null);
  const [currentProjectId, setCurrentProjectId] = useState<string | null>(null);
  const [importProgress, setImportProgress] = useState<{
    stage: string;
    progress: number;
  } | null>(null);

  const [projects, setProjects] = useState<SongProject[]>([]);
  const [urlInput, setUrlInput] = useState("");
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const fetchLibrary = async () => {
    try {
      const resp = await fetch(`${API_URL}/api/library/projects`);
      if (resp.ok) {
        const data = await resp.json();
        const normalized = Array.isArray(data) ? data : (data.projects ?? []);
        setProjects(normalized);
      }
    } catch (e) {
      console.error("Failed to fetch library", e);
    }
  };

  useEffect(() => {
    fetchLibrary();
  }, []);

  const refreshLibrary = () => {
    fetchLibrary();
  };
  __testHooks.refreshLibrary = refreshLibrary;

  const deleteProject = async (id: string) => {
    try {
      await fetch(`${API_URL}/api/library/projects/${id}?deleteFiles=true`, {
        method: "DELETE",
      });
      refreshLibrary();
    } catch (e) {
      console.error("Failed to delete project", e);
    }
  };

  const editProject = async (project: SongProject) => {
    const title = window.prompt("Update title", project.title)?.trim();
    if (!title) return;
    const artist = window.prompt("Update artist", project.artist)?.trim();
    if (!artist) return;

    const updatedProject = { ...project, title, artist };

    try {
      const resp = await fetch(
        `${API_URL}/api/library/projects/${project.id}`,
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(updatedProject),
        },
      );
      if (!resp.ok) {
        console.error("Failed to update project", await resp.text());
        return;
      }
      refreshLibrary();
    } catch (e) {
      console.error("Failed to update project", e);
    }
  };

  const loadStemsFromPath = (stemsPath: string) => {
    const pathParts = stemsPath.split(/[\/\\]/);
    const relPath = pathParts.slice(-2).join("/");
    setDurationReady(false);
    // Register the handler BEFORE assigning src so the event is never missed,
    // even if the browser resolves metadata from cache synchronously.
    const drumsEl = stems.current.drums;
    const onMetadata = () => {
      const rawDuration = drumsEl.duration;
      if (Number.isFinite(rawDuration) && rawDuration > 0) {
        durationGuardRef.current = rawDuration;
        setDuration(rawDuration);
        setDurationReady(true);
      } else {
        // Duration not yet known (e.g. VBR/streaming) — wait for durationchange
        const onDurationChange = () => {
          const d = drumsEl.duration;
          if (Number.isFinite(d) && d > 0) {
            durationGuardRef.current = d;
            setDuration(d);
            setDurationReady(true);
            drumsEl.removeEventListener("durationchange", onDurationChange);
          }
        };
        drumsEl.addEventListener("durationchange", onDurationChange);
      }
      if (!isDragging) setSliderTime(0);
      drumsEl.removeEventListener("loadedmetadata", onMetadata);
    };
    drumsEl.addEventListener("loadedmetadata", onMetadata);
    stems.current.drums.src = `${API_URL}/api/analysis/audio/${relPath}/drums.mp3`;
    stems.current.bass.src = `${API_URL}/api/analysis/audio/${relPath}/bass.mp3`;
    stems.current.other.src = `${API_URL}/api/analysis/audio/${relPath}/other.mp3`;
    stems.current.vocals.src = `${API_URL}/api/analysis/audio/${relPath}/vocals.mp3`;
  };

  const loadProject = async (p: SongProject) => {
    if (p.stemsPath) loadStemsFromPath(p.stemsPath);
    setResult({ bpm: p.bpm, key: p.key, title: p.title, artist: p.artist });
    setNowPlaying({ title: p.title, artist: p.artist });
    setCurrentProjectId(p.id);
    setCurrentTime(0);
    setSliderTime(0);
    setIsPlaying(false);
    setStructure(null);
    setIsStructureLoading(true);
    try {
      const structResp = await fetch(`${API_URL}/api/analysis/structure`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          artist: p.artist,
          title: p.title,
          duration: 180,
        }),
      });
      const structData = await structResp.json();
      if (structResp.ok && !structData.error) setStructure(structData);
    } catch {
      /* structure is optional */
    } finally {
      setIsStructureLoading(false);
    }
  };

  const handleYoutubeImport = async () => {
    if (!urlInput) return;
    setLoading(true);
    setError(null);
    setImportProgress({ stage: "⏳ Starting import...", progress: 0 });

    try {
      // Start the async job (returns immediately with job_id)
      const startResp = await fetch(`${API_URL}/api/import/start`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ url: urlInput }),
      });
      const startData = await startResp.json();
      if (!startResp.ok || startData.status === "error") {
        setError(startData.message || "Failed to start import");
        setLoading(false);
        setImportProgress(null);
        return;
      }

      const jobId: string = startData.job_id;

      // Poll every 2 seconds
      if (pollRef.current) clearInterval(pollRef.current);
      pollRef.current = setInterval(async () => {
        try {
          const statusResp = await fetch(
            `${API_URL}/api/import/status/${jobId}`,
          );
          const statusData = await statusResp.json();

          setImportProgress({
            stage: statusData.stage || "...",
            progress: statusData.progress ?? 0,
          });

          if (statusData.status === "done" && statusData.result) {
            clearInterval(pollRef.current!);
            pollRef.current = null;
            setLoading(false);
            setImportProgress(null);

            const data: AnalysisResult = statusData.result;
            setResult(data);
            const trackTitle = data.title || "Unknown Track";
            const trackArtist = data.artist || "Unknown Artist";
            setNowPlaying({ title: trackTitle, artist: trackArtist });
            setCurrentProjectId(null);
            setUrlInput("");

            if (data.stems_path) loadStemsFromPath(data.stems_path);

            // Optimistically add the imported song to the library immediately
            // so the user sees it without waiting for the fetchLibrary round-trip.
            const optimisticProject: SongProject = {
              id: `yt-${trackTitle}-${trackArtist}`,
              title: trackTitle,
              artist: trackArtist,
              bpm: data.bpm ?? 0,
              key: data.key ?? "",
              stemsPath: data.stems_path ?? "",
              genre: "Uncategorized",
            };
            setProjects((prev) => [optimisticProject, ...prev]);

            setIsStructureLoading(true);
            try {
              const structResp = await fetch(
                `${API_URL}/api/analysis/structure`,
                {
                  method: "POST",
                  headers: { "Content-Type": "application/json" },
                  body: JSON.stringify({
                    artist: trackArtist,
                    title: trackTitle,
                    duration: 180,
                  }),
                },
              );
              const structData = await structResp.json();
              if (structResp.ok && !structData.error) setStructure(structData);
            } catch {
              /* optional */
            } finally {
              setIsStructureLoading(false);
            }

            // Sync with the real DB record (replaces the optimistic entry with
            // the persisted one that has the correct id and full metadata).
            refreshLibrary();
          } else if (statusData.status === "error") {
            clearInterval(pollRef.current!);
            pollRef.current = null;
            setError(statusData.error || "Import failed");
            setLoading(false);
            setImportProgress(null);
          }
        } catch (pollErr: any) {
          // Network glitch — keep polling
          console.warn("Poll error (will retry):", pollErr.message);
        }
      }, 2000);
    } catch (e: any) {
      setError("Network error: " + e.message);
      setLoading(false);
      setImportProgress(null);
    }
  };

  const fileInputRef = useRef<HTMLInputElement>(null);
  const lyricsScrollRef = useRef<HTMLDivElement>(null);

  // Audio elements
  const stems = useRef({
    drums: Object.assign(new Audio(), { crossOrigin: "anonymous" }),
    bass: Object.assign(new Audio(), { crossOrigin: "anonymous" }),
    other: Object.assign(new Audio(), { crossOrigin: "anonymous" }),
    vocals: Object.assign(new Audio(), { crossOrigin: "anonymous" }),
  });

  // ── Pitch-shift pipeline (Tone.js) ─────────────────────────────────────
  const pitchShiftRef = useRef<PitchShift | null>(null);
  const stemGainNodesRef = useRef<
    Partial<Record<keyof typeof stems.current, GainNode>>
  >({});
  const audioPipelineInitializedRef = useRef(false);

  const initAudioPipeline = () => {
    if (audioPipelineInitializedRef.current) return;
    audioPipelineInitializedRef.current = true; // Guard before any async yields
    try {
      const toneCtx = getContext().rawContext as AudioContext;
      const ps = new PitchShift(transposeSteps);
      ps.toDestination();
      pitchShiftRef.current = ps;
      (Object.keys(stems.current) as Array<keyof typeof stems.current>).forEach(
        (k) => {
          try {
            const source = toneCtx.createMediaElementSource(stems.current[k]);
            const gain = toneCtx.createGain();
            stemGainNodesRef.current[k] = gain;
            source.connect(gain);
            // ps.input is a Tone.Gain wrapper; .input on that gives the native GainNode
            gain.connect((ps as any).input.input as GainNode);
          } catch (e) {
            console.warn(`[PitchShift] stem "${k}" pipeline error:`, e);
          }
        },
      );
    } catch (e) {
      console.warn("[PitchShift] initAudioPipeline error:", e);
      audioPipelineInitializedRef.current = false; // Allow retry
    }
  };
  // ────────────────────────────────────────────────────────────────────────

  const isManualScrollRef = useRef<boolean>(false);
  const manualScrollTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(
    null,
  );
  const [isDragging, setIsDragging] = useState(false);
  const [sliderTime, setSliderTime] = useState(0);
  const lastLyricIndexRef = useRef<number | null>(null);
  const durationGuardRef = useRef(0);

  // ── Metronome scheduler refs ────────────────────────────────────────────
  /** Web Audio clock time of the next beat to schedule */
  const metroNextBeatTimeRef = useRef<number>(0);
  /** Which beat within the measure we're on (0 = beat 1 accent) */
  const metroBeatCountRef = useRef<number>(0);
  /** setTimeout handle for the lookahead scheduler tick */
  const metroSchedulerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // ────────────────────────────────────────────────────────────────────────
  const clampTime = (value: number, max: number) => {
    if (!Number.isFinite(value)) return 0;
    if (!Number.isFinite(max) || max <= 0) return Math.max(0, value);
    return Math.max(0, Math.min(value, max));
  };
  const readDuration = () => {
    const rawDuration = stems.current.drums.duration;
    if (Number.isFinite(rawDuration) && rawDuration > 0) {
      durationGuardRef.current = rawDuration;
      return rawDuration;
    }
    return durationGuardRef.current;
  };
  const syncStemsToTime = (time: number) => {
    const safeDuration = readDuration();
    const clamped = clampTime(time, safeDuration);
    Object.values(stems.current).forEach((a) => {
      a.currentTime = clamped;
    });
    setCurrentTime(clamped);
    setSliderTime(clamped);
    return clamped;
  };

  // Apply volumes and mutes/solos — uses GainNodes once the pipeline is live
  useEffect(() => {
    const anySolo = Object.values(solos).some((s) => s);
    (Object.keys(stems.current) as Array<keyof typeof stems.current>).forEach(
      (k) => {
        let vol = (volumes[k] / 100) * (masterVol / 100);
        if (mutes[k]) vol = 0;
        if (anySolo && !solos[k]) vol = 0;
        vol = Math.min(Math.max(vol, 0), 1);

        const gainNode = stemGainNodesRef.current[k];
        if (gainNode) {
          gainNode.gain.value = vol;
        } else {
          stems.current[k].volume = vol;
        }
      },
    );
  }, [volumes, mutes, solos, masterVol]);

  // Update pitch shifter when transposeSteps changes
  useEffect(() => {
    if (pitchShiftRef.current) {
      pitchShiftRef.current.pitch = transposeSteps;
    }
  }, [transposeSteps]);

  // ── Metronome lookahead scheduler ───────────────────────────────────────
  // Uses the Web Audio API clock (audioCtx.currentTime) for drift-free
  // timing. A short setTimeout loop schedules oscillator nodes 100 ms ahead,
  // which fully decouples click accuracy from JS timer jitter.
  // Derived state to control metronome lifecycle
  const isMetroActive = metronomeEnabled && (isPlaying || isCountingIn);

  useEffect(() => {
    // Stop any running scheduler before (re-)starting or disabling
    if (metroSchedulerRef.current !== null) {
      clearTimeout(metroSchedulerRef.current);
      metroSchedulerRef.current = null;
    }

    if (!isMetroActive) return;

    const bpm = result?.bpm ?? 120;
    const beatsPerMeasure = result?.time_signature ?? 4;
    const beatInterval = 60 / bpm; // seconds per beat

    const SCHEDULE_AHEAD_SECS = 0.1; // how far ahead to schedule
    const TICK_MS = 25; // scheduler poll interval

    // Ensure the AudioContext is running (requires a prior user gesture)
    if (audioCtx.state === "suspended") audioCtx.resume();

    // Kick off the first beat slightly in the future so the first
    // oscillator is always scheduled ahead of the current clock
    metroNextBeatTimeRef.current = audioCtx.currentTime + 0.05;
    metroBeatCountRef.current = 0;

    const tick = () => {
      // Schedule every beat that falls within the lookahead window
      while (
        metroNextBeatTimeRef.current <
        audioCtx.currentTime + SCHEDULE_AHEAD_SECS
      ) {
        const isAccent = metroBeatCountRef.current === 0;
        scheduleMetroClick(metroNextBeatTimeRef.current, isAccent);
        metroNextBeatTimeRef.current += beatInterval;
        metroBeatCountRef.current =
          (metroBeatCountRef.current + 1) % beatsPerMeasure;
      }
      // Re-schedule this tick
      metroSchedulerRef.current = setTimeout(tick, TICK_MS);
    };

    tick();

    return () => {
      if (metroSchedulerRef.current !== null) {
        clearTimeout(metroSchedulerRef.current);
        metroSchedulerRef.current = null;
      }
    };
  }, [isMetroActive, result?.bpm, result?.time_signature]);
  // ────────────────────────────────────────────────────────────────────────

  // Sync time
  useEffect(() => {
    const interval = setInterval(() => {
      if (isPlaying) {
        const current = stems.current.drums.currentTime;
        const safeDuration = readDuration();
        const safeCurrent = clampTime(current, safeDuration);
        setCurrentTime(safeCurrent);
        if (!isDragging) {
          setSliderTime(safeCurrent);
        }

        if (
          result?.lyrics &&
          !isManualScrollRef.current &&
          lyricsScrollRef.current
        ) {
          const lyrics = result.lyrics;
          let activeIdx = lyrics.findIndex(
            (l) => l.start <= current && l.end >= current,
          );
          if (
            activeIdx === -1 &&
            lyrics.length > 0 &&
            current > lyrics[lyrics.length - 1].end
          ) {
            activeIdx = lyrics.length - 1;
          }
          if (activeIdx !== -1 && lastLyricIndexRef.current !== activeIdx) {
            lastLyricIndexRef.current = activeIdx;
            const container = lyricsScrollRef.current;
            const activeElem = container.children[activeIdx] as HTMLElement;
            if (activeElem) {
              const scrollTarget =
                activeElem.offsetTop -
                container.clientHeight / 2 +
                activeElem.clientHeight / 2;
              container.scrollTo({
                top: scrollTarget,
                behavior: "smooth",
              });
            }
          }
        }
      }
    }, 100);
    return () => clearInterval(interval);
  }, [isPlaying, result, isDragging]);

  // Handle playback end (repeat and play/pause icon)
  useEffect(() => {
    const drums = stems.current.drums;
    const handleEnded = () => {
      if (repeatEnabled) {
        Object.values(stems.current).forEach((a) => {
          a.currentTime = 0;
          a.play();
        });
      } else {
        setIsPlaying(false);
      }
    };
    drums.addEventListener("ended", handleEnded);
    return () => {
      drums.removeEventListener("ended", handleEnded);
    };
  }, [repeatEnabled]);

  const handleManualScroll = (_e: React.UIEvent<HTMLDivElement>) => {
    isManualScrollRef.current = true;
    if (manualScrollTimeoutRef.current !== null) {
      clearTimeout(manualScrollTimeoutRef.current);
    }
    manualScrollTimeoutRef.current = setTimeout(() => {
      isManualScrollRef.current = false;
      manualScrollTimeoutRef.current = null;
    }, 3000);
  };

  const handlePlayPause = async () => {
    if (isPlaying) {
      Object.values(stems.current).forEach((a) => a.pause());
      setIsPlaying(false);
      return;
    }
    const targetTime = isDragging ? sliderTime : currentTime;
    if (isDragging) setIsDragging(false);
    syncStemsToTime(targetTime);
    // Resume Tone.js AudioContext (required by browser autoplay policy)
    await toneStart();
    initAudioPipeline();
    // Count-in logic
    if (countInEnabled) {
      setIsCountingIn(true);
      const bpm = result?.bpm ?? 120;
      const beatIntervalMs = (60 / bpm) * 1000;
      let count = 0;
      const interval = setInterval(() => {
        // If metronome is enabled, the scheduler handles clicks.
        // Otherwise, we play a manual click for the count-in.
        if (!metronomeEnabled) playClick();
        count++;
        if (count >= 4) {
          clearInterval(interval);
          setIsCountingIn(false);
          syncStemsToTime(targetTime);
          Object.values(stems.current).forEach((a) => a.play());
          setIsPlaying(true);
        }
      }, beatIntervalMs);
      return;
    }
    Object.values(stems.current).forEach((a) => a.play());
    setIsPlaying(true);
  };

  const skipToStart = () => {
    syncStemsToTime(0);
    if (!isPlaying) handlePlayPause();
  };

  const seekTime = (offset: number) => {
    const safeDuration = readDuration();
    const baseTime = isDragging ? sliderTime : currentTime;
    const updatedTime = clampTime(baseTime + offset, safeDuration);
    Object.values(stems.current).forEach((a) => {
      a.currentTime = updatedTime;
    });
    setCurrentTime(updatedTime);
    setSliderTime(updatedTime);
  };

  const jumpToTime = (time: number) => {
    // Ensure the value is a real number even if the API returned a string
    const parsed = parseFloat(String(time));
    if (!Number.isFinite(parsed) || parsed < 0) return;
    const safeDuration = readDuration();
    const clamped = clampTime(parsed, safeDuration);
    Object.values(stems.current).forEach((a) => {
      a.currentTime = clamped;
    });
    setCurrentTime(clamped);
    setSliderTime(clamped);
  };

  const saveStructureSections = async (sections: Section[]) => {
    setStructure({ sections });
    if (!currentProjectId) return;
    try {
      const resp = await fetch(
        `${API_URL}/api/songs/${currentProjectId}/structure`,
        {
          method: "PATCH",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ sections }),
        },
      );
      if (!resp.ok) {
        console.error("Failed to save song structure", await resp.text());
        alert("Saved locally, but failed to sync structure to the server.");
      }
    } catch (e) {
      console.error("Failed to save song structure", e);
      alert("Saved locally, but failed to sync structure to the server.");
    }
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    if (
      f &&
      (f.type === "audio/mpeg" ||
        f.name.endsWith(".mp3") ||
        f.type.includes("audio/"))
    ) {
      analyzeFile(f);
    } else {
      setError("Please select a valid Audio file.");
    }
  };

  const triggerFileInput = () => fileInputRef.current?.click();

  const analyzeFile = async (selectedFile: File) => {
    setLoading(true);
    setError(null);

    let analysisSucceeded = false;
    let savedProject: SongProject | null = null;

    try {
      const form = new FormData();
      form.append("file", selectedFile);

      // Using /analyze to actually get stems on the backend
      const resp = await fetch(`${API_URL}/api/analysis/analyze`, {
        method: "POST",
        body: form,
      });

      const data = await resp.json();
      // Treat both HTTP errors and Python-engine-level errors as failures.
      // No setLoading(false) here — the finally block handles it.
      if (!resp.ok || data.error || data.status === "error") {
        setError(data.error || data.message || data.detail || resp.statusText);
        return;
      }

      setResult(data);
      const fileName = selectedFile.name.replace(/\.[^.]+$/, "");
      setNowPlaying({ title: fileName, artist: "Local Upload" });
      setCurrentProjectId(null);
      setCurrentTime(0);
      setSliderTime(0);

      // Optimistically add the new song to the library so it appears immediately,
      // before the fetchLibrary() round-trip completes.
      savedProject = {
        id: `local-${fileName}`,
        title: fileName,
        artist: "Local Upload",
        bpm: data.bpm ?? 0,
        key: data.key ?? "",
        stemsPath: data.stems_path ?? "",
        genre: "Uncategorized",
      };
      setProjects((prev) => [savedProject!, ...prev]);
      analysisSucceeded = true;

      // Load stems from proxy if available, else use local file
      if (data.stems_path) {
        loadStemsFromPath(data.stems_path);
      } else {
        const objectUrl = URL.createObjectURL(selectedFile);
        setDurationReady(false);
        // Register the handler BEFORE assigning src to avoid missing a cached load.
        const localDrumsEl = stems.current.drums;
        const onLocalMetadata = () => {
          const rawDuration = localDrumsEl.duration;
          if (Number.isFinite(rawDuration) && rawDuration > 0) {
            durationGuardRef.current = rawDuration;
            setDuration(rawDuration);
            setDurationReady(true);
          } else {
            const onDurationChange = () => {
              const d = localDrumsEl.duration;
              if (Number.isFinite(d) && d > 0) {
                durationGuardRef.current = d;
                setDuration(d);
                setDurationReady(true);
                localDrumsEl.removeEventListener(
                  "durationchange",
                  onDurationChange,
                );
              }
            };
            localDrumsEl.addEventListener("durationchange", onDurationChange);
          }
          if (!isDragging) setSliderTime(0);
          localDrumsEl.removeEventListener("loadedmetadata", onLocalMetadata);
        };
        localDrumsEl.addEventListener("loadedmetadata", onLocalMetadata);
        stems.current.drums.src = objectUrl;
        stems.current.bass.src = objectUrl;
        stems.current.other.src = objectUrl;
        stems.current.vocals.src = objectUrl;
      }

      setIsStructureLoading(true);
      try {
        const structResp = await fetch(`${API_URL}/api/analysis/structure`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            artist: "Local Upload",
            title: fileName,
            duration: 180,
          }),
        });
        const structData = await structResp.json();
        if (structResp.ok && !structData.error) setStructure(structData);
      } catch {
        /* structure is optional */
      } finally {
        setIsStructureLoading(false);
      }
    } catch (e: any) {
      setError("Network error: " + e.message);
    } finally {
      // Always sync the library when analysis succeeded so the optimistic entry
      // is replaced with the real DB record (correct id, bpm, key, etc.).
      if (analysisSucceeded) refreshLibrary();
      setLoading(false);
    }
  };

  const formatTime = (sec: number) => {
    if (!Number.isFinite(sec) || sec < 0) return "00:00";
    const m = Math.floor(sec / 60);
    const s = Math.floor(sec % 60);
    return `${m.toString().padStart(2, "0")}:${s.toString().padStart(2, "0")}`;
  };

  const setMixerVolume = (stem: keyof typeof volumes, val: number) => {
    setVolumes((prev) => ({ ...prev, [stem]: val }));
  };

  const toggleMute = (stem: keyof typeof mutes) => {
    setMutes((prev) => ({ ...prev, [stem]: !prev[stem] }));
  };

  const toggleSolo = (stem: keyof typeof solos) => {
    setSolos((prev) => ({ ...prev, [stem]: !prev[stem] }));
  };

  // ── Export Mix ────────────────────────────────────────────────────────────
  const handleExportMix = async () => {
    if (!durationReady || duration <= 0) {
      alert("Load a song before exporting.");
      return;
    }
    if (isExporting) return;
    setIsExporting(true);
    try {
      const stemKeys = ["drums", "bass", "other", "vocals"] as const;
      const anySolo = Object.values(solos).some((s) => s);

      // Decode each stem's MP3 into an AudioBuffer via a temporary AudioContext
      const decodingCtx = new AudioContext();
      const decoded = await Promise.all(
        stemKeys.map(async (k) => {
          const src = stems.current[k].src;
          if (!src) return null;
          try {
            const resp = await fetch(src);
            if (!resp.ok) return null;
            const ab = await resp.arrayBuffer();
            return decodingCtx.decodeAudioData(ab);
          } catch {
            return null;
          }
        }),
      );
      await decodingCtx.close();

      const validBuffers = decoded.filter(Boolean) as AudioBuffer[];
      if (validBuffers.length === 0) {
        alert("No audio data available to export.");
        return;
      }

      const maxLength = Math.max(...validBuffers.map((b) => b.length));
      const sampleRate = validBuffers[0].sampleRate;

      // Render the mix offline
      const offlineCtx = new OfflineAudioContext(2, maxLength, sampleRate);

      stemKeys.forEach((k, i) => {
        const buf = decoded[i];
        if (!buf) return;

        let vol = (volumes[k] / 100) * (masterVol / 100);
        if (mutes[k]) vol = 0;
        if (anySolo && !solos[k]) vol = 0;
        vol = Math.max(0, Math.min(1, vol));

        const source = offlineCtx.createBufferSource();
        source.buffer = buf;

        const gain = offlineCtx.createGain();
        gain.gain.value = vol;

        source.connect(gain);
        gain.connect(offlineCtx.destination);
        source.start(0);
      });

      const rendered = await offlineCtx.startRendering();
      const wavArrayBuffer = encodeWav(rendered);

      const blob = new Blob([wavArrayBuffer], { type: "audio/wav" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      const trackName = nowPlaying
        ? `${nowPlaying.artist} - ${nowPlaying.title}`
        : "mix";
      a.href = url;
      a.download = `${trackName}.wav`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error("[ExportMix]", err);
      alert("Export failed. See console for details.");
    } finally {
      setIsExporting(false);
    }
  };
  // ─────────────────────────────────────────────────────────────────────────

  const updateTranspose = (delta: number) => {
    setTransposeSteps((prev) => {
      const next = Math.max(-12, Math.min(12, prev + delta));
      setTransposeEnabled(next !== 0);
      return next;
    });
  };

  // ── Scale data (derived, recomputed on every render) ──────────────────────
  const { root: _scaleRoot, type: _scaleType } = parseKey(result?.key ?? "");
  const scaleNotes = getScale(_scaleRoot, _scaleType, transposeSteps);
  // Fallback: ensure last chord remains active if analysis ends early.
  const activeChordIdx =
    result?.chords && result.chords.length > 0
      ? result.chords.reduce(
          (bestIdx, ch, idx) => (ch.time <= currentTime ? idx : bestIdx),
          0,
        )
      : -1;

  const activeChordDisplay =
    activeChordIdx !== -1
      ? transposeChord(result!.chords![activeChordIdx].chord, transposeSteps)
      : null;
  const activeChordTones: Set<string> = activeChordDisplay
    ? new Set(getChordNotes(activeChordDisplay))
    : new Set();
  const displayKey = result?.key
    ? transposeKey(result.key, transposeSteps)
    : null;
  const _scaleDegreesLabel =
    _scaleType === "major"
      ? ["I", "II", "III", "IV", "V", "VI", "VII"]
      : ["i", "ii", "♭III", "iv", "v", "♭VI", "♭VII"];
  // ─────────────────────────────────────────────────────────────────────────

  return (
    <div className="app-container">
      {/* COLUMN 1: LIBRARY */}
      <div className={`library-shell ${showLibrary ? "" : "is-collapsed"}`}>
        <button
          className="library-toggle-btn"
          onClick={() => setShowLibrary((prev) => !prev)}
          aria-label={showLibrary ? "Hide library panel" : "Show library panel"}
          title={showLibrary ? "Hide library" : "Show library"}
        >
          {showLibrary ? "◀" : "▶"}
        </button>
        <div className={`col-library ${showLibrary ? "" : "is-collapsed"}`}>
          <div className="glass-panel" style={{ flex: 1 }}>
            <div className="panel-header">
              <span className="section-title" style={{ margin: 0 }}>
                LIBRARY
              </span>
              <div className="flex gap-1">
                <button
                  className="btn-icon"
                  title={
                    nowPlaying
                      ? "Rescan orphaned stems"
                      : "No song loaded — load a song first"
                  }
                  disabled={!nowPlaying}
                  onClick={async () => {
                    if (!nowPlaying) {
                      alert(
                        "No song loaded. Please load a song before rescanning.",
                      );
                      return;
                    }
                    try {
                      const resp = await fetch(
                        `${API_URL}/api/library/rescan-stems`,
                        { method: "POST" },
                      );
                      const data = await resp.json();
                      console.log("[Rescan] raw API response:", data);

                      if (!resp.ok) {
                        alert(
                          `Rescan failed: ${data.message ?? `HTTP ${resp.status}`}`,
                        );
                        return;
                      }

                      const { added, total } = data as {
                        added: number;
                        total: number;
                      };
                      if (added > 0) fetchLibrary();
                      alert(
                        added === 0
                          ? `Rescan complete — no new stems found (${total} total in library)`
                          : `Rescan: ${added} new stem${added !== 1 ? "s" : ""} added (${total} total)`,
                      );
                    } catch (e: any) {
                      alert("Rescan failed: " + e.message);
                    }
                  }}
                >
                  🔍
                </button>
                <button
                  className="btn-icon"
                  title="Refresh library"
                  onClick={fetchLibrary}
                >
                  ⟳
                </button>
              </div>
            </div>
            <input
              type="text"
              className="input-dark mb-2"
              placeholder="Search..."
            />
            <div className="flex gap-1 mb-2">
              <select className="input-dark">
                <option>Latest</option>
                <option>A-Z</option>
              </select>
            </div>
            <div style={{ overflowY: "auto", flex: 1, paddingRight: "5px" }}>
              {projects.map((p) => (
                <div
                  key={p.id}
                  className="glass-panel-inner mb-1 flex justify-between"
                  style={{ cursor: "pointer" }}
                  onClick={() => loadProject(p)}
                >
                  <div>
                    <div
                      style={{
                        color: "white",
                        fontWeight: "bold",
                        fontSize: "13px",
                      }}
                    >
                      {p.title}
                    </div>
                    <div
                      style={{
                        color: "#aaa",
                        fontSize: "11px",
                        marginBottom: "4px",
                      }}
                    >
                      {p.artist}
                    </div>
                  </div>
                  <div className="flex flex-col gap-1">
                    <button
                      className="btn-icon"
                      style={{ fontSize: "10px" }}
                      onClick={(e) => {
                        e.stopPropagation();
                        editProject(p);
                      }}
                    >
                      ✏️
                    </button>
                    <button
                      className="btn-icon"
                      style={{ fontSize: "10px", color: "#ff4444" }}
                      onClick={(e) => {
                        e.stopPropagation();
                        deleteProject(p.id);
                      }}
                    >
                      ❌
                    </button>
                  </div>
                </div>
              ))}
              {projects.length === 0 && (
                <div
                  style={{
                    color: "#aaa",
                    fontSize: "11px",
                    textAlign: "center",
                    marginTop: "10px",
                  }}
                >
                  Library is empty
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* COLUMN 2: CHORDS & SCALES */}
      {showChords && (
        <div className="col-chords">
          <div className="glass-panel" style={{ flex: 1 }}>
            <div className="flex gap-2 mb-2">
              <button
                className={`btn-primary w-full ${activeTab === "chord" ? "active" : ""}`}
                onClick={() => setActiveTab("chord")}
              >
                Chord
              </button>
              <button
                className={`btn-primary w-full ${activeTab === "scale" ? "active" : ""}`}
                onClick={() => setActiveTab("scale")}
              >
                Scale
              </button>
            </div>

            {activeTab === "chord" && (
              <div
                className="flex flex-col items-center justify-center"
                style={{ flex: 1, gap: 12 }}
              >
                {/* Rolling Chord Queue */}
                <div
                  className="glass-panel-inner w-full flex items-center justify-center gap-4"
                  style={{ minHeight: "60px", overflow: "hidden" }}
                >
                  {result?.chords && activeChordIdx !== -1 ? (
                    result.chords
                      .slice(activeChordIdx, activeChordIdx + 5)
                      .map((ch, i) => {
                        const isActive = i === 0;
                        return (
                          <div
                            key={i}
                            style={{
                              display: "flex",
                              flexDirection: "column",
                              alignItems: "center",
                              opacity: isActive ? 1 : 0.4,
                              transform: isActive ? "scale(1.2)" : "scale(1)",
                              transition: "all 0.3s ease",
                            }}
                          >
                            <span
                              style={{
                                fontSize: isActive ? "24px" : "16px",
                                fontWeight: "bold",
                                color: isActive ? "var(--neon-cyan)" : "white",
                                textDecoration: isActive ? "underline" : "none",
                                textUnderlineOffset: "4px",
                              }}
                            >
                              {transposeChord(ch.chord, transposeSteps)}
                            </span>
                          </div>
                        );
                      })
                  ) : (
                    <div style={{ fontSize: "13px", color: "#555" }}>
                      {result?.chords
                        ? "Waiting for playback..."
                        : "Waiting for analysis..."}
                    </div>
                  )}
                </div>

                {/* Active chord diagram */}
                {activeChordIdx !== -1 ? (
                  <ChordDiagram
                    chord={transposeChord(
                      result!.chords![activeChordIdx].chord,
                      transposeSteps,
                    )}
                  />
                ) : (
                  <div
                    style={{
                      fontSize: 40,
                      fontWeight: "bold",
                      color: "var(--neon-yellow)",
                    }}
                  >
                    {result?.key ?? "—"}
                  </div>
                )}
              </div>
            )}

            {activeTab === "scale" && (
              <div className="flex flex-col" style={{ flex: 1, gap: 8 }}>
                {/* Scale name header */}
                <div
                  className="glass-panel-inner text-center"
                  style={{ padding: "8px 10px" }}
                >
                  <div
                    style={{
                      fontSize: "10px",
                      color: "#888",
                      letterSpacing: "2px",
                      marginBottom: 4,
                    }}
                  >
                    SCALE
                  </div>
                  {displayKey ? (
                    <div
                      style={{
                        fontSize: "20px",
                        fontWeight: "bold",
                        color: "var(--neon-cyan)",
                        letterSpacing: "1px",
                      }}
                    >
                      {displayKey}
                    </div>
                  ) : (
                    <div style={{ fontSize: "13px", color: "#555" }}>
                      Waiting for analysis…
                    </div>
                  )}
                  {displayKey && (
                    <div
                      style={{
                        fontSize: "10px",
                        color: "#666",
                        marginTop: 2,
                      }}
                    >
                      {_scaleType === "major" ? "Major" : "Natural Minor"}
                    </div>
                  )}
                </div>

                {/* Note chips */}
                {scaleNotes.length > 0 ? (
                  <div
                    style={{
                      display: "grid",
                      gridTemplateColumns: "repeat(7, 1fr)",
                      gap: 4,
                    }}
                  >
                    {scaleNotes.map((note, i) => {
                      const isChordTone = activeChordTones.has(note);
                      return (
                        <div
                          key={i}
                          style={{
                            display: "flex",
                            flexDirection: "column",
                            alignItems: "center",
                            gap: 2,
                            background: isChordTone
                              ? "rgba(0,240,255,0.16)"
                              : "rgba(255,255,255,0.04)",
                            border: `1px solid ${
                              isChordTone
                                ? "var(--neon-cyan)"
                                : "rgba(255,255,255,0.09)"
                            }`,
                            borderRadius: 6,
                            padding: "7px 2px 5px",
                            transition: "all 0.2s ease",
                            boxShadow: isChordTone
                              ? "0 0 8px rgba(0,240,255,0.3)"
                              : "none",
                          }}
                        >
                          <span
                            style={{
                              fontSize: "12px",
                              fontWeight: "bold",
                              color: isChordTone ? "var(--neon-cyan)" : "#ccc",
                              lineHeight: 1,
                            }}
                          >
                            {note}
                          </span>
                          <span
                            style={{
                              fontSize: "9px",
                              color: isChordTone
                                ? "rgba(0,240,255,0.6)"
                                : "#444",
                              fontStyle: "italic",
                              lineHeight: 1,
                            }}
                          >
                            {_scaleDegreesLabel[i]}
                          </span>
                        </div>
                      );
                    })}
                  </div>
                ) : (
                  <div
                    style={{
                      fontSize: "13px",
                      color: "#555",
                      textAlign: "center",
                    }}
                  >
                    {result?.key ? "Unknown scale" : "Waiting for analysis…"}
                  </div>
                )}

                {/* Active chord tones */}
                {activeChordDisplay && (
                  <div
                    className="glass-panel-inner text-center"
                    style={{ padding: "6px 8px" }}
                  >
                    <div
                      style={{
                        fontSize: "10px",
                        color: "#888",
                        letterSpacing: "1px",
                        marginBottom: 3,
                      }}
                    >
                      ACTIVE CHORD
                    </div>
                    <div
                      style={{
                        fontSize: "18px",
                        fontWeight: "bold",
                        color: "var(--neon-yellow)",
                      }}
                    >
                      {activeChordDisplay}
                    </div>
                    <div
                      style={{
                        fontSize: "11px",
                        color: "var(--neon-cyan)",
                        marginTop: 3,
                        letterSpacing: "1px",
                      }}
                    >
                      {[...activeChordTones].join(" · ")}
                    </div>
                  </div>
                )}

                {/* Footer */}
                {transposeSteps !== 0 && (
                  <div
                    style={{
                      fontSize: "10px",
                      color: "#555",
                      textAlign: "center",
                    }}
                  >
                    Transposed {transposeSteps > 0 ? "+" : ""}
                    {transposeSteps} semitone
                    {Math.abs(transposeSteps) !== 1 ? "s" : ""}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* COLUMN 3: MAIN WORKSTATION (Now strictly matches Desktop layout) */}
      <div className="col-main">
        {/* ── Main header row ── */}
        <div
          className="flex justify-between items-center"
          style={{ padding: "0 10px" }}
        >
          <h1 className="neon-text-gradient m-0" style={{ fontSize: "24px" }}>
            AI MUSIC WORKSTATION
          </h1>
          <div className="flex items-center gap-2">
            <span
              style={{ fontSize: "10px", fontWeight: "bold", color: "#888" }}
            >
              MASTER VOL
            </span>
            <input
              type="range"
              min="0"
              max="100"
              value={masterVol}
              onChange={(e) => setMasterVol(Number(e.target.value))}
              style={{ width: "80px" }}
            />
          </div>
        </div>

        {/* ── Panel toggle toolbar (dedicated row below header) ── */}
        <div className="panel-toggle-toolbar">
          <button
            className="toggle-btn panel-toggle-btn"
            data-active={showChords ? "true" : "false"}
            onClick={() => setShowChords(!showChords)}
          >
            🎹 Chords / Scale
          </button>
          <button
            className="toggle-btn panel-toggle-btn"
            data-active={showLyrics ? "true" : "false"}
            onClick={() => setShowLyrics(!showLyrics)}
          >
            🎤 Lyrics
          </button>
          <button
            className="toggle-btn panel-toggle-btn"
            data-active={showStructure ? "true" : "false"}
            onClick={() => setShowStructure(!showStructure)}
          >
            📑 Song Structure
          </button>
        </div>

        {error && (
          <div
            style={{
              background: "rgba(255,0,0,0.1)",
              color: "#ff4444",
              padding: "10px",
              margin: "10px 0",
            }}
          >
            ⚠️ {error}
          </div>
        )}

        {/* Import Section — collapses to Now Playing bar when a song is loaded */}
        {nowPlaying && !loading && !isStructureLoading ? (
          <div
            className="glass-panel"
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              padding: "8px 14px",
            }}
          >
            <div>
              <div
                style={{
                  color: "var(--neon-cyan)",
                  fontSize: 11,
                  fontWeight: "bold",
                  letterSpacing: 1,
                }}
              >
                NOW PLAYING
              </div>
              <div style={{ color: "#fff", fontWeight: "bold", fontSize: 14 }}>
                {nowPlaying.title}
              </div>
              <div style={{ color: "#aaa", fontSize: 11 }}>
                {nowPlaying.artist}
              </div>
            </div>
            <button
              className="btn-icon"
              style={{ fontSize: 11, color: "#aaa", whiteSpace: "nowrap" }}
              onClick={() => {
                setNowPlaying(null);
                setCurrentProjectId(null);
                setResult(null);
                setStructure(null);
                setUrlInput("");
              }}
            >
              ✕ Import new
            </button>
          </div>
        ) : (
          <div className="glass-panel">
            {importProgress ? (
              /* Live progress bar while importing */
              <div style={{ padding: "8px 0" }}>
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    marginBottom: 6,
                  }}
                >
                  <span
                    style={{
                      fontSize: 12,
                      color: "var(--neon-cyan)",
                      fontWeight: "bold",
                    }}
                  >
                    {importProgress.stage}
                  </span>
                  <span style={{ fontSize: 12, color: "#aaa" }}>
                    {importProgress.progress}%
                  </span>
                </div>
                <div
                  style={{
                    background: "rgba(255,255,255,0.08)",
                    borderRadius: 4,
                    height: 8,
                    overflow: "hidden",
                  }}
                >
                  <div
                    style={{
                      height: "100%",
                      width: `${importProgress.progress}%`,
                      background:
                        "linear-gradient(90deg, var(--neon-cyan), var(--neon-magenta))",
                      borderRadius: 4,
                      transition: "width 0.6s ease",
                    }}
                  />
                </div>
                <div
                  style={{
                    fontSize: 10,
                    color: "#555",
                    marginTop: 4,
                    textAlign: "center",
                  }}
                >
                  This may take 4-6 minutes (Demucs stem separation)
                </div>
              </div>
            ) : (
              <>
                <div className="flex gap-2 mb-2">
                  <input
                    type="text"
                    className="input-dark"
                    placeholder="Paste YouTube or Spotify Link here..."
                    value={urlInput}
                    onChange={(e) => setUrlInput(e.target.value)}
                    onKeyDown={(e) =>
                      e.key === "Enter" && handleYoutubeImport()
                    }
                  />
                  <button
                    className="btn-accent"
                    style={{
                      background: "rgba(255,0,0,0.1)",
                      color: "#ff4444",
                      borderColor: "#ff4444",
                    }}
                    onClick={handleYoutubeImport}
                    disabled={loading}
                  >
                    ⬇ DOWNLOAD
                  </button>
                </div>
                <input
                  type="file"
                  accept="audio/*"
                  ref={fileInputRef}
                  onChange={handleFileSelect}
                  style={{ display: "none" }}
                />
                <button
                  className="btn-primary"
                  style={{ padding: "12px", width: "100%" }}
                  onClick={triggerFileInput}
                  disabled={loading}
                >
                  {loading
                    ? "⚡ ANALYZING WITH AI..."
                    : "📂 OPEN LOCAL AUDIO FILE"}
                </button>
              </>
            )}
          </div>
        )}

        {/* Analysis Data (Tempo, Key, Transpose, Metronome) */}
        <div className="flex gap-2">
          <div
            className="glass-panel flex-col items-center justify-center"
            style={{ flex: 1 }}
          >
            <span className="section-title">TEMPO</span>
            <div className="analysis-value-row">
              <span
                className="highlight-cyan"
                style={{ fontSize: "40px", fontWeight: "bold" }}
              >
                {result?.bpm ? Math.round(result.bpm) : "—"}
              </span>
              {renderSourceBadge(result?.bpmSource)}
            </div>
          </div>
          <div
            className="glass-panel flex-col items-center justify-center"
            style={{ flex: 1 }}
          >
            <span className="section-title">TIME SIG</span>
            <div className="analysis-value-row">
              <span
                className="highlight-cyan"
                style={{ fontSize: "40px", fontWeight: "bold" }}
              >
                {result?.time_signature ? `${result.time_signature}/4` : "—"}
              </span>
              {renderSourceBadge(result?.timeSigSource)}
            </div>
          </div>
          <div
            className="glass-panel flex-col items-center justify-center"
            style={{ flex: 1 }}
          >
            <span className="section-title">KEY</span>
            <div className="analysis-value-row">
              <span
                className="highlight-cyan"
                style={{ fontSize: "40px", fontWeight: "bold" }}
              >
                {result?.key ? transposeKey(result.key, transposeSteps) : "—"}
              </span>
              {renderSourceBadge(result?.keySource)}
            </div>
          </div>
        </div>

        {/* Transpose & Transport Controls */}
        <div className="glass-panel flex flex-col items-center gap-3">
          {/* Transpose row */}
          <div className="flex items-center gap-2">
            <span
              style={{ fontSize: "10px", fontWeight: "bold", color: "#888" }}
            >
              TRANSPOSE
            </span>
            <button
              className={`btn-icon ${transposeEnabled ? "active" : ""}`}
              onClick={() => updateTranspose(-1)}
              data-testid="transpose-toggle-minus"
            >
              −
            </button>
            <span
              style={{
                fontSize: "16px",
                fontWeight: "bold",
                color: "var(--neon-cyan)",
                minWidth: "28px",
                textAlign: "center",
              }}
            >
              {transposeSteps}
            </span>
            <button
              className={`btn-icon ${transposeEnabled ? "active" : ""}`}
              onClick={() => updateTranspose(1)}
              data-testid="transpose-toggle-plus"
            >
              +
            </button>
          </div>
          {/* Count In / Metro / Repeat — symmetrically centred below transpose */}
          <div className="flex items-center gap-3">
            <button
              className="toggle-btn transport-toggle-btn"
              data-active={countInEnabled ? "true" : "false"}
              onClick={() => setCountInEnabled(!countInEnabled)}
            >
              ⏱ Count In
            </button>
            <button
              className="toggle-btn transport-toggle-btn"
              data-active={metronomeEnabled ? "true" : "false"}
              onClick={() => setMetronomeEnabled(!metronomeEnabled)}
              data-testid="metro-toggle"
            >
              🥁 Metro
            </button>
          </div>
        </div>

        {/* LYRICS PANEL IN THE MIDDLE */}
        {showLyrics && (
          <div
            className="glass-panel"
            style={{ flex: 1, display: "flex", flexDirection: "column" }}
          >
            <div className="panel-header">
              <span className="section-title" style={{ margin: 0 }}>
                LYRICS
              </span>
            </div>
            <div
              ref={lyricsScrollRef}
              onScroll={handleManualScroll}
              className="lyrics-container"
              style={{ flex: 1, paddingRight: "5px" }}
            >
              {result?.lyrics ? (
                result.lyrics.map((seg, idx) => (
                  <p
                    key={idx}
                    style={{
                      color:
                        currentTime >= seg.start && currentTime <= seg.end
                          ? "#fff"
                          : "#555",
                      margin: "5px 0",
                      fontSize: "14px",
                      fontWeight:
                        currentTime >= seg.start && currentTime <= seg.end
                          ? "bold"
                          : "normal",
                      transition: "color 0.2s",
                    }}
                  >
                    <span
                      style={{
                        color: "#444",
                        fontSize: "10px",
                        marginRight: "5px",
                      }}
                    >
                      [{seg.start.toFixed(1)}s]
                    </span>
                    {seg.text}
                  </p>
                ))
              ) : (
                <div
                  style={{
                    fontSize: "12px",
                    color: "#555",
                    textAlign: "center",
                    marginTop: "20px",
                  }}
                >
                  {loading
                    ? "Transcribing lyrics with Whisper..."
                    : "No lyrics data"}
                </div>
              )}
            </div>
          </div>
        )}

        {/* Mixer */}
        <div className="glass-panel" style={{ paddingBottom: "4px" }}>
          <div className="panel-header">
            <span
              className="section-title"
              style={{ margin: 0, width: "100%", textAlign: "center" }}
            >
              STEM MIXER
            </span>
          </div>
          <div className="mixer-grid">
            {(["drums", "bass", "other", "vocals"] as const).map((stem) => (
              <div key={stem} className="mixer-channel">
                <span
                  style={{
                    fontSize: "11px",
                    fontWeight: "bold",
                    color: "#888",
                  }}
                >
                  {stem.toUpperCase()}
                </span>
                <div className="vertical-slider-container">
                  <input
                    type="range"
                    className="vertical-slider"
                    min="0"
                    max="100"
                    value={volumes[stem]}
                    onChange={(e) =>
                      setMixerVolume(stem, Number(e.target.value))
                    }
                  />
                </div>
                <div className="flex gap-1">
                  <button
                    className={`toggle-btn toggle-mute ${mutes[stem] ? "active" : ""}`}
                    onClick={() => toggleMute(stem)}
                  >
                    M
                  </button>
                  <button
                    className={`toggle-btn toggle-solo ${solos[stem] ? "active" : ""}`}
                    onClick={() => toggleSolo(stem)}
                  >
                    S
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Transport */}
        <div className="glass-panel" style={{ marginTop: "auto" }}>
          <div className="flex items-center gap-2 mb-2">
            <span style={{ fontSize: "12px", color: "#888", width: "40px" }}>
              {formatTime(currentTime)}
            </span>
            <input
              type="range"
              className="timeline-slider"
              data-testid="timeline-slider"
              min="0"
              max={durationReady ? duration : 0}
              value={isDragging ? sliderTime : currentTime}
              onMouseDown={() => setIsDragging(true)}
              onMouseUp={() => setIsDragging(false)}
              onMouseLeave={() => setIsDragging(false)}
              onChange={(e) => {
                const newTime = Number(e.target.value);
                setSliderTime(newTime);
                jumpToTime(newTime);
              }}
              onTouchStart={() => setIsDragging(true)}
              onTouchEnd={() => setIsDragging(false)}
            />
            <span
              style={{
                fontSize: "12px",
                color: "#888",
                width: "40px",
                textAlign: "right",
              }}
            >
              {durationReady ? formatTime(duration) : "--:--"}
            </span>
          </div>
          <div className="flex justify-center items-center gap-2">
            <button
              className={`btn-icon${repeatEnabled ? " active" : ""}`}
              style={{ fontSize: "20px" }}
              title="Loop / Repeat"
              onClick={() => setRepeatEnabled(!repeatEnabled)}
            >
              🔁
            </button>
            <button
              className="btn-icon"
              style={{ fontSize: "24px" }}
              onClick={() => seekTime(-10)}
            >
              «
            </button>
            <button
              className="btn-icon"
              style={{ fontSize: "16px", color: "#ff4444" }}
              onClick={skipToStart}
            >
              ⏮
            </button>
            <button
              className="btn-accent"
              style={{
                width: "60px",
                height: "60px",
                borderRadius: "50%",
                fontSize: "24px",
                padding: 0,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
              }}
              onClick={handlePlayPause}
            >
              {isPlaying ? "⏸" : "▶"}
            </button>
            <button
              className="btn-icon"
              style={{ fontSize: "24px" }}
              onClick={() => seekTime(10)}
            >
              »
            </button>
            <button
              className="btn-primary ml-4"
              onClick={handleExportMix}
              disabled={isExporting || !durationReady}
              title={
                !durationReady ? "Load a song first" : "Export stems as WAV mix"
              }
              style={{
                opacity: isExporting || !durationReady ? 0.6 : 1,
                cursor: isExporting
                  ? "wait"
                  : !durationReady
                    ? "not-allowed"
                    : "pointer",
                minWidth: "120px",
              }}
            >
              {isExporting ? "⏳ Exporting…" : "💾 EXPORT MIX"}
            </button>
          </div>
        </div>
      </div>

      {/* COLUMN 4: SECTIONS */}
      {showStructure && (
        <div className="col-sections">
          <SongStructurePanel
            structure={structure}
            isStructureLoading={isStructureLoading}
            onJumpToTime={jumpToTime}
            onSaveSections={saveStructureSections}
          />
        </div>
      )}
      <SpeedInsights />
    </div>
  );
}
