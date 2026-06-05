import React, { useEffect, useCallback } from 'react';
import { X, ChevronLeft, ChevronRight, Star, RotateCw } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import type { Photo } from '../types';

interface LightboxProps {
  photos: Photo[];
  currentIndex: number;
  selectedIds: Set<number>;
  maxSelections: number; // pass Infinity when upgraded
  onClose: () => void;
  onNavigate: (index: number) => void;
  onToggleSelect: (photo: Photo) => void;
  onRotate?: (photoId: number) => void;
}

export default function Lightbox({
  photos, currentIndex, selectedIds, maxSelections,
  onClose, onNavigate, onToggleSelect, onRotate
}: LightboxProps) {
  const photo = photos[currentIndex];
  const isSelected = selectedIds.has(photo.id);
  const atMax = selectedIds.size >= maxSelections && !isSelected;

  const handleKey = useCallback((e: KeyboardEvent) => {
    if (e.key === 'Escape') onClose();
    if (e.key === 'ArrowLeft' && currentIndex > 0) onNavigate(currentIndex - 1);
    if (e.key === 'ArrowRight' && currentIndex < photos.length - 1) onNavigate(currentIndex + 1);
    if (e.key === 's' || e.key === 'S') onToggleSelect(photo);
  }, [currentIndex, photo, photos.length, onClose, onNavigate, onToggleSelect]);

  useEffect(() => {
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [handleKey]);

  return (
    <motion.div
      className="lightbox-overlay"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      onClick={onClose}
    >
      <div className="lightbox-inner" onClick={e => e.stopPropagation()}>
        {/* Header */}
        <div className="lightbox-header">
          <span className="lightbox-counter">{currentIndex + 1} / {photos.length}</span>
          <span className="lightbox-filename">{photo.fileName}</span>
          <div className="lightbox-header-actions">
            {onRotate && (
              <button className="lightbox-rotate" onClick={() => onRotate(photo.id)} title="Rotate 90°">
                <RotateCw size={20} />
              </button>
            )}
            <button className="lightbox-close" onClick={onClose}><X size={22} /></button>
          </div>
        </div>

        {/* Image */}
        <AnimatePresence mode="wait">
          <motion.div
            key={photo.id}
            className="lightbox-image-wrapper"
            initial={{ opacity: 0, scale: 0.97 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.15 }}
          >
            <img
              src={photo.previewUrl ?? photo.thumbnailUrl}
              alt={photo.fileName}
              className="lightbox-image"
              style={{ transform: `rotate(${photo.rotation}deg)`, transition: 'transform 0.25s ease' }}
            />
          </motion.div>
        </AnimatePresence>

        {/* Nav arrows */}
        {currentIndex > 0 && (
          <button className="lightbox-nav lightbox-nav--prev" onClick={() => onNavigate(currentIndex - 1)}>
            <ChevronLeft size={32} />
          </button>
        )}
        {currentIndex < photos.length - 1 && (
          <button className="lightbox-nav lightbox-nav--next" onClick={() => onNavigate(currentIndex + 1)}>
            <ChevronRight size={32} />
          </button>
        )}

        {/* Star button */}
        <button
          className={`lightbox-star ${isSelected ? 'lightbox-star--active' : ''} ${atMax ? 'lightbox-star--disabled' : ''}`}
          onClick={() => !atMax && onToggleSelect(photo)}
          title={isSelected ? 'Remove from selection' : atMax ? 'Max selections reached' : 'Add to selection'}
        >
          <Star size={28} fill={isSelected ? 'currentColor' : 'none'} />
          {isSelected ? 'Selected' : atMax ? 'Limit reached' : 'Select'}
        </button>
      </div>
    </motion.div>
  );
}
