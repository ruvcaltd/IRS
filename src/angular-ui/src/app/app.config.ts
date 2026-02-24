import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection, APP_INITIALIZER } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, HTTP_INTERCEPTORS, withInterceptorsFromDi } from '@angular/common/http';
import { routes } from './app.routes';
import { AuthInterceptor } from './auth/interceptors/auth.interceptor';
import { API_BASE_URL, IrsApiClient } from '../generated/generated/irs-api.client';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi()),
    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true
    },
    {
      provide: API_BASE_URL,
      useValue: environment.apiBaseUrl
    },
    {
      provide: APP_INITIALIZER,
      useFactory: (baseUrl?: string) => () => {
        if (!baseUrl || baseUrl.toString().trim() === '') {
          console.warn('[startup] API_BASE_URL is empty — API client will call relative paths. Add `provide: API_BASE_URL` to `app.config.ts` or use `proxy.conf.json` during development.');
        }
      },
      deps: [API_BASE_URL],
      multi: true
    },
    IrsApiClient
  ]
};
