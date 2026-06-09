import type { DrillDownConfig } from '@/utils/widgets/surfaceInteractions';
import { resolveParamMap } from '@/utils/widgets/surfaceInteractions';
import type { SurfaceContext } from '@/types/apps/widgetManifest';

export function useWidgetDrillDown(surfaceContext?: () => SurfaceContext | undefined) {
  const router = useRouter();

  function navigateDrillDown(
    config: DrillDownConfig,
    row: Record<string, unknown> = {},
  ) {
    const context = surfaceContext?.() ?? {};
    const type = config.type ?? 'route';

    if (type === 'external') {
      const url = config.path;
      if (config.openInNewTab) window.open(url, '_blank', 'noopener');
      else window.location.href = url;
      return;
    }

    const query = resolveParamMap(config.paramMap, row, context);
    const target = { path: config.path, query };

    if (config.openInNewTab) {
      window.open(router.resolve(target).href, '_blank', 'noopener');
      return;
    }
    void router.push(target);
  }

  return { navigateDrillDown };
}
