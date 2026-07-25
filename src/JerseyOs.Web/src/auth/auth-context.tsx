import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { apiRequest, restoreSession, tokenStore } from '@/lib/api';
import type { LoginRequestDto, SessionDto, UserDto } from '@/types/api';

interface AuthState {
  user: UserDto | null;
  isRestoring: boolean;
  login: (input: LoginRequestDto) => Promise<void>;
  logout: () => Promise<void>;
}
const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [isRestoring, setRestoring] = useState(true);
  useEffect(() => {
    void (async () => {
      try {
        if (await restoreSession()) setUser(await apiRequest<UserDto>('/auth/me'));
      } catch {
        tokenStore.set(null);
      } finally {
        setRestoring(false);
      }
    })();
  }, []);
  const login = useCallback(async (input: LoginRequestDto) => {
    const session = await apiRequest<SessionDto>(
      '/auth/login',
      { method: 'POST', body: JSON.stringify(input) },
      false,
    );
    tokenStore.set(session.accessToken);
    setUser(session.user);
  }, []);
  const logout = useCallback(async () => {
    try {
      await apiRequest<undefined>('/auth/logout', { method: 'POST' }, false);
    } finally {
      tokenStore.set(null);
      setUser(null);
    }
  }, []);
  const value = useMemo(
    () => ({ user, isRestoring, login, logout }),
    [user, isRestoring, login, logout],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
// The provider and hook are intentionally colocated to keep one auth context instance.
// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within AuthProvider');
  return value;
}
