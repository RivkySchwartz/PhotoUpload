import React, { useEffect, useState } from 'react';
import { Plus, Trash2, KeyRound, Copy, RefreshCw } from 'lucide-react';
import { getUsers, createUser, deleteUser } from '../../api/galleries';
import AdminSidebar from '../../components/AdminSidebar';
import type { UserDto, UserRole } from '../../types';
import toast from 'react-hot-toast';

const ROLES: UserRole[] = ['Admin', 'Photographer', 'Editor', 'Secretary'];

const ROLE_COLORS: Record<UserRole, string> = {
  Admin:        'badge badge--complete',
  Photographer: 'badge badge--pending',
  Editor:       'badge badge--editing',
  Secretary:    'badge badge--selections',
};

const CHARSET = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$';

function generatePassword(length = 12): string {
  return Array.from({ length }, () => CHARSET[Math.floor(Math.random() * CHARSET.length)]).join('');
}

const BLANK = { username: '', role: 'Photographer' as UserRole };

export default function UsersPage() {
  const [users, setUsers] = useState<UserDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState(BLANK);
  const [tempPassword, setTempPassword] = useState('');
  const [creating, setCreating] = useState(false);

  useEffect(() => { load(); }, []);

  const load = async () => {
    try { setUsers(await getUsers()); }
    finally { setLoading(false); }
  };

  const openCreate = () => {
    setForm(BLANK);
    setTempPassword(generatePassword());
    setShowCreate(true);
  };

  const copyPassword = () => {
    navigator.clipboard.writeText(tempPassword).then(() => toast.success('Password copied!'));
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreating(true);
    try {
      const user = await createUser({ ...form, temporaryPassword: tempPassword });
      setUsers(prev => [...prev, user]);
      setShowCreate(false);
      toast.success(`User "${user.username}" created.`);
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Failed to create user');
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (user: UserDto) => {
    if (!confirm(`Delete user "${user.username}"? This cannot be undone.`)) return;
    try {
      await deleteUser(user.id);
      setUsers(prev => prev.filter(u => u.id !== user.id));
      toast.success('User deleted');
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Failed to delete user');
    }
  };

  return (
    <div className="admin-layout">
      <AdminSidebar />
      <main className="admin-main">
        <div className="admin-header">
          <h1>Users</h1>
          <button className="btn btn--primary" onClick={openCreate}>
            <Plus size={16} /> New User
          </button>
        </div>

        {showCreate && (
          <div className="modal-overlay" onClick={() => setShowCreate(false)}>
            <div className="modal" onClick={e => e.stopPropagation()}>
              <h2>New User</h2>
              <form onSubmit={handleCreate} className="create-form">
                <div className="form-row">
                  <div className="form-group">
                    <label>Username *</label>
                    <input
                      required
                      value={form.username}
                      onChange={e => setForm(p => ({ ...p, username: e.target.value }))}
                    />
                  </div>
                  <div className="form-group">
                    <label>Role *</label>
                    <select
                      value={form.role}
                      onChange={e => setForm(p => ({ ...p, role: e.target.value as UserRole }))}
                    >
                      {ROLES.map(r => <option key={r} value={r}>{r}</option>)}
                    </select>
                  </div>
                </div>

                <div className="form-group">
                  <label>Temporary Password</label>
                  <div className="temp-password-box">
                    <code className="temp-password-value">{tempPassword}</code>
                    <div className="temp-password-actions">
                      <button type="button" className="icon-btn" title="Copy password" onClick={copyPassword}>
                        <Copy size={14} />
                      </button>
                      <button type="button" className="icon-btn" title="Regenerate" onClick={() => setTempPassword(generatePassword())}>
                        <RefreshCw size={14} />
                      </button>
                    </div>
                  </div>
                  <span className="form-hint">Auto-generated. Copy and share it — the user must change it on first login.</span>
                </div>

                <div className="modal-actions">
                  <button type="button" className="btn btn--ghost" onClick={() => setShowCreate(false)}>Cancel</button>
                  <button type="submit" className="btn btn--primary" disabled={creating}>
                    {creating ? 'Creating…' : 'Create User'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {loading ? (
          <div className="loading-grid">
            {[...Array(4)].map((_, i) => <div key={i} className="skeleton-row" />)}
          </div>
        ) : (
          <div className="gallery-table-wrapper">
            <table className="gallery-table">
              <thead>
                <tr>
                  <th>Username</th>
                  <th>Role</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {users.map(u => (
                  <tr key={u.id}>
                    <td>{u.username}</td>
                    <td><span className={ROLE_COLORS[u.role]}>{u.role}</span></td>
                    <td>
                      {u.mustChangePassword
                        ? <span className="user-status user-status--temp"><KeyRound size={12} /> Temporary password</span>
                        : <span className="user-status user-status--active">Active</span>
                      }
                    </td>
                    <td>
                      <button className="icon-btn icon-btn--danger" title="Delete user" onClick={() => handleDelete(u)}>
                        <Trash2 size={15} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </main>
    </div>
  );
}
