export interface BackupResponse {
  id: string
  type: 'system' | 'domain'
  databaseName: string
  domainName?: string
  status: 'in_progress' | 'completed' | 'failed'
  startedAt: string
  completedAt?: string
  durationMs?: number
  sizeBytes?: number
  errorMessage?: string
  backupPath: string
}

export interface BackupListResponse {
  backups: BackupResponse[]
  totalCount: number
}

export interface BackupRequest {
  databaseType?: 'mongodb' | 'postgresql'
  databaseName?: string
}
