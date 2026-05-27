/** Paylaşılan provisioning source UI yardımcıları (grup + kullanıcı). */

export function isDirectoryProvisioningSource(source?: string): boolean {
  return (source || '').toLowerCase() === 'directory';
}

export function provisioningSourceChipColor(source?: string): string {
  return isDirectoryProvisioningSource(source) ? 'info' : 'secondary';
}
