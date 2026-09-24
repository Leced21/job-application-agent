import { Routes } from '@angular/router';
import { AppShell } from './core/layout/app-shell/app-shell';

export const routes: Routes = [
    {
        path: '',
        redirectTo: 'profile',
        pathMatch: 'full'
    },
    {
        path: '',
        component: AppShell,
        children: [
            {
                path: 'dashboard',
                loadComponent: () =>
                    import('./features/dashboard/pages/dashboard/dashboard')
                        .then(m => m.Dashboard)
            },
            {
                path: 'jobs',
                loadComponent: () =>
                    import('./features/jobs/pages/jobs/jobs')
                        .then(m => m.Jobs)
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
        redirectTo: 'profile'
    }
];
