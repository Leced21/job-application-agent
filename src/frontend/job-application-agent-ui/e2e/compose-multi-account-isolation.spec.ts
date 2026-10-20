import { expect, test, type Page, type Response } from '@playwright/test';

const password = 'Strong!Compose2026';

test('@compose candidate account isolation', async ({ browser }) => {
  const firstEmail = `owner-a-${globalThis.crypto.randomUUID()}@example.test`;
  const secondEmail = `owner-b-${globalThis.crypto.randomUUID()}@example.test`;
  const contexts = [await browser.newContext(), await browser.newContext()];
  const [firstPage, secondPage] = await Promise.all(contexts.map((context) => context.newPage()));

  try {
    await register(firstPage, firstEmail);
    await createProfileThroughUi(firstPage, {
      firstName: 'Candidate A',
      lastName: 'Example',
      email: firstEmail,
      jobTitle: 'Engineer A',
      summary: 'Private profile for account A',
    });

    const firstExperience = await requestJson(firstPage, '/api/v1/profile/experiences', 'POST', {
      companyName: 'Private Company A',
      jobTitle: 'Engineer A',
      location: 'Lyon',
      startDate: '2020-01-01',
      endDate: null,
      isCurrent: true,
      description: 'Private experience for account A',
    });
    expect(firstExperience.status).toBe(201);
    const firstExperienceId = asRecord(firstExperience.body).id;
    expect(typeof firstExperienceId).toBe('string');

    await register(secondPage, secondEmail);
    await secondPage.goto('/profile');
    await expect(secondPage.getByRole('heading', { name: 'Créez votre profil candidat' })).toBeVisible();
    const secondProfileBeforeCreation = await requestJson(secondPage, '/api/v1/profile');
    expect(secondProfileBeforeCreation.status).toBe(404);

    await createProfileThroughUi(secondPage, {
      firstName: 'Candidate B',
      lastName: 'Example',
      email: secondEmail,
      jobTitle: 'Engineer B',
      summary: 'Private profile for account B',
    });

    const secondExperience = await requestJson(secondPage, '/api/v1/profile/experiences', 'POST', {
      companyName: 'Private Company B',
      jobTitle: 'Engineer B',
      location: 'Nantes',
      startDate: '2021-01-01',
      endDate: null,
      isCurrent: true,
      description: 'Private experience for account B',
    });
    expect(secondExperience.status).toBe(201);

    const firstAccountExperiences = await requestJson(firstPage, '/api/v1/profile/experiences');
    const secondAccountExperiences = await requestJson(secondPage, '/api/v1/profile/experiences');
    expect(firstAccountExperiences.status).toBe(200);
    expect(secondAccountExperiences.status).toBe(200);
    expect(companyNames(firstAccountExperiences.body)).toEqual(['Private Company A']);
    expect(companyNames(secondAccountExperiences.body)).toEqual(['Private Company B']);

    const crossAccountUpdate = await requestJson(
      secondPage,
      `/api/v1/profile/experiences/${String(firstExperienceId)}`,
      'PUT',
      {
        companyName: 'Unauthorized change by account B',
        jobTitle: 'Engineer B',
        location: 'Nantes',
        startDate: '2020-01-01',
        endDate: null,
        isCurrent: true,
        description: 'This change must not be applied',
      },
    );
    expect(crossAccountUpdate.status).toBe(404);

    const firstAccountAfterAttack = await requestJson(firstPage, '/api/v1/profile/experiences');
    expect(companyNames(firstAccountAfterAttack.body)).toEqual(['Private Company A']);
  } finally {
    await Promise.allSettled(contexts.map((context) => context.close()));
  }
});

async function register(page: Page, email: string): Promise<void> {
  const sessionResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith('/auth/session') && response.request().method() === 'GET',
  );
  await page.goto('/login');
  await expectRegistrationEnabled(await sessionResponse);
  await page.getByRole('button', { name: 'Créer un compte' }).click();
  await expect(page.getByRole('heading', { name: 'Créez votre compte' })).toBeVisible();
  await page.getByLabel('Adresse e-mail').fill(email);
  await page.getByLabel('Mot de passe').fill(password);

  const registrationResponse = page.waitForResponse(
    (response) => response.url().endsWith('/auth/register') && response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Créer mon compte' }).click();
  expect((await registrationResponse).status()).toBe(200);
  await expect(page).toHaveURL(/\/dashboard$/);
}

async function expectRegistrationEnabled(response: Response): Promise<void> {
  const body = await response.text();
  expect(response.status(), `GET ${response.url()} returned ${body}`).toBe(200);
  const session = JSON.parse(body) as { registrationEnabled?: boolean };
  expect(session.registrationEnabled, `Unexpected account session: ${body}`).toBe(true);
}

async function createProfileThroughUi(
  page: Page,
  profile: { firstName: string; lastName: string; email: string; jobTitle: string; summary: string },
): Promise<void> {
  await page.goto('/profile');
  await expect(page.getByRole('heading', { name: 'Créez votre profil candidat' })).toBeVisible();
  await page.getByLabel('Prénom').fill(profile.firstName);
  await page.getByLabel('Nom').fill(profile.lastName);
  await page.getByLabel('Adresse e-mail').fill(profile.email);
  await page.getByLabel('Poste recherché').fill(profile.jobTitle);
  await page.getByLabel('Résumé professionnel').fill(profile.summary);

  const creationResponse = page.waitForResponse(
    (response) => response.url().endsWith('/api/v1/profile') && response.request().method() === 'POST',
  );
  await page.getByRole('button', { name: 'Créer mon profil' }).click();
  expect((await creationResponse).status()).toBe(201);
  await expect(page.getByText(profile.email, { exact: true })).toBeVisible();
}

async function requestJson(
  page: Page,
  path: string,
  method = 'GET',
  body?: Record<string, string | boolean | null>,
): Promise<{ status: number; body: unknown }> {
  return page.evaluate(async ({ path, method, body }) => {
    const headers: Record<string, string> = {};
    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
      const antiforgeryResponse = await fetch('/auth/antiforgery', { credentials: 'same-origin' });
      if (!antiforgeryResponse.ok) {
        return { status: antiforgeryResponse.status, body: null };
      }
      const antiforgery = (await antiforgeryResponse.json()) as { requestToken: string };
      headers['X-CSRF-TOKEN'] = antiforgery.requestToken;
    }

    const response = await fetch(path, {
      method,
      credentials: 'same-origin',
      headers,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    return { status: response.status, body: await response.json().catch(() => null) };
  }, { path, method, body });
}

function asRecord(value: unknown): Record<string, unknown> {
  expect(value).not.toBeNull();
  expect(Array.isArray(value)).toBe(false);
  return value as Record<string, unknown>;
}

function companyNames(body: unknown): string[] {
  expect(Array.isArray(body)).toBe(true);
  return (body as Array<Record<string, unknown>>).map((experience) => String(experience.companyName));
}
