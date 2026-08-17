import { http, HttpResponse } from 'msw';
export const handlers = [
  http.post('/api/v1/auth/refresh', () => new HttpResponse(null, { status: 401 })),
  http.get('/api/v1/auth/me', () =>
    HttpResponse.json({
      id: 'user-1',
      displayName: 'Test Operator',
      email: 'operator@example.com',
      organizationId: '00000000-0000-0000-0000-000000000001',
      organizationName: 'Test Org',
      permissions: ['system.health.read'],
    }),
  ),
  http.post('/api/v1/auth/login', () =>
    HttpResponse.json({
      accessToken: 'test-access-token',
      user: {
        id: 'user-1',
        displayName: 'Test Operator',
        email: 'operator@example.com',
        organizationId: '00000000-0000-0000-0000-000000000001',
        organizationName: 'Test Org',
        permissions: ['system.health.read'],
      },
    }),
  ),
];
