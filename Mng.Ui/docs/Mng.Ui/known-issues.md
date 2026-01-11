# Bilinen Hatalar ve Sorunlar

## MenuItemToolbar.vue - Duplicate defineEmits() Hatası ✅

**Dosya**: `Mng.Ui/components/apps/side-menu-manager/MenuItemToolbar.vue`  
**Hata**: `[plugin:vite:vue] [@vue/compiler-sfc] duplicate defineEmits() call`  
**Satır**: 5 ve 24  
**Tarih**: 2026-01-09  
**Düzeltme Tarihi**: 2026-01-09  
**Durum**: ✅ Çözüldü

### Çözüm

İlk `defineEmits()` çağrısı (satır 5-10) kaldırıldı. Sadece `const emit = defineEmits<...>()` kullanıldı ve `emit` değişkeni `handleSearch` fonksiyonundan önce tanımlandı.

### Hata Detayı

Component'te `defineEmits()` iki kez çağrılmış:
- İlk çağrı: Satır 5-10
- İkinci çağrı: Satır 24-29

### Çözüm

İkinci `defineEmits()` çağrısını kaldırıp, `emit` değişkenini ilk çağrıdan almak gerekiyor:

```typescript
// ÖNCE (HATALI):
defineEmits<{...}>();  // Satır 5

const emit = defineEmits<{...}>();  // Satır 24 - DUPLICATE!

// SONRA (DOĞRU):
const emit = defineEmits<{
  'new-header': [];
  'new-item': [];
  'search': [query: string];
  'refresh': [];
}>();
```

### Notlar

- Bu hata build'i engellemiyor ama console'da warning veriyor
- Component çalışıyor ama best practice'e uygun değil
- Düzeltme basit: duplicate çağrıyı kaldırmak yeterli
