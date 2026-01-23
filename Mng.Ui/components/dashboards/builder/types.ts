import type { DashboardPermissions } from '@/stores/apps/dashboard';

/** Dashboard Builder form alanları (sol panel) */
export interface DashboardFormData {
  name: string;
  title: string;
  description?: string;
  slug?: string;
  isDefault: boolean;
  isActive: boolean;
  permissions?: DashboardPermissions;
}
