export type GalleryStatus = 'Pending' | 'SelectionsMade' | 'Editing' | 'Complete';

export type UserRole = 'Admin' | 'Photographer' | 'Editor' | 'Secretary';

export interface Gallery {
  id: number;
  name: string;
  clientName: string;
  clientEmail?: string;
  maxSelections: number;
  isPasswordProtected: boolean;
  uniqueToken: string;
  createdAt: string;
  status: GalleryStatus;
  photoCount: number;
  selectionCount: number;
}

export interface GalleryPublic {
  id: number;
  name: string;
  clientName: string;
  maxSelections: number;
  isPasswordProtected: boolean;
  status: GalleryStatus;
}

export interface Photo {
  id: number;
  galleryId: number;
  fileName: string;
  thumbnailUrl: string;
  originalUrl: string;
  previewUrl?: string;
  uploadedAt: string;
  sortOrder: number;
  rotation: number;
}

export interface Selection {
  id: number;
  photoId: number;
  fileName: string;
  thumbnailUrl: string;
  selectedAt: string;
}

export interface CreateGalleryRequest {
  name: string;
  clientName: string;
  clientEmail?: string;
  maxSelections: number;
  password?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  username: string;
  role: UserRole;
  mustChangePassword: boolean;
}

export interface UserDto {
  id: number;
  username: string;
  role: UserRole;
  mustChangePassword: boolean;
}

export interface PrintItem {
  photoId: number;
  size: string;
  quantity: number;
}

export interface PrintOrderDto {
  id: number;
  photoId: number;
  fileName: string;
  thumbnailUrl: string;
  size: string;
  quantity: number;
}

export interface SubmitSelectionsResponse {
  selections: Selection[];
  printOrders: PrintItem[];
  collageUrl: string | null;
  emailSent: boolean;
}
