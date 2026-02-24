import { Injectable, signal, Signal } from '@angular/core';

type ToastType = 'success' | 'error';
interface Toast { id: number; message: string; type: ToastType }

@Injectable({ providedIn: 'root' })
export class ToastService {
  private toastsSignal = signal<Toast[]>([]);
  private nextId = 1;

  /** Readonly signal of active toasts */
  readonly toasts: Signal<Toast[]> = this.toastsSignal;

  show(message: string, type: ToastType = 'success', timeout = 4000) {
    const id = this.nextId++;
    const toast: Toast = { id, message, type };
    this.toastsSignal.update(t => [...t, toast]);
    if (timeout > 0) setTimeout(() => this.dismiss(id), timeout);
    return id;
  }

  showSuccess(message: string, timeout = 4000) { this.show(message, 'success', timeout); }
  showError(message: string, timeout = 6000) { this.show(message, 'error', timeout); }

  dismiss(id: number) {
    this.toastsSignal.update(ts => ts.filter(t => t.id !== id));
  }
}