import React, { useEffect, useState, useCallback, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Star, AlertCircle, Lock, Sparkles, ShoppingCart, Printer, ChevronRight, ChevronLeft, X, Plus, Minus } from 'lucide-react';
import { AnimatePresence, motion } from 'framer-motion';
import {
  getPublicGallery, verifyGalleryPassword,
  getPublicPhotos, submitSelections, rotatePhotoPublic
} from '../../api/galleries';
import PhotoCard from '../../components/PhotoCard';
import Lightbox from '../../components/Lightbox';
import type { GalleryPublic, Photo, PrintItem } from '../../types';
import toast from 'react-hot-toast';

type PageState = 'loading' | 'password' | 'gallery' | 'prints' | 'cart' | 'submitting' | 'error';

const PRINT_SIZES = [
  { id: '4x6',   label: '4 × 6"',   note: 'Standard' },
  { id: '5x7',   label: '5 × 7"',   note: 'Medium' },
  { id: '8x10',  label: '8 × 10"',  note: 'Large' },
  { id: '11x14', label: '11 × 14"', note: 'X-Large' },
  { id: '16x20', label: '16 × 20"', note: 'Poster' },
];

export default function GalleryPage() {
  const { token } = useParams<{ token: string }>();
  const navigate = useNavigate();

  const [pageState, setPageState] = useState<PageState>('loading');
  const [gallery, setGallery] = useState<GalleryPublic | null>(null);
  const [photos, setPhotos] = useState<Photo[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [lightboxIdx, setLightboxIdx] = useState<number | null>(null);
  const [password, setPassword] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [upgraded, setUpgraded] = useState(false);
  const [showUpgradePrompt, setShowUpgradePrompt] = useState(false);
  const [pendingPhoto, setPendingPhoto] = useState<Photo | null>(null);

  // Print cart
  const [printItems, setPrintItems] = useState<PrintItem[]>([]);
  const [printModalPhotoId, setPrintModalPhotoId] = useState<number | null>(null);

  useEffect(() => {
    if (!token) { setPageState('error'); return; }
    getPublicGallery(token)
      .then(g => {
        setGallery(g);
        if (g.isPasswordProtected) setPageState('password');
        else loadPhotos();
      })
      .catch(() => setPageState('error'));
  }, [token]);

  const loadPhotos = useCallback(async () => {
    if (!token) return;
    try {
      setPhotos(await getPublicPhotos(token));
      setPageState('gallery');
    } catch { setPageState('error'); }
  }, [token]);

  // Freshly uploaded RAW/HEIC photos may still be generating a preview server-side.
  // Poll briefly so they appear automatically instead of staying broken until a manual refresh.
  const previewPollAttempts = useRef(0);
  useEffect(() => {
    if (pageState !== 'gallery' || !token) return;
    if (!photos.some(p => !p.previewUrl)) { previewPollAttempts.current = 0; return; }
    if (previewPollAttempts.current >= 15) return; // ~1 minute of retries, then give up automatically

    const timer = setTimeout(() => {
      previewPollAttempts.current += 1;
      getPublicPhotos(token).then(setPhotos).catch(() => {});
    }, 4000);

    return () => clearTimeout(timer);
  }, [pageState, photos, token]);

  const handlePasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setPasswordError('');
    try {
      await verifyGalleryPassword(token!, password);
      await loadPhotos();
    } catch { setPasswordError('Incorrect password. Please try again.'); }
  };

  const toggleSelect = useCallback((photo: Photo) => {
    setSelectedIds(prev => {
      if (prev.has(photo.id)) {
        const next = new Set(prev); next.delete(photo.id); return next;
      }
      const max = gallery?.maxSelections ?? 0;
      if (prev.size < max) { const next = new Set(prev); next.add(photo.id); return next; }
      if (upgraded) { const next = new Set(prev); next.add(photo.id); return next; }
      setPendingPhoto(photo); setShowUpgradePrompt(true); return prev;
    });
  }, [gallery, upgraded]);

  const handleUpgradeConfirm = () => {
    setUpgraded(true); setShowUpgradePrompt(false);
    if (pendingPhoto) { setSelectedIds(prev => new Set(prev).add(pendingPhoto.id)); setPendingPhoto(null); }
  };

  const handleRotate = async (photoId: number) => {
    if (!token) return;
    const { rotation } = await rotatePhotoPublic(token, photoId);
    setPhotos(prev => prev.map(p => p.id === photoId ? { ...p, rotation } : p));
  };

  // ── Print cart ────────────────────────────────────────────────────────────

  const getPrintItem = (photoId: number, size: string) =>
    printItems.find(p => p.photoId === photoId && p.size === size);

  const setPrintQty = (photoId: number, size: string, qty: number) => {
    if (qty <= 0) {
      setPrintItems(prev => prev.filter(p => !(p.photoId === photoId && p.size === size)));
    } else {
      setPrintItems(prev => {
        const exists = prev.find(p => p.photoId === photoId && p.size === size);
        if (exists) return prev.map(p => p.photoId === photoId && p.size === size ? { ...p, quantity: qty } : p);
        return [...prev, { photoId, size, quantity: qty }];
      });
    }
  };

  // ── Submit ────────────────────────────────────────────────────────────────

  const handleSubmit = async () => {
    if (!token) return;
    setPageState('submitting');
    try {
      const res = await submitSelections(token, Array.from(selectedIds), printItems);
      navigate(`/gallery/${token}/collage`, {
        state: {
          selections: res.selections,
          collageUrl: res.collageUrl,
          emailSent: res.emailSent,
          clientName: gallery?.clientName ?? '',
          galleryName: gallery?.name ?? '',
        },
        replace: true,
      });
    } catch (err: any) {
      toast.error(err?.response?.data?.message ?? 'Submission failed. Please try again.');
      setPageState('cart');
    }
  };

  const max = gallery?.maxSelections ?? 0;
  const extraCount = Math.max(0, selectedIds.size - max);
  const atHardLimit = !upgraded && selectedIds.size >= max;
  const selectedPhotos = photos.filter(p => selectedIds.has(p.id));
  const printModalPhoto = printModalPhotoId !== null ? photos.find(p => p.id === printModalPhotoId) : null;

  // ── Loading / error / password states ─────────────────────────────────────

  if (pageState === 'loading') return (
    <div className="gallery-loading"><div className="loading-spinner large" /></div>
  );
  if (pageState === 'error') return (
    <div className="gallery-error"><AlertCircle size={48} /><h2>Gallery not found</h2></div>
  );
  if (pageState === 'password') return (
    <div className="password-gate">
      <div className="password-card">
        <Lock size={36} />
        <h2>Password Protected Gallery</h2>
        <p>Enter the password provided by your photographer.</p>
        <form onSubmit={handlePasswordSubmit}>
          <input type="password" value={password} onChange={e => setPassword(e.target.value)}
            placeholder="Gallery password" autoFocus required />
          {passwordError && <p className="error-text">{passwordError}</p>}
          <button type="submit" className="btn btn--primary btn--full">Enter Gallery</button>
        </form>
      </div>
    </div>
  );

  // ── Step 2: Print add-ons ─────────────────────────────────────────────────

  if (pageState === 'prints') return (
    <div className="gallery-page">
      <div className="step-header">
        <button className="step-back" onClick={() => setPageState('gallery')}>
          <ChevronLeft size={18} /> Back to Selection
        </button>
        <div className="step-indicator">
          <span className="step-dot step-dot--done">1</span>
          <span className="step-line" />
          <span className="step-dot step-dot--active">2</span>
          <span className="step-line" />
          <span className="step-dot">3</span>
        </div>
        <button className="btn btn--primary" onClick={() => setPageState('cart')}>
          View Cart <ShoppingCart size={16} />
          {printItems.length > 0 && <span className="cart-badge">{printItems.reduce((s, p) => s + p.quantity, 0)}</span>}
        </button>
      </div>

      <div className="prints-page">
        <div className="prints-heading">
          <Printer size={28} />
          <div>
            <h2>Add Print Upgrades</h2>
            <p>Want physical prints of any of your selected photos? Choose a size for each.</p>
          </div>
        </div>

        <div className="prints-grid">
          {selectedPhotos.map(photo => {
            const photoItems = printItems.filter(p => p.photoId === photo.id);
            return (
              <div key={photo.id} className="print-card">
                <div className="print-card__img-wrap">
                  <img src={photo.previewUrl ?? photo.thumbnailUrl} alt={photo.fileName} />
                </div>
                <div className="print-card__body">
                  <p className="print-card__name">{photo.fileName}</p>
                  {photoItems.length > 0 && (
                    <div className="print-card__added">
                      {photoItems.map(item => (
                        <span key={item.size} className="print-tag">
                          {PRINT_SIZES.find(s => s.id === item.size)?.label} ×{item.quantity}
                          <button onClick={() => setPrintQty(photo.id, item.size, 0)}><X size={12} /></button>
                        </span>
                      ))}
                    </div>
                  )}
                  <button
                    className="btn btn--ghost btn--sm"
                    onClick={() => setPrintModalPhotoId(photo.id)}
                  >
                    <Plus size={14} /> {photoItems.length > 0 ? 'Add another size' : 'Add a print'}
                  </button>
                </div>
              </div>
            );
          })}
        </div>

        <div className="prints-footer">
          <button className="btn btn--ghost" onClick={() => setPageState('gallery')}>
            <ChevronLeft size={16} /> Back
          </button>
          <button className="btn btn--primary" onClick={() => setPageState('cart')}>
            Continue to Cart <ChevronRight size={16} />
          </button>
        </div>
      </div>

      {/* Size picker modal */}
      <AnimatePresence>
        {printModalPhoto && (
          <motion.div className="modal-overlay" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            onClick={() => setPrintModalPhotoId(null)}>
            <motion.div className="modal print-size-modal" initial={{ scale: 0.9, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }} exit={{ scale: 0.9, opacity: 0 }}
              onClick={e => e.stopPropagation()}>
              <div className="print-size-modal__header">
                <img src={printModalPhoto.previewUrl ?? printModalPhoto.thumbnailUrl} alt="" />
                <div>
                  <h3>Select Print Size</h3>
                  <p>{printModalPhoto.fileName}</p>
                </div>
                <button className="modal-close" onClick={() => setPrintModalPhotoId(null)}><X size={20} /></button>
              </div>
              <div className="print-size-list">
                {PRINT_SIZES.map(size => {
                  const item = getPrintItem(printModalPhoto.id, size.id);
                  return (
                    <div key={size.id} className="print-size-row">
                      <div className="print-size-row__info">
                        <span className="print-size-row__label">{size.label}</span>
                        <span className="print-size-row__note">{size.note}</span>
                      </div>
                      {item ? (
                        <div className="qty-control">
                          <button onClick={() => setPrintQty(printModalPhoto.id, size.id, item.quantity - 1)}><Minus size={14} /></button>
                          <span>{item.quantity}</span>
                          <button onClick={() => setPrintQty(printModalPhoto.id, size.id, item.quantity + 1)}><Plus size={14} /></button>
                        </div>
                      ) : (
                        <button className="btn btn--ghost btn--sm" onClick={() => setPrintQty(printModalPhoto.id, size.id, 1)}>
                          <Plus size={14} /> Add
                        </button>
                      )}
                    </div>
                  );
                })}
              </div>
              <button className="btn btn--primary btn--full" onClick={() => setPrintModalPhotoId(null)}>Done</button>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );

  // ── Step 3: Cart review ───────────────────────────────────────────────────

  if (pageState === 'cart' || pageState === 'submitting') return (
    <div className="gallery-page">
      <div className="step-header">
        <button className="step-back" onClick={() => setPageState('prints')}>
          <ChevronLeft size={18} /> Back to Prints
        </button>
        <div className="step-indicator">
          <span className="step-dot step-dot--done">1</span>
          <span className="step-line" />
          <span className="step-dot step-dot--done">2</span>
          <span className="step-line" />
          <span className="step-dot step-dot--active">3</span>
        </div>
        <div />
      </div>

      <div className="cart-page">
        <h2><ShoppingCart size={24} /> Your Order</h2>

        <div className="cart-section">
          <h3>Selected Photos ({selectedIds.size})</h3>
          <div className="cart-photos">
            {selectedPhotos.map(p => (
              <div key={p.id} className="cart-photo-thumb">
                <img src={p.previewUrl ?? p.thumbnailUrl} alt={p.fileName} />
              </div>
            ))}
          </div>
        </div>

        {printItems.length > 0 && (
          <div className="cart-section">
            <h3>Print Upgrades ({printItems.reduce((s, p) => s + p.quantity, 0)})</h3>
            <div className="cart-prints">
              {printItems.map((item, idx) => {
                const photo = photos.find(p => p.id === item.photoId);
                const sizeLabel = PRINT_SIZES.find(s => s.id === item.size)?.label ?? item.size;
                if (!photo) return null;
                return (
                  <div key={idx} className="cart-print-item">
                    <img src={photo.previewUrl ?? photo.thumbnailUrl} alt={photo.fileName} />
                    <div className="cart-print-item__info">
                      <span>{photo.fileName}</span>
                      <span className="text-muted">{sizeLabel} × {item.quantity}</span>
                    </div>
                    <div className="qty-control">
                      <button onClick={() => setPrintQty(item.photoId, item.size, item.quantity - 1)}><Minus size={14} /></button>
                      <span>{item.quantity}</span>
                      <button onClick={() => setPrintQty(item.photoId, item.size, item.quantity + 1)}><Plus size={14} /></button>
                    </div>
                    <button className="cart-print-item__remove" onClick={() => setPrintQty(item.photoId, item.size, 0)}>
                      <X size={16} />
                    </button>
                  </div>
                );
              })}
            </div>
          </div>
        )}

        <div className="cart-footer">
          <p className="text-muted cart-note">Once submitted you cannot change your selections.</p>
          <div className="cart-actions">
            <button className="btn btn--ghost" onClick={() => setPageState('prints')}>
              <ChevronLeft size={16} /> Edit Prints
            </button>
            <button
              className="btn btn--primary"
              onClick={handleSubmit}
              disabled={pageState === 'submitting'}
            >
              {pageState === 'submitting' ? 'Submitting…' : 'Submit Order'}
              {pageState !== 'submitting' && <ChevronRight size={16} />}
            </button>
          </div>
        </div>
      </div>
    </div>
  );

  // ── Step 1: Gallery (photo selection) ─────────────────────────────────────

  return (
    <div className="gallery-page">
      <header className="gallery-header">
        <div className="gallery-header__info">
          <h1>{gallery?.name}</h1>
          <p>Hi {gallery?.clientName}! Please select your favorite photos.</p>
        </div>
        <div className="selection-counter">
          <Star size={18} fill={selectedIds.size > 0 ? 'currentColor' : 'none'} />
          <span>
            <strong>{selectedIds.size}</strong> of <strong>{max}</strong> selected
            {extraCount > 0 && <span className="extra-badge">+{extraCount} extra</span>}
          </span>
          {selectedIds.size > 0 && (
            <button className="btn btn--primary" onClick={() => setPageState('prints')}>
              Continue <ChevronRight size={16} />
            </button>
          )}
        </div>
      </header>

      {atHardLimit && !upgraded && (
        <div className="max-warning">
          You've reached your {max} included photos.{' '}
          <button className="max-warning__link" onClick={() => setShowUpgradePrompt(true)}>Add more photos</button>
        </div>
      )}
      {upgraded && (
        <div className="upgraded-banner"><Sparkles size={15} /> Upgrade active — select as many photos as you like!</div>
      )}

      {photos.length === 0 ? (
        <div className="empty-state">No photos in this gallery yet.</div>
      ) : (
        <div className="photo-grid">
          {photos.map((p, i) => (
            <PhotoCard key={p.id} photo={p}
              isSelected={selectedIds.has(p.id)}
              isExtra={selectedIds.has(p.id) && upgraded && Array.from(selectedIds).indexOf(p.id) >= max}
              atMax={atHardLimit}
              onToggleSelect={toggleSelect}
              onOpenLightbox={() => setLightboxIdx(i)}
            />
          ))}
        </div>
      )}

      {/* Upgrade prompt */}
      <AnimatePresence>
        {showUpgradePrompt && (
          <motion.div className="modal-overlay" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            onClick={() => { setShowUpgradePrompt(false); setPendingPhoto(null); }}>
            <motion.div className="modal upgrade-modal" initial={{ scale: 0.9, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }} exit={{ scale: 0.9, opacity: 0 }} onClick={e => e.stopPropagation()}>
              <div className="upgrade-modal__icon"><Sparkles size={36} /></div>
              <h2>Want to add more photos?</h2>
              <p>You've used all <strong>{max}</strong> of your included selections. Would you like to add more?</p>
              <div className="modal-actions">
                <button className="btn btn--ghost" onClick={() => { setShowUpgradePrompt(false); setPendingPhoto(null); }}>No thanks</button>
                <button className="btn btn--primary" onClick={handleUpgradeConfirm}>Yes, add more</button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      <AnimatePresence>
        {lightboxIdx !== null && (
          <Lightbox photos={photos} currentIndex={lightboxIdx} selectedIds={selectedIds}
            maxSelections={upgraded ? Infinity : max} onClose={() => setLightboxIdx(null)}
            onNavigate={setLightboxIdx} onToggleSelect={toggleSelect} onRotate={handleRotate} />
        )}
      </AnimatePresence>
    </div>
  );
}
