import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '@/auth/auth-context';

export function RequireAuth() {
  const { user, isRestoring } = useAuth();
  const location = useLocation();
  if (isRestoring)
    return (
      <main className="grid min-h-screen place-items-center" aria-live="polite">
        Restoring your session…
      </main>
    );
  return user ? <Outlet /> : <Navigate to="/login" replace state={{ from: location.pathname }} />;
}
export function RequirePermission({ permission }: { permission: string }) {
  const { user } = useAuth();
  return user?.permissions.includes(permission) ? (
    <Outlet />
  ) : (
    <Navigate to="/unauthorized" replace />
  );
}
