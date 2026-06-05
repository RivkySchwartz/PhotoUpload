import React from 'react';
import type { GalleryStatus } from '../types';

const LABELS: Record<GalleryStatus, string> = {
  Pending: 'Pending Review',
  SelectionsMade: 'Selections Made',
  Editing: 'Editing',
  Complete: 'Complete',
};

const CLASSES: Record<GalleryStatus, string> = {
  Pending: 'badge badge--pending',
  SelectionsMade: 'badge badge--selections',
  Editing: 'badge badge--editing',
  Complete: 'badge badge--complete',
};

export default function StatusBadge({ status }: { status: GalleryStatus }) {
  return <span className={CLASSES[status]}>{LABELS[status]}</span>;
}
