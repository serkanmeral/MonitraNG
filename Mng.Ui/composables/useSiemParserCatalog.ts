import { SIEM_SCENARIO_CATALOG } from '@/composables/useSiemScenarioCatalog';

/** SIEM parser / kapsam sözlüğü — UI salt okunur referans (SIEM_PARSER_PLAN ile hizalı). */
export const SIEM_REFERENCE_VERSION = '1.0.0';
export const SIEM_ALARM_PACKAGE_ID = 'siem-mvp-v1';
export const SIEM_ALARM_PACKAGE_VERSION = '1.0.0';

export type SiemParserCatalogStatus = 'supported' | 'partial' | 'raw_only';

export interface SiemCollectionMethodDef {
  id: string;
  icon: string;
  titleKey: string;
  descriptionKey: string;
  exampleTargetsKey: string;
}

export interface SiemParserMappingDef {
  /** Ham girdi (Event ID, syslog pattern, vendor hint). */
  input: string;
  eventAction: string;
  scenarioIds: string[];
  /** siem-mvp-v1 operasyonel pakette alarm kuralı var mı. */
  inAlarmPack: boolean;
}

export interface SiemParserDef {
  id: string;
  status: SiemParserCatalogStatus;
  sourceType: string;
  sourceProduct: string;
  collectionMethodId: string;
  titleKey: string;
  descriptionKey: string;
  mappings: SiemParserMappingDef[];
}

export interface SiemScenarioReferenceDef {
  id: string;
  matchKey: string;
  eventAction?: string;
  inAlarmPack: boolean;
  descriptionKey: string;
  defaultRuleKey: string;
}

export interface SiemOutOfScopeDef {
  titleKey: string;
  descriptionKey: string;
}

export const SIEM_COLLECTION_METHODS: SiemCollectionMethodDef[] = [
  {
    id: 'nxlog-windows',
    icon: 'mdi-microsoft-windows',
    titleKey: 'siemCenter.reference.collection.nxlog.title',
    descriptionKey: 'siemCenter.reference.collection.nxlog.description',
    exampleTargetsKey: 'siemCenter.reference.collection.nxlog.targets',
  },
  {
    id: 'rsyslog-linux',
    icon: 'mdi-linux',
    titleKey: 'siemCenter.reference.collection.rsyslog.title',
    descriptionKey: 'siemCenter.reference.collection.rsyslog.description',
    exampleTargetsKey: 'siemCenter.reference.collection.rsyslog.targets',
  },
  {
    id: 'syslog-network',
    icon: 'mdi-lan',
    titleKey: 'siemCenter.reference.collection.syslogNetwork.title',
    descriptionKey: 'siemCenter.reference.collection.syslogNetwork.description',
    exampleTargetsKey: 'siemCenter.reference.collection.syslogNetwork.targets',
  },
];

export const SIEM_PARSERS: SiemParserDef[] = [
  {
    id: 'windows.security.v1',
    status: 'supported',
    sourceType: 'ad',
    sourceProduct: 'windows',
    collectionMethodId: 'nxlog-windows',
    titleKey: 'siemCenter.reference.parsers.windowsSecurity.title',
    descriptionKey: 'siemCenter.reference.parsers.windowsSecurity.description',
    mappings: [
      { input: '4625', eventAction: 'login_failed', scenarioIds: ['U1', 'U2'], inAlarmPack: true },
      { input: '4624', eventAction: 'login_success', scenarioIds: ['U2'], inAlarmPack: true },
      {
        input: '4624 / 4672 (LogonType 2,10 + bakım dışı)',
        eventAction: 'privileged_login_outside_window',
        scenarioIds: ['U3'],
        inAlarmPack: true,
      },
      { input: '4740', eventAction: 'account_locked', scenarioIds: ['U1'], inAlarmPack: false },
      { input: '4771', eventAction: 'kerberos_preauth_failed', scenarioIds: [], inAlarmPack: false },
    ],
  },
  {
    id: 'windows.security.extended.v1',
    status: 'supported',
    sourceType: 'ad',
    sourceProduct: 'windows',
    collectionMethodId: 'nxlog-windows',
    titleKey: 'siemCenter.reference.parsers.windowsExtended.title',
    descriptionKey: 'siemCenter.reference.parsers.windowsExtended.description',
    mappings: [
      { input: '4720', eventAction: 'account_created', scenarioIds: ['U9'], inAlarmPack: false },
      { input: '4722', eventAction: 'account_enabled', scenarioIds: [], inAlarmPack: false },
      { input: '4726', eventAction: 'account_deleted', scenarioIds: [], inAlarmPack: false },
      { input: '4728, 4732, 4738', eventAction: 'group_member_added', scenarioIds: ['U8'], inAlarmPack: false },
      { input: '5136', eventAction: 'directory_object_modified', scenarioIds: ['U10'], inAlarmPack: false },
      { input: '5137', eventAction: 'directory_object_created', scenarioIds: [], inAlarmPack: false },
      { input: '5139', eventAction: 'directory_object_deleted', scenarioIds: [], inAlarmPack: false },
    ],
  },
  {
    id: 'linux.auth.v1',
    status: 'supported',
    sourceType: 'endpoint',
    sourceProduct: 'linux-syslog',
    collectionMethodId: 'rsyslog-linux',
    titleKey: 'siemCenter.reference.parsers.linuxAuth.title',
    descriptionKey: 'siemCenter.reference.parsers.linuxAuth.description',
    mappings: [
      {
        input: 'sshd: Failed password…',
        eventAction: 'login_failed',
        scenarioIds: ['U1', 'U2'],
        inAlarmPack: true,
      },
      {
        input: 'sshd: Accepted password…',
        eventAction: 'login_success',
        scenarioIds: ['U2'],
        inAlarmPack: true,
      },
      { input: 'sudo: … command not allowed', eventAction: 'privilege_denied', scenarioIds: [], inAlarmPack: false },
    ],
  },
  {
    id: 'firewall.vendor.v1',
    status: 'supported',
    sourceType: 'firewall',
    sourceProduct: 'fortigate | pan-os | cisco-asa',
    collectionMethodId: 'syslog-network',
    titleKey: 'siemCenter.reference.parsers.firewallVendor.title',
    descriptionKey: 'siemCenter.reference.parsers.firewallVendor.description',
    mappings: [
      { input: 'traffic deny', eventAction: 'denied_flow', scenarioIds: ['U4'], inAlarmPack: true },
      { input: 'traffic allow (yoğun)', eventAction: 'allowed_flow', scenarioIds: ['U5'], inAlarmPack: true },
      { input: 'policy / config change', eventAction: 'rule_change', scenarioIds: ['U6'], inAlarmPack: true },
      { input: 'baseline sonrası yeni src→dst', eventAction: 'new_flow', scenarioIds: ['U7'], inAlarmPack: true },
    ],
  },
  {
    id: 'firewall.generic_syslog.v1',
    status: 'partial',
    sourceType: 'firewall',
    sourceProduct: '*',
    collectionMethodId: 'syslog-network',
    titleKey: 'siemCenter.reference.parsers.firewallGeneric.title',
    descriptionKey: 'siemCenter.reference.parsers.firewallGeneric.description',
    mappings: [
      { input: 'CEF / key=value deny', eventAction: 'denied_flow', scenarioIds: ['U4'], inAlarmPack: true },
      { input: 'CEF / key=value allow', eventAction: 'allowed_flow', scenarioIds: ['U5'], inAlarmPack: true },
    ],
  },
  {
    id: 'bastion.generic.v1',
    status: 'supported',
    sourceType: 'bastion',
    sourceProduct: 'bastion',
    collectionMethodId: 'syslog-network',
    titleKey: 'siemCenter.reference.parsers.bastion.title',
    descriptionKey: 'siemCenter.reference.parsers.bastion.description',
    mappings: [
      { input: 'sshd auth (syslog)', eventAction: 'login_failed | login_success', scenarioIds: ['U1', 'U2'], inAlarmPack: true },
    ],
  },
  {
    id: 'generic.syslog.v1',
    status: 'raw_only',
    sourceType: '*',
    sourceProduct: '*',
    collectionMethodId: 'syslog-network',
    titleKey: 'siemCenter.reference.parsers.generic.title',
    descriptionKey: 'siemCenter.reference.parsers.generic.description',
    mappings: [
      { input: 'Tanınmayan syslog satırı', eventAction: 'unknown', scenarioIds: [], inAlarmPack: false },
    ],
  },
];

export const SIEM_SCENARIO_REFERENCES: SiemScenarioReferenceDef[] = SIEM_SCENARIO_CATALOG.map((s) => {
  const inAlarmPack = ['U1', 'U2', 'U3', 'U4', 'U5', 'U6', 'U7'].includes(s.id);
  return {
    id: s.id,
    matchKey: s.matchKey,
    eventAction: s.eventAction,
    inAlarmPack,
    descriptionKey: `siemCenter.reference.scenarios.${s.id}.description`,
    defaultRuleKey: `siemCenter.reference.scenarios.${s.id}.defaultRule`,
  };
});

export const SIEM_OUT_OF_SCOPE: SiemOutOfScopeDef[] = [
  {
    titleKey: 'siemCenter.reference.outOfScope.monitoring.title',
    descriptionKey: 'siemCenter.reference.outOfScope.monitoring.description',
  },
  {
    titleKey: 'siemCenter.reference.outOfScope.windowsChannels.title',
    descriptionKey: 'siemCenter.reference.outOfScope.windowsChannels.description',
  },
  {
    titleKey: 'siemCenter.reference.outOfScope.sysmon.title',
    descriptionKey: 'siemCenter.reference.outOfScope.sysmon.description',
  },
  {
    titleKey: 'siemCenter.reference.outOfScope.ot.title',
    descriptionKey: 'siemCenter.reference.outOfScope.ot.description',
  },
  {
    titleKey: 'siemCenter.reference.outOfScope.assetPoll.title',
    descriptionKey: 'siemCenter.reference.outOfScope.assetPoll.description',
  },
];

export function parserStatusColor(status: SiemParserCatalogStatus): string {
  switch (status) {
    case 'supported':
      return 'success';
    case 'partial':
      return 'warning';
    default:
      return 'grey';
  }
}

export function parserStatusLabelKey(status: SiemParserCatalogStatus): string {
  return `siemCenter.reference.parserStatus.${status}`;
}
