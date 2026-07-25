import { env } from '@/lib/env';
import type { ProblemDetailsDto } from '@/types/api';

let accessToken: string | null = null;
let refreshPromise: Promise<string | null> | null = null;
export const tokenStore = {
  get: () => accessToken,
  set: (value: string | null) => {
    accessToken = value;
  },
};
const apiUrl = (path: string) =>
  new URL(
    `${env.VITE_API_BASE_URL.replace(/\/$/, '')}/${path.replace(/^\//, '')}`,
    window.location.origin,
  ).toString();

export class ApiError extends Error {
  constructor(
    public readonly problem: ProblemDetailsDto,
    public readonly response: Response,
  ) {
    super(problem.detail ?? problem.title ?? `Request failed (${String(response.status)})`);
    this.name = 'ApiError';
  }
}

async function problemFrom(response: Response): Promise<ProblemDetailsDto> {
  const contentType = response.headers.get('content-type') ?? '';
  if (
    contentType.includes('application/problem+json') ||
    contentType.includes('application/json')
  ) {
    try {
      return (await response.json()) as ProblemDetailsDto;
    } catch {
      /* normalized below */
    }
  }
  return { title: response.statusText || 'Request failed', status: response.status };
}

async function refresh(): Promise<string | null> {
  refreshPromise ??= fetch(apiUrl('/auth/refresh'), { method: 'POST', credentials: 'include' })
    .then(async (response) => {
      if (!response.ok) {
        accessToken = null;
        return null;
      }
      const value = (await response.json()) as { accessToken: string };
      accessToken = value.accessToken;
      return accessToken;
    })
    .finally(() => {
      refreshPromise = null;
    });
  return refreshPromise;
}

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
  retry = true,
): Promise<T> {
  const headers = new Headers(init.headers);
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`);
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');
  const response = await fetch(apiUrl(path), { ...init, headers, credentials: 'include' });
  if (response.status === 401 && retry && (await refresh()))
    return apiRequest<T>(path, init, false);
  if (!response.ok) throw new ApiError(await problemFrom(response), response);
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const restoreSession = () => refresh();
