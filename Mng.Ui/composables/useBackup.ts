import type { BackupResponse, BackupListResponse, BackupRequest } from '@/types/backup'

export const useBackup = () => {
  /**
   * Get domain backup list
   */
  const getDomainBackups = async (domainName: string, databaseName?: string): Promise<BackupListResponse> => {
    const query = databaseName ? `?databaseName=${encodeURIComponent(databaseName)}` : ''
    return $fetch<BackupListResponse>(`/api/admin/backup/domain/${encodeURIComponent(domainName)}${query}`)
  }

  /**
   * Create domain backup
   */
  const createDomainBackup = async (domainName: string, request?: BackupRequest): Promise<BackupResponse> => {
    return $fetch<BackupResponse>(`/api/admin/backup/domain/${encodeURIComponent(domainName)}`, {
      method: 'POST',
      body: request || { databaseType: 'mongodb' },
    })
  }

  /**
   * Get backup status by ID
   */
  const getBackupStatus = async (backupId: string): Promise<BackupResponse> => {
    return $fetch<BackupResponse>(`/api/admin/backup/${encodeURIComponent(backupId)}`)
  }

  /**
   * Get system backup list
   */
  const getSystemBackups = async (databaseName?: string): Promise<BackupListResponse> => {
    const query = databaseName ? `?databaseName=${encodeURIComponent(databaseName)}` : ''
    return $fetch<BackupListResponse>(`/api/admin/backup/system${query}`)
  }

  return {
    getDomainBackups,
    createDomainBackup,
    getBackupStatus,
    getSystemBackups,
  }
}
