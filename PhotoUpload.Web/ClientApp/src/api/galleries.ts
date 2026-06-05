import api from './client';
import axios from 'axios';
import type {
  Gallery, Photo, Selection, CreateGalleryRequest,
  LoginRequest, LoginResponse, GalleryPublic, UserDto,
  SubmitSelectionsResponse, PrintOrderDto
} from '../types';

// ── Auth ────────────────────────────────────────────────────────────────────

export const login = (data: LoginRequest) =>
  api.post<LoginResponse>('/auth/login', data).then(r => r.data);

// ── Admin: Galleries ─────────────────────────────────────────────────────────

export const getGalleries = () =>
  api.get<Gallery[]>('/galleries').then(r => r.data);

export const getGallery = (id: number) =>
  api.get<Gallery>(`/galleries/${id}`).then(r => r.data);

export const createGallery = (data: CreateGalleryRequest) =>
  api.post<Gallery>('/galleries', data).then(r => r.data);

export const deleteGallery = (id: number) =>
  api.delete(`/galleries/${id}`);

export const getAdminPhotos = (id: number) =>
  api.get<Photo[]>(`/galleries/${id}/photos`).then(r => r.data);

export const deletePhoto = (galleryId: number, photoId: number) =>
  api.delete(`/galleries/${galleryId}/photos/${photoId}`);

export const getAdminSelections = (id: number) =>
  api.get<Selection[]>(`/galleries/${id}/selections`).then(r => r.data);

export const getAdminPrintOrders = (id: number) =>
  api.get<PrintOrderDto[]>(`/galleries/${id}/print-orders`).then(r => r.data);

export const downloadSelections = async (id: number, galleryName: string) => {
  const response = await api.get(`/galleries/${id}/download`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data as Blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${galleryName.replace(/\s+/g, '_')}_selections.zip`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
};

export const updateGalleryStatus = (id: number, status: number) =>
  api.patch(`/galleries/${id}/status`, { status }).then(r => r.data);

export const updatePhotoOrder = (id: number, items: { photoId: number; sortOrder: number }[]) =>
  api.put(`/galleries/${id}/photos/order`, { items });

export const regeneratePreviews = (id: number) =>
  api.post<{ fixedCount: number; total: number }>(`/galleries/${id}/regenerate-previews`).then(r => r.data);

export const rotatePhoto = (galleryId: number, photoId: number) =>
  api.post<Photo>(`/galleries/${galleryId}/photos/${photoId}/rotate`).then(r => r.data);

export const generatePhotoPreview = (galleryId: number, photoId: number) =>
  api.post<Photo>(`/galleries/${galleryId}/photos/${photoId}/generate-preview`).then(r => r.data);

export const uploadPhotos = (galleryId: number, files: FileList | File[]) => {
  const form = new FormData();
  Array.from(files).forEach(f => form.append('files', f));

  // In dev, bypass the Vite proxy — large RAW files through Node.js cause
  // ERR_CONNECTION_TIMED_OUT because Node.js buffers the full multipart body.
  // CORS on the backend already allows localhost:3000.
  if (import.meta.env.DEV) {
    const token = sessionStorage.getItem('token');
    return axios.post<Photo[]>(
      `http://localhost:5292/api/galleries/${galleryId}/upload`, form,
      { headers: { 'Content-Type': 'multipart/form-data', Authorization: `Bearer ${token}` } }
    ).then(r => r.data);
  }

  return api.post<Photo[]>(`/galleries/${galleryId}/upload`, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  }).then(r => r.data);
};

// ── Admin: User management ───────────────────────────────────────────────────

export const getUsers = () =>
  api.get<UserDto[]>('/users').then(r => r.data);

export const createUser = (data: { username: string; role: string; temporaryPassword: string }) =>
  api.post<UserDto>('/users', data).then(r => r.data);

export const deleteUser = (id: number) =>
  api.delete(`/users/${id}`);

export const changePassword = (newPassword: string) =>
  api.post('/auth/change-password', { newPassword });

// ── Public: Client gallery ────────────────────────────────────────────────────

export const getPublicGallery = (token: string) =>
  api.get<GalleryPublic>(`/gallery/${token}`).then(r => r.data);

export const verifyGalleryPassword = (token: string, password: string) =>
  api.post(`/gallery/${token}/verify`, { password }).then(r => r.data);

export const getPublicPhotos = (token: string) =>
  api.get<Photo[]>(`/gallery/${token}/photos`).then(r => r.data);

export const submitSelections = (token: string, photoIds: number[], printOrders: { photoId: number; size: string; quantity: number }[] = []) =>
  api.post<SubmitSelectionsResponse>(`/gallery/${token}/selections`, { photoIds, printOrders }).then(r => r.data);

export const getPublicSelections = (token: string) =>
  api.get<Selection[]>(`/gallery/${token}/selections`).then(r => r.data);

export const rotatePhotoPublic = (token: string, photoId: number) =>
  api.post<{ rotation: number }>(`/gallery/${token}/photos/${photoId}/rotate`).then(r => r.data);
