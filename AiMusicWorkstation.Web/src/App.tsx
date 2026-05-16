import React, { useState } from "react";

type LyricSegment = { start: number; end: number; text: string };
type ChordEntry = { time: number; chord: string };
type Section = { label: string; start: number; end: number };

type AnalysisResult = {
    bpm?: number;
    key?: string;
    chords?: ChordEntry[];
    time_signature?: number;
    lyrics?: LyricSegment[];
    status?: string;
    error?: string;
};

type StructureResult = {
    sections?: Section[];
    status?: string;
    error?: string;
};

const API_URL = import.meta.env.VITE_API_URL ?? "";

export default function App() {
    const [file, setFile] = useState<File | null>(null);
    const [result, setResult] = useState<AnalysisResult | null>(null);
    const [structure, setStructure] = useState<StructureResult | null>(null);
    const [loading, setLoading] = useState(false);
    const [structureLoading, setStructureLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const handleFile = (e: React.ChangeEvent<HTMLInputElement>) => {
        const f = e.target.files?.[0];
        setFile(f && f.type === "audio/mpeg" ? f : null);
        setResult(null);
        setStructure(null);
        setError(null);
    };

    const analyze = async () => {
        if (!file) return;
        setLoading(true);
        setResult(null);
        setStructure(null);
        setError(null);
        try {
            const form = new FormData();
            form.append("file", file);
            const resp = await fetch(`${API_URL}/api/analysis/analyze-only`, {
                method: "POST",
                body: form,
            });
            const data = await resp.json();
            if (!resp.ok || data.error) {
                setError(data.error || data.detail || resp.statusText);
            } else {
                setResult(data);
            }
        } catch (e: any) {
            setError("Network or server error: " + e.message);
        }
        setLoading(false);
    };

    const fetchStructure = async () => {
        if (!file) return;
        setStructureLoading(true);
        setStructure(null);
        try {
            const name = file.name.replace(/\.[^.]+$/, "");
            const resp = await fetch(`${API_URL}/api/analysis/structure`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ artist: "Unknown", title: name, duration: 180 }),
            });
            const data = await resp.json();
            if (!resp.ok || data.error) {
                setStructure({ error: data.error || data.detail || resp.statusText });
            } else {
                setStructure(data);
            }
        } catch (e: any) {
            setStructure({ error: "Network error: " + e.message });
        }
        setStructureLoading(false);
    };

    return (
        <main style={{ maxWidth: 600, margin: "3rem auto", fontFamily: "sans-serif", padding: "0 1rem" }}>
            <h2>🎵 AI Music Analyzer</h2>
            <div style={{ marginBottom: 16 }}>
                <input type="file" accept="audio/mp3,.mp3,audio/mpeg" onChange={handleFile} />
                <button style={{ marginLeft: 10 }} onClick={analyze} disabled={!file || loading}>
                    {loading ? "Analyzing…" : "Analyze"}
                </button>
                {result && (
                    <button style={{ marginLeft: 10 }} onClick={fetchStructure} disabled={!file || structureLoading}>
                        {structureLoading ? "Loading structure…" : "Get Structure"}
                    </button>
                )}
            </div>

            {!API_URL && (
                <div style={{ color: "orange", marginBottom: 12 }}>
                    ⚠️ VITE_API_URL is not set. Create <code>.env</code> from <code>.env.example</code>.
                </div>
            )}

            {error && <div style={{ color: "red", marginTop: 12 }}>❌ {error}</div>}

            {result && !error && (
                <div style={{ marginTop: 16 }}>
                    <h3>Analysis</h3>
                    <table style={{ borderCollapse: "collapse", width: "100%" }}>
                        <tbody>
                            <tr><td style={tdLabel}>BPM</td><td><b>{result.bpm ?? "—"}</b></td></tr>
                            <tr><td style={tdLabel}>Key</td><td><b>{result.key ?? "—"}</b></td></tr>
                            <tr><td style={tdLabel}>Time Signature</td><td><b>{result.time_signature ? `${result.time_signature}/4` : "—"}</b></td></tr>
                        </tbody>
                    </table>

                    {result.chords && result.chords.length > 0 && (
                        <>
                            <h4>Chords</h4>
                            <div style={{ maxHeight: 160, overflowY: "auto", fontSize: 14 }}>
                                {result.chords.map((ch, i) => (
                                    <span key={i} style={{ marginRight: 12 }}>
                                        <span style={{ color: "#888" }}>{ch.time.toFixed(1)}s</span>{" "}
                                        <b>{ch.chord}</b>
                                    </span>
                                ))}
                            </div>
                        </>
                    )}

                    {result.lyrics && result.lyrics.length > 0 && (
                        <>
                            <h4>Lyrics</h4>
                            <div style={{ maxHeight: 200, overflowY: "auto", background: "#f4f4f4", padding: 8, borderRadius: 4, fontSize: 14 }}>
                                {result.lyrics.map((seg, i) => (
                                    <p key={i} style={{ margin: "4px 0" }}>
                                        <span style={{ color: "#888", fontSize: 12 }}>[{seg.start.toFixed(1)}s–{seg.end.toFixed(1)}s]</span>{" "}
                                        {seg.text}
                                    </p>
                                ))}
                            </div>
                        </>
                    )}
                </div>
            )}

            {structure && (
                <div style={{ marginTop: 16 }}>
                    {structure.error ? (
                        <div style={{ color: "red" }}>❌ Structure error: {structure.error}</div>
                    ) : (
                        <>
                            <h3>Song Structure</h3>
                            <table style={{ borderCollapse: "collapse", width: "100%", fontSize: 14 }}>
                                <thead>
                                    <tr>
                                        <th style={th}>Section</th>
                                        <th style={th}>Start</th>
                                        <th style={th}>End</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {(structure.sections ?? []).map((s, i) => (
                                        <tr key={i}>
                                            <td style={td}><b>{s.label}</b></td>
                                            <td style={td}>{s.start.toFixed(1)}s</td>
                                            <td style={td}>{s.end.toFixed(1)}s</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </>
                    )}
                </div>
            )}
        </main>
    );
}

const tdLabel: React.CSSProperties = { color: "#555", paddingRight: 16, paddingBottom: 4 };
const th: React.CSSProperties = { textAlign: "left", borderBottom: "2px solid #ddd", paddingBottom: 4, paddingRight: 12 };
const td: React.CSSProperties = { paddingTop: 4, paddingBottom: 4, paddingRight: 12 };
