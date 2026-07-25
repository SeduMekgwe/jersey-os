import { http, HttpResponse } from 'msw';
import { apiRequest, tokenStore } from '@/lib/api';
import { server } from '@/test/server';

describe('apiRequest', () => {
  afterEach(() => {
    tokenStore.set(null);
  });
  it('coordinates one refresh for concurrent unauthorized requests', async () => {
    let refreshes = 0;
    server.use(
      http.get('/api/v1/protected/:id', ({ request }) =>
        request.headers.get('Authorization')
          ? HttpResponse.json({ ok: true })
          : new HttpResponse(null, { status: 401 }),
      ),
      http.post('/api/v1/auth/refresh', async () => {
        refreshes += 1;
        await new Promise((resolve) => setTimeout(resolve, 10));
        return HttpResponse.json({ accessToken: 'renewed' });
      }),
    );
    const results = await Promise.all([
      apiRequest<{ ok: boolean }>('/protected/1'),
      apiRequest<{ ok: boolean }>('/protected/2'),
    ]);
    expect(results).toEqual([{ ok: true }, { ok: true }]);
    expect(refreshes).toBe(1);
  });
  it('normalizes problem details failures', async () => {
    server.use(
      http.get('/api/v1/failure', () =>
        HttpResponse.json(
          { title: 'Unavailable', status: 503, detail: 'Dependency failed' },
          { status: 503, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    );
    await expect(apiRequest('/failure', {}, false)).rejects.toMatchObject({
      message: 'Dependency failed',
      problem: { status: 503 },
    });
  });
});
