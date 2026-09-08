import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider, useAuth } from './contexts/AuthContext';

import LoginPage from './Pages/admin/LoginPage';
import GalleryListPage from './Pages/admin/GalleryListPage';
import GalleryDetailPage from './Pages/admin/GalleryDetailPage';
import UsersPage from './Pages/admin/UsersPage';
import ChangePasswordPage from './Pages/admin/ChangePasswordPage';
import GalleryPage from './Pages/client/GalleryPage';
import CollagePage from './Pages/client/CollagePage';

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, role, mustChangePassword } = useAuth();
  if (!isAuthenticated || !role) return <Navigate to="/admin/login" replace />;
  if (mustChangePassword) return <Navigate to="/admin/change-password" replace />;
  return <>{children}</>;
}

function RequireAdmin({ children }: { children: React.ReactNode }) {
  const { role } = useAuth();
  if (role !== 'Admin') return <Navigate to="/admin" replace />;
  return <>{children}</>;
}

function App() {
  return (
    <AuthProvider>
      <Toaster
        position="top-right"
        toastOptions={{
          style: {
            background: '#1a1a1a',
            color: '#fff',
            border: '1px solid #333',
          }
        }}
      />
      <Routes>
        {/* Public client routes */}
        <Route path="/gallery/:token" element={<GalleryPage />} />
        <Route path="/gallery/:token/collage" element={<CollagePage />} />

        {/* Admin routes */}
        <Route path="/admin/login" element={<LoginPage />} />
        <Route path="/admin/change-password" element={<ChangePasswordPage />} />
        <Route path="/admin" element={<RequireAuth><GalleryListPage /></RequireAuth>} />
        <Route path="/admin/galleries/:id" element={<RequireAuth><GalleryDetailPage /></RequireAuth>} />
        <Route path="/admin/users" element={<RequireAuth><RequireAdmin><UsersPage /></RequireAdmin></RequireAuth>} />

        {/* Default redirect */}
        <Route path="/" element={<Navigate to="/admin" replace />} />
        <Route path="*" element={<Navigate to="/admin" replace />} />
      </Routes>
    </AuthProvider>
  );
}

export default App;
