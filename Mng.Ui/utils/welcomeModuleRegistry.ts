export type WelcomeModuleGroupId = 'operation' | 'monitoring' | 'platform' | 'domainApps';

export interface WelcomeModuleLink {
  labelKey: string;
  to: string;
  /** Link yalnızca manager veya admin için gösterilir */
  requireManager?: boolean;
}

export interface WelcomeModuleDefinition {
  id: string;
  /** Menü erişimi bu prefix ile eşleştirilir: /apps/operation-core */
  routePrefix: string;
  group: WelcomeModuleGroupId;
  groupOrder: number;
  order: number;
  titleKey: string;
  descriptionKey: string;
  icon: string;
  color: string;
  links: WelcomeModuleLink[];
}

export const WELCOME_MODULE_GROUP_KEYS: Record<WelcomeModuleGroupId, string> = {
  operation: 'welcome.groups.operation',
  monitoring: 'welcome.groups.monitoring',
  platform: 'welcome.groups.platform',
  domainApps: 'welcome.groups.domainApps',
};

export const WELCOME_MODULE_GROUP_ORDER: Record<WelcomeModuleGroupId, number> = {
  operation: 1,
  monitoring: 2,
  platform: 3,
  domainApps: 4,
};

/** Statik modül metadata — görünürlük side menu yetkisinden türetilir. */
export const welcomeModuleRegistry: WelcomeModuleDefinition[] = [
  {
    id: 'operation-core',
    routePrefix: '/apps/operation-core',
    group: 'operation',
    groupOrder: 1,
    order: 1,
    titleKey: 'welcome.modules.operationCore.title',
    descriptionKey: 'welcome.modules.operationCore.description',
    icon: 'mdi-cog-play-outline',
    color: 'deep-purple',
    links: [
      { labelKey: 'welcome.modules.operationCore.linkWorkspace', to: '/apps/operation-core/workspace' },
      {
        labelKey: 'welcome.modules.operationCore.linkApprovals',
        to: '/apps/operation-core/approvals',
        requireManager: true,
      },
    ],
  },
  {
    id: 'task-manager',
    routePrefix: '/apps/task-manager',
    group: 'operation',
    groupOrder: 1,
    order: 2,
    titleKey: 'welcome.modules.taskManager.title',
    descriptionKey: 'welcome.modules.taskManager.description',
    icon: 'mdi-clipboard-list-outline',
    color: 'primary',
    links: [
      { labelKey: 'welcome.modules.taskManager.linkWorkspace', to: '/apps/task-manager/workspace' },
      { labelKey: 'welcome.modules.taskManager.linkAssigned', to: '/apps/task-manager/assigned' },
      { labelKey: 'welcome.modules.taskManager.linkHub', to: '/apps/task-manager' },
    ],
  },
  {
    id: 'automation-center',
    routePrefix: '/apps/automation-center',
    group: 'operation',
    groupOrder: 1,
    order: 3,
    titleKey: 'welcome.modules.automationCenter.title',
    descriptionKey: 'welcome.modules.automationCenter.description',
    icon: 'mdi-robot-outline',
    color: 'cyan',
    links: [
      { labelKey: 'welcome.modules.automationCenter.linkWorkflows', to: '/apps/automation-center/workflows' },
      { labelKey: 'welcome.modules.automationCenter.linkHub', to: '/apps/automation-center' },
    ],
  },
  {
    id: 'monitoring',
    routePrefix: '/apps/monitoring',
    group: 'monitoring',
    groupOrder: 2,
    order: 1,
    titleKey: 'welcome.modules.monitoring.title',
    descriptionKey: 'welcome.modules.monitoring.description',
    icon: 'mdi-monitor-dashboard',
    color: 'teal',
    links: [
      { labelKey: 'welcome.modules.monitoring.linkControl', to: '/apps/monitoring/control' },
      { labelKey: 'welcome.modules.monitoring.linkMap', to: '/apps/monitoring/map' },
    ],
  },
  {
    id: 'alarm-center',
    routePrefix: '/apps/alarm-center',
    group: 'monitoring',
    groupOrder: 2,
    order: 2,
    titleKey: 'welcome.modules.alarmCenter.title',
    descriptionKey: 'welcome.modules.alarmCenter.description',
    icon: 'mdi-bell-alert-outline',
    color: 'orange',
    links: [
      { labelKey: 'welcome.modules.alarmCenter.linkAlarms', to: '/apps/alarm-center/alarms' },
      { labelKey: 'welcome.modules.alarmCenter.linkRules', to: '/apps/alarm-center/rules' },
    ],
  },
  {
    id: 'siem-center',
    routePrefix: '/apps/siem-center',
    group: 'monitoring',
    groupOrder: 2,
    order: 3,
    titleKey: 'welcome.modules.siemCenter.title',
    descriptionKey: 'welcome.modules.siemCenter.description',
    icon: 'mdi-shield-search',
    color: 'red-darken-1',
    links: [
      { labelKey: 'welcome.modules.siemCenter.linkDashboard', to: '/apps/siem-center' },
      { labelKey: 'welcome.modules.siemCenter.linkReference', to: '/apps/siem-center/reference' },
    ],
  },
  {
    id: 'datasets',
    routePrefix: '/apps/datasets',
    group: 'platform',
    groupOrder: 3,
    order: 1,
    titleKey: 'welcome.modules.datasets.title',
    descriptionKey: 'welcome.modules.datasets.description',
    icon: 'mdi-database-outline',
    color: 'brown',
    links: [{ labelKey: 'welcome.modules.datasets.linkList', to: '/apps/datasets' }],
  },
  {
    id: 'document-intelligence',
    routePrefix: '/apps/document-intelligence',
    group: 'platform',
    groupOrder: 3,
    order: 2,
    titleKey: 'welcome.modules.documentIntelligence.title',
    descriptionKey: 'welcome.modules.documentIntelligence.description',
    icon: 'mdi-file-document-outline',
    color: 'indigo',
    links: [
      { labelKey: 'welcome.modules.documentIntelligence.linkDesigner', to: '/apps/document-intelligence/designer' },
    ],
  },
  {
    id: 'odak-egitim',
    routePrefix: '/apps/odak-egitim',
    group: 'domainApps',
    groupOrder: 4,
    order: 1,
    titleKey: 'welcome.modules.odakEgitim.title',
    descriptionKey: 'welcome.modules.odakEgitim.description',
    icon: 'mdi-school-outline',
    color: 'green',
    links: [
      { labelKey: 'welcome.modules.odakEgitim.linkTrainings', to: '/apps/odak-egitim/trainings' },
      { labelKey: 'welcome.modules.odakEgitim.linkStats', to: '/apps/odak-egitim/stats' },
    ],
  },
  {
    id: 'odak-siparis',
    routePrefix: '/apps/odak-siparis',
    group: 'domainApps',
    groupOrder: 4,
    order: 2,
    titleKey: 'welcome.modules.odakSiparis.title',
    descriptionKey: 'welcome.modules.odakSiparis.description',
    icon: 'mdi-package-variant-closed',
    color: 'blue-grey',
    links: [
      { labelKey: 'welcome.modules.odakSiparis.linkPackages', to: '/apps/odak-siparis/packages' },
      { labelKey: 'welcome.modules.odakSiparis.linkShipments', to: '/apps/odak-siparis/shipments' },
    ],
  },
];
