import React, { useState, useCallback } from 'react';
import { useDropzone } from 'react-dropzone';
import { Upload } from 'lucide-react';
import { uploadPhotos } from '../api/galleries';
import type { Photo } from '../types';
import toast from 'react-hot-toast';

const CHUNK_SIZE = 1;    // one file per request — keeps each upload small and fast
const MAX_CONCURRENT = 4;

const ACCEPTED: Record<string, string[]> = {
  'image/jpeg': ['.jpg', '.jpeg'],
  'image/png': ['.png'],
  'image/heic': ['.heic'],
  'image/webp': ['.webp'],
  'image/tiff': ['.tiff', '.tif'],
  'image/x-canon-cr2': ['.cr2'],
  'image/x-nikon-nef': ['.nef'],
  'image/x-sony-arw': ['.arw'],
};

function chunkArray<T>(arr: T[], size: number): T[][] {
  const chunks: T[][] = [];
  for (let i = 0; i < arr.length; i += size) chunks.push(arr.slice(i, i + size));
  return chunks;
}

interface UploadZoneProps {
  galleryId: number;
  onUploaded: (photos: Photo[]) => void;
}

export default function UploadZone({ galleryId, onUploaded }: UploadZoneProps) {
  const [uploading, setUploading] = useState(false);
  const [uploadedCount, setUploadedCount] = useState(0);
  const [totalCount, setTotalCount] = useState(0);

  const onDrop = useCallback(async (accepted: File[]) => {
    if (!accepted.length) return;
    setUploading(true);
    const total = accepted.length;
    setTotalCount(total);
    setUploadedCount(0);

    const chunks = chunkArray(accepted, CHUNK_SIZE);
    let uploaded = 0;
    let chunkIdx = 0;
    let totalUploaded = 0;

    async function runNext(): Promise<void> {
      if (chunkIdx >= chunks.length) return;
      const chunk = chunks[chunkIdx++];
      const photos = await uploadPhotos(galleryId, chunk);
      onUploaded(photos);   // show each file in the grid as soon as it lands
      uploaded += chunk.length;
      totalUploaded += photos.length;
      setUploadedCount(uploaded);
      await runNext();
    }

    try {
      await Promise.all(Array.from({ length: MAX_CONCURRENT }, runNext));
      toast.success(`${totalUploaded} photo(s) uploaded`);
    } catch {
      toast.error('Some files failed to upload. Please try again.');
    } finally {
      setUploading(false);
      setUploadedCount(0);
      setTotalCount(0);
    }
  }, [galleryId, onUploaded]);

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: ACCEPTED,
    disabled: uploading,
    multiple: true,
  });

  const pct = totalCount > 0 ? Math.round((uploadedCount / totalCount) * 100) : 0;

  return (
    <div className="upload-zone-wrapper">
      <div
        {...getRootProps()}
        className={`upload-zone ${isDragActive ? 'upload-zone--active' : ''} ${uploading ? 'upload-zone--uploading' : ''}`}
      >
        <input {...getInputProps()} />
        <Upload size={40} className="upload-zone__icon" />
        <p className="upload-zone__text">
          {isDragActive ? 'Drop photos here…' : 'Drag & drop photos here, or click to select'}
        </p>
        <p className="upload-zone__hint">JPG, PNG, HEIC, RAW (CR2, NEF, ARW), WebP, TIFF</p>
      </div>

      {uploading && totalCount > 0 && (
        <div className="upload-progress">
          <div className="upload-progress__bar">
            <div className="upload-progress__fill" style={{ width: `${pct}%` }} />
          </div>
          <p className="upload-progress__label">{uploadedCount} of {totalCount} files uploaded</p>
        </div>
      )}
    </div>
  );
}
