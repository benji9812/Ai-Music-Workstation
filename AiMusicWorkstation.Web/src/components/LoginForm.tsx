import React, { useState } from "react";
import { useAuthStore } from "../store/authStore";

type LoginFormProps = {
  onSuccess: () => void;
  onSwitchToRegister: () => void;
};

export function LoginForm({ onSuccess, onSwitchToRegister }: LoginFormProps) {
  const signIn = useAuthStore((state) => state.signIn);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    const result = await signIn(email, password);
    if (result.error) {
      setError(result.error);
      setLoading(false);
      return;
    }
    setLoading(false);
    onSuccess();
  };

  return (
    <div
      className="auth-container glass-panel"
      style={{ maxWidth: "400px", margin: "40px auto", padding: "30px" }}
    >
      <h2 className="section-title" style={{ textAlign: "center" }}>
        LOG IN
      </h2>
      <form onSubmit={handleSubmit} className="flex flex-col gap-4 mt-4">
        <div className="flex flex-col gap-1">
          <label style={{ fontSize: "12px", color: "#ccc" }}>Email</label>
          <input
            className="input-dark"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>
        <div className="flex flex-col gap-1">
          <label style={{ fontSize: "12px", color: "#ccc" }}>Password</label>
          <input
            className="input-dark"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>
        {error && <div style={{ color: "#ff6666", fontSize: "12px" }}>{error}</div>}
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Processing..." : "Login"}
        </button>
      </form>
      <div style={{ textAlign: "center", marginTop: "16px", fontSize: "14px" }}>
        <button
          onClick={onSwitchToRegister}
          style={{
            background: "none",
            border: "none",
            color: "var(--neon-cyan)",
            cursor: "pointer",
            textDecoration: "underline",
          }}
        >
          Don't have an account? Sign up
        </button>
      </div>
    </div>
  );
}
