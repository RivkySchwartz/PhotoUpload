import React, { useEffect, useState, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft, Download, Copy, Star, RefreshCw } from 'lucide-react';
import {
  getGallery, getAdminPhotos, getAdminSelections, deletePhoto,
  updateGalleryStatus, downloadSelections, updatePhotoOrder, rotatePhoto,
  generatePhotoPreview, getAdminPrintOrders, regeneratePreviews
} from '../../api/galleries';
import AdminSidebar from '../../components/AdminSidebar';
import PhotoCard from '../../components/PhotoCard';
import UploadZone from '../../components/UploadZone';
import { AnimatePresence } from 'framer-motion';
import Lightbox from '../../components/Lightbox';
import { useAuth } from '../../contexts/AuthContext';
import type { Gallery, Photo, Selection, PrintOrderDto } from '../../types';
import toast from 'react-hot-toast';

export default function GalleryDetailPage() {
  const { id } = useParams<{ id: string }>();
  const galleryId = Number(id);
  const { role } = useAuth();

  const [gallery, setGallery] = useState<Gallery | null>(null);
  const [photos, setPhotos] = useState<Photo[]>([]);
  const [selections, setSelections] = useState<Selection[]>([]);
  const [printOrders, setPrintOrders] = useState<PrintOrderDto[]>([]);
  const [lightboxIdx, setLightboxIdx] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [tab, setTab] = useState<'photos' | 'selections'>('photos');
  const [downloading, setDownloading] = useState(false);
  const [fixingRotation, setFixingRotation] = useState(false);
  const [statusChanging, setStatusChanging] = useState(false);

  const canUpload = role === 'Admin' || role === 'Photographer';
  const canDeletePhoto = role === 'Admin' || role === 'Photographer';
  const canChangeStatus = role === 'Admin' || role === 'Editor';

  const STATUS_OPTIONS = [
    { value: 0, label: 'Pending Review' },
    { value: 1, label: 'Selections Made' },
    { value: 2, label: 'Editing' },
    { value: 3, label: 'Complete' },
  ] as const;

  const STATUS_MAP: Record<string, number> = {
    Pending: 0, SelectionsMade: 1, Editing: 2, Complete: 3,
  };

  const load = useCallback(async () => {
    const [g, p, s, po] = await Promise.all([
      getGallery(galleryId),
      getAdminPhotos(galleryId),
      getAdminSelections(galleryId),
      getAdminPrintOrders(galleryId),
    ]);
    setGallery(g);
    setPhotos(p);
    setSelections(s);
    setPrintOrders(po);
    setLoading(false);

    // Editor opens a SelectionsMade gallery → auto-advance to Editing
    if (role === 'Editor' && g.status === 'SelectionsMade') {
      try {
        const updated = await updateGalleryStatus(galleryId, STATUS_MAP['Editing']);
        setGallery(updated);
        toast.success('Status advanced to Editing');
      } catch {
        // non-critical; don't block the page
      }
    }

    // Auto-generate previews for any photos still missing one (background, bounded concurrency).
    // Each photo is its own file, so there's no cross-photo race — the server already
    // handles same-file races safely, so workers can run in parallel.
    const missing = p.filter(photo => !photo.previewUrl);
    if (missing.length > 0) {
      let idx = 0;
      const processNext = async (): Promise<void> => {
        if (idx >= missing.length) return;
        const photo = missing[idx++];
        try {
          const updated = await generatePhotoPreview(galleryId, photo.id);
          setPhotos(prev => prev.map(ph => ph.id === updated.id ? updated : ph));
        } catch {}
        await processNext();
      };
      // Fire-and-forget — a handful of parallel workers drain the queue
      const WORKERS = 4;
      Array.from({ length: Math.min(WORKERS, missing.length) }, () => processNext());
    }
  }, [galleryId, role]);

  useEffect(() => { load(); }, [load]);

  const handleUploaded = (newPhotos: Photo[]) => {
    setPhotos(prev => [...prev, ...newPhotos]);
    // Second call: generate preview per photo — runs separately, updates grid when ready
    newPhotos.forEach(photo => {
      generatePhotoPreview(galleryId, photo.id)
        .then(updated => setPhotos(prev => prev.map(p => p.id === updated.id ? updated : p)))
        .catch(() => {});
    });
  };

  const handleDeletePhoto = async (photoId: number) => {
    if (!confirm('Delete this photo?')) return;
    try {
      await deletePhoto(galleryId, photoId);
      setPhotos(prev => prev.filter(p => p.id !== photoId));
      toast.success('Photo deleted');
    } catch {
      toast.error('Failed to delete photo');
    }
  };

  const handleRotatePhoto = async (photoId: number) => {
    try {
      const updated = await rotatePhoto(galleryId, photoId);
      setPhotos(prev => prev.map(p => p.id === photoId ? { ...p, rotation: updated.rotation } : p));
    } catch {
      toast.error('Failed to rotate photo');
    }
  };

  const handleDownload = async () => {
    if (!gallery) return;
    setDownloading(true);
    try {
      await downloadSelections(galleryId, gallery.name, gallery.clientName);
    } catch {
      toast.error('Download failed. Make sure there are selections to download.');
    } finally {
      setDownloading(false);
    }
  };

  const handleFixRotation = async () => {
    if (!confirm('Regenerate thumbnails and previews for every photo in this gallery? Useful after a processing fix — can take a while for large RAW galleries.')) return;
    setFixingRotation(true);
    try {
      const { fixedCount, total } = await regeneratePreviews(galleryId, true);
      setPhotos(await getAdminPhotos(galleryId));
      toast.success(`Regenerated ${fixedCount} of ${total} photos`);
    } catch {
      toast.error('Failed to regenerate previews');
    } finally {
      setFixingRotation(false);
    }
  };

  const handleStatusChange = async (newValue: number) => {
    if (!gallery) return;
    setStatusChanging(true);
    try {
      const updated = await updateGalleryStatus(galleryId, newValue);
      setGallery(updated);
      toast.success('Status updated');
    } catch {
      toast.error('Failed to update status');
    } finally {
      setStatusChanging(false);
    }
  };

  const copyLink = () => {
    if (!gallery) return;
    const link = `${window.location.origin}/gallery/${gallery.uniqueToken}`;
    navigator.clipboard.writeText(link).then(() => toast.success('Link copied!'));
  };

  const selectedPhotoIds = new Set(selections.map(s => s.photoId));

  if (loading) return (
    <div className="admin-layout">
      <AdminSidebar />
      <main className="admin-main"><div className="loading-spinner" /></main>
    </div>
  );

  if (!gallery) return (
    <div className="admin-layout">
      <AdminSidebar />
      <main className="admin-main"><p>Gallery not found.</p></main>
    </div>
  );

  return (
    <div className="admin-layout">
      <AdminSidebar />
      <main className="admin-main">
        {/* Back */}
        <Link to="/admin" className="back-link"><ArrowLeft size={16} /> All Galleries</Link>

        {/* Header */}
        <div className="admin-header">
          <div>
            <h1>{gallery.name}</h1>
            <div className="gallery-meta">
              <span>{gallery.clientName}</span>
              {gallery.clientEmail && <span className="text-muted">{gallery.clientEmail}</span>}
            </div>
          </div>
          <div className="action-btns">
            {canChangeStatus ? (
              <select
                className="status-select"
                value={STATUS_MAP[gallery.status]}
                disabled={statusChanging}
                onChange={e => handleStatusChange(Number(e.target.value))}
              >
                {STATUS_OPTIONS.map(o => (
                  <option key={o.value} value={o.value}>{o.label}</option>
                ))}
              </select>
            ) : (
              <span className="status-select status-select--readonly">
                {STATUS_OPTIONS.find(o => o.value === STATUS_MAP[gallery.status])?.label}
              </span>
            )}
            <button className="btn btn--ghost" onClick={copyLink}><Copy size={15} /> Copy Link</button>
            <button className="btn btn--ghost" onClick={handleFixRotation} disabled={fixingRotation}>
              <RefreshCw size={15} /> {fixingRotation ? 'Fixing…' : 'Fix Rotation'}
            </button>
            {selections.length > 0 && (
              <button className="btn btn--ghost" onClick={handleDownload} disabled={downloading}>
                <Download size={15} /> {downloading ? 'Downloading…' : 'Download ZIP'}
              </button>
            )}
          </div>
        </div>

        {/* Stats */}
        <div className="stats-row">
          <div className="stat-card">
            <div className="stat-card__value">{gallery.photoCount}</div>
            <div className="stat-card__label">Photos</div>
          </div>
          <div className="stat-card">
            <div className="stat-card__value">{gallery.selectionCount} / {gallery.maxSelections}</div>
            <div className="stat-card__label">Selected</div>
          </div>
        </div>

        {/* Upload zone — Photographer and Admin only */}
        {canUpload && <UploadZone galleryId={galleryId} onUploaded={handleUploaded} />}

        {/* Tabs */}
        <div className="tabs">
          <button className={`tab ${tab === 'photos' ? 'tab--active' : ''}`} onClick={() => setTab('photos')}>
            All Photos ({photos.length})
          </button>
          <button className={`tab ${tab === 'selections' ? 'tab--active' : ''}`} onClick={() => setTab('selections')}>
            <Star size={14} /> Client Selections ({selections.length})
          </button>
        </div>

        {tab === 'photos' && (
          photos.length === 0 ? (
            <p className="empty-state">No photos yet. {canUpload ? 'Upload some above.' : ''}</p>
          ) : (
            <div className="photo-grid">
              {photos.map((p, i) => (
                <PhotoCard
                  key={p.id}
                  photo={p}
                  isSelected={selectedPhotoIds.has(p.id)}
                  atMax={false}
                  onToggleSelect={() => {}}
                  onOpenLightbox={() => setLightboxIdx(i)}
                  isAdmin
                  onDelete={canDeletePhoto ? handleDeletePhoto : undefined}
                  onRotate={canDeletePhoto ? handleRotatePhoto : undefined}
                />
              ))}
            </div>
          )
        )}

        {tab === 'selections' && (
          selections.length === 0 ? (
            <p className="empty-state">No selections submitted yet.</p>
          ) : (
            <>
              <div className="photo-grid">
                {selections.map(s => {
                  const photo = photos.find(p => p.id === s.photoId);
                  if (!photo) return null;
                  return (
                    <PhotoCard
                      key={s.id}
                      photo={photo}
                      isSelected={true}
                      atMax={false}
                      onToggleSelect={() => {}}
                      onOpenLightbox={() => setLightboxIdx(photos.indexOf(photo))}
                      isAdmin
                      onDelete={undefined}
                    />
                  );
                })}
              </div>

              {printOrders.length > 0 && (
                <div className="print-orders-panel">
                  <h3 className="print-orders-panel__title">🖨️ Print Order ({printOrders.reduce((s, p) => s + p.quantity, 0)} items)</h3>
                  <div className="print-orders-list">
                    {printOrders.map(order => (
                      <div key={order.id} className="print-order-row">
                        <img src={order.thumbnailUrl} alt={order.fileName} className="print-order-row__thumb" />
                        <div className="print-order-row__info">
                          <span className="print-order-row__file">{order.fileName}</span>
                          <span className="print-order-row__size">{order.size.replace('x', ' × ')}" print</span>
                        </div>
                        <span className="print-order-row__qty">×{order.quantity}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </>
          )
        )}

        {/* Lightbox */}
        <AnimatePresence>
          {lightboxIdx !== null && (
            <Lightbox
              photos={photos}
              currentIndex={lightboxIdx}
              selectedIds={selectedPhotoIds}
              maxSelections={gallery.maxSelections}
              onClose={() => setLightboxIdx(null)}
              onNavigate={setLightboxIdx}
              onToggleSelect={() => {}}
              onRotate={canDeletePhoto ? handleRotatePhoto : undefined}
            />
          )}
        </AnimatePresence>
      </main>
    </div>
  );
}
