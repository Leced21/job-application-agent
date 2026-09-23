import { Routes } from '@angular/router';

export const routes: Routes = [
    {
        path: '',
        redirectTo: 'profile',
        pathMatch: 'full'
    },
    {
        path: '',
        loadComponent: () => import('./layout/shell/shell').then((m) => m.Shell),
        children: [
            {
               path: 'profile',
               loadComponent: () => import('./features/profile/pages/profile/profile').then((m) => m.Profile) 
            }
        ]
    },
    {
        path: '**',
        redirectTo: 'profile'
    }
];
