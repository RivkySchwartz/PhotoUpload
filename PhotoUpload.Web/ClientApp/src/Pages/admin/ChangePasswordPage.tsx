import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { KeyRound } from 'lucide-react';
import { changePassword } from '../../api/galleries';
import { useAuth } from '../../contexts/AuthContext';
import toast from 'react-hot-toast';

export default function ChangePasswordPage() {
  const { username, clearMustChangePassword } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ newPassword: '', confirm: '' });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    if (form.newPassword !== form.confirm) {
      setError('Passwords do not match.');
      return;
    }
    if (form.newPassword.length < 6) {
      setError('Password must be at least 6 characters.');
      return;
    }
    setSaving(true);
    try {
      await changePassword(form.newPassword);
      clearMustChangePassword();
      toast.success('Password changed! Welcome.');
      navigate('/admin', { replace: true });
    } catch {
      setError('Failed to change password. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-card__brand">
          <KeyRound size={32} />
          <h1>Set Your Password</h1>
          <p>Hi {username}, your account uses a temporary password. Please set a new one to continue.</p>
        </div>
        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-group">
            <label>New Password</label>
            <input
              type="password"
              required
              minLength={6}
              value={form.newPassword}
              onChange={e => setForm(p => ({ ...p, newPassword: e.target.value }))}
            />
          </div>
          <div className="form-group">
            <label>Confirm Password</label>
            <input
              type="password"
              required
              value={form.confirm}
              onChange={e => setForm(p => ({ ...p, confirm: e.target.value }))}
            />
          </div>
          {error && <p className="error-text">{error}</p>}
          <button type="submit" className="btn btn--primary btn--full" disabled={saving}>
            {saving ? 'Saving…' : 'Set Password & Continue'}
          </button>
        </form>
      </div>
    </div>
  );
}
