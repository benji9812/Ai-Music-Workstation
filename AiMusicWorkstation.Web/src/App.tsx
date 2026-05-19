import { useState, useRef } from 'react';
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
  const [activeTab, setActiveTab] = useState<'chord' | 'scale'>('chord');
  const [isPlaying, setIsPlaying] = useState(false);

  // API Integration States
  const [file, setFile] = useState<File | null>(null);
  const [result, setResult] = useState<AnalysisResult | null>(null);
  const [structure, setStructure] = useState<StructureResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Mock data for UI presentation
  const projects = [
    { title: "Cyberpunk Beat 1", artist: "Unknown", bpm: 124, key: "Am", genre: "Synthwave" },
    { title: "Neon Nights", artist: "Benji", bpm: 95, key: "C", genre: "Lofi" },
  ];

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0];
    if (f && (f.type === "audio/mpeg" || f.name.endsWith(".mp3"))) {
        setFile(f);
        analyzeFile(f);
    } else {
        setError("Please select a valid MP3 file.");
    }
  };

  const triggerFileInput = () => {
      fileInputRef.current?.click();
  };

  const analyzeFile = async (selectedFile: File) => {
      setLoading(true);
      setResult(null);
      setStructure(null);
      setError(null);
      
      try {
          // 1. Analyze Audio
          const form = new FormData();
          form.append("file", selectedFile);
          
          const resp = await fetch(`${API_URL}/api/analysis/analyze-only`, {
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

          // 2. Fetch Structure (Run in parallel or right after)
          const name = selectedFile.name.replace(/\.[^.]+$/, "");
          const structResp = await fetch(`${API_URL}/api/analysis/structure`, {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ artist: "Unknown", title: name, duration: 180 }),
          });
          
          const structData = await structResp.json();
          if (!structResp.ok || structData.error) {
              console.error("Structure error:", structData.error);
          } else {
              setStructure(structData);
          }
          
      } catch (e: any) {
          setError("Network or server error: " + e.message);
      }
      setLoading(false);
  };

  return (
    <div className="app-container">
      
      {/* COLUMN 1: LIBRARY */}
      <div className="col-library">
        <div className="glass-panel" style={{ flex: 1 }}>
          <div className="flex justify-between items-center mb-2">
            <span className="section-title" style={{ marginBottom: 0 }}>Library</span>
            <button className="btn-icon">⟳</button>
          </div>
          <input type="text" className="input-dark mb-2" placeholder="Search..." />
          <div className="flex gap-1 mb-2">
            <select className="input-dark">
              <option>Latest</option>
              <option>A-Z</option>
              <option>BPM</option>
            </select>
          </div>
          
          <div style={{ overflowY: 'auto', flex: 1, paddingRight: '5px' }}>
            {projects.map((proj, idx) => (
              <div key={idx} className="glass-panel-inner mb-1" style={{ cursor: 'pointer' }}>
                <div style={{ color: 'white', fontWeight: 'bold', fontSize: '13px' }}>{proj.title}</div>
                <div style={{ color: '#aaa', fontSize: '11px', marginBottom: '4px' }}>{proj.artist}</div>
                <div style={{ fontSize: '10px', color: '#888' }}>
                  {proj.bpm} BPM • <span className="highlight-cyan">{proj.key}</span> • <i>{proj.genre}</i>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* COLUMN 2: CHORDS & SCALES */}
      <div className="col-chords">
        <div className="glass-panel" style={{ flex: 1 }}>
          <div className="glass-panel-inner text-center mb-2" style={{ borderBottom: '1px solid rgba(0,240,255,0.2)' }}>
            <span className="highlight-cyan" style={{ fontSize: '16px', fontWeight: 'bold' }}>— — —</span>
          </div>
          
          <div className="flex gap-2 mb-2">
            <button 
              className={`btn-primary w-full ${activeTab === 'chord' ? 'active' : ''}`}
              style={activeTab !== 'chord' ? { borderColor: 'rgba(255,255,255,0.2)', color: '#aaa'} : {}}
              onClick={() => setActiveTab('chord')}
            >
              Chord
            </button>
            <button 
              className={`btn-primary w-full ${activeTab === 'scale' ? 'active' : ''}`}
              style={activeTab !== 'scale' ? { borderColor: 'rgba(255,255,255,0.2)', color: '#aaa'} : {}}
              onClick={() => setActiveTab('scale')}
            >
              Scale
            </button>
          </div>

          {activeTab === 'chord' && (
            <div className="flex flex-col items-center justify-center" style={{ flex: 1 }}>
              <div style={{ fontSize: '50px', fontWeight: 'bold', color: 'var(--neon-yellow)' }}>
                {result?.key ? result.key : "Am"}
              </div>
              <div className="glass-panel-inner mt-2 w-full text-center" style={{ maxHeight: '200px', overflowY: 'auto' }}>
                <div style={{ color: '#888', fontSize: '14px', marginBottom: '5px' }}>Chord Timeline</div>
                {result?.chords ? (
                    result.chords.map((ch, i) => (
                        <div key={i} className="flex justify-between items-center mb-1 border-b border-gray-800 pb-1">
                            <span style={{ fontSize: '12px', color: '#aaa' }}>{ch.time.toFixed(1)}s</span>
                            <span style={{ fontSize: '14px', fontWeight: 'bold', color: 'var(--neon-cyan)' }}>{ch.chord}</span>
                        </div>
                    ))
                ) : (
                    <div style={{ fontSize: '14px', color: '#555' }}>Waiting for analysis...</div>
                )}
              </div>
            </div>
          )}

          {activeTab === 'scale' && (
            <div className="flex flex-col items-center justify-center" style={{ flex: 1 }}>
              <div className="flex gap-2 mb-2 w-full">
                <button className="btn-primary w-full" style={{ fontSize: '10px' }}>Penta</button>
                <button className="btn-primary w-full" style={{ fontSize: '10px', borderColor: 'rgba(255,255,255,0.2)', color: '#aaa' }}>Maj/Min</button>
              </div>
              <div style={{ fontSize: '20px', fontWeight: 'bold', color: 'var(--neon-magenta)' }}>{result?.key ? `${result.key} Scale` : "A Minor Scale"}</div>
              <div className="mt-2" style={{ color: '#aaa', fontSize: '12px' }}>A B C D E F G</div>
            </div>
          )}
        </div>
      </div>

      {/* COLUMN 3: MAIN WORKSTATION */}
      <div className="col-main">
        {/* Header */}
        <div className="flex justify-between items-center" style={{ padding: '0 10px' }}>
          <h1 className="neon-text-gradient m-0" style={{ fontSize: '24px' }}>AI MUSIC WORKSTATION</h1>
          <div className="flex items-center gap-2">
            <span style={{ fontSize: '10px', fontWeight: 'bold', color: '#888' }}>MASTER VOL</span>
            <input type="range" min="0" max="100" defaultValue="100" style={{ width: '80px' }} />
          </div>
        </div>

        {/* Error Message */}
        {error && (
            <div style={{ background: 'rgba(255,0,0,0.1)', border: '1px solid #ff4444', color: '#ff4444', padding: '10px', borderRadius: '4px', margin: '10px 0', fontSize: '14px' }}>
                ⚠️ {error}
            </div>
        )}

        {/* Import Section */}
        <div className="glass-panel">
          <div className="flex gap-2 mb-2">
            <input type="text" className="input-dark" placeholder="Paste YouTube or Spotify Link here..." />
            <button className="btn-accent" style={{ background: 'rgba(255,0,0,0.1)', color: '#ff4444', borderColor: '#ff4444' }}>
              ⬇ DOWNLOAD
            </button>
          </div>
          
          {/* Hidden File Input */}
          <input 
            type="file" 
            accept="audio/mp3,.mp3,audio/mpeg" 
            ref={fileInputRef} 
            onChange={handleFileSelect} 
            style={{ display: 'none' }} 
          />
          
          <button 
            className="btn-primary" 
            style={{ padding: '12px', width: '100%' }}
            onClick={triggerFileInput}
            disabled={loading}
          >
            {loading ? "⚡ ANALYZING AUDIO WITH AI..." : "📂 OPEN LOCAL AUDIO FILE"}
          </button>
          
          <div className="text-center mt-2" style={{ fontSize: '12px', color: '#888' }}>
            {file ? `Selected: ${file.name}` : "Ready"}
          </div>
        </div>

        {/* Analysis Data (Tempo, Key, Transpose) */}
        <div className="flex gap-2">
          <div className="glass-panel flex-col items-center justify-center" style={{ flex: 1 }}>
            <span className="section-title">TEMPO</span>
            <span className="highlight-cyan" style={{ fontSize: '40px', fontWeight: 'bold' }}>
                {result?.bpm ? Math.round(result.bpm) : "—"}
            </span>
            <span className="badge badge-ai mt-1">⚡ AI ANALYSIS</span>
          </div>
          <div className="glass-panel flex-col items-center justify-center" style={{ flex: 1 }}>
            <span className="section-title">TIME SIG</span>
            <span className="highlight-cyan" style={{ fontSize: '40px', fontWeight: 'bold' }}>
                {result?.time_signature ? `${result.time_signature}/4` : "—"}
            </span>
            <span className="badge badge-ai mt-1">⚡ AI ANALYSIS</span>
          </div>
          <div className="glass-panel flex-col items-center justify-center" style={{ flex: 1 }}>
            <span className="section-title">KEY</span>
            <span className="highlight-cyan" style={{ fontSize: '40px', fontWeight: 'bold' }}>
                {result?.key ? result.key : "—"}
            </span>
            <span className="badge badge-ai mt-1">⚡ AI ANALYSIS</span>
          </div>
        </div>

        {/* Mixer */}
        <div className="glass-panel">
          <span className="section-title mb-2 text-center">STEM MIXER</span>
          <div className="mixer-grid">
            {['DRUMS', 'BASS', 'OTHER', 'VOCALS'].map((stem) => (
              <div key={stem} className="mixer-channel">
                <span style={{ fontSize: '11px', fontWeight: 'bold', color: '#888' }}>{stem}</span>
                <input type="text" className="input-dark" defaultValue="80" style={{ width: '45px', textAlign: 'center', padding: '4px' }} />
                <div className="vertical-slider-container">
                  <input type="range" className="vertical-slider" min="0" max="100" defaultValue="80" />
                </div>
                <div className="flex gap-1">
                  <button className="toggle-btn toggle-mute">M</button>
                  <button className="toggle-btn toggle-solo">S</button>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Transport (Play controls) */}
        <div className="glass-panel" style={{ marginTop: 'auto' }}>
          <div className="flex items-center gap-2 mb-2">
            <span style={{ fontSize: '12px', color: '#888', width: '40px' }}>00:00</span>
            <input type="range" className="timeline-slider" min="0" max="100" defaultValue="0" />
            <span style={{ fontSize: '12px', color: '#888', width: '40px', textAlign: 'right' }}>03:45</span>
          </div>
          <div className="flex justify-center items-center gap-2">
            <button className="btn-icon" style={{ fontSize: '20px' }}>🔁</button>
            <button className="btn-icon" style={{ fontSize: '24px' }}>«</button>
            <button 
              className="btn-accent" 
              style={{ width: '60px', height: '60px', borderRadius: '50%', fontSize: '24px', padding: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}
              onClick={() => setIsPlaying(!isPlaying)}
            >
              {isPlaying ? '⏸' : '▶'}
            </button>
            <button className="btn-icon" style={{ fontSize: '24px' }}>»</button>
            <button className="btn-primary ml-4">💾 EXPORT MIX</button>
          </div>
        </div>
      </div>

      {/* COLUMN 4: SECTIONS & LYRICS */}
      <div className="col-sections">
        {/* Sections */}
        <div className="glass-panel" style={{ flex: 1, maxHeight: '50%' }}>
          <div className="flex justify-between items-center mb-2">
            <span className="section-title" style={{ marginBottom: 0 }}>SONG STRUCTURE</span>
          </div>
          <div style={{ overflowY: 'auto', flex: 1, paddingRight: '5px' }}>
            {structure?.sections ? (
                structure.sections.map((sec, idx) => (
                    <div key={idx} className="glass-panel-inner mb-1 flex justify-between items-center cursor-pointer hover:border-cyan">
                        <span style={{ color: 'white', fontWeight: 'bold', fontSize: '12px' }}>{sec.label}</span>
                        <span style={{ color: 'var(--neon-cyan)', fontSize: '10px' }}>
                            {Math.floor(sec.start / 60)}:{(sec.start % 60).toFixed(0).padStart(2, '0')}
                        </span>
                    </div>
                ))
            ) : (
                <div style={{ fontSize: '12px', color: '#555', textAlign: 'center', marginTop: '20px' }}>
                    {loading ? "Analyzing structure..." : "No structure data"}
                </div>
            )}
          </div>
        </div>
        
        {/* Lyrics */}
        <div className="glass-panel" style={{ flex: 1 }}>
          <div className="flex justify-between items-center mb-2">
            <span className="section-title" style={{ marginBottom: 0 }}>LYRICS</span>
            <button className="btn-icon">📝</button>
          </div>
          <div style={{ overflowY: 'auto', flex: 1, paddingRight: '5px' }}>
            {result?.lyrics ? (
                result.lyrics.map((seg, idx) => (
                    <p key={idx} style={{ color: '#fff', margin: '5px 0', fontSize: '14px' }}>
                        <span style={{ color: '#555', fontSize: '10px', marginRight: '5px' }}>[{seg.start.toFixed(1)}s]</span>
                        {seg.text}
                    </p>
                ))
            ) : (
                <div style={{ fontSize: '12px', color: '#555', textAlign: 'center', marginTop: '20px' }}>
                    {loading ? "Transcribing lyrics..." : "No lyrics data"}
                </div>
            )}
          </div>
        </div>
      </div>

    </div>
  );
}
