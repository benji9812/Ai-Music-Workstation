import { describe, expect, it } from "vitest";
import {
  buildSavedProjectState,
  buildStructureRequest,
  loadAudioMetadata,
  selectProjectAudioSource,
  type SongProject,
} from "./projectLoading";

const project = (overrides: Partial<SongProject> = {}): SongProject => ({
  id: "project-1",
  title: "Original song",
  artist: "Artist",
  genre: "Rock",
  dateAdded: "2026-09-16T10:00:00Z",
  bpm: 124,
  key: "D",
  timeSignature: 3,
  durationSeconds: 90,
  stemsPath: "",
  originalPath: "imports/original.mp3",
  lyrics: [{ start: 0, end: 2, text: "Hello" }],
  sections: [{ label: "Intro", start: 0, end: 10 }],
  chords: [{ time: 0, chord: "D" }],
  bpmSource: "analysis",
  keySource: "analysis",
  timeSignatureSource: "analysis",
  sectionsSource: "analysis",
  lyricsSource: "analysis",
  chordsSource: "analysis",
  groupId: null,
  group: null,
  ...overrides,
});

describe("saved project loading", () => {
  it("selects original audio when an original-only project has no stems", () => {
    expect(selectProjectAudioSource(project(), "https://api.test")).toEqual({
      kind: "original",
      url: "https://api.test/api/analysis/audio/imports/original.mp3",
    });
  });

  it("loads persisted stems without requiring an original path", () => {
    expect(
      selectProjectAudioSource(
        project({ stemsPath: "stems/song", originalPath: "" }),
        "https://api.test",
      ),
    ).toEqual({ kind: "stems", stemsPath: "stems/song" });
  });

  it("restores saved analysis, lyrics, chords, structure, and duration", () => {
    const state = buildSavedProjectState(project());

    expect(state.result).toMatchObject({
      bpm: 124,
      key: "D",
      time_signature: 3,
      original_path: "imports/original.mp3",
      lyrics: [{ start: 0, end: 2, text: "Hello" }],
      lyricsSource: "analysis",
      chords: [{ time: 0, chord: "D" }],
      chordsSource: "analysis",
    });
    expect(state.structure).toEqual({
      sections: [{ label: "Intro", start: 0, end: 10 }],
      sectionsSource: "analysis",
    });
    expect(state.fallbackDuration).toBe(90);
  });

  it("does not fabricate a fallback duration", () => {
    expect(buildSavedProjectState(project({ durationSeconds: 0 })).fallbackDuration).toBeNull();
    expect(
      buildSavedProjectState(project({ durationSeconds: Number.NaN })).fallbackDuration,
    ).toBeNull();
  });

  it("replaces the saved fallback when media reports an authoritative duration", async () => {
    const audio = new EventTarget() as HTMLAudioElement;
    Object.defineProperty(audio, "duration", { value: 0, writable: true });
    let activeDuration = buildSavedProjectState(project()).fallbackDuration;

    const loaded = loadAudioMetadata(audio, "blob:original", (duration) => {
      activeDuration = duration;
    });
    Object.defineProperty(audio, "duration", { value: 93.5, writable: true });
    audio.dispatchEvent(new Event("loadedmetadata"));

    await expect(loaded).resolves.toBe(93.5);
    expect(activeDuration).toBe(93.5);
    expect(audio.src).toBe("blob:original");
  });

  it("cancels a pending metadata wait when another project replaces it", async () => {
    const audio = new EventTarget() as HTMLAudioElement;
    Object.defineProperty(audio, "duration", { value: 0, writable: true });
    const abortController = new AbortController();

    const loaded = loadAudioMetadata(
      audio,
      "blob:old-project",
      () => {
        throw new Error("A cancelled load must not publish duration");
      },
      abortController.signal,
    );
    abortController.abort();

    await expect(loaded).resolves.toBeNull();
  });

  it("only regenerates missing structure with a positive duration", () => {
    expect(buildStructureRequest(project(), 90)).toBeNull();
    expect(buildStructureRequest(project({ sections: [] }), 0)).toBeNull();
    expect(buildStructureRequest(project({ sections: [] }), undefined)).toBeNull();
    expect(buildStructureRequest(project({ sections: [] }), 93.5)).toEqual({
      artist: "Artist",
      title: "Original song",
      duration: 93.5,
      projectId: "project-1",
      filePath: "imports/original.mp3",
    });
  });

});
