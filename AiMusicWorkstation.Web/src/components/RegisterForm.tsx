import React, { useState } from "react";
import { useAuthStore } from "../store/authStore";

type RegisterFormProps = {
  onSuccess: () => void;
  onSwitchToLogin: () => void;
};

export function RegisterForm({ onSuccess, onSwitchToLogin }: RegisterFormProps) {
  const signUp = useAuthStore((state) => state.signUp);
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setNotice(null);
    const result = await signUp(email, password, displayName);
    if (result.error) {
      setError(result.error);
      setLoading(false);
      return;
    }
    if (result.needsEmailConfirmation) {
      setNotice("Check your email to confirm your account.");
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
        SIGN UP
      </h2>
      <form onSubmit={handleSubmit} className="flex flex-col gap-4 mt-4">
        <div className="flex flex-col gap-1">
          <label style={{ fontSize: "12px", color: "#ccc" }}>
            Display Name
          </label>
          <input
            className="input-dark"
            type="text"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            required
          />
        </div>
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
        {notice && <div style={{ color: "#8be9fd", fontSize: "12px" }}>{notice}</div>}
        <button className="btn-primary" type="submit" disabled={loading}>
          {loading ? "Processing..." : "Register"}
        </button>
      </form>
      <div style={{ textAlign: "center", marginTop: "16px", fontSize: "14px" }}>
        <button
          onClick={onSwitchToLogin}
          style={{
            background: "none",
            border: "none",
            color: "var(--neon-cyan)",
            cursor: "pointer",
            textDecoration: "underline",
          }}
        >
          Already have an account? Log in
        </button>
      </div>
    </div>
  );
}
