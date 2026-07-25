import { expect, test } from '@playwright/test';

test('restores a session and shows authorized system health', async ({ page }) => {
  await page.route('**/api/v1/auth/refresh', (route) =>
    route.fulfill({ json: { accessToken: 'e2e-token' } }),
  );
  await page.route('**/api/v1/auth/me', (route) =>
    route.fulfill({
      json: {
        id: '1',
        displayName: 'Platform Operator',
        email: 'operator@example.com',
        permissions: ['system.health.read'],
      },
    }),
  );
  await page.route('**/api/v1/system/health', (route) =>
    route.fulfill({
      json: {
        status: 'Healthy',
        checkedAt: new Date().toISOString(),
        checks: [{ name: 'API', status: 'Healthy' }],
      },
    }),
  );
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Platform dashboard' })).toBeVisible();
  await page.getByRole('link', { name: 'System health' }).click();
  await expect(page.getByRole('heading', { name: 'System health' })).toBeVisible();
  await expect(page.getByText('API')).toBeVisible();
});

test('unknown routes show not found', async ({ page }) => {
  await page.goto('/does-not-exist');
  await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible();
});
