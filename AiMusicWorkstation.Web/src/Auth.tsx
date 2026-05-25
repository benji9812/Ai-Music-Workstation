import React, { useState } from 'react';
import { supabase } from './supabaseClient';

export function Auth({ onSession }: { onSession: (session: any) => void }) {
  const [loading, setLoading] = useState(false);
  const [isRegister, setIsRegister] = useState(false);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleAuth = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      if (isRegister) {
        const { data, error: signUpError } = await supabase.auth.signUp({
          email,
          password,
          options: {
            data: {
              display_name: displayName,
            },
          },
        });
        if (signUpError) throw signUpError;
        if (data.session) onSession(data.session);
        else alert('Check your email for the confirmation link!');
      } else {
        const { data, error: signInError } = await supabase.auth.signInWithPassword({
          email,
          password,
        });
        if (signInError) throw signInError;
        onSession(data.session);
      }
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-container glass-panel" style={{ maxWidth: '400px', margin: '100px auto', padding: '30px' }}>
      <h2 className="section-title" style={{ textAlign: 'center', marginBottom: '20px' }}>
        {isRegister ? 'SIGN UP' : 'LOG IN'}
      </h2>
      <form onSubmit={handleAuth} className="flex flex-col gap-4">
        {isRegister && (
          <div className="flex flex-col gap-1">
            <label style={{ fontSize: '12px', color: '#ccc' }}>Display Name</label>
            <input
              className="input-dark"
              type="text"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              required
            />
          </div>
        )}
        <div className="flex flex-col gap-1">
          <label style={{ fontSize: '12px', color: '#ccc' }}>Email</label>
          <input
            className="input-dark"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>
        <div className="flex flex-col gap-1">
          <label style={{ fontSize: '12px', color: '#ccc' }}>Password</label>
          <input
            className="input-dark"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>
        {error && <div style={{ color: '#ff6666', fontSize: '12px' }}>{error}</div>}
        <button className="btn-primary" type="submit" disabled={loading} style={{ marginTop: '10px' }}>
          {loading ? 'Processing...' : isRegister ? 'Register' : 'Login'}
        </button>
      </form>
      <div style={{ textAlign: 'center', marginTop: '20px', fontSize: '14px' }}>
        <button
          onClick={() => setIsRegister(!isRegister)}
          style={{ background: 'none', border: 'none', color: 'var(--neon-cyan)', cursor: 'pointer', textDecoration: 'underline' }}
        >
          {isRegister ? 'Already have an account? Log in' : "Don't have an account? Sign up"}
        </button>
      </div>
    </div>
  );
}
