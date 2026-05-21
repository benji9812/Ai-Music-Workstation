import { useEffect, useRef } from "react";
import { SVGuitarChord, SILENT, OPEN, type Chord, type ChordSettings } from "svguitar";

// ─────────────────────────────────────────────────────────────────────────────
// Fret dictionary – ported 1-to-1 from ChordDiagramRenderer.cs
//
// Array layout: [string6(low E), string5(A), string4(D), string3(G), string2(B), string1(high e)]
//   -1 = muted (X)   0 = open (O)   N = fret number
// ─────────────────────────────────────────────────────────────────────────────
const CHORD_FRETS: Record<string, number[]> = {
  // ── Major ──────────────────────────────────────────────────────────────
  C:   [-1, 3, 2, 0, 1, 0],
  "C#":[-1, 4, 3, 1, 2, 1],
  D:   [-1,-1, 0, 2, 3, 2],
  "D#":[-1,-1, 1, 3, 4, 3],
  E:   [ 0, 2, 2, 1, 0, 0],
  F:   [ 1, 1, 2, 3, 3, 1],
  "F#":[ 2, 4, 4, 3, 2, 2],
  G:   [ 3, 2, 0, 0, 0, 3],
  "G#":[ 4, 6, 6, 5, 4, 4],
  A:   [-1, 0, 2, 2, 2, 0],
  "A#":[-1, 1, 3, 3, 3, 1],
  Bb:  [-1, 1, 3, 3, 3, 1],
  B:   [-1, 2, 4, 4, 4, 2],

  // ── Minor ──────────────────────────────────────────────────────────────
  Cm:  [-1, 3, 5, 5, 4, 3],
  "C#m":[-1,4, 6, 6, 5, 4],
  Dm:  [-1,-1, 0, 2, 3, 1],
  "D#m":[-1, 6, 8, 8, 7, 6],
  Em:  [ 0, 2, 2, 0, 0, 0],
  Fm:  [ 1, 3, 3, 1, 1, 1],
  "F#m":[ 2, 4, 4, 2, 2, 2],
  Gm:  [ 3, 5, 5, 3, 3, 3],
  "G#m":[ 4, 6, 6, 4, 4, 4],
  Am:  [-1, 0, 2, 2, 1, 0],
  "A#m":[-1, 1, 3, 3, 2, 1],
  Bbm: [-1, 1, 3, 3, 2, 1],
  Bm:  [-1, 2, 4, 4, 3, 2],

  // ── Dominant 7th ───────────────────────────────────────────────────────
  C7:  [-1, 3, 2, 3, 1, 0],
  D7:  [-1,-1, 0, 2, 1, 2],
  E7:  [ 0, 2, 0, 1, 0, 0],
  F7:  [ 1, 1, 2, 1, 3, 1],
  G7:  [ 3, 2, 0, 0, 0, 1],
  A7:  [-1, 0, 2, 0, 2, 0],
  B7:  [-1, 2, 1, 2, 0, 2],

  // ── Minor 7th ──────────────────────────────────────────────────────────
  Am7: [-1, 0, 2, 0, 1, 0],
  Em7: [ 0, 2, 2, 0, 3, 0],
  Dm7: [-1,-1, 0, 2, 1, 1],
  Bm7: [-1, 2, 4, 2, 3, 2],
};

// ─────────────────────────────────────────────────────────────────────────────
// Adapter: resolve a chord name → fret array (with C# fallback logic)
// ─────────────────────────────────────────────────────────────────────────────
function resolveChordFrets(chordName: string): number[] | null {
  const chord = chordName.trim();

  // Parse root (may be 2 chars if sharp/flat)
  let root: string, suffix: string;
  if (chord.length >= 2 && (chord[1] === "#" || chord[1] === "b")) {
    root = chord.slice(0, 2);
    suffix = chord.slice(2);
  } else {
    root = chord.slice(0, 1);
    suffix = chord.slice(1);
  }

  // Detect minor quality (suffix starts with 'm' but NOT 'maj')
  const isMinor =
    suffix.length > 0 &&
    suffix[0] === "m" &&
    !suffix.toLowerCase().startsWith("maj");
  const minorKey = root + (isMinor ? "m" : "");
  const majorKey = root;

  // Priority: exact → root+minor → root only
  if (CHORD_FRETS[chord]) return CHORD_FRETS[chord];
  if (CHORD_FRETS[minorKey]) return CHORD_FRETS[minorKey];
  if (CHORD_FRETS[majorKey]) return CHORD_FRETS[majorKey];
  return null;
}

// ─────────────────────────────────────────────────────────────────────────────
// Adapter: convert desktop fret array → svguitar Chord object
//
// svguitar string numbering: 1 = high e (thin), 6 = low E (thick)
// desktop array index 0 = low E (string 6), index 5 = high e (string 1)
//   → svguitarString = 6 - desktopIndex
//
// svguitar fret positions are RELATIVE to the displayed window.
//   If position = 4, finger at relative fret 1 sounds on guitar fret 4.
// ─────────────────────────────────────────────────────────────────────────────
function toSvguitarChord(frets: number[], title: string): Chord {
  const nonZeroFrets = frets.filter((f) => f > 0);
  const minFret = nonZeroFrets.length > 0 ? Math.min(...nonZeroFrets) : 1;
  // Only shift the window when the chord starts above fret 1
  const position = minFret > 1 ? minFret : 1;

  const fingers: Chord["fingers"] = [];
  for (let i = 0; i < 6; i++) {
    const fret = frets[i];
    const stringNum = 6 - i; // map desktop index → svguitar string number
    if (fret === -1) {
      fingers.push([stringNum, SILENT]);
    } else if (fret === 0) {
      fingers.push([stringNum, OPEN]);
    } else {
      // Convert absolute fret to relative position within displayed window
      fingers.push([stringNum, fret - position + 1]);
    }
  }

  return { fingers, barres: [], position, title };
}

// ─────────────────────────────────────────────────────────────────────────────
// svguitar display settings – dark neon theme matching the web app
// ─────────────────────────────────────────────────────────────────────────────
const SVGUITAR_SETTINGS: ChordSettings = {
  strings: 6,
  frets: 5,
  backgroundColor: "transparent",
  // Global fallback colour (open circles, muted Xs, fret label)
  color: "#888888",
  // Individual overrides
  titleColor: "#ffdd00",   // --neon-yellow
  stringColor: "#505050",
  fretColor: "#454545",
  fingerColor: "#007acc",  // matches C# Color.FromRgb(0, 122, 204)
  fingerTextColor: "#ffffff",
  fretLabelColor: "#888888",
  // Geometry
  nutWidth: 6,
  strokeWidth: 1.2,
  fretSize: 1.5,
  fingerSize: 0.85,
  sidePadding: 0.18,
  emptyStringIndicatorSize: 0.6,
  // Typography
  titleFontSize: 48,
  titleBottomMargin: 10,
  fontFamily: "'Segoe UI', Roboto, Arial, sans-serif",
};

// ─────────────────────────────────────────────────────────────────────────────
// React component
// ─────────────────────────────────────────────────────────────────────────────
interface ChordDiagramProps {
  chord: string;
}

export function ChordDiagram({ chord }: ChordDiagramProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const svguitarRef = useRef<SVGuitarChord | null>(null);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    // Tear down any previous render
    if (svguitarRef.current) {
      try { svguitarRef.current.clear(); } catch { /* ignore */ }
      svguitarRef.current = null;
    }
    el.innerHTML = "";

    const frets = resolveChordFrets(chord);
    if (!frets) return; // unknown chord – fallback UI shown below

    const chordData = toSvguitarChord(frets, chord);
    const instance = new SVGuitarChord(el);
    instance.chord(chordData).configure(SVGUITAR_SETTINGS).draw();
    svguitarRef.current = instance;

    return () => {
      if (svguitarRef.current) {
        try { svguitarRef.current.clear(); } catch { /* ignore */ }
        svguitarRef.current = null;
      }
    };
  }, [chord]);

  const known = resolveChordFrets(chord) !== null;

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        gap: 4,
      }}
    >
      {/* Fallback for chords not in the dictionary */}
      {!known && (
        <div
          style={{
            fontSize: 28,
            fontWeight: "bold",
            color: "var(--neon-yellow)",
            marginBottom: 8,
          }}
        >
          {chord}
        </div>
      )}

      {/* svguitar renders the SVG into this div (title is embedded by svguitar) */}
      <div ref={containerRef} style={{ width: 160 }} />
    </div>
  );
}
