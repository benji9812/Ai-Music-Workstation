export type AnalysisSource = "spotify" | "analysis";

export type SavedLyric = { start: number; end: number; text: string };
export type SavedSection = { label: string; start: number; end: number };
export type SavedChord = { time: number; chord: string };

export type SavedSongGroup = {
  id: string;
  name: string;
  createdAt: string;
};

export type SongProject = {
  id: string;
  title: string;
  artist: string;
  genre: string;
  dateAdded: string;
  bpm: number;
  key: string;
  timeSignature: number;
  durationSeconds: number;
  stemsPath: string;
  originalPath: string;
  lyrics: SavedLyric[];
  sections: SavedSection[];
  chords: SavedChord[];
  bpmSource: AnalysisSource | null;
  keySource: AnalysisSource | null;
  timeSignatureSource: AnalysisSource | null;
  sectionsSource: AnalysisSource | null;
  lyricsSource: AnalysisSource | null;
  chordsSource: AnalysisSource | null;
  groupId: string | null;
  group: SavedSongGroup | null;
};

export type ProjectAudioSource =
  | { kind: "stems"; stemsPath: string }
  | { kind: "original"; url: string }
  | { kind: "none" };

export const isValidDuration = (
  duration: number | undefined | null,
): duration is number =>
  typeof duration === "number" && Number.isFinite(duration) && duration > 0;

export const selectProjectAudioSource = (
  project: SongProject,
  apiUrl: string,
): ProjectAudioSource => {
  if (project.stemsPath) {
    return { kind: "stems", stemsPath: project.stemsPath };
  }
  if (project.originalPath) {
    return {
      kind: "original",
      url: `${apiUrl}/api/analysis/audio/${project.originalPath}`,
    };
  }
  return { kind: "none" };
};

export const buildSavedProjectState = (project: SongProject) => ({
  result: {
    title: project.title,
    artist: project.artist,
    bpm: project.bpm,
    bpmSource: project.bpmSource ?? undefined,
    key: project.key,
    keySource: project.keySource ?? undefined,
    time_signature: project.timeSignature,
    timeSigSource: project.timeSignatureSource ?? undefined,
    lyrics: project.lyrics,
    lyricsSource: project.lyricsSource ?? undefined,
    chords: project.chords,
    chordsSource: project.chordsSource ?? undefined,
    stems_path: project.stemsPath || undefined,
    original_path: project.originalPath || undefined,
  },
  structure:
    project.sections.length > 0
      ? {
          sections: project.sections,
          sectionsSource: project.sectionsSource ?? undefined,
        }
      : null,
  fallbackDuration: isValidDuration(project.durationSeconds)
    ? project.durationSeconds
    : null,
});

export const buildStructureRequest = (
  project: SongProject,
  duration: number | undefined | null,
) => {
  if (project.sections.length > 0 || !isValidDuration(duration)) {
    return null;
  }
  return {
    artist: project.artist,
    title: project.title,
    duration,
    projectId: project.id,
    filePath: project.originalPath || null,
  };
};

export const loadAudioMetadata = (
  audio: HTMLAudioElement,
  sourceUrl: string,
  onDuration: (duration: number) => void,
  signal?: AbortSignal,
): Promise<number | null> =>
  new Promise((resolve) => {
    let settled = false;

    const cleanup = () => {
      audio.removeEventListener("loadedmetadata", handleDuration);
      audio.removeEventListener("durationchange", handleDuration);
      audio.removeEventListener("error", handleError);
      signal?.removeEventListener("abort", handleAbort);
    };
    const finish = (duration: number | null) => {
      if (settled) return;
      settled = true;
      cleanup();
      resolve(duration);
    };
    const handleDuration = () => {
      if (isValidDuration(audio.duration)) {
        onDuration(audio.duration);
        finish(audio.duration);
      }
    };
    const handleError = () => finish(null);
    const handleAbort = () => finish(null);

    audio.addEventListener("loadedmetadata", handleDuration);
    audio.addEventListener("durationchange", handleDuration);
    audio.addEventListener("error", handleError);
    if (signal?.aborted) {
      finish(null);
      return;
    }
    signal?.addEventListener("abort", handleAbort, { once: true });
    audio.src = sourceUrl;
  });
