import { randomUUID } from 'node:crypto';
import { expect, test, type Response } from '@playwright/test';

test('@compose candidate registration and sign-in', async ({ page }) => {
  const email = `compose-${randomUUID()}@example.test`;
  const password = 'Strong!Compose2026';

  const sessionResponse = page.waitForResponse(
    (response) => response.url().endsWith('/auth/session') && response.request().method() === 'GET',
  );
  await page.goto('/login');
  await expectRegistrationEnabled(await sessionResponse);
  await page.getByRole('button', { name: 'Créer un compte' }).click();
  await expect(page.getByRole('heading', { name: 'Créez votre compte' })).toBeVisible();
  await page.getByLabel('Adresse e-mail').fill(email);
  await page.getByLabel('Mot de passe').fill(password);

  const registrationResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith('/auth/register') && response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Créer mon compte' }).click();
  expect((await registrationResponse).status()).toBe(200);
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(
    page.getByRole('heading', { name: 'Faites avancer votre prochaine étape.' }),
  ).toBeVisible();

  await page.context().clearCookies();
  await page.goto('/login');
  await page.getByLabel('Adresse e-mail').fill(email);
  await page.getByLabel('Mot de passe').fill(password);

  const loginResponse = page.waitForResponse(
    (response) => response.url().endsWith('/auth/login') && response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Se connecter' }).click();
  expect((await loginResponse).status()).toBe(200);
  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(
    page.getByRole('heading', { name: 'Faites avancer votre prochaine étape.' }),
  ).toBeVisible();
});

async function expectRegistrationEnabled(response: Response): Promise<void> {
  const body = await response.text();
  expect(response.status(), `GET ${response.url()} returned ${body}`).toBe(200);
  const session = JSON.parse(body) as { registrationEnabled?: boolean };
  expect(session.registrationEnabled, `Unexpected account session: ${body}`).toBe(true);
}
