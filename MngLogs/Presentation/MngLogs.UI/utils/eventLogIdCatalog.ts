/** Known Windows Event Log IDs collected by MngLogs default/optional packages. */
export type EventIdInfo = {
  id: number
  title: string
  description?: string
}

const CATALOG: Record<number, EventIdInfo> = {
  // System / lifecycle + SCM
  41: {
    id: 41,
    title: 'Beklenmeyen kapanış (Kernel-Power)',
    description: 'Sistem düzgün kapanmadan kapandı veya yeniden başladı.'
  },
  104: {
    id: 104,
    title: 'Olay günlüğü temizlendi',
    description: 'Bir Event Log kanalı temizlendi.'
  },
  6005: {
    id: 6005,
    title: 'Event Log servisi başladı',
    description: 'Windows Event Log servisi başlatıldı (genelde boot).'
  },
  6006: {
    id: 6006,
    title: 'Event Log servisi durdu',
    description: 'Windows Event Log servisi durduruldu (genelde shutdown).'
  },
  7031: {
    id: 7031,
    title: 'Servis beklenmedik şekilde sonlandı (+ recovery)',
    description: 'Service Control Manager: servis çöktü; recovery eylemi bilgisi içerir.'
  },
  7034: {
    id: 7034,
    title: 'Servis beklenmedik şekilde sonlandı',
    description: 'Service Control Manager: servis beklenmeyen şekilde kapandı.'
  },
  7036: {
    id: 7036,
    title: 'Servis durum değişimi',
    description: 'Servis running/stopped gibi bir duruma geçti.'
  },
  7040: {
    id: 7040,
    title: 'Servis başlangıç tipi değişti',
    description: 'Servisin start type değeri değişti (ör. auto → disabled).'
  },
  7045: {
    id: 7045,
    title: 'Yeni servis kuruldu',
    description: 'Sisteme yeni bir Windows servisi yüklendi.'
  },

  // Application
  1000: {
    id: 1000,
    title: 'Uygulama hatası',
    description: 'Application Error — süreç çökmesi / hata raporu.'
  },
  1001: {
    id: 1001,
    title: 'Windows Error Reporting',
    description: 'WER kaydı (hata raporlama).'
  },
  1026: {
    id: 1026,
    title: '.NET Runtime hatası',
    description: '.NET uygulaması çalışma zamanı hatası.'
  },

  // PowerShell
  400: {
    id: 400,
    title: 'PowerShell motoru başladı',
    description: 'Windows PowerShell engine start.'
  },
  403: {
    id: 403,
    title: 'PowerShell motoru durdu',
    description: 'Windows PowerShell engine stop.'
  },
  600: {
    id: 600,
    title: 'PowerShell sağlayıcı / host',
    description: 'PowerShell sağlayıcı veya host yaşam döngüsü sinyali.'
  },

  // RDP / LocalSessionManager
  21: {
    id: 21,
    title: 'Oturum açıldı (logon)',
    description: 'Terminal Services Local Session Manager — oturum logon.'
  },
  23: {
    id: 23,
    title: 'Oturum kapandı (logoff)',
    description: 'Oturum logoff.'
  },
  24: {
    id: 24,
    title: 'Oturum bağlantısı kesildi',
    description: 'RDP/oturum disconnect.'
  },
  25: {
    id: 25,
    title: 'Oturum yeniden bağlandı',
    description: 'RDP/oturum reconnect.'
  },

  // Security (optional)
  4624: {
    id: 4624,
    title: 'Başarılı oturum açma',
    description: 'Security — successful logon (admin gerekir).'
  },
  4625: {
    id: 4625,
    title: 'Başarısız oturum açma',
    description: 'Security — failed logon (admin gerekir).'
  },
  4634: {
    id: 4634,
    title: 'Oturum kapatıldı',
    description: 'Security — logoff.'
  },
  4648: {
    id: 4648,
    title: 'Açık kimlik bilgileriyle oturum',
    description: 'Logon with explicit credentials.'
  },
  4672: {
    id: 4672,
    title: 'Özel ayrıcalıklar atandı',
    description: 'Special privileges assigned to new logon.'
  },
  4720: {
    id: 4720,
    title: 'Kullanıcı hesabı oluşturuldu',
    description: 'A user account was created.'
  },
  4726: {
    id: 4726,
    title: 'Kullanıcı hesabı silindi',
    description: 'A user account was deleted.'
  },
  4740: {
    id: 4740,
    title: 'Kullanıcı hesabı kilitlendi',
    description: 'A user account was locked out.'
  }
}

export function describeEventId(id: number): EventIdInfo {
  return (
    CATALOG[id] || {
      id,
      title: 'Tanım yok',
      description: 'Bu Event ID için henüz yerel katalog açıklaması yok.'
    }
  )
}

export function describeEventIds(ids: number[]): EventIdInfo[] {
  return [...ids].sort((a, b) => a - b).map(describeEventId)
}
