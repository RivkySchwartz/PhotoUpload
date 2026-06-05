import React from 'react';
import { useLocation, useParams } from 'react-router-dom';
import { CheckCircle, Mail, MailX, ImageOff } from 'lucide-react';
import { motion } from 'framer-motion';
import type { Selection } from '../../types';

interface CollageState {
  selections: Selection[];
  collageUrl: string | null;
  emailSent: boolean;
  clientName: string;
  galleryName: string;
}

export default function CollagePage() {
  const location = useLocation();
  const { token } = useParams<{ token: string }>();
  const state = location.state as CollageState | null;

  if (!state || !state.selections) {
    return (
      <div className="gallery-error">
        <ImageOff size={48} />
        <h2>No selections found</h2>
        <p>Please go back and submit your selections first.</p>
      </div>
    );
  }

  const { selections, collageUrl, emailSent, clientName, galleryName } = state;

  return (
    <div className="collage-page">
      <motion.div
        className="collage-container"
        initial={{ opacity: 0, y: 30 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5 }}
      >
        {/* Success header */}
        <div className="collage-header">
          <CheckCircle size={52} className="collage-check-icon" />
          <h1>Selections Submitted!</h1>
          <p className="collage-subtitle">
            Thank you, <strong>{clientName}</strong>! Your photographer will be in touch soon.
          </p>

          {/* Email status */}
          <div className={`collage-email-badge ${emailSent ? 'collage-email-badge--sent' : 'collage-email-badge--none'}`}>
            {emailSent ? (
              <>
                <Mail size={16} />
                <span>A collage was emailed to you</span>
              </>
            ) : (
              <>
                <MailX size={16} />
                <span>Your photographer has been notified</span>
              </>
            )}
          </div>
        </div>

        {/* Collage image (server-generated) */}
        {collageUrl && (
          <motion.div
            className="collage-image-wrapper"
            initial={{ opacity: 0, scale: 0.97 }}
            animate={{ opacity: 1, scale: 1 }}
            transition={{ delay: 0.25, duration: 0.5 }}
          >
            <img
              src={collageUrl}
              alt="Your photo collage"
              className="collage-image"
            />
          </motion.div>
        )}

        {/* Thumbnail strip (fallback or supplemental) */}
        {!collageUrl && selections.length > 0 && (
          <div className="collage-thumb-grid">
            {selections.map((s, i) => (
              <motion.img
                key={s.id}
                src={s.thumbnailUrl}
                alt={s.fileName}
                className="collage-thumb"
                initial={{ opacity: 0, scale: 0.9 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ delay: 0.1 + i * 0.04 }}
              />
            ))}
          </div>
        )}

        <p className="collage-count">
          <strong>{selections.length}</strong> photo{selections.length !== 1 ? 's' : ''} selected
        </p>
      </motion.div>
    </div>
  );
}
