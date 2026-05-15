import React, { useState } from "react";

type Result = {
    bpm?: number;
    key?: string;
    chords?: { time: number; chord: string }[];
    time_signature?: number;
    lyrics?: any;
    status?: string;
    error?: string;
};

export default function App() {
    const [file, setFile] = useState<File | null>(null);
    const [result, setResult] = useState<Result | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const handleFile = (e: React.ChangeEvent<HTMLInputElement>) => {
        const f = e.target.files?.[0];
        setFile(f && f.type === "audio/mpeg" ? f : null);
    };

    const analyze = async () => {
        if (!file) return;
        setLoading(true);
        setResult(null);
        setError(null);
        try {
            const form = new FormData();
            form.append("file", file);
            const resp = await fetch(`${import.meta.env.VITE_API_URL}/analyze-only`, {
                method: "POST",
                body: form
            });
            const data = await resp.json();
            if (!resp.ok || data.error) {
                setError(data.error || resp.statusText);
            } else {
                setResult(data);
            }
        } catch (e: any) {
            setError("Network or server error: " + e.message);
        }
        setLoading(false);
    };

    return (
        <main style={{ maxWidth: 520, margin: "3rem auto", fontFamily: "sans-serif" }}>
            <h2>AI Music Analyzer</h2>
            <input type="file" accept="audio/mp3,.mp3,audio/mpeg" onChange={handleFile} />
            <button style={{ marginLeft: 10 }} onClick={analyze} disabled={!file || loading}>
                {loading ? "Analyzing..." : "Analyze"}
            </button>
            {error && <div style={{ color: "red", marginTop: 12 }}>Error: {error}</div>}
            {result && !error && (
                <div style={{ marginTop: 16 }}>
                    <div>BPM: <b>{result.bpm ?? "-"}</b></div>
                    <div>Key: <b>{result.key ?? "-"}</b></div>
                    <div>Time Signature: <b>{result.time_signature ?? "-"}</b></div>
                    <div>
                        Chords:
                        <ul>
                            {(result.chords ?? []).map((ch, i) => (
                                <li key={i}>
                                    {ch.time}s: {ch.chord}
                                </li>
                            ))}
                        </ul>
                    </div>
                </div>
            )}
        </main>
    );
}