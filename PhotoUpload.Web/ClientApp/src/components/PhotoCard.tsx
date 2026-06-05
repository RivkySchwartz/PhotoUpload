import React, { useState, useEffect } from 'react';
import { Star, ImageOff, RotateCw } from 'lucide-react';
import { motion } from 'framer-motion';
import type { Photo } from '../types';

interface PhotoCardProps {
  photo: Photo;
  isSelected: boolean;
  isExtra?: boolean;
  atMax: boolean;
  onToggleSelect: (photo: Photo) => void;
  onOpenLightbox: () => void;
  isAdmin?: boolean;
  onDelete?: (photoId: number) => void;
  onRotate?: (photoId: number) => void;
}

export default function PhotoCard({
  photo, isSelected, isExtra, atMax, onToggleSelect, onOpenLightbox, isAdmin, onDelete, onRotate
}: PhotoCardProps) {
  const [loaded, setLoaded] = useState(false);
  const [imgError, setImgError] = useState(false);

  // When preview/thumbnail arrives, retry loading the image
  useEffect(() => { setImgError(false); setLoaded(false); }, [photo.previewUrl, photo.thumbnailUrl]);

  return (
    <motion.div
      className={`photo-card ${isSelected ? 'photo-card--selected' : ''}`}
      layout
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      transition={{ duration: 0.2 }}
    >
      {/* Skeleton */}
      {!loaded && !imgError && <div className="photo-card__skeleton" />}

      {imgError ? (
        <div className="photo-card__no-preview" onClick={onOpenLightbox}>
          <ImageOff size={28} />
          <span>RAW</span>
        </div>
      ) : (
        <img
          src={photo.previewUrl ?? photo.thumbnailUrl}
          alt={photo.fileName}
          className={`photo-card__img ${loaded ? 'photo-card__img--loaded' : ''}`}
          style={photo.rotation ? { transform: `rotate(${photo.rotation}deg)` } : undefined}
          loading="lazy"
          onLoad={() => setLoaded(true)}
          onError={() => { setLoaded(true); setImgError(true); }}
          onClick={onOpenLightbox}
        />
      )}

      {/* Overlay */}
      <div className="photo-card__overlay" onClick={onOpenLightbox} />

      {/* Star button (client mode only) */}
      {!isAdmin && (
        <motion.button
          className={`photo-card__star ${isSelected ? 'photo-card__star--active' : ''}`}
          onClick={(e) => {
            e.stopPropagation();
            if (!atMax || isSelected) onToggleSelect(photo);
          }}
          whileTap={{ scale: 0.85 }}
          title={isSelected ? 'Unselect' : atMax ? 'Max reached' : 'Select'}
          disabled={atMax && !isSelected}
        >
          <Star size={18} fill={isSelected ? 'currentColor' : 'none'} />
        </motion.button>
      )}

      {/* Admin action buttons */}
      {isAdmin && onRotate && (
        <button
          className="photo-card__rotate"
          onClick={(e) => { e.stopPropagation(); onRotate(photo.id); }}
          title="Rotate 90°"
        >
          <RotateCw size={14} />
        </button>
      )}
      {isAdmin && onDelete && (
        <button
          className="photo-card__delete"
          onClick={(e) => { e.stopPropagation(); onDelete(photo.id); }}
          title="Delete photo"
        >
          ×
        </button>
      )}

      {/* Selection badge — gold for included, blue for extra */}
      {isSelected && (
        <motion.div
          className={`photo-card__badge ${isExtra ? 'photo-card__badge--extra' : ''}`}
          initial={{ scale: 0 }}
          animate={{ scale: 1 }}
        >
          {isExtra ? '+' : '✓'}
        </motion.div>
      )}
    </motion.div>
  );
}
