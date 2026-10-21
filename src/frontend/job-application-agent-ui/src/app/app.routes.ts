import { Routes } from '@angular/router';
import { AppShell } from './core/layout/app-shell/app-shell';
import { requireSession } from './core/auth/auth.guard';

export const routes: Routes = [
    {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
    },
    {
        path: 'login',
        loadComponent: () => import('./features/auth/pages/login/login').then(m => m.LoginPage)
    },
    {
        path: '',
        component: AppShell,
        canActivate: [requireSession],
        children: [
            {
                path: 'recommendations',
                loadComponent: () => import('./features/matching/recommendations')
                    .then(m => m.Recommendations)
            },
            {
                path: 'dashboard',
                loadComponent: () =>
                    import('./features/dashboard/pages/dashboard/dashboard')
                        .then(m => m.Dashboard)
            },
            {
                path: 'jobs/:id',
                loadComponent: () =>
                    import('./features/jobs/pages/job-detail/job-detail')
                        .then(m => m.JobDetailPage)
            },
            {
                path: 'jobs',
                loadComponent: () =>
                    import('./features/jobs/pages/jobs/jobs')
                        .then(m => m.Jobs)
            },
            {
                path: 'preparations',
                loadComponent: () =>
                    import('./features/documents/pages/preparations/preparations')
                        .then(m => m.Preparations)
            },
            {
                path: 'applications',
                loadComponent: () =>
                    import('./features/applications/pages/applications/applications')
                        .then(m => m.Applications)
            },
            {
                path: 'agents',
                loadComponent: () =>
                    import('./features/agents/pages/agents/agents')
                        .then(m => m.Agents)
            },
            {
                path: 'documents',
                loadComponent: () =>
                    import('./features/documents/pages/documents/documents')
                        .then(m => m.Documents)
            },
            {
                path: 'profile',
                loadComponent: () =>
                    import('./features/profile/pages/profile/profile')
                        .then(m => m.Profile)
            },
            {
                path: '',
                pathMatch: 'full',
                redirectTo: 'jobs'
            }
        ]
    },
    {
        path: '**',
        redirectTo: 'dashboard'
    }
];
