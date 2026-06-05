import React, { useEffect, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Plus, Trash2, ExternalLink, Copy } from 'lucide-react';
import { getGalleries, deleteGallery, createGallery } from '../../api/galleries';
import AdminSidebar from '../../components/AdminSidebar';
import StatusBadge from '../../components/StatusBadge';
import { useAuth } from '../../contexts/AuthContext';
import type { Gallery, CreateGalleryRequest, GalleryStatus, UserRole } from '../../types';
import toast from 'react-hot-toast';

const ROLE_ALLOWED_STATUSES: Record<UserRole, GalleryStatus[]> = {
  Admin:        ['Pending', 'SelectionsMade', 'Editing', 'Complete'],
  Photographer: ['Pending'],
  Editor:       ['SelectionsMade', 'Editing'],
  Secretary:    ['Pending', 'Complete'],
};

export default function GalleryListPage() {
  const [galleries, setGalleries] = useState<Gallery[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState<CreateGalleryRequest>({
    name: '', clientName: '', clientEmail: '', maxSelections: 10, password: ''
  });
  const [creating, setCreating] = useState(false);

  const { role } = useAuth();
  const location = useLocation();
  const params = new URLSearchParams(location.search);
  const statusParam = params.get('status') as GalleryStatus | null;

  const canCreate = role === 'Admin' || role === 'Photographer';
  const canDelete = role === 'Admin';

  useEffect(() => { load(); }, []);

  const load = async () => {
    try {
      setGalleries(await getGalleries());
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreating(true);
    try {
      const g = await createGallery({
        ...form,
        password: form.password || undefined,
        clientEmail: form.clientEmail || undefined
      });
      setGalleries(prev => [g, ...prev]);
      setShowCreate(false);
      setForm({ name: '', clientName: '', clientEmail: '', maxSelections: 10, password: '' });

      const link = `${window.location.origin}/gallery/${g.uniqueToken}`;
      await navigator.clipboard.writeText(link).catch(() => {});
      toast.success('Gallery created! Share link copied to clipboard.');
    } catch {
      toast.error('Failed to create gallery');
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (id: number, name: string) => {
    if (!confirm(`Delete gallery "${name}"? This cannot be undone.`)) return;
    try {
      await deleteGallery(id);
      setGalleries(prev => prev.filter(g => g.id !== id));
      toast.success('Gallery deleted');
    } catch {
      toast.error('Failed to delete gallery');
    }
  };

  const copyLink = (token: string) => {
    const link = `${window.location.origin}/gallery/${token}`;
    navigator.clipboard.writeText(link).then(() => toast.success('Link copied!'));
  };

  const allowedStatuses = role ? ROLE_ALLOWED_STATUSES[role] : [];

  const filteredGalleries = galleries.filter(g => {
    if (!allowedStatuses.includes(g.status)) return false;
    if (statusParam && g.status !== statusParam) return false;
    return true;
  });

  return (
    <div className="admin-layout">
      <AdminSidebar />
      <main className="admin-main">
        <div className="admin-header">
          <h1>Galleries</h1>
          {canCreate && (
            <button className="btn btn--primary" onClick={() => setShowCreate(true)}>
              <Plus size={16} /> New Gallery
            </button>
          )}
        </div>

        {/* Create modal */}
        {showCreate && (
          <div className="modal-overlay" onClick={() => setShowCreate(false)}>
            <div className="modal" onClick={e => e.stopPropagation()}>
              <h2>New Gallery</h2>
              <form onSubmit={handleCreate} className="create-form">
                <div className="form-row">
                  <div className="form-group">
                    <label>Gallery Name *</label>
                    <input required value={form.name} onChange={e => setForm(p => ({ ...p, name: e.target.value }))} />
                  </div>
                  <div className="form-group">
                    <label>Client Name *</label>
                    <input required value={form.clientName} onChange={e => setForm(p => ({ ...p, clientName: e.target.value }))} />
                  </div>
                </div>
                <div className="form-row">
                  <div className="form-group">
                    <label>Client Email</label>
                    <input type="email" value={form.clientEmail} onChange={e => setForm(p => ({ ...p, clientEmail: e.target.value }))} />
                  </div>
                  <div className="form-group">
                    <label>Max Selections *</label>
                    <input type="number" min={1} required value={form.maxSelections} onChange={e => setForm(p => ({ ...p, maxSelections: +e.target.value }))} />
                  </div>
                </div>
                <div className="form-group">
                  <label>Password (optional)</label>
                  <input type="password" value={form.password} onChange={e => setForm(p => ({ ...p, password: e.target.value }))} />
                </div>
                <div className="modal-actions">
                  <button type="button" className="btn btn--ghost" onClick={() => setShowCreate(false)}>Cancel</button>
                  <button type="submit" className="btn btn--primary" disabled={creating}>
                    {creating ? 'Creating…' : 'Create Gallery'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {loading ? (
          <div className="loading-grid">
            {[...Array(6)].map((_, i) => <div key={i} className="skeleton-row" />)}
          </div>
        ) : filteredGalleries.length === 0 ? (
          <div className="empty-state">
            <p>{galleries.length === 0 ? 'No galleries yet. Create your first one above.' : 'No galleries match this filter.'}</p>
          </div>
        ) : (
          <div className="gallery-table-wrapper">
            <table className="gallery-table">
              <thead>
                <tr>
                  <th>Gallery</th>
                  <th>Client</th>
                  <th>Photos</th>
                  <th>Selections</th>
                  <th>Status</th>
                  <th>Created</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredGalleries.map(g => (
                  <tr key={g.id}>
                    <td>
                      <Link to={`/admin/galleries/${g.id}`} className="gallery-name-link">
                        {g.name}
                      </Link>
                    </td>
                    <td>
                      <div>{g.clientName}</div>
                      {g.clientEmail && <div className="text-muted text-sm">{g.clientEmail}</div>}
                    </td>
                    <td>{g.photoCount}</td>
                    <td>{g.selectionCount} / {g.maxSelections}</td>
                    <td><StatusBadge status={g.status} /></td>
                    <td className="text-sm">{new Date(g.createdAt).toLocaleDateString()}</td>
                    <td>
                      <div className="action-btns">
                        <button className="icon-btn" title="Copy share link" onClick={() => copyLink(g.uniqueToken)}>
                          <Copy size={15} />
                        </button>
                        <a
                          href={`/gallery/${g.uniqueToken}`}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="icon-btn"
                          title="Open client view"
                        >
                          <ExternalLink size={15} />
                        </a>
                        {canDelete && (
                          <button className="icon-btn icon-btn--danger" title="Delete" onClick={() => handleDelete(g.id, g.name)}>
                            <Trash2 size={15} />
                          </button>
                        )}
                      </div>
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
