import React, { useState, useRef, useEffect } from 'react';
import './index.css';

type LyricSegment = { start: number; end: number; text: string };
type ChordEntry = { time: number; chord: string };
type Section = { label: string; start: number; end: number };

type AnalysisResult = {
    bpm?: number;
    key?: string;
    chords?: ChordEntry[];
    time_signature?: number;
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

type SongProject = {
    id: string;
    title: string;
    artist: string;
    genre: string;
    bpm: number;
    key: string;
    stemsPath: string;
};

const API_URL = import.meta.env.VITE_API_URL ?? "";

// --- Chord diagram data (root note → intervals shown as dots) ---
const CHORD_INTERVALS: Record<string, number[]> = {
    '':    [0, 4, 7],
    'm':   [0, 3, 7],
    '7':   [0, 4, 7, 10],
    'maj7':[0, 4, 7, 11],
    'm7':  [0, 3, 7, 10],
    'sus2':[0, 2, 7],
    'sus4':[0, 5, 7],
    'dim': [0, 3, 6],
    'aug': [0, 4, 8],
};
const NOTES = ['C','C#','D','D#','E','F','F#','G','G#','A','Bb','B'];
function parseChord(chord: string): { root: string; quality: string } {
    const match = chord.match(/^([A-G](?:#|b)?)(.*)?$/);
    if (!match) return { root: chord, quality: '' };
    return { root: match[1], quality: match[2] || '' };
}
function ChordDiagram({ chord }: { chord: string }) {
    const { root, quality } = parseChord(chord);
    const rootIdx = NOTES.findIndex(n => n === root);
    const intervals = CHORD_INTERVALS[quality] ?? CHORD_INTERVALS[''];
    const activeNotes = new Set(intervals.map(i => (rootIdx + i) % 12));
    return (
        <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 4 }}>
            <div style={{ fontSize: 28, fontWeight: 'bold', color: 'var(--neon-yellow)' }}>{chord}</div>
            <div style={{ display:'flex', gap: 3 }}>
                {NOTES.map((n, i) => (
                    <div key={n} style={{
                        width: 20, height: 20, borderRadius: '50%',
                        background: activeNotes.has(i) ? 'var(--neon-cyan)' : 'rgba(255,255,255,0.08)',
                        border: activeNotes.has(i) ? '2px solid var(--neon-cyan)' : '1px solid rgba(255,255,255,0.15)',
                        display:'flex', alignItems:'center', justifyContent:'center',
                        fontSize: 7, color: activeNotes.has(i) ? '#000' : '#555',
                        fontWeight: 'bold', transition: 'all 0.2s'
                    }}>{n}</div>
                ))}
            </div>
        </div>
    );
}

// Web Audio Context for Metronome beep
const audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
const playClick = () => {
    if (audioCtx.state === 'suspended') audioCtx.resume();
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

export default function App() {
  const [activeTab, setActiveTab] = useState<'chord' | 'scale'>('chord');
  
  // Audio playback states
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  
  const [metronomeEnabled, setMetronomeEnabled] = useState(false);
  const [countInEnabled, setCountInEnabled] = useState(false);
  const [masterVol, setMasterVol] = useState(100);
  
  // Mixer states
  const [volumes, setVolumes] = useState({ drums: 80, bass: 80, other: 80, vocals: 80 });
  const [mutes, setMutes] = useState({ drums: false, bass: false, other: false, vocals: false });
  const [solos, setSolos] = useState({ drums: false, bass: false, other: false, vocals: false });

  // Panel toggles
  const [showChords, setShowChords] = useState(true);
  const [showLyrics, setShowLyrics] = useState(true);
  const [showStructure, setShowStructure] = useState(true);

  // API Integration States
  const [result, setResult] = useState<AnalysisResult | null>(null);
  const [structure, setStructure] = useState<StructureResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [nowPlaying, setNowPlaying] = useState<{ title: string; artist: string } | null>(null);
  
  const [projects, setProjects] = useState<SongProject[]>([]);
  const [urlInput, setUrlInput] = useState("");

  const fetchLibrary = async () => {
      try {
          const resp = await fetch(`${API_URL}/api/library/projects`);
          if (resp.ok) {
              const data = await resp.json();
              setProjects(data);
          }
      } catch (e) {
          console.error("Failed to fetch library", e);
      }
  };

  useEffect(() => {
      fetchLibrary();
  }, []);

  const deleteProject = async (id: string) => {
      try {
          await fetch(`${API_URL}/api/library/projects/${id}`, { method: 'DELETE' });
          fetchLibrary();
      } catch (e) {
          console.error("Failed to delete project", e);
      }
  };

  const loadStemsFromPath = (stemsPath: string) => {
      const pathParts = stemsPath.split(/[\/\\]/);
      const relPath = pathParts.slice(-2).join('/');
      stems.current.drums.src    = `${API_URL}/api/analysis/audio/${relPath}/drums.mp3`;
      stems.current.bass.src     = `${API_URL}/api/analysis/audio/${relPath}/bass.mp3`;
      stems.current.other.src    = `${API_URL}/api/analysis/audio/${relPath}/other.mp3`;
      stems.current.vocals.src   = `${API_URL}/api/analysis/audio/${relPath}/vocals.mp3`;
      stems.current.drums.onloadedmetadata = () => setDuration(stems.current.drums.duration);
  };

  const loadProject = async (p: SongProject) => {
      if (p.stemsPath) loadStemsFromPath(p.stemsPath);
      setResult({ bpm: p.bpm, key: p.key, title: p.title, artist: p.artist });
      setNowPlaying({ title: p.title, artist: p.artist });
      setCurrentTime(0);
      setIsPlaying(false);
      setStructure(null);
      try {
          const structResp = await fetch(`${API_URL}/api/analysis/structure`, {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ artist: p.artist, title: p.title, duration: 180 }),
          });
          const structData = await structResp.json();
          if (structResp.ok && !structData.error) setStructure(structData);
      } catch { /* structure is optional */ }
  };

  const handleYoutubeImport = async () => {
      if (!urlInput) return;
      setLoading(true);
      setError(null);
      try {
          const resp = await fetch(`${API_URL}/api/import/youtube`, {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ url: urlInput }),
          });
          
          const text = await resp.text();
          if (!text) { setError(`Server returned empty response (HTTP ${resp.status})`); setLoading(false); return; }
          
          let data: AnalysisResult;
          try { data = JSON.parse(text); }
          catch { setError(`Server error (HTTP ${resp.status}): ${text.substring(0, 200)}`); setLoading(false); return; }
          
          if (!resp.ok || data.error) {
              setError((data as any).error || (data as any).message || (data as any).detail || resp.statusText);
          } else {
              setResult(data);
              const trackTitle = data.title || 'Unknown Track';
              const trackArtist = data.artist || 'Unknown Artist';
              setNowPlaying({ title: trackTitle, artist: trackArtist });
              setUrlInput('');

              // Load stems audio
              if (data.stems_path) loadStemsFromPath(data.stems_path);

              // Fetch song structure
              try {
                  const structResp = await fetch(`${API_URL}/api/analysis/structure`, {
                      method: "POST",
                      headers: { "Content-Type": "application/json" },
                      body: JSON.stringify({ artist: trackArtist, title: trackTitle, duration: 180 }),
                  });
                  const structData = await structResp.json();
                  if (structResp.ok && !structData.error) setStructure(structData);
              } catch { /* structure is optional */ }

              fetchLibrary();
          }
      } catch (e: any) {
          setError("Network error: " + e.message);
      }
      setLoading(false);
  };
  
  const fileInputRef = useRef<HTMLInputElement>(null);
  const lyricsScrollRef = useRef<HTMLDivElement>(null);
  
  // Audio elements
  const stems = useRef({
      drums: new Audio(),
      bass: new Audio(),
      other: new Audio(),
      vocals: new Audio()
  });

  const [autoScrollLyrics, setAutoScrollLyrics] = useState(true);

  // Apply volumes and mutes/solos
  useEffect(() => {
      const anySolo = Object.values(solos).some(s => s);
      Object.keys(stems.current).forEach(key => {
          const k = key as keyof typeof stems.current;
          const audio = stems.current[k];
          
          let targetVol = (volumes[k] / 100) * (masterVol / 100);
          if (mutes[k]) targetVol = 0;
          if (anySolo && !solos[k]) targetVol = 0;
          
          audio.volume = Math.min(Math.max(targetVol, 0), 1);
      });
  }, [volumes, mutes, solos, masterVol]);

  // Sync time
  useEffect(() => {
      const interval = setInterval(() => {
          if (isPlaying) {
              const current = stems.current.drums.currentTime;
              setCurrentTime(current);
              
              if (result?.lyrics && autoScrollLyrics && lyricsScrollRef.current) {
                  // Simple auto-scroll based on time
                  const activeIdx = result.lyrics.findIndex(l => l.start <= current && l.end >= current);
                  if (activeIdx !== -1) {
                      const container = lyricsScrollRef.current;
                      const activeElem = container.children[activeIdx] as HTMLElement;
                      if (activeElem) {
                          container.scrollTo({ top: activeElem.offsetTop - container.clientHeight / 2, behavior: 'smooth' });
                      }
                  }
              }
              
              // Metronome logic
              if (metronomeEnabled && result?.bpm) {
                  const beatInterval = 60 / result.bpm;
                  if (current % beatInterval < 0.1 && (current % beatInterval) > 0) {
                      // playClick(); // Basic implementation, needs tight scheduling in real app
                  }
              }
          }
      }, 100);
      return () => clearInterval(interval);
  }, [isPlaying, result, autoScrollLyrics, metronomeEnabled]);

  const handleManualScroll = (_e: React.UIEvent<HTMLDivElement>) => {
      // If user scrolls manually, disable auto-scroll temporarily
      setAutoScrollLyrics(false);
      // Re-enable after 3 seconds of no scrolling
      setTimeout(() => setAutoScrollLyrics(true), 3000);
  };

  const handlePlayPause = () => {
      if (isPlaying) {
          Object.values(stems.current).forEach(a => a.pause());
      } else {
          // Count-in logic
          if (countInEnabled) {
              let count = 0;
              const interval = setInterval(() => {
                  playClick();
                  count++;
                  if (count >= 4) {
                      clearInterval(interval);
                      Object.values(stems.current).forEach(a => a.play());
                      setIsPlaying(true);
                  }
              }, 500);
              return;
          }
          Object.values(stems.current).forEach(a => a.play());
      }
      setIsPlaying(!isPlaying);
  };

  const skipToStart = () => {
      Object.values(stems.current).forEach(a => a.currentTime = 0);
      setCurrentTime(0);
      if (!isPlaying) handlePlayPause();
  };

  const seekTime = (offset: number) => {
      Object.values(stems.current).forEach(a => {
          a.currentTime = Math.max(0, Math.min(a.currentTime + offset, duration));
      });
      setCurrentTime(stems.current.drums.currentTime);
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    if (f && (f.type === "audio/mpeg" || f.name.endsWith(".mp3") || f.type.includes("audio/"))) {
        analyzeFile(f);
    } else {
        setError("Please select a valid Audio file.");
    }
  };

  const triggerFileInput = () => fileInputRef.current?.click();

  const analyzeFile = async (selectedFile: File) => {
      setLoading(true);
      setError(null);
      
      try {
          const form = new FormData();
          form.append("file", selectedFile);
          
          // Using /analyze to actually get stems on the backend
          const resp = await fetch(`${API_URL}/api/analysis/analyze`, {
              method: "POST",
              body: form,
          });
          
          const data = await resp.json();
          if (!resp.ok || data.error) {
              setError(data.error || data.detail || resp.statusText);
              setLoading(false);
              return;
          }
          
          setResult(data);
          const fileName = selectedFile.name.replace(/\.[^.]+$/, '');
          setNowPlaying({ title: fileName, artist: 'Local Upload' });

          // Load stems from proxy if available, else use local file
          if (data.stems_path) {
              loadStemsFromPath(data.stems_path);
          } else {
              const objectUrl = URL.createObjectURL(selectedFile);
              stems.current.drums.src   = objectUrl;
              stems.current.bass.src    = objectUrl;
              stems.current.other.src   = objectUrl;
              stems.current.vocals.src  = objectUrl;
              stems.current.drums.onloadedmetadata = () => setDuration(stems.current.drums.duration);
          }

          const structResp = await fetch(`${API_URL}/api/analysis/structure`, {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ artist: 'Local Upload', title: fileName, duration: 180 }),
          });
          const structData = await structResp.json();
          if (structResp.ok && !structData.error) setStructure(structData);

          fetchLibrary();
          
      } catch (e: any) {
          setError("Network error: " + e.message);
      }
      setLoading(false);
  };

  const formatTime = (sec: number) => {
      const m = Math.floor(sec / 60);
      const s = Math.floor(sec % 60);
      return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  };

  const setMixerVolume = (stem: keyof typeof volumes, val: number) => {
      setVolumes(prev => ({ ...prev, [stem]: val }));
  };

  const toggleMute = (stem: keyof typeof mutes) => {
      setMutes(prev => ({ ...prev, [stem]: !prev[stem] }));
  };

  const toggleSolo = (stem: keyof typeof solos) => {
      setSolos(prev => ({ ...prev, [stem]: !prev[stem] }));
  };

  return (
    <div className="app-container">
      
      {/* COLUMN 1: LIBRARY */}
      <div className="col-library">
        <div className="glass-panel" style={{ flex: 1 }}>
          <div className="flex justify-between items-center mb-2">
            <span className="section-title" style={{ marginBottom: 0 }}>LIBRARY</span>
            <button className="btn-icon">⟳</button>
          </div>
          <input type="text" className="input-dark mb-2" placeholder="Search..." />
          <div className="flex gap-1 mb-2">
            <select className="input-dark">
              <option>Latest</option>
              <option>A-Z</option>
            </select>
          </div>
          <div style={{ overflowY: 'auto', flex: 1, paddingRight: '5px' }}>
              {projects.map(p => (
                  <div key={p.id} className="glass-panel-inner mb-1 flex justify-between" style={{ cursor: 'pointer' }} onClick={() => loadProject(p)}>
                    <div>
                      <div style={{ color: 'white', fontWeight: 'bold', fontSize: '13px' }}>{p.title}</div>
                      <div style={{ color: '#aaa', fontSize: '11px', marginBottom: '4px' }}>{p.artist}</div>
                    </div>
                    <div className="flex flex-col gap-1">
                        <button className="btn-icon" style={{ fontSize: '10px' }}>✏️</button>
                        <button className="btn-icon" style={{ fontSize: '10px', color: '#ff4444' }} onClick={(e) => { e.stopPropagation(); deleteProject(p.id); }}>❌</button>
                    </div>
                  </div>
              ))}
              {projects.length === 0 && (
                  <div style={{ color: '#aaa', fontSize: '11px', textAlign: 'center', marginTop: '10px' }}>Library is empty</div>
              )}
          </div>
        </div>
      </div>

      {/* COLUMN 2: CHORDS & SCALES */}
      {showChords && (
      <div className="col-chords">
        <div className="glass-panel" style={{ flex: 1 }}>
          <div className="glass-panel-inner text-center mb-2" style={{ borderBottom: '1px solid rgba(0,240,255,0.2)' }}>
            <span className="highlight-cyan" style={{ fontSize: '16px', fontWeight: 'bold' }}>— — —</span>
          </div>
          
          <div className="flex gap-2 mb-2">
            <button className={`btn-primary w-full ${activeTab === 'chord' ? 'active' : ''}`} onClick={() => setActiveTab('chord')}>Chord</button>
            <button className={`btn-primary w-full ${activeTab === 'scale' ? 'active' : ''}`} onClick={() => setActiveTab('scale')}>Scale</button>
          </div>

          {activeTab === 'chord' && (
            <div className="flex flex-col items-center justify-center" style={{ flex: 1, gap: 8 }}>
              {/* Active chord diagram */}
              {result?.chords && result.chords.length > 0 ? (
                <ChordDiagram chord={
                  result.chords.reduce((best, ch) =>
                    ch.time <= currentTime ? ch : best,
                    result.chords[0]
                  ).chord
                } />
              ) : (
                <div style={{ fontSize: 40, fontWeight: 'bold', color: 'var(--neon-yellow)' }}>
                  {result?.key ?? 'Am'}
                </div>
              )}
              <div className="glass-panel-inner w-full text-center" style={{ maxHeight: '180px', overflowY: 'auto' }}>
                <div style={{ color: '#888', fontSize: '12px', marginBottom: '4px' }}>Chord Timeline</div>
                {result?.chords ? (
                    result.chords.map((ch, i) => {
                        const isActive = currentTime >= ch.time && (i === result.chords!.length - 1 || currentTime < result.chords![i + 1].time);
                        return (
                          <div key={i} className="flex justify-between items-center mb-1 border-b border-gray-800 pb-1"
                            style={{ background: isActive ? 'rgba(0,240,255,0.08)' : 'transparent', borderRadius: 4, padding: '2px 4px' }}>
                            <span style={{ fontSize: '11px', color: '#aaa' }}>{ch.time.toFixed(1)}s</span>
                            <span style={{ fontSize: '13px', fontWeight: 'bold', color: isActive ? 'var(--neon-cyan)' : '#888' }}>{ch.chord}</span>
                          </div>
                        );
                    })
                ) : (
                    <div style={{ fontSize: '13px', color: '#555' }}>Waiting for analysis...</div>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
      )}

      {/* COLUMN 3: MAIN WORKSTATION (Now strictly matches Desktop layout) */}
      <div className="col-main">
        {/* Header */}
        <div className="flex justify-between items-center" style={{ padding: '0 10px' }}>
          <div className="flex items-center gap-4">
            <h1 className="neon-text-gradient m-0" style={{ fontSize: '24px' }}>AI MUSIC WORKSTATION</h1>
            <div className="flex gap-2">
               <button className={`btn-primary ${!showChords ? 'opacity-50' : ''}`} style={{ padding: '4px 8px', fontSize: '10px' }} onClick={() => setShowChords(!showChords)}>🎹 Chords</button>
               <button className={`btn-primary ${!showLyrics ? 'opacity-50' : ''}`} style={{ padding: '4px 8px', fontSize: '10px' }} onClick={() => setShowLyrics(!showLyrics)}>🎤 Lyrics</button>
               <button className={`btn-primary ${!showStructure ? 'opacity-50' : ''}`} style={{ padding: '4px 8px', fontSize: '10px' }} onClick={() => setShowStructure(!showStructure)}>📑 Structure</button>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span style={{ fontSize: '10px', fontWeight: 'bold', color: '#888' }}>MASTER VOL</span>
            <input type="range" min="0" max="100" value={masterVol} onChange={e => setMasterVol(Number(e.target.value))} style={{ width: '80px' }} />
          </div>
        </div>

        {error && <div style={{ background: 'rgba(255,0,0,0.1)', color: '#ff4444', padding: '10px', margin: '10px 0' }}>⚠️ {error}</div>}

        {/* Import Section — collapses to Now Playing bar when a song is loaded */}
        {nowPlaying && !loading ? (
          <div className="glass-panel" style={{ display:'flex', alignItems:'center', justifyContent:'space-between', padding: '8px 14px' }}>
            <div>
              <div style={{ color: 'var(--neon-cyan)', fontSize: 11, fontWeight: 'bold', letterSpacing: 1 }}>NOW PLAYING</div>
              <div style={{ color: '#fff', fontWeight: 'bold', fontSize: 14 }}>{nowPlaying.title}</div>
              <div style={{ color: '#aaa', fontSize: 11 }}>{nowPlaying.artist}</div>
            </div>
            <button
              className="btn-icon"
              style={{ fontSize: 11, color: '#aaa', whiteSpace: 'nowrap' }}
              onClick={() => { setNowPlaying(null); setResult(null); setStructure(null); setUrlInput(''); }}
            >✕ Import new</button>
          </div>
        ) : (
          <div className="glass-panel">
            <div className="flex gap-2 mb-2">
              <input
                type="text" className="input-dark"
                placeholder="Paste YouTube or Spotify Link here..."
                value={urlInput}
                onChange={(e) => setUrlInput(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleYoutubeImport()}
              />
              <button className="btn-accent" style={{ background: 'rgba(255,0,0,0.1)', color: '#ff4444', borderColor: '#ff4444' }} onClick={handleYoutubeImport} disabled={loading}>⬇ DOWNLOAD</button>
            </div>
            <input type="file" accept="audio/*" ref={fileInputRef} onChange={handleFileSelect} style={{ display: 'none' }} />
            <button className="btn-primary" style={{ padding: '12px', width: '100%' }} onClick={triggerFileInput} disabled={loading}>
              {loading ? '⚡ ANALYZING WITH AI... (Separating Stems & Transcribing)' : '📂 OPEN LOCAL AUDIO FILE'}
            </button>
          </div>
        )}

        {/* Analysis Data (Tempo, Key, Transpose, Metronome) */}
        <div className="flex gap-2">
          <div className="glass-panel flex-col items-center justify-center" style={{ flex: 1 }}>
            <span className="section-title">TEMPO</span>
            <span className="highlight-cyan" style={{ fontSize: '40px', fontWeight: 'bold' }}>{result?.bpm ? Math.round(result.bpm) : "—"}</span>
          </div>
          <div className="glass-panel flex-col items-center justify-center" style={{ flex: 1 }}>
            <span className="section-title">TIME SIG</span>
            <span className="highlight-cyan" style={{ fontSize: '40px', fontWeight: 'bold' }}>{result?.time_signature ? `${result.time_signature}/4` : "—"}</span>
          </div>
          <div className="glass-panel flex-col items-center justify-center" style={{ flex: 1 }}>
            <span className="section-title">KEY</span>
            <span className="highlight-cyan" style={{ fontSize: '40px', fontWeight: 'bold' }}>{result?.key ? result.key : "—"}</span>
          </div>
        </div>
        
        {/* Transpose & Metronome (Like Desktop) */}
        <div className="glass-panel flex justify-center items-center gap-4">
            <div className="flex items-center gap-2">
                <span style={{ fontSize: '10px', fontWeight: 'bold', color: '#888' }}>TRANSPOSE</span>
                <button className="btn-icon">− </button>
                <span style={{ fontSize: '16px', fontWeight: 'bold', color: 'var(--neon-cyan)' }}>0</span>
                <button className="btn-icon"> +</button>
            </div>
            <div className="flex items-center gap-2">
                <button 
                    className={`btn-primary ${countInEnabled ? 'active' : ''}`} 
                    style={{ fontSize: '10px', padding: '4px 8px' }}
                    onClick={() => setCountInEnabled(!countInEnabled)}
                >Count In</button>
                <button 
                    className={`btn-primary ${metronomeEnabled ? 'active' : ''}`} 
                    style={{ fontSize: '10px', padding: '4px 8px' }}
                    onClick={() => setMetronomeEnabled(!metronomeEnabled)}
                >🥁 Metro</button>
            </div>
        </div>

        {/* LYRICS PANEL IN THE MIDDLE */}
        {showLyrics && (
        <div className="glass-panel" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
          <div className="flex justify-between items-center mb-2">
            <span className="section-title" style={{ marginBottom: 0 }}>LYRICS</span>
            <button className="btn-icon">📝</button>
          </div>
          <div 
            ref={lyricsScrollRef}
            onScroll={handleManualScroll}
            style={{ overflowY: 'auto', flex: 1, paddingRight: '5px' }}
          >
            {result?.lyrics ? (
                result.lyrics.map((seg, idx) => (
                    <p key={idx} style={{ 
                        color: (currentTime >= seg.start && currentTime <= seg.end) ? '#fff' : '#555', 
                        margin: '5px 0', 
                        fontSize: '14px',
                        fontWeight: (currentTime >= seg.start && currentTime <= seg.end) ? 'bold' : 'normal',
                        transition: 'color 0.2s'
                    }}>
                        <span style={{ color: '#444', fontSize: '10px', marginRight: '5px' }}>[{seg.start.toFixed(1)}s]</span>
                        {seg.text}
                    </p>
                ))
            ) : (
                <div style={{ fontSize: '12px', color: '#555', textAlign: 'center', marginTop: '20px' }}>
                    {loading ? "Transcribing lyrics with Whisper..." : "No lyrics data"}
                </div>
            )}
          </div>
        </div>
        )}

        {/* Mixer */}
        <div className="glass-panel" style={{ paddingBottom: '4px' }}>
          <span className="section-title mb-2 text-center">STEM MIXER</span>
          <div className="mixer-grid">
            {(['drums', 'bass', 'other', 'vocals'] as const).map((stem) => (
              <div key={stem} className="mixer-channel">
                <span style={{ fontSize: '11px', fontWeight: 'bold', color: '#888' }}>{stem.toUpperCase()}</span>
                <div className="vertical-slider-container">
                  <input type="range" className="vertical-slider" min="0" max="100" value={volumes[stem]} onChange={e => setMixerVolume(stem, Number(e.target.value))} />
                </div>
                <div className="flex gap-1">
                  <button className={`toggle-btn toggle-mute ${mutes[stem] ? 'active' : ''}`} onClick={() => toggleMute(stem)}>M</button>
                  <button className={`toggle-btn toggle-solo ${solos[stem] ? 'active' : ''}`} onClick={() => toggleSolo(stem)}>S</button>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Transport */}
        <div className="glass-panel" style={{ marginTop: 'auto' }}>
          <div className="flex items-center gap-2 mb-2">
            <span style={{ fontSize: '12px', color: '#888', width: '40px' }}>{formatTime(currentTime)}</span>
            <input type="range" className="timeline-slider" min="0" max={duration || 100} value={currentTime} onChange={e => seekTime(Number(e.target.value) - currentTime)} />
            <span style={{ fontSize: '12px', color: '#888', width: '40px', textAlign: 'right' }}>{formatTime(duration)}</span>
          </div>
          <div className="flex justify-center items-center gap-2">
            <button className="btn-icon" style={{ fontSize: '20px' }}>🔁</button>
            <button className="btn-icon" style={{ fontSize: '24px' }} onClick={() => seekTime(-10)}>«</button>
            <button className="btn-icon" style={{ fontSize: '16px', color: '#ff4444' }} onClick={skipToStart}>⏮</button>
            <button className="btn-accent" style={{ width: '60px', height: '60px', borderRadius: '50%', fontSize: '24px', padding: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }} onClick={handlePlayPause}>
              {isPlaying ? '⏸' : '▶'}
            </button>
            <button className="btn-icon" style={{ fontSize: '24px' }} onClick={() => seekTime(10)}>»</button>
            <button className="btn-primary ml-4">💾 EXPORT MIX</button>
          </div>
        </div>
      </div>

      {/* COLUMN 4: SECTIONS */}
      {showStructure && (
      <div className="col-sections">
        <div className="glass-panel" style={{ flex: 1 }}>
          <div className="flex justify-between items-center mb-2">
            <span className="section-title" style={{ marginBottom: 0 }}>SONG STRUCTURE</span>
          </div>
          <div style={{ overflowY: 'auto', flex: 1, paddingRight: '5px' }}>
            {structure?.sections ? (
                structure.sections.map((sec: Section, idx: number) => (
                    <div key={idx} className="glass-panel-inner mb-1 flex justify-between items-center cursor-pointer hover:border-cyan" onClick={() => seekTime(sec.start - currentTime)}>
                        <span style={{ color: 'white', fontWeight: 'bold', fontSize: '12px' }}>{sec.label}</span>
                        <span style={{ color: 'var(--neon-cyan)', fontSize: '10px' }}>{formatTime(sec.start)}</span>
                    </div>
                ))
            ) : (
                <div style={{ fontSize: '12px', color: '#555', textAlign: 'center', marginTop: '20px' }}>
                    {loading ? "Analyzing structure..." : "No structure data"}
                </div>
            )}
          </div>
        </div>
      </div>
      )}
    </div>
  );
}
