import type { InjectionKey, Ref } from 'vue';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import type { WidgetDataResponse } from '@/services/widgetDataService';

export const DASHBOARD_SURFACE_CONTEXT_KEY: InjectionKey<Ref<SurfaceContext>> = Symbol('dashboardSurfaceContext');
export const DASHBOARD_WIDGET_DATA_KEY: InjectionKey<Ref<Map<string, WidgetDataResponse>>> =
  Symbol('dashboardWidgetData');
export const DASHBOARD_WIDGET_BATCH_MODE_KEY: InjectionKey<boolean> = Symbol('dashboardWidgetBatchMode');
