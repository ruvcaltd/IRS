import { Routes } from '@angular/router';
import { LoginComponent } from './auth/components/login/login.component';
import { RegisterComponent } from './auth/components/register/register.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { authGuard, publicGuard } from './auth/guards/auth.guard';
import { TeamsHomeComponent } from './teams/teams-home.component';
import { TeamCreateComponent } from './teams/team-create.component';
import { TeamDetailComponent } from './teams/team-detail.component';
import { TeamSearchComponent } from './teams/team-search.component';
import { AgentsLibraryComponent } from './agents/agents-library.component';
import { LayoutComponent } from './layout/layout.component';
import { ResearchWorkspaceComponent } from './research/research-workspace.component';
import { ResearchPageComponent } from './research/research-page.component';
import { MyResearchComponent } from './research/my-research.component';

export const routes: Routes = [
  {
    path: '',
    redirectTo: '/dashboard',
    pathMatch: 'full'
  },
  {
    path: 'login',
    component: LoginComponent,
    canActivate: [publicGuard]
  },
  {
    path: 'register',
    component: RegisterComponent,
    canActivate: [publicGuard]
  },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        component: DashboardComponent
      },
      {
        path: 'my-research',
        component: MyResearchComponent
      },
      {
        path: 'research',
        children: [
          { path: '', component: ResearchWorkspaceComponent },
          { path: ':id', component: ResearchPageComponent }
        ]
      },
      {
        path: 'teams',
        children: [
          { path: '', component: TeamsHomeComponent },
          { path: 'create', component: TeamCreateComponent },
          { path: 'search', component: TeamSearchComponent },
          { path: ':id', component: TeamDetailComponent },
          { path: ':id/agents', component: AgentsLibraryComponent }
        ]
      }
    ]
  },
  {
    path: '**',
    redirectTo: '/dashboard'
  }
];
