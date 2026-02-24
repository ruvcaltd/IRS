import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { BaseApiService } from '../core/base-api.service';
import { LlmModelResponse } from '../../generated/generated/irs-api.client';

@Injectable({ providedIn: 'root' })
export class LlmService extends BaseApiService {
  getModels(providerId?: number): Observable<LlmModelResponse[]> {
    return this.apiClient.llm_GetModels(providerId).pipe(map(r => r.result ?? []));
  }
}
