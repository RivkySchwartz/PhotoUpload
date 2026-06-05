import React from 'react';
import { NavLink, useNavigate, useLocation } from 'react-router-dom';
import { Camera, LayoutDashboard, LogOut, Users } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import type { UserRole } from '../types';

const STATUS_LINKS: { label: string; search: string; roles: UserRole[] }[] = [
  { label: 'All',              search: '',                      roles: ['Admin'] },
  { label: 'Pending Review',   search: '?status=Pending',       roles: ['Admin', 'Photographer', 'Secretary'] },
  { label: 'Selections Made',  search: '?status=SelectionsMade', roles: ['Admin', 'Editor'] },
  { label: 'Editing',          search: '?status=Editing',       roles: ['Admin', 'Editor'] },
  { label: 'Complete',         search: '?status=Complete',      roles: ['Admin', 'Secretary'] },
];

export default function AdminSidebar() {
  const { username, role, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const handleLogout = () => {
    logout();
    navigate('/admin/login');
  };

  const isOnAdmin = location.pathname === '/admin';
  const visibleLinks = STATUS_LINKS.filter(s => role && s.roles.includes(role));

  return (
    <aside className="sidebar">
      <div className="sidebar__brand">
        <Camera size={24} />
        <span>PhotoSelect</span>
      </div>

      <nav className="sidebar__nav">
        <NavLink to="/admin" end className={({ isActive }) => `sidebar__link ${isActive ? 'sidebar__link--active' : ''}`}>
          <LayoutDashboard size={18} />
          <span>Galleries</span>
        </NavLink>

        {isOnAdmin && visibleLinks.length > 0 && (
          <div className="sidebar__sub">
            {visibleLinks.map(s => {
              const active = location.search === s.search;
              return (
                <NavLink
                  key={s.search}
                  to={`/admin${s.search}`}
                  className={`sidebar__sub-link ${active ? 'sidebar__sub-link--active' : ''}`}
                >
                  {s.label}
                </NavLink>
              );
            })}
          </div>
        )}

        {role === 'Admin' && (
          <NavLink to="/admin/users" className={({ isActive }) => `sidebar__link ${isActive ? 'sidebar__link--active' : ''}`}>
            <Users size={18} />
            <span>Users</span>
          </NavLink>
        )}
      </nav>

      <div className="sidebar__footer">
        <div>
          <div className="sidebar__user">{username}</div>
          {role && <div className="sidebar__role">{role}</div>}
        </div>
        <button className="sidebar__logout" onClick={handleLogout}>
          <LogOut size={16} />
        </button>
      </div>
    </aside>
  );
}
