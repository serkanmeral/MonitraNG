export type AlarmNotificationEventType = 'AlarmRaised' | 'AlarmUpdated' | 'AlarmResolved';

export type AlarmNotificationChannel = 'inApp' | 'email';

export type AcToastSeverity = 'info' | 'success' | 'warning' | 'error';

export interface AlarmNotificationPolicySettings {
  pushToast?: boolean;
  toastSeverity?: AcToastSeverity | string | null;
}

export interface AlarmNotificationPolicy {
  id: string;
  domainId: string;
  domainName: string;
  name: string;
  description?: string | null;
  eventType: AlarmNotificationEventType | string;
  ruleId?: string | null;
  minSeverity?: number | null;
  maxSeverity?: number | null;
  channels: AlarmNotificationChannel[] | string[];
  recipientPersonIds: string[];
  emailTemplateKey?: string | null;
  emailSubject?: string | null;
  settings?: AlarmNotificationPolicySettings | null;
  cooldownMinutes?: number | null;
  excludeAcknowledgedBy?: boolean;
  priority?: number | null;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreateAlarmNotificationPolicyRequest {
  name: string;
  description?: string | null;
  eventType: string;
  ruleId?: string | null;
  minSeverity?: number | null;
  maxSeverity?: number | null;
  channels: string[];
  recipientPersonIds: string[];
  emailTemplateKey?: string | null;
  emailSubject?: string | null;
  settings?: AlarmNotificationPolicySettings | null;
  cooldownMinutes?: number | null;
  excludeAcknowledgedBy?: boolean;
  priority?: number | null;
  isActive?: boolean;
}

export interface UpdateAlarmNotificationPolicyRequest {
  name?: string;
  description?: string | null;
  eventType?: string;
  ruleId?: string | null;
  minSeverity?: number | null;
  maxSeverity?: number | null;
  channels?: string[];
  recipientPersonIds?: string[];
  emailTemplateKey?: string | null;
  emailSubject?: string | null;
  settings?: AlarmNotificationPolicySettings | null;
  cooldownMinutes?: number | null;
  excludeAcknowledgedBy?: boolean;
  priority?: number | null;
  isActive?: boolean;
}
