export function Landing({ onGetStarted }: { onGetStarted: () => void }) {
  return (
    <div
      className="landing-container"
      style={{ textAlign: "center", padding: "100px 20px" }}
    >
      <h1
        style={{
          fontSize: "3rem",
          color: "var(--neon-cyan)",
          marginBottom: "20px",
          textShadow: "0 0 10px var(--neon-cyan)",
        }}
      >
        AI MUSIC WORKSTATION
      </h1>
      <p
        style={{
          fontSize: "1.2rem",
          color: "#ccc",
          maxWidth: "600px",
          margin: "0 auto 40px auto",
        }}
      >
        Unlock the secrets of your favorite tracks. Separate stems, analyze
        chords, and master your music with AI-powered tools.
      </p>
      <div className="flex justify-center gap-4">
        <button
          className="btn-primary"
          style={{ padding: "12px 30px", fontSize: "1.1rem" }}
          onClick={onGetStarted}
        >
          Get Started
        </button>
      </div>
      <div
        className="mt-20 grid grid-cols-1 md:grid-cols-3 gap-8 max-w-5xl mx-auto"
        style={{ marginTop: "80px" }}
      >
        <div className="glass-panel p-6">
          <h3 style={{ color: "var(--neon-cyan)", marginBottom: "10px" }}>
            Stem Separation
          </h3>
          <p style={{ fontSize: "14px", color: "#aaa" }}>
            Isolate vocals, drums, bass, and more with high-fidelity AI
            separation.
          </p>
        </div>
        <div className="glass-panel p-6">
          <h3 style={{ color: "var(--neon-cyan)", marginBottom: "10px" }}>
            Chord Analysis
          </h3>
          <p style={{ fontSize: "14px", color: "#aaa" }}>
            Automatically detect chords and song structure in seconds.
          </p>
        </div>
        <div className="glass-panel p-6">
          <h3 style={{ color: "var(--neon-cyan)", marginBottom: "10px" }}>
            Personal Library
          </h3>
          <p style={{ fontSize: "14px", color: "#aaa" }}>
            Save your projects and organize your music in one secure place.
          </p>
        </div>
      </div>
    </div>
  );
}
