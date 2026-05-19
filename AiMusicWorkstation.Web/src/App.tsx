import { useState } from 'react';
import './index.css';

export default function App() {
  const [activeTab, setActiveTab] = useState<'chord' | 'scale'>('chord');
  const [isPlaying, setIsPlaying] = useState(false);

  // Mock data for UI presentation
  const projects = [
    { title: "Cyberpunk Beat 1", artist: "Unknown", bpm: 124, key: "Am", genre: "Synthwave" },
    { title: "Neon Nights", artist: "Benji", bpm: 95, key: "C", genre: "Lofi" },
  ];

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
                Am
              </div>
              <div className="glass-panel-inner mt-2 w-full text-center">
                <div style={{ color: '#888', fontSize: '14px', marginBottom: '5px' }}>Upcoming</div>
                <div style={{ fontSize: '18px', fontWeight: 'bold' }}>Fmaj7</div>
              </div>
            </div>
          )}

          {activeTab === 'scale' && (
            <div className="flex flex-col items-center justify-center" style={{ flex: 1 }}>
              <div className="flex gap-2 mb-2 w-full">
                <button className="btn-primary w-full" style={{ fontSize: '10px' }}>Penta</button>
                <button className="btn-primary w-full" style={{ fontSize: '10px', borderColor: 'rgba(255,255,255,0.2)', color: '#aaa' }}>Maj/Min</button>
              </div>
              <div style={{ fontSize: '20px', fontWeight: 'bold', color: 'var(--neon-magenta)' }}>A Minor Scale</div>
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

        {/* Import Section */}
        <div className="glass-panel">
          <div className="flex gap-2 mb-2">
            <input type="text" className="input-dark" placeholder="Paste YouTube or Spotify Link here..." />
            <button className="btn-accent" style={{ background: 'rgba(255,0,0,0.1)', color: '#ff4444', borderColor: '#ff4444' }}>
              ⬇ DOWNLOAD
            </button>
          </div>
          <button className="btn-primary" style={{ padding: '12px' }}>
            📂 OPEN LOCAL AUDIO FILE
          </button>
          <div className="text-center mt-2" style={{ fontSize: '12px', color: '#888' }}>Ready</div>
        </div>

        {/* Analysis Data (Tempo, Key, Transpose) */}
        <div className="flex gap-2">
          <div className="glass-panel flex-col items-center justify-center" style={{ flex: 1 }}>
            <span className="section-title">TEMPO</span>
            <span className="highlight-cyan" style={{ fontSize: '40px', fontWeight: 'bold' }}>124</span>
            <span className="badge badge-ai mt-1">⚡ AI ANALYSIS</span>
          </div>
          <div className="glass-panel flex-col items-center justify-center" style={{ flex: 1 }}>
            <span className="section-title">TIME SIG</span>
            <span className="highlight-cyan" style={{ fontSize: '40px', fontWeight: 'bold' }}>4/4</span>
            <span className="badge badge-ai mt-1">⚡ AI ANALYSIS</span>
          </div>
          <div className="glass-panel flex-col items-center justify-center" style={{ flex: 1 }}>
            <span className="section-title">KEY</span>
            <span className="highlight-cyan" style={{ fontSize: '40px', fontWeight: 'bold' }}>Am</span>
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
        <div className="glass-panel" style={{ flex: 1 }}>
          <div className="flex justify-between items-center mb-2">
            <span className="section-title" style={{ marginBottom: 0 }}>SONG STRUCTURE</span>
          </div>
          <div style={{ overflowY: 'auto', flex: 1, paddingRight: '5px' }}>
            {['Intro', 'Verse 1', 'Chorus', 'Verse 2', 'Chorus', 'Bridge', 'Outro'].map((sec, idx) => (
              <div key={idx} className="glass-panel-inner mb-1 flex justify-between items-center cursor-pointer hover:border-cyan">
                <span style={{ color: 'white', fontWeight: 'bold', fontSize: '12px' }}>{sec}</span>
                <span style={{ color: 'var(--neon-cyan)', fontSize: '10px' }}>00:{10 + idx*15}</span>
              </div>
            ))}
          </div>
        </div>
        
        {/* Lyrics */}
        <div className="glass-panel" style={{ flex: 1 }}>
          <div className="flex justify-between items-center mb-2">
            <span className="section-title" style={{ marginBottom: 0 }}>LYRICS</span>
            <button className="btn-icon">📝</button>
          </div>
          <div style={{ overflowY: 'auto', flex: 1, paddingRight: '5px' }}>
            <p style={{ color: '#fff', fontWeight: 'bold', margin: '5px 0' }}>Welcome to the neon lights</p>
            <p style={{ color: '#555', margin: '5px 0' }}>Walking through the cyber city</p>
            <p style={{ color: '#555', margin: '5px 0' }}>Where the future is tonight</p>
            <p style={{ color: '#555', margin: '5px 0' }}>And everything is electric...</p>
          </div>
        </div>
      </div>

    </div>
  );
}
