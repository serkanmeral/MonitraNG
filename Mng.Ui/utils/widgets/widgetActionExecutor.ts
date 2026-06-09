import type { WidgetActionConfig } from '@/utils/widgets/surfaceInteractions';
import { resolveActionParams, resolveParamMap } from '@/utils/widgets/surfaceInteractions';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import type { DrillDownConfig } from '@/utils/widgets/surfaceInteractions';
import { alarmAcknowledge, alarmResolve, alarmSuppress } from '@/services/alarmService';

type ApiActionHandler = (params: Record<string, unknown>) => Promise<void>;

const API_ACTION_HANDLERS: Record<string, ApiActionHandler> = {
  'alarm.acknowledge': async (p) => {
    const id = String(p.alarmId ?? p.id ?? '');
    if (!id) throw new Error('alarmId gerekli');
    await alarmAcknowledge(id);
  },
  'alarm.resolve': async (p) => {
    const id = String(p.alarmId ?? p.id ?? '');
    if (!id) throw new Error('alarmId gerekli');
    await alarmResolve(id);
  },
  'alarm.suppress': async (p) => {
    const id = String(p.alarmId ?? p.id ?? '');
    if (!id) throw new Error('alarmId gerekli');
    await alarmSuppress(id);
  },
};

export async function executeWidgetAction(
  action: WidgetActionConfig,
  row: Record<string, unknown>,
  context: SurfaceContext,
  navigate: (config: DrillDownConfig, row: Record<string, unknown>) => void,
): Promise<void> {
  if (action.type === 'route') {
    if (!action.path) throw new Error('Action path eksik');
    navigate(
      {
        type: 'route',
        path: action.path,
        paramMap: action.parameterMap,
      },
      row,
    );
    return;
  }

  if (action.type === 'api') {
    const handler = API_ACTION_HANDLERS[action.id];
    if (!handler) throw new Error(`Desteklenmeyen API action: ${action.id}`);
    const params = resolveActionParams(action.parameterMap, row, context);
    await handler(params);
    return;
  }

  if (action.type === 'workflow') {
    throw new Error('Workflow action henüz desteklenmiyor');
  }
}

export function canRunWidgetAction(
  action: WidgetActionConfig,
  options: { isAdmin?: boolean; userGroups?: string[]; hasRow?: boolean },
): boolean {
  const groups = action.requiredGroups ?? [];
  if (groups.length) {
    if (options.isAdmin) return true;
    const userGroups = options.userGroups ?? [];
    if (!groups.some((g) => userGroups.includes(g))) return false;
  }
  if (action.type === 'api' && action.parameterMap && options.hasRow === false) {
    return Object.values(action.parameterMap).some((v) => v.startsWith('$row.')) === false;
  }
  return true;
}

export { resolveParamMap };
