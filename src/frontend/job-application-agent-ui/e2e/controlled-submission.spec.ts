import { expect, test } from '@playwright/test';

for (const outcome of ['Submitted', 'Unknown'] as const) {
  test(`candidate records the controlled attempt outcome ${outcome} without external submission`, async ({
    page,
  }) => {
  const applicationId = 'application-controlled-1';
  const reviewSha256 = 'a'.repeat(64);
  const attemptId = 'attempt-controlled-1';
  const resume = { exportId: 'resume-export-1', kind: 'Resume', sha256: 'b'.repeat(64) };
  const coverLetter = {
    exportId: 'letter-export-1',
    kind: 'CoverLetter',
    sha256: 'c'.repeat(64),
  };
  const externallyContactedHosts = new Set<string>();
  let reservationCount = 0;
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (!['127.0.0.1', 'localhost'].includes(url.hostname))
      externallyContactedHosts.add(url.hostname);
  });

  let application = {
    id: applicationId,
    profileId: 'profile-1',
    jobId: 'job-1',
    jobTitle: 'Ingénieure logiciel',
    companyName: 'Entreprise Synthétique',
    status: 'Approved',
    revision: 5,
    createdAtUtc: '2026-10-18T08:00:00.000Z',
    updatedAtUtc: '2026-10-18T08:00:00.000Z',
    resume,
    coverLetter,
    submission: null,
    followUps: [],
    pendingFollowUp: null,
    events: [],
    submissionApproval: null,
    submissionAttempt: null,
    submissionAttemptResult: null,
  };
  const detail = () => structuredClone(application);
  const summary = () => ({
    id: application.id,
    profileId: application.profileId,
    jobId: application.jobId,
    jobTitle: application.jobTitle,
    companyName: application.companyName,
    status: application.status,
    revision: application.revision,
    createdAtUtc: application.createdAtUtc,
    updatedAtUtc: application.updatedAtUtc,
  });

  await page.route('**/auth/session', (route) =>
    route.fulfill({ json: { isAuthenticated: true, email: 'candidate@example.test' } }),
  );
  await page.route('**/auth/antiforgery', (route) =>
    route.fulfill({ json: { requestToken: 'playwright-csrf-token' } }),
  );
  await page.route('**/api/v1/applications?*', (route) => {
    if (new URL(route.request().url()).pathname !== '/api/v1/applications') return route.continue();
    return route.fulfill({ json: { page: 1, pageSize: 100, items: [summary()] } });
  });
  await page.route('**/api/v1/applications/follow-ups/due?*', (route) =>
    route.fulfill({
      json: { page: 1, pageSize: 100, asOfUtc: new Date().toISOString(), items: [] },
    }),
  );
  await page.route('**/api/v1/applications/submission-platforms', (route) =>
    route.fulfill({ json: { externalSubmissionEnabled: true, platformIds: ['synthetic-official-api'] } }),
  );
  await page.route('**/api/v1/applications/*/submission-preview', (route) => {
    if (
      application.submissionAttempt &&
      application.submissionAttemptResult?.outcome !== 'DefinitelyNotSubmitted'
    )
      return route.fulfill({ status: 409 });
    return route.fulfill({
      json: {
        id: application.id,
        revision: application.revision,
        jobTitle: application.jobTitle,
        companyName: application.companyName,
        resume: { exportId: resume.exportId, sha256: resume.sha256 },
        coverLetter: { exportId: coverLetter.exportId, sha256: coverLetter.sha256 },
        reviewSha256,
        externalSubmissionEnabled: false,
      },
    });
  });
  await page.route('**/api/v1/applications/*/submission-approval', async (route) => {
    expect(route.request().postDataJSON()).toEqual({
      expectedRevision: 5,
      confirmed: true,
      reviewSha256,
    });
    application = {
      ...application,
      revision: 6,
      submissionApproval: {
        candidacyId: applicationId,
        revision: 6,
        reviewSha256,
        confirmed: true,
      },
    };
    await route.fulfill({ json: detail() });
  });
  await page.route('**/api/v1/applications/*/submission-attempts', async (route) => {
    reservationCount += 1;
    const body = route.request().postDataJSON();
    expect(body).toMatchObject({
      expectedRevision: 6,
      confirmed: true,
      platformId: 'synthetic-official-api',
      reviewSha256,
    });
    expect(typeof body.attemptId).toBe('string');
    const preparedAtUtc = new Date(Date.now() - 60_000).toISOString();
    application = {
      ...application,
      revision: 7,
      submissionAttempt: {
        attemptId: body.attemptId,
        approvedRevision: 6,
        reviewSha256,
        platform: {
          platformId: 'synthetic-official-api',
          hasOfficialApi: true,
          termsAllowAutomation: true,
          requiresCaptcha: false,
          supportsIdempotencyKey: true,
        },
        preparedAtUtc,
      },
    };
    await route.fulfill({ json: detail() });
  });
  await page.route('**/api/v1/applications/*/submission-attempt-result', async (route) => {
    const body = route.request().postDataJSON();
    expect(body).toMatchObject({
      expectedRevision: 7,
      attemptId: application.submissionAttempt?.attemptId,
      outcome,
      confirmed: true,
      reference: outcome === 'Submitted' ? 'SYNTH-42' : null,
    });
    application = {
      ...application,
      revision: 8,
      status: outcome === 'Submitted' ? 'Submitted' : 'Approved',
      submissionAttemptResult: {
        attemptId: body.attemptId,
        outcome,
        occurredAtUtc: body.occurredAtUtc,
        reference: body.reference,
      },
    };
    await route.fulfill({ json: detail() });
  });
  await page.route('**/api/v1/applications/*', (route) => {
    const path = new URL(route.request().url()).pathname;
    if (route.request().method() === 'GET' && path === `/api/v1/applications/${applicationId}`)
      return route.fulfill({ json: detail() });
    return route.fallback();
  });
  await page.route('**/api/v1/documents/drafts?*', (route) => route.fulfill({ json: [] }));
  await page.route('**/api/v1/documents/exports?*', (route) => route.fulfill({ json: [] }));

  await page.goto('/applications');
  await page.getByRole('button', { name: /Ingénieure logiciel Entreprise Synthétique/ }).click();
  await expect(page.getByRole('heading', { name: 'Relire l’aperçu immuable' })).toBeVisible();
  await page.getByLabel(/Je confirme avoir relu le poste/).check();
  await page.getByRole('button', { name: 'Enregistrer mon accord' }).click();

  await expect(page.getByText('Accord enregistré pour la révision 6.')).toBeVisible();
  await page.getByLabel(/Je confirme réserver une seule tentative/).check();
  await page.getByRole('button', { name: 'Réserver la tentative' }).click();

  await expect(page.getByText(/Plateforme autorisée : synthetic-official-api/)).toBeVisible();
  await page.locator('.controlled-result-form select').selectOption(outcome);
  const occurredAt = new Date(Date.now() - 10_000);
  occurredAt.setMinutes(occurredAt.getMinutes() - occurredAt.getTimezoneOffset());
  await page.locator('input[type="datetime-local"]').fill(occurredAt.toISOString().slice(0, 16));
  if (outcome === 'Submitted')
    await page.getByPlaceholder('Référence fournie par la plateforme').fill('SYNTH-42');
  await page.getByLabel(/Je confirme que ce résultat correspond/).check();
  await page.getByRole('button', { name: 'Enregistrer le résultat' }).click();

  const resultStatus = page.locator('.controlled-status');
  await expect(resultStatus).toBeVisible();

  if (outcome === 'Submitted') {
    await expect(resultStatus).toContainText('Envoi confirmé');
    await expect(page.locator('.detail-heading .status-pill')).toHaveText('Envoyée manuellement');
  } else {
    await expect(resultStatus).toContainText('Résultat incertain');
    await expect(resultStatus).toContainText(
      'Aucune nouvelle tentative contrôlée ne sera réservée',
    );
    await expect(page.getByRole('button', { name: 'Réserver la tentative' })).toHaveCount(0);
    await expect(page.getByRole('heading', { name: 'Enregistrer un envoi manuel' })).toHaveCount(0);
  }
  expect(reservationCount).toBe(1);
  expect([...externallyContactedHosts]).toEqual([]);
  });
}
