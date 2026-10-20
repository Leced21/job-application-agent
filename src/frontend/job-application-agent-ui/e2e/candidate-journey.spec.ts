import { expect, test } from '@playwright/test';

test('new candidate creates an account through the antiforgery-protected registration flow', async ({ page }) => {
  let authenticated = false;
  const session = () => ({
    isAuthenticated: authenticated,
    email: authenticated ? 'new-candidate@example.test' : null,
    registrationEnabled: true,
  });

  await page.route('**/auth/session', (route) => route.fulfill({ json: session() }));
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'registration-csrf-token' } }),
  );
  await page.route('**/auth/register', async (route) => {
    expect(route.request().postDataJSON()).toEqual({
      email: 'new-candidate@example.test',
      password: 'Strong!Password123',
    });
    expect(route.request().headers()['x-csrf-token']).toBe('registration-csrf-token');
    authenticated = true;
    await route.fulfill({ json: session() });
  });
  await page.route('**/api/v1/matching/recommendations**', (route) =>
    route.fulfill({
      json: {
        candidateProfileId: 'new-profile',
        page: 1,
        pageSize: 3,
        totalCount: 0,
        totalPages: 0,
        scoringVersion: 'matching-v2:rules-1',
        items: [],
      },
    }),
  );

  await page.goto('/login');
  await page.getByRole('button', { name: 'Créer un compte' }).click();
  await expect(page.getByRole('heading', { name: 'Créez votre compte' })).toBeVisible();
  await page.getByLabel('Adresse e-mail').fill('new-candidate@example.test');
  await page.getByLabel('Mot de passe').fill('Strong!Password123');
  await page.getByRole('button', { name: 'Créer mon compte' }).click();

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole('heading', { name: 'Faites avancer votre prochaine étape.' })).toBeVisible();
});

test('anonymous candidate signs in and opens ranked recommendations', async ({ page }) => {
  let authenticated = false;
  let failFirstRecommendationsPage = true;
  const session = () => ({
    isAuthenticated: authenticated,
    email: authenticated ? 'candidate@example.test' : null,
    registrationEnabled: false,
  });

  await page.route('**/auth/session', (route) => route.fulfill({ json: session() }));
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({
      json: { requestToken: 'playwright-csrf-token' },
    }),
  );
  await page.route('**/auth/login', async (route) => {
    const request = route.request();
    expect(request.postDataJSON()).toEqual({
      email: 'candidate@example.test',
      password: 'a-valid-password',
      rememberMe: false,
    });
    expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
    authenticated = true;
    await route.fulfill({ json: session() });
  });
  await page.route('**/api/v1/matching/recommendations**', async (route) => {
    const pageSize = Number(new URL(route.request().url()).searchParams.get('pageSize'));
    if (pageSize === 10 && failFirstRecommendationsPage) {
      failFirstRecommendationsPage = false;
      await route.fulfill({ status: 503, json: { title: 'Matching indisponible' } });
      return;
    }

    await route.fulfill({
      json: {
        candidateProfileId: 'profile-1',
        page: 1,
        pageSize,
        totalCount: 1,
        totalPages: 1,
        scoringVersion: 'matching-v2:rules-1',
        items: [
          {
            jobId: 'job-1',
            title: 'Développeuse .NET',
            companyName: 'Atelier Numérique',
            location: 'Lyon',
            overallScore: 82,
            rankingScore: 78,
            evaluationCoverage: 90,
            criteria: [],
          },
        ],
      },
    });
  });

  await page.goto('/dashboard');
  await expect(page).toHaveURL(/\/login\?returnUrl=/);
  await expect(page.getByRole('heading', { name: 'Ravi de vous revoir' })).toBeVisible();

  await page.getByLabel('Adresse e-mail').fill('candidate@example.test');
  await page.getByLabel('Mot de passe').fill('a-valid-password');
  await page.getByRole('button', { name: /Se connecter/ }).click();

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(
    page.getByRole('heading', { name: 'Faites avancer votre prochaine étape.' }),
  ).toBeVisible();
  await expect(page.getByText('Atelier Numérique')).toBeVisible();

  await page.getByRole('link', { name: /Voir mes recommandations/ }).click();
  await expect(page).toHaveURL(/\/recommendations$/);
  await expect(
    page.getByRole('heading', { name: 'Des offres qui vous correspondent.' }),
  ).toBeVisible();
  await expect(page.getByRole('alert')).toContainText(
    'Les recommandations ne sont pas disponibles.',
  );
  await page.getByRole('button', { name: 'Réessayer' }).click();
  await expect(page.getByText('Développeuse .NET')).toBeVisible();
  await expect(page.getByText('78 / 100', { exact: true })).toBeVisible();
});

test('expired session redirects a protected page to login and preserves its return URL', async ({
  page,
}) => {
  let sessionChecks = 0;
  await page.route('**/auth/session', (route) => {
    sessionChecks += 1;
    return route.fulfill({
      json: {
        isAuthenticated: sessionChecks <= 2,
        email: sessionChecks <= 2 ? 'candidate@example.test' : null,
        registrationEnabled: false,
      },
    });
  });

  await page.goto('/recommendations');
  await expect(page).toHaveURL(/\/recommendations$/);
  await page.goto('/profile');

  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fprofile$/);
  await expect(page.getByRole('heading', { name: 'Ravi de vous revoir' })).toBeVisible();
});

test('invalid credentials show a clear error and keep the candidate on login', async ({ page }) => {
  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: { isAuthenticated: false, email: null, registrationEnabled: false },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/auth/login', async (route) => {
    expect(route.request().headers()['x-csrf-token']).toBe('playwright-csrf-token');
    await route.fulfill({ status: 401, json: { title: 'Unauthorized' } });
  });

  await page.goto('/login');
  await page.getByLabel('Adresse e-mail').fill('candidate@example.test');
  await page.getByLabel('Mot de passe').fill('incorrect-password');
  await page.getByRole('button', { name: /Se connecter/ }).click();

  await expect(page.getByRole('alert')).toHaveText('Adresse e-mail ou mot de passe invalide.');
  await expect(page).toHaveURL(/\/login$/);
});

test('successful sign-in returns the candidate to the protected destination', async ({ page }) => {
  let authenticated = false;
  const session = () => ({
    isAuthenticated: authenticated,
    email: authenticated ? 'candidate@example.test' : null,
    registrationEnabled: false,
  });
  await page.route('**/auth/session', (route) => route.fulfill({ json: session() }));
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/auth/login', async (route) => {
    expect(route.request().headers()['x-csrf-token']).toBe('playwright-csrf-token');
    authenticated = true;
    await route.fulfill({ json: session() });
  });
  await page.route('**/api/v1/matching/recommendations**', (route) =>
    route.fulfill({
      json: {
        candidateProfileId: 'profile-1',
        page: 1,
        pageSize: 10,
        totalCount: 1,
        totalPages: 1,
        scoringVersion: 'matching-v2:rules-1',
        items: [
          {
            jobId: 'job-1',
            title: 'Développeuse .NET',
            companyName: 'Atelier Numérique',
            location: 'Lyon',
            overallScore: 82,
            rankingScore: 78,
            evaluationCoverage: 90,
            criteria: [],
          },
        ],
      },
    }),
  );

  await page.goto('/recommendations');
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Frecommendations$/);
  await page.getByLabel('Adresse e-mail').fill('candidate@example.test');
  await page.getByLabel('Mot de passe').fill('a-valid-password');
  await page.getByRole('button', { name: /Se connecter/ }).click();

  await expect(page).toHaveURL(/\/recommendations$/);
  await expect(
    page.getByRole('heading', { name: 'Des offres qui vous correspondent.' }),
  ).toBeVisible();
  await expect(page.getByText('Développeuse .NET')).toBeVisible();
});

test('candidate adds a skill to the profile through the authenticated API', async ({ page }) => {
  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: {
        isAuthenticated: true,
        email: 'candidate@example.test',
        registrationEnabled: false,
      },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/profile', (route) =>
    route.fulfill({
      json: {
        id: 'profile-1',
        firstName: 'Camille',
        lastName: 'Martin',
        email: 'candidate@example.test',
        phoneNumber: null,
        jobTitle: 'Développeuse',
        summary: null,
        createdAtUtc: '2026-10-17T09:00:00Z',
        updatedAtUtc: '2026-10-17T09:00:00Z',
      },
    }),
  );
  await page.route('**/api/v1/profile/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname.endsWith('/skills')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      const skill = request.postDataJSON();
      expect(skill).toEqual({
        name: 'C#',
        category: 'Backend',
        level: 'Avancé',
        yearsOfExperience: 5,
      });
      await route.fulfill({
        json: {
          ...skill,
          id: 'skill-1',
          createdAtUtc: '2026-10-17T09:00:00Z',
          updatedAtUtc: '2026-10-17T09:00:00Z',
        },
      });
      return;
    }

    if (url.pathname.endsWith('/preferences')) {
      await route.fulfill({ status: 404, json: { title: 'Not found' } });
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/profile');
  await expect(page.getByRole('heading', { name: 'Informations personnelles' })).toBeVisible();
  await expect(page.getByText('Camille', { exact: true })).toBeVisible();
  await expect(page.getByText('Martin', { exact: true })).toBeVisible();
  await page.locator('.skills-card').getByRole('button', { name: '+ Ajouter' }).click();
  await page.getByLabel('Compétence *').fill('C#');
  await page.getByLabel('Catégorie').fill('Backend');
  await page.getByLabel('Niveau').fill('Avancé');
  await page.getByLabel(/Années d'expérience/).fill('5');
  await page.getByRole('button', { name: 'Ajouter la compétence' }).click();

  await expect(page.getByRole('button', { name: 'Modifier C#' })).toBeVisible();
  await expect(page.getByText('Avancé')).toBeVisible();
});

test('candidate configures job preferences with validated values', async ({ page }) => {
  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: {
        isAuthenticated: true,
        email: 'candidate@example.test',
        registrationEnabled: false,
      },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/profile', (route) =>
    route.fulfill({
      json: {
        id: 'profile-1',
        firstName: 'Camille',
        lastName: 'Martin',
        email: 'candidate@example.test',
        phoneNumber: null,
        jobTitle: 'Développeuse',
        summary: null,
        createdAtUtc: '2026-10-17T09:00:00Z',
        updatedAtUtc: '2026-10-17T09:00:00Z',
      },
    }),
  );
  await page.route('**/api/v1/profile/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname.endsWith('/preferences')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      const preferences = request.postDataJSON();
      expect(preferences).toEqual({
        desiredJobTitles: ['Développeuse .NET'],
        preferredLocations: ['Lyon'],
        contractTypes: ['CDI'],
        workModes: ['Hybrid', 'Remote'],
        minimumAnnualGrossSalary: 60000,
        salaryCurrency: 'EUR',
        availableFrom: null,
      });
      await route.fulfill({
        json: {
          ...preferences,
          candidateProfileId: 'profile-1',
          createdAtUtc: '2026-10-17T09:00:00Z',
          updatedAtUtc: '2026-10-17T09:00:00Z',
        },
      });
      return;
    }

    if (url.pathname.endsWith('/preferences')) {
      await route.fulfill({ status: 404, json: { title: 'Not found' } });
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto('/profile');
  await page.getByRole('button', { name: 'Configurer' }).click();
  await page.getByLabel('Postes recherchés').fill('Développeuse .NET');
  await page.getByLabel('Localisations souhaitées').fill('Lyon');
  await page.getByLabel('Types de contrat').fill('CDI');
  await page.getByLabel('Hybride').check();
  await page.getByLabel('Télétravail').check();
  await page.getByLabel('Salaire annuel brut minimum').fill('60000');
  await page.getByLabel('Devise').fill('eur');
  await page.getByRole('button', { name: 'Enregistrer les préférences' }).click();

  await expect(page.getByText('Développeuse .NET')).toBeVisible();
  await expect(page.getByText('Lyon')).toBeVisible();
  await expect(page.getByText('Hybride, Télétravail')).toBeVisible();
  await expect(page.getByText('60000 EUR')).toBeVisible();
});

test('candidate imports a PDF resume into the private document library', async ({ page }) => {
  let uploaded = false;
  const resume = {
    id: 'resume-1',
    fileName: 'cv-camille.pdf',
    mediaType: 'application/pdf',
    length: 128,
    status: 'UploadedUnanalysed',
    uploadedAtUtc: '2026-10-17T09:00:00Z',
  };
  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: {
        isAuthenticated: true,
        email: 'candidate@example.test',
        registrationEnabled: false,
      },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/documents/resumes?**', (route) =>
    route.fulfill({ json: uploaded ? [resume] : [] }),
  );
  await page.route('**/api/v1/documents/resume-series?**', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/documents/resumes', async (route) => {
    const request = route.request();
    expect(request.method()).toBe('POST');
    expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
    expect(request.headers()['content-type']).toContain('multipart/form-data');
    expect(request.postDataBuffer()?.toString()).toContain('cv-camille.pdf');
    uploaded = true;
    await route.fulfill({ json: resume });
  });

  await page.goto('/documents');
  await expect(page.getByText('Aucun CV importé')).toBeVisible();
  await page.locator('input[type="file"]').setInputFiles('e2e/fixtures/cv-camille.pdf');

  await expect(page.locator('.resume-row__main strong')).toHaveText('cv-camille.pdf');
  await expect(page.getByText('Importé · non analysé')).toBeVisible();
  await expect(page.getByText('1', { exact: true }).first()).toBeVisible();
});

test('candidate explicitly groups an imported resume into a version series', async ({ page }) => {
  const resume = {
    id: 'resume-1',
    fileName: 'cv-camille.pdf',
    mediaType: 'application/pdf',
    length: 128,
    sha256: 'a'.repeat(64),
    uploadedAtUtc: '2026-10-17T09:00:00Z',
    status: 'UploadedUnanalysed',
  };
  let seriesCreated = false;
  const series = {
    id: 'series-1',
    name: 'CV Développement backend',
    createdAtUtc: '2026-10-17T09:05:00Z',
    versionCount: 1,
  };
  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: {
        isAuthenticated: true,
        email: 'candidate@example.test',
        registrationEnabled: false,
      },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/documents/resumes?**', (route) => route.fulfill({ json: [resume] }));
  await page.route('**/api/v1/documents/resume-series**', async (route) => {
    const request = route.request();
    if (request.method() === 'POST') {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({
        name: 'CV Développement backend',
        firstResumeId: 'resume-1',
        label: 'Version initiale',
      });
      seriesCreated = true;
      await route.fulfill({
        json: {
          series,
          version: {
            seriesId: 'series-1',
            versionNumber: 1,
            label: 'Version initiale',
            addedAtUtc: '2026-10-17T09:05:00Z',
            resume,
          },
          created: true,
        },
      });
      return;
    }

    await route.fulfill({ json: seriesCreated ? [series] : [] });
  });

  await page.goto('/documents');
  await page.getByLabel('Nom du dossier').fill('CV Développement backend');
  await page.getByLabel('CV initial').selectOption('resume-1');
  await page.getByLabel('Libellé de version').fill('Version initiale');
  await page.getByRole('button', { name: 'Créer le dossier' }).click();

  await expect(page.getByText('CV Développement backend')).toBeVisible();
  await expect(page.getByText('1 version')).toBeVisible();
});

test('candidate adds and reviews a new resume version', async ({ page }) => {
  const firstResume = {
    id: 'resume-1',
    fileName: 'cv-camille-v1.pdf',
    mediaType: 'application/pdf',
    length: 128,
    sha256: 'a'.repeat(64),
    uploadedAtUtc: '2026-10-17T09:00:00Z',
    status: 'UploadedUnanalysed',
  };
  const secondResume = {
    ...firstResume,
    id: 'resume-2',
    fileName: 'cv-camille-v2.pdf',
    uploadedAtUtc: '2026-10-17T10:00:00Z',
  };
  let addedVersion = false;
  let versionsRequested = false;
  const series = {
    id: 'series-1',
    name: 'CV Développement backend',
    createdAtUtc: '2026-10-17T09:05:00Z',
    versionCount: 1,
  };
  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: {
        isAuthenticated: true,
        email: 'candidate@example.test',
        registrationEnabled: false,
      },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/documents/resumes?**', (route) =>
    route.fulfill({ json: [firstResume, secondResume] }),
  );
  await page.route('**/api/v1/documents/resume-series**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST') {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(url.pathname).toBe('/api/v1/documents/resume-series/series-1/versions');
      expect(request.postDataJSON()).toEqual({
        resumeId: 'resume-2',
        label: 'Ciblé .NET',
      });
      addedVersion = true;
      await route.fulfill({
        json: {
          series: { ...series, versionCount: 2 },
          version: {
            seriesId: 'series-1',
            versionNumber: 2,
            label: 'Ciblé .NET',
            addedAtUtc: '2026-10-17T10:05:00Z',
            resume: secondResume,
          },
          created: true,
        },
      });
      return;
    }

    if (url.pathname.endsWith('/versions')) {
      versionsRequested = true;
      await route.fulfill({
        json: [
          {
            seriesId: 'series-1',
            versionNumber: 1,
            label: 'Version initiale',
            addedAtUtc: '2026-10-17T09:05:00Z',
            resume: firstResume,
          },
          ...(addedVersion
            ? [
              {
                seriesId: 'series-1',
                versionNumber: 2,
                label: 'Ciblé .NET',
                addedAtUtc: '2026-10-17T10:05:00Z',
                resume: secondResume,
              },
            ]
            : []),
        ],
      });
      return;
    }

    await route.fulfill({
      json: [{ ...series, versionCount: addedVersion ? 2 : 1 }],
    });
  });

  await page.goto('/documents');
  await page.getByRole('button', { name: /CV Développement backend/ }).click();
  await expect(page.locator('.version-copy strong').first()).toHaveText('cv-camille-v1.pdf');
  await page.getByLabel('Ajouter une version').selectOption('resume-2');
  await page.locator('.add-version').getByLabel('Libellé').fill('Ciblé .NET');
  await page.getByRole('button', { name: 'Ajouter' }).click();
  await expect(page.getByText('2 versions')).toBeVisible();

  await page.getByRole('button', { name: /CV Développement backend/ }).click();
  await page.getByRole('button', { name: /CV Développement backend/ }).click();
  await expect(
    page.locator('.version-copy strong').filter({ hasText: 'cv-camille-v2.pdf' }),
  ).toBeVisible();
  await expect(page.getByText('Ciblé .NET')).toBeVisible();
  expect(versionsRequested).toBe(true);
});

test('candidate approves composed content only after explicit confirmation', async ({ page }) => {
  const draftSummary = {
    id: 'draft-1',
    requestId: 'request-1',
    resumeId: 'resume-1',
    jobId: 'job-1',
    kind: 'Resume',
    status: 'PreparedForReview',
    snapshotSha256: 'b'.repeat(64),
    createdAtUtc: '2026-10-17T09:00:00Z',
  };
  const resume = {
    id: 'resume-1',
    fileName: 'cv-camille.pdf',
    mediaType: 'application/pdf',
    length: 128,
    sha256: 'a'.repeat(64),
    uploadedAtUtc: '2026-10-17T08:00:00Z',
    status: 'UploadedUnanalysed',
  };
  const contentSha256 = 'c'.repeat(64);
  let approved = false;
  let exportCreated = false;
  const composition = () => ({
    composition: {
      draftId: 'draft-1',
      kind: 'Resume',
      templateVersion: 'resume-v1',
      sourceSnapshotSha256: draftSummary.snapshotSha256,
      contentSha256,
      createdAtUtc: '2026-10-17T09:05:00Z',
      decision: approved ? 'Approved' : 'Pending',
      reviewedAtUtc: approved ? '2026-10-17T09:10:00Z' : null,
      reviewNote: approved ? 'Vérifié par la candidate' : null,
    },
    content: {
      title: 'CV adapté — Atelier Numérique',
      plainText: 'Camille Martin\nDéveloppeuse .NET',
      blocks: [],
      notices: [],
    },
  });
  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: {
        isAuthenticated: true,
        email: 'candidate@example.test',
        registrationEnabled: false,
      },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/profile', (route) =>
    route.fulfill({
      json: {
        id: 'profile-1',
        firstName: 'Camille',
        lastName: 'Martin',
        email: 'candidate@example.test',
      },
    }),
  );
  await page.route('**/api/v1/applications**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname === '/api/v1/applications') {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toMatchObject({ profileId: 'profile-1', jobId: 'job-1' });
      await route.fulfill({ json: { id: 'application-1' } });
      return;
    }
    if (request.method() === 'POST' && url.pathname.endsWith('/documents')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({ expectedRevision: 1, exportId: 'export-1' });
      await route.fulfill({ json: { revision: 2 } });
      return;
    }
    if (request.method() === 'GET' && url.pathname.endsWith('/application-1')) {
      await route.fulfill({
        json: {
          id: 'application-1',
          profileId: 'profile-1',
          jobId: 'job-1',
          jobTitle: 'Développeuse .NET',
          companyName: 'Atelier Numérique',
          status: 'Draft',
          revision: 1,
        },
      });
      return;
    }
    await route.fulfill({ status: 404, json: { title: 'Not found' } });
  });
  await page.route('**/api/v1/documents/**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname.endsWith('/composition/review')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({
        contentSha256,
        decision: 'Approved',
        confirmed: true,
        note: 'Vérifié par la candidate',
      });
      approved = true;
      await route.fulfill({ json: composition() });
      return;
    }
    if (request.method() === 'POST' && url.pathname.endsWith('/draft-1/exports')) {
      expect(approved).toBe(true);
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({ format: 'Pdf', contentSha256 });
      exportCreated = true;
      await route.fulfill({
        json: {
          export: {
            id: 'export-1',
            draftId: 'draft-1',
            format: 'Pdf',
            rendererVersion: 'pdf-v1',
            compositionSha256: contentSha256,
            fileSha256: 'd'.repeat(64),
            fileName: 'cv-adapte.pdf',
            mediaType: 'application/pdf',
            length: 256,
            pageCount: 1,
            createdAtUtc: '2026-10-17T09:15:00Z',
            approvedAtUtc: '2026-10-17T09:10:00Z',
          },
          created: true,
        },
      });
      return;
    }
    if (url.pathname.endsWith('/exports/export-1/content')) {
      expect(exportCreated).toBe(true);
      await route.fulfill({
        status: 200,
        contentType: 'application/pdf',
        body: '%PDF-1.4\n% synthetic export\n%%EOF',
      });
      return;
    }

    if (url.pathname.endsWith('/drafts')) {
      await route.fulfill({ json: [draftSummary] });
      return;
    }
    if (url.pathname.endsWith('/draft-1/composition')) {
      await route.fulfill({ json: composition() });
      return;
    }
    if (url.pathname.endsWith('/draft-1')) {
      await route.fulfill({
        json: {
          draft: draftSummary,
          snapshot: {
            schemaVersion: '1',
            job: {
              id: 'job-1',
              title: 'Développeuse .NET',
              companyName: 'Atelier Numérique',
              location: 'Lyon',
              description: null,
              source: 'Local',
              updatedAtUtc: '2026-10-17T08:00:00Z',
            },
            candidate: { resume, version: null, facts: [] },
          },
        },
      });
      return;
    }
    if (url.pathname.endsWith('/exports')) {
      await route.fulfill({ json: [] });
      return;
    }

    await route.fulfill({ status: 404, json: { title: 'Not found' } });
  });

  await page.goto('/preparations');
  await page.getByRole('button', { name: 'Ouvrir' }).click();
  await expect(page.getByText('Camille Martin\nDéveloppeuse .NET')).toBeVisible();
  const approveButton = page.getByRole('button', { name: 'Approuver ce contenu' });
  await expect(page.getByRole('button', { name: 'Générer PDF' })).toHaveCount(0);
  await expect(approveButton).toBeDisabled();
  await page.getByLabel('Note de revue, facultative').fill('Vérifié par la candidate');
  await page.getByLabel(/J’ai relu ce contenu/).check();
  await expect(approveButton).toBeEnabled();
  await approveButton.click();

  await expect(page.getByText('Approuvé', { exact: true })).toBeVisible();
  await expect(page.getByText('Note de revue : Vérifié par la candidate')).toBeVisible();
  await expect(
    page.getByText(
      'La composition, la revue et l’export restent des actions explicites. Aucun fichier n’est envoyé à un recruteur.',
    ),
  ).toBeVisible();
  await page.getByRole('button', { name: 'Générer PDF' }).click();
  await expect(page.getByText('Document PDF')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Créer le dossier' })).toBeVisible();
  await page.getByRole('button', { name: 'Créer le dossier' }).click();
  await expect(page.getByRole('button', { name: 'Dossier créé' })).toBeDisabled();
  await expect(
    page.getByRole('status').filter({ hasText: 'Dossier prêt et export associé' }),
  ).toBeVisible();
});

test('candidate records a manually submitted approved application', async ({ page }) => {
  const summary = {
    id: 'application-1',
    profileId: 'profile-1',
    jobId: 'job-1',
    jobTitle: 'Développeuse .NET',
    companyName: 'Atelier Numérique',
    status: 'Approved',
    revision: 2,
    createdAtUtc: '2026-10-17T08:00:00Z',
    updatedAtUtc: '2026-10-17T09:00:00Z',
  };
  let submitted = false;
  const detail = () => ({
    ...summary,
    status: submitted ? 'Submitted' : 'Approved',
    revision: submitted ? 3 : 2,
    updatedAtUtc: submitted ? '2026-10-17T10:00:00Z' : summary.updatedAtUtc,
    resume: { exportId: 'export-1', kind: 'Resume', sha256: 'a'.repeat(64) },
    coverLetter: { exportId: 'export-2', kind: 'CoverLetter', sha256: 'b'.repeat(64) },
    submission: submitted
      ? {
        occurredAtUtc: '2026-10-17T10:00:00Z',
        channel: 'CompanyWebsite',
        reference: 'REF-2026-15',
      }
      : null,
    followUps: [],
    pendingFollowUp: null,
    events: [
      {
        sequence: 1,
        kind: 'Created',
        status: 'Draft',
        revision: 1,
        atUtc: '2026-10-17T08:00:00Z',
        note: null,
        document: null,
        submission: null,
        recruiterUpdate: null,
        followUp: null,
      },
      {
        sequence: 2,
        kind: 'Approved',
        status: 'Approved',
        revision: 2,
        atUtc: '2026-10-17T09:00:00Z',
        note: null,
        document: null,
        submission: null,
        recruiterUpdate: null,
        followUp: null,
      },
      ...(submitted
        ? [
          {
            sequence: 3,
            kind: 'ManualSubmissionDeclared',
            status: 'Submitted',
            revision: 3,
            atUtc: '2026-10-17T10:00:00Z',
            note: null,
            document: null,
            submission: {
              occurredAtUtc: '2026-10-17T10:00:00Z',
              channel: 'CompanyWebsite',
              reference: 'REF-2026-15',
            },
            recruiterUpdate: null,
            followUp: null,
          },
        ]
        : []),
    ],
  });

  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: {
        isAuthenticated: true,
        email: 'candidate@example.test',
        registrationEnabled: false,
      },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/applications**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname.endsWith('/manual-submission')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      const payload = request.postDataJSON();
      expect(payload).toMatchObject({
        expectedRevision: 2,
        confirmed: true,
        channel: 'CompanyWebsite',
        reference: 'REF-2026-15',
      });
      expect(Date.parse(payload.occurredAtUtc)).not.toBeNaN();
      submitted = true;
      await route.fulfill({ json: detail() });
      return;
    }
    if (url.pathname.endsWith('/follow-ups/due')) {
      await route.fulfill({
        json: { page: 1, pageSize: 100, asOfUtc: '2026-10-17T10:00:00Z', items: [] },
      });
      return;
    }
    if (url.pathname.endsWith('/application-1')) {
      await route.fulfill({ json: detail() });
      return;
    }
    await route.fulfill({ json: { page: 1, pageSize: 100, items: [detail()] } });
  });

  await page.goto('/applications');
  await page.getByRole('button', { name: /Développeuse .NET/ }).click();
  await expect(
    page.getByRole('heading', { name: 'Vérifiez le dossier avant l’envoi' }),
  ).toBeVisible();
  await expect(page.getByRole('status')).toHaveCount(0);
  await page.getByLabel('J’ai vérifié le poste et l’entreprise affichés ci-dessus.').check();
  await page
    .getByLabel('J’ai ouvert et vérifié le CV et la lettre associés à cette version approuvée.')
    .check();
  await expect(page.getByRole('status')).toContainText(
    'Vous pouvez envoyer ces pièces manuellement',
  );
  await expect(page.getByRole('heading', { name: 'Enregistrer un envoi manuel' })).toBeVisible();
  await page.getByLabel('Date et heure de l’envoi').fill('2026-10-17T10:00');
  await page.getByLabel('Canal utilisé').selectOption('CompanyWebsite');
  await page.getByLabel('Référence, facultative').fill('REF-2026-15');
  await page.getByLabel('Je confirme avoir envoyé cette candidature manuellement.').check();
  await page.getByRole('button', { name: 'Enregistrer l’envoi' }).click();

  await expect(page.locator('.detail-heading .status-pill')).toHaveText('Envoyée manuellement');
  await expect(page.getByText('Envoi manuel déclaré')).toBeVisible();
  await expect(page.getByText(/Site entreprise · REF-2026-15/)).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Enregistrer un envoi manuel' })).toHaveCount(0);
});

test('candidate records a recruiter reply and advances the application status', async ({
  page,
}) => {
  const base = {
    id: 'application-2',
    profileId: 'profile-1',
    jobId: 'job-2',
    jobTitle: 'Ingénieure backend',
    companyName: 'Atelier Numérique',
    createdAtUtc: '2026-10-17T08:00:00Z',
  };
  let recruiterReplied = false;
  const detail = () => ({
    ...base,
    status: recruiterReplied ? 'Interview' : 'InReview',
    revision: recruiterReplied ? 5 : 4,
    updatedAtUtc: recruiterReplied ? '2026-10-17T11:00:00Z' : '2026-10-17T10:00:00Z',
    resume: null,
    coverLetter: null,
    submission: {
      occurredAtUtc: '2026-10-17T09:30:00Z',
      channel: 'CompanyWebsite',
      reference: null,
    },
    followUps: [],
    pendingFollowUp: null,
    events: [
      {
        sequence: 1,
        kind: 'Created',
        status: 'Draft',
        revision: 1,
        atUtc: '2026-10-17T08:00:00Z',
        note: null,
        document: null,
        submission: null,
        recruiterUpdate: null,
        followUp: null,
      },
      {
        sequence: 2,
        kind: 'ManualSubmissionDeclared',
        status: 'Submitted',
        revision: 3,
        atUtc: '2026-10-17T09:30:00Z',
        note: null,
        document: null,
        submission: {
          occurredAtUtc: '2026-10-17T09:30:00Z',
          channel: 'CompanyWebsite',
          reference: null,
        },
        recruiterUpdate: null,
        followUp: null,
      },
      ...(recruiterReplied
        ? [
          {
            sequence: 3,
            kind: 'RecruiterUpdateRecorded',
            status: 'Interview',
            revision: 5,
            atUtc: '2026-10-17T11:00:00Z',
            note: null,
            document: null,
            submission: null,
            recruiterUpdate: {
              occurredAtUtc: '2026-10-17T11:00:00Z',
              channel: 'Email',
              message: 'Nous souhaitons organiser un entretien.',
            },
            followUp: null,
          },
        ]
        : []),
    ],
  });

  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: {
        isAuthenticated: true,
        email: 'candidate@example.test',
        registrationEnabled: false,
      },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/applications**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname.endsWith('/recruiter-updates')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toMatchObject({
        expectedRevision: 4,
        confirmed: true,
        status: 'Interview',
        channel: 'Email',
        message: 'Nous souhaitons organiser un entretien.',
      });
      recruiterReplied = true;
      await route.fulfill({ json: detail() });
      return;
    }
    if (url.pathname.endsWith('/follow-ups/due')) {
      await route.fulfill({
        json: { page: 1, pageSize: 100, asOfUtc: '2026-10-17T11:00:00Z', items: [] },
      });
      return;
    }
    if (url.pathname.endsWith('/application-2')) {
      await route.fulfill({ json: detail() });
      return;
    }
    await route.fulfill({ json: { page: 1, pageSize: 100, items: [detail()] } });
  });

  await page.goto('/applications');
  await page.locator('.application-row').filter({ hasText: 'Ingénieure backend' }).click();
  await expect(
    page.getByRole('heading', { name: 'Enregistrer un retour recruteur' }),
  ).toBeVisible();
  await expect(
    page.getByLabel('Nouveau statut').locator('option', { hasText: 'Refusée' }),
  ).toHaveCount(1);
  await page.getByLabel('Nouveau statut').selectOption('Interview');
  await page.getByLabel('Date et heure du retour').fill('2026-10-17T11:00');
  await page.getByLabel('Canal reçu').selectOption('Email');
  await page.getByLabel('Message reçu').fill('Nous souhaitons organiser un entretien.');
  await page.getByLabel('Je confirme que ce retour a été reçu.').check();
  await page.getByRole('button', { name: 'Enregistrer le retour' }).click();

  await expect(page.locator('.detail-heading .status-pill')).toHaveText('Entretien');
  await expect(
    page.getByText('Retour reçu · Courriel · Nous souhaitons organiser un entretien.'),
  ).toBeVisible();
});

test('candidate completes a planned follow-up and records its outcome', async ({ page }) => {
  const plan = {
    id: 'follow-up-1',
    dueAtUtc: '2026-10-20T09:00:00Z',
    channel: 'Email',
    note: 'Demander un retour après l’entretien',
  };
  let completed = false;
  const detail = () => ({
    id: 'application-3',
    profileId: 'profile-1',
    jobId: 'job-3',
    jobTitle: 'Ingénieure backend',
    companyName: 'Atelier Numérique',
    status: 'InReview',
    revision: completed ? 5 : 4,
    createdAtUtc: '2026-10-17T08:00:00Z',
    updatedAtUtc: completed ? '2026-10-20T09:15:00Z' : '2026-10-17T10:00:00Z',
    resume: null,
    coverLetter: null,
    submission: {
      occurredAtUtc: '2026-10-17T09:30:00Z',
      channel: 'CompanyWebsite',
      reference: null,
    },
    followUps: [
      {
        plan,
        status: completed ? 'Completed' : 'Planned',
        occurredAtUtc: completed ? '2026-10-20T09:15:00Z' : null,
        actualChannel: completed ? 'Email' : null,
        outcome: completed ? 'Le recruteur a confirmé le prochain entretien.' : null,
      },
    ],
    pendingFollowUp: completed
      ? null
      : {
        plan,
        status: 'Planned',
        occurredAtUtc: null,
        actualChannel: null,
        outcome: null,
      },
    events: [
      {
        sequence: 1,
        kind: completed ? 'FollowUpCompleted' : 'FollowUpPlanned',
        status: 'InReview',
        revision: completed ? 5 : 4,
        atUtc: completed ? '2026-10-20T09:15:00Z' : '2026-10-17T10:00:00Z',
        note: null,
        document: null,
        submission: null,
        recruiterUpdate: null,
        followUp: {
          plan,
          status: completed ? 'Completed' : 'Planned',
          occurredAtUtc: completed ? '2026-10-20T09:15:00Z' : null,
          actualChannel: completed ? 'Email' : null,
          outcome: completed ? 'Le recruteur a confirmé le prochain entretien.' : null,
        },
      },
    ],
  });

  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: { isAuthenticated: true, email: 'candidate@example.test', registrationEnabled: false },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/applications**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname.endsWith('/follow-ups/follow-up-1/complete')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toMatchObject({
        expectedRevision: 4,
        confirmed: true,
        channel: 'Email',
        outcome: 'Le recruteur a confirmé le prochain entretien.',
      });
      expect(Date.parse(request.postDataJSON().occurredAtUtc)).not.toBeNaN();
      completed = true;
      await route.fulfill({ json: detail() });
      return;
    }
    if (url.pathname.endsWith('/follow-ups/due')) {
      await route.fulfill({
        json: {
          page: 1,
          pageSize: 100,
          asOfUtc: '2026-10-20T09:15:00Z',
          items: completed
            ? []
            : [
              {
                candidacyId: 'application-3',
                jobTitle: 'Ingénieure backend',
                companyName: 'Atelier Numérique',
                status: 'InReview',
                revision: 4,
                followUpId: plan.id,
                dueAtUtc: plan.dueAtUtc,
                channel: plan.channel,
                note: plan.note,
              },
            ],
        },
      });
      return;
    }
    if (url.pathname.endsWith('/application-3')) {
      await route.fulfill({ json: detail() });
      return;
    }
    await route.fulfill({ json: { page: 1, pageSize: 100, items: [detail()] } });
  });

  await page.goto('/applications');
  await page.locator('.application-row').filter({ hasText: 'Ingénieure backend' }).click();
  await expect(
    page.getByRole('heading', { name: 'Enregistrer la relance effectuée' }),
  ).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Annuler le rappel' })).toBeVisible();
  await page.getByLabel('Date et heure de la relance').fill('2026-10-20T09:15');
  await page.getByLabel('Résultat').fill('Le recruteur a confirmé le prochain entretien.');
  await page.getByLabel('Je confirme avoir effectué cette relance.').check();
  await page.getByRole('button', { name: 'Marquer comme effectuée' }).click();

  await expect(page.getByText('Relance effectuée', { exact: true })).toBeVisible();
  await expect(page.getByText(/Le recruteur a confirmé le prochain entretien\./)).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Annuler le rappel' })).toHaveCount(0);
});

test('candidate reschedules a pending follow-up and keeps both plans in the history', async ({
  page,
}) => {
  let rescheduled = false;
  let expectedDueAtUtc: string | null = null;
  const plan = () => ({
    id: 'follow-up-reschedule',
    dueAtUtc: rescheduled ? '2026-10-21T09:00:00Z' : '2026-10-17T09:00:00Z',
    channel: 'Email',
    note: rescheduled ? 'Relancer après vérification du dossier' : 'Demander un retour',
  });
  const detail = () => ({
    id: 'application-reschedule',
    profileId: 'profile-1',
    jobId: 'job-3',
    jobTitle: 'Ingénieure backend',
    companyName: 'Atelier Numérique',
    status: 'InReview',
    revision: rescheduled ? 5 : 4,
    createdAtUtc: '2026-10-17T08:00:00Z',
    updatedAtUtc: rescheduled ? '2026-10-20T08:00:00Z' : '2026-10-17T10:00:00Z',
    resume: null,
    coverLetter: null,
    submission: null,
    followUps: [
      { plan: plan(), status: 'Planned', occurredAtUtc: null, actualChannel: null, outcome: null },
    ],
    pendingFollowUp: {
      plan: plan(),
      status: 'Planned',
      occurredAtUtc: null,
      actualChannel: null,
      outcome: null,
    },
    events: (rescheduled ? ['original', 'rescheduled'] : ['original']).map((version, index) => ({
      sequence: index + 1,
      kind: version === 'original' ? 'FollowUpPlanned' : 'FollowUpRescheduled',
      status: 'InReview',
      revision: version === 'original' ? 4 : 5,
      atUtc: version === 'original' ? '2026-10-17T10:00:00Z' : '2026-10-20T08:00:00Z',
      note: null,
      document: null,
      submission: null,
      recruiterUpdate: null,
      followUp: {
        plan:
          version === 'original'
            ? { ...plan(), dueAtUtc: '2026-10-17T09:00:00Z', note: 'Demander un retour' }
            : plan(),
        status: 'Planned',
        occurredAtUtc: null,
        actualChannel: null,
        outcome: null,
      },
    })),
  });

  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: { isAuthenticated: true, email: 'candidate@example.test', registrationEnabled: false },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/applications**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (
      request.method() === 'POST' &&
      url.pathname.endsWith('/follow-ups/follow-up-reschedule/reschedule')
    ) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({
        expectedRevision: 4,
        dueAtUtc: expectedDueAtUtc,
        channel: 'Email',
        note: 'Relancer après vérification du dossier',
      });
      rescheduled = true;
      await route.fulfill({ json: detail() });
      return;
    }
    if (url.pathname.endsWith('/follow-ups/due')) {
      await route.fulfill({
        json: {
          page: 1,
          pageSize: 100,
          asOfUtc: '2026-10-20T08:00:00Z',
          items: rescheduled
            ? []
            : [
              {
                candidacyId: 'application-reschedule',
                jobTitle: 'Ingénieure backend',
                companyName: 'Atelier Numérique',
                status: 'InReview',
                revision: 4,
                followUpId: 'follow-up-reschedule',
                dueAtUtc: '2026-10-17T09:00:00Z',
                channel: 'Email',
                note: 'Demander un retour',
              },
            ],
        },
      });
      return;
    }
    if (url.pathname.endsWith('/application-reschedule')) {
      await route.fulfill({ json: detail() });
      return;
    }
    await route.fulfill({ json: { page: 1, pageSize: 100, items: [detail()] } });
  });

  await page.goto('/applications');
  await expect(page.locator('.due-item')).toHaveCount(1);
  await page.locator('.application-row').filter({ hasText: 'Ingénieure backend' }).click();
  const dueInput = page.getByLabel('Nouvelle date et heure');
  await expect(dueInput).toHaveAttribute('min', /.+/);
  await expect(dueInput).toHaveAttribute('max', /.+/);
  await dueInput.fill('2020-01-01T09:00');
  expect(await dueInput.evaluate((input: HTMLInputElement) => input.validity.rangeUnderflow)).toBe(
    true,
  );
  await dueInput.fill('2035-01-01T09:00');
  expect(await dueInput.evaluate((input: HTMLInputElement) => input.validity.rangeOverflow)).toBe(
    true,
  );
  await dueInput.fill('2026-10-21T09:00');
  expectedDueAtUtc = await page.evaluate(() => new Date('2026-10-21T09:00').toISOString());
  await page
    .locator('.action-card')
    .filter({ hasText: 'Modifier la relance' })
    .getByLabel('Note')
    .fill('Relancer après vérification du dossier');
  await page.getByRole('button', { name: 'Enregistrer la nouvelle relance' }).click();

  await expect(page.getByText('Relance planifiée')).toBeVisible();
  await expect(page.getByText('Relance replanifiée')).toBeVisible();
  await expect(page.getByText(/Demander un retour/)).toBeVisible();
  await expect(
    page
      .locator('.timeline-copy')
      .filter({ hasText: 'Relance replanifiée' })
      .getByText(/Relancer après vérification du dossier/),
  ).toBeVisible();
  await expect(page.getByText('Après cet événement : En cours · Révision 5')).toBeVisible();
  await expect(page.locator('.due-item')).toHaveCount(0);
});

test('candidate cancels a planned follow-up with a recorded reason', async ({ page }) => {
  const plan = {
    id: 'follow-up-2',
    dueAtUtc: '2026-10-20T09:00:00Z',
    channel: 'Email',
    note: 'Demander un retour après l’entretien',
  };
  let cancelled = false;
  const change = () => ({
    plan,
    status: cancelled ? 'Cancelled' : 'Planned',
    occurredAtUtc: null,
    actualChannel: null,
    outcome: cancelled ? 'Le recruteur a déjà répondu.' : null,
  });
  const detail = () => ({
    id: 'application-4',
    profileId: 'profile-1',
    jobId: 'job-4',
    jobTitle: 'Ingénieure backend',
    companyName: 'Atelier Numérique',
    status: 'InReview',
    revision: cancelled ? 5 : 4,
    createdAtUtc: '2026-10-17T08:00:00Z',
    updatedAtUtc: cancelled ? '2026-10-17T11:00:00Z' : '2026-10-17T10:00:00Z',
    resume: null,
    coverLetter: null,
    submission: {
      occurredAtUtc: '2026-10-17T09:30:00Z',
      channel: 'CompanyWebsite',
      reference: null,
    },
    followUps: [change()],
    pendingFollowUp: cancelled ? null : change(),
    events: [
      {
        sequence: 1,
        kind: cancelled ? 'FollowUpCancelled' : 'FollowUpPlanned',
        status: 'InReview',
        revision: cancelled ? 5 : 4,
        atUtc: cancelled ? '2026-10-17T11:00:00Z' : '2026-10-17T10:00:00Z',
        note: null,
        document: null,
        submission: null,
        recruiterUpdate: null,
        followUp: change(),
      },
    ],
  });

  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: { isAuthenticated: true, email: 'candidate@example.test', registrationEnabled: false },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/applications**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname.endsWith('/follow-ups/follow-up-2/cancel')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({
        expectedRevision: 4,
        confirmed: true,
        reason: 'Le recruteur a déjà répondu.',
      });
      cancelled = true;
      await route.fulfill({ json: detail() });
      return;
    }
    if (url.pathname.endsWith('/follow-ups/due')) {
      await route.fulfill({
        json: {
          page: 1,
          pageSize: 100,
          asOfUtc: '2026-10-17T11:00:00Z',
          items: cancelled
            ? []
            : [
              {
                candidacyId: 'application-4',
                jobTitle: 'Ingénieure backend',
                companyName: 'Atelier Numérique',
                status: 'InReview',
                revision: 4,
                followUpId: plan.id,
                dueAtUtc: plan.dueAtUtc,
                channel: plan.channel,
                note: plan.note,
              },
            ],
        },
      });
      return;
    }
    if (url.pathname.endsWith('/application-4')) {
      await route.fulfill({ json: detail() });
      return;
    }
    await route.fulfill({ json: { page: 1, pageSize: 100, items: [detail()] } });
  });

  await page.goto('/applications');
  await page.locator('.application-row').filter({ hasText: 'Ingénieure backend' }).click();
  await page
    .locator('.action-card')
    .filter({ hasText: 'Annuler le rappel' })
    .getByLabel('Motif')
    .fill('Le recruteur a déjà répondu.');
  await page.getByLabel('Je confirme l’annulation de ce rappel.').check();
  await page.getByRole('button', { name: 'Annuler le rappel' }).click();

  await expect(page.getByText('Relance annulée')).toBeVisible();
  await expect(page.getByText(/Le recruteur a déjà répondu\./)).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Annuler le rappel' })).toHaveCount(0);
});

test('candidate requests review and approves a dossier with both documents', async ({ page }) => {
  let status = 'Draft';
  let revision = 2;
  let resumeExportId = 'resume-export';
  const detail = () => ({
    id: 'application-review',
    profileId: 'profile-1',
    jobId: 'job-1',
    jobTitle: 'Développeuse .NET',
    companyName: 'Atelier Numérique',
    status,
    revision,
    createdAtUtc: '2026-10-17T08:00:00Z',
    updatedAtUtc: '2026-10-17T09:00:00Z',
    resume: { exportId: resumeExportId, kind: 'Resume', sha256: 'a'.repeat(64) },
    coverLetter: { exportId: 'letter-export', kind: 'CoverLetter', sha256: 'b'.repeat(64) },
    submission: null,
    followUps: [],
    pendingFollowUp: null,
    events: [
      {
        sequence: revision,
        kind:
          status === 'Draft'
            ? revision >= 6
              ? 'ReturnedToDraft'
              : 'DocumentAttached'
            : status === 'ReadyForReview'
              ? 'ReviewRequested'
              : 'Approved',
        status,
        revision,
        atUtc: '2026-10-17T09:00:00Z',
        note: null,
        document: null,
        submission: null,
        recruiterUpdate: null,
        followUp: null,
      },
    ],
  });

  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: { isAuthenticated: true, email: 'candidate@example.test', registrationEnabled: false },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/documents/drafts**', (route) =>
    route.fulfill({
      json: [
        { id: 'resume-draft', jobId: 'job-1', kind: 'Resume' },
        { id: 'letter-draft', jobId: 'job-1', kind: 'CoverLetter' },
        { id: 'other-job-draft', jobId: 'job-2', kind: 'Resume' },
      ],
    }),
  );
  await page.route('**/api/v1/documents/exports**', async (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith('/resume-export/content')) {
      await route.fulfill({
        contentType: 'application/pdf',
        body: '%PDF-1.4\n% preview fixture\n%%EOF',
      });
      return;
    }
    await route.fulfill({
      json: [
        {
          id: 'resume-export',
          draftId: 'resume-draft',
          format: 'Pdf',
          fileName: 'cv-actuel.pdf',
          mediaType: 'application/pdf',
          approvedAtUtc: '2026-10-17T08:00:00Z',
        },
        {
          id: 'resume-new',
          draftId: 'resume-draft',
          format: 'Pdf',
          fileName: 'cv-revise.pdf',
          mediaType: 'application/pdf',
          approvedAtUtc: '2026-10-17T08:00:00Z',
        },
        {
          id: 'letter-new',
          draftId: 'letter-draft',
          format: 'Pdf',
          fileName: 'lettre.pdf',
          mediaType: 'application/pdf',
          approvedAtUtc: '2026-10-17T08:00:00Z',
        },
        {
          id: 'other-job-export',
          draftId: 'other-job-draft',
          format: 'Pdf',
          fileName: 'autre-offre.pdf',
          mediaType: 'application/pdf',
          approvedAtUtc: '2026-10-17T08:00:00Z',
        },
      ],
    });
  });
  await page.route('**/api/v1/applications**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname.endsWith('/application-review/documents')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({ expectedRevision: 2, exportId: 'resume-new' });
      resumeExportId = 'resume-new';
      revision = 3;
      await route.fulfill({ json: detail() });
      return;
    }
    if (request.method() === 'POST' && url.pathname.endsWith('/application-review/review')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({ expectedRevision: 3 });
      status = 'ReadyForReview';
      revision = 4;
      await route.fulfill({ json: detail() });
      return;
    }
    if (request.method() === 'POST' && url.pathname.endsWith('/application-review/approve')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({ expectedRevision: 4, confirmed: true });
      status = 'Approved';
      revision = 5;
      await route.fulfill({ json: detail() });
      return;
    }
    if (
      request.method() === 'POST' &&
      url.pathname.endsWith('/application-review/return-to-draft')
    ) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({ expectedRevision: 5, confirmed: true });
      status = 'Draft';
      revision = 6;
      await route.fulfill({ json: detail() });
      return;
    }
    if (url.pathname.endsWith('/follow-ups/due')) {
      await route.fulfill({
        json: { page: 1, pageSize: 100, asOfUtc: '2026-10-17T09:00:00Z', items: [] },
      });
      return;
    }
    if (url.pathname.endsWith('/application-review')) {
      await route.fulfill({ json: detail() });
      return;
    }
    await route.fulfill({ json: { page: 1, pageSize: 100, items: [detail()] } });
  });

  await page.goto('/applications');
  await page.getByRole('button', { name: /Développeuse \.NET/ }).click();
  await page.getByRole('button', { name: 'Prévisualiser le CV' }).click();
  await expect(page.getByTitle('Prévisualisation du document cv-actuel.pdf')).toBeVisible();
  await page.getByRole('button', { name: 'Fermer' }).click();
  await expect(page.getByRole('option', { name: /autre-offre\.pdf/ })).toHaveCount(0);
  await page.getByLabel('Export approuvé').selectOption('resume-new');
  await page.getByRole('button', { name: 'Associer l’export' }).click();
  await expect(page.getByText('Associé', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Demander la revue' }).click();
  await expect(page.getByRole('heading', { name: 'Revue du dossier' })).toBeVisible();
  await expect(page.getByText('Après cet événement : À relire · Révision 4')).toBeVisible();
  const approveButton = page.getByRole('button', { name: 'Approuver le dossier' });
  await expect(approveButton).toBeDisabled();
  await page.getByLabel('Je confirme avoir relu et approuver les deux pièces.').check();
  await approveButton.click();
  await expect(page.locator('.detail-heading .status-pill')).toHaveText('Approuvée');
  await expect(page.getByText('Après cet événement : Approuvée · Révision 5')).toBeVisible();
  await page.getByLabel('Je confirme le retour au brouillon.').check();
  await page.getByRole('button', { name: 'Renvoyer au brouillon' }).click();
  await expect(page.locator('.detail-heading .status-pill')).toHaveText('Brouillon');
  await expect(page.getByRole('button', { name: 'Demander la revue' })).toBeVisible();
});

test('candidate withdraws an active application with a reason and confirmation', async ({
  page,
}) => {
  let withdrawn = false;
  const detail = () => ({
    id: 'application-withdraw',
    profileId: 'profile-1',
    jobId: 'job-5',
    jobTitle: 'Ingénieure plateforme',
    companyName: 'Atelier Numérique',
    status: withdrawn ? 'Withdrawn' : 'InReview',
    revision: withdrawn ? 5 : 4,
    createdAtUtc: '2026-10-17T08:00:00Z',
    updatedAtUtc: '2026-10-17T09:00:00Z',
    resume: null,
    coverLetter: null,
    submission: {
      occurredAtUtc: '2026-10-17T08:30:00Z',
      channel: 'CompanyWebsite',
      reference: null,
    },
    followUps: [],
    pendingFollowUp: null,
    events: [
      {
        sequence: withdrawn ? 5 : 4,
        kind: withdrawn ? 'Withdrawn' : 'ManualSubmissionDeclared',
        status: withdrawn ? 'Withdrawn' : 'InReview',
        revision: withdrawn ? 5 : 4,
        atUtc: '2026-10-17T09:00:00Z',
        note: withdrawn ? 'J’ai accepté une autre offre.' : null,
        document: null,
        submission: null,
        recruiterUpdate: null,
        followUp: null,
      },
    ],
  });

  await page.route('**/auth/session', (route) =>
    route.fulfill({
      json: { isAuthenticated: true, email: 'candidate@example.test', registrationEnabled: false },
    }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/applications**', async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    if (request.method() === 'POST' && url.pathname.endsWith('/application-withdraw/withdraw')) {
      expect(request.headers()['x-csrf-token']).toBe('playwright-csrf-token');
      expect(request.postDataJSON()).toEqual({
        expectedRevision: 4,
        confirmed: true,
        reason: 'J’ai accepté une autre offre.',
      });
      withdrawn = true;
      await route.fulfill({ json: detail() });
      return;
    }
    if (url.pathname.endsWith('/follow-ups/due')) {
      await route.fulfill({
        json: { page: 1, pageSize: 100, asOfUtc: '2026-10-17T09:00:00Z', items: [] },
      });
      return;
    }
    if (url.pathname.endsWith('/application-withdraw')) {
      await route.fulfill({ json: detail() });
      return;
    }
    await route.fulfill({ json: { page: 1, pageSize: 100, items: [detail()] } });
  });

  await page.goto('/applications');
  await page.getByRole('button', { name: /Ingénieure plateforme/ }).click();
  const withdrawButton = page.getByRole('button', { name: 'Retirer la candidature' });
  await expect(withdrawButton).toBeDisabled();
  await page.getByLabel('Motif du retrait').fill('J’ai accepté une autre offre.');
  await expect(withdrawButton).toBeDisabled();
  await page.getByLabel('Je confirme le retrait de cette candidature.').check();
  await withdrawButton.click();
  await expect(page.locator('.detail-heading .status-pill')).toHaveText('Retirée');
  await expect(page.getByText('J’ai accepté une autre offre.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Retirer la candidature' })).toHaveCount(0);
});
