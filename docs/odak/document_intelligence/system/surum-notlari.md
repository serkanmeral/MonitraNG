# MonitraNG — Sürüm Notları

> **Kitle:** `MonitraNG Users` (IT / geliştirici ekibi)  
> **Son güncelleme:** 5 Haziran 2026  
> **Konum:** Dokümanlar → **System** → bu doküman

Bu sayfa platformdaki önemli değişiklikleri tarih sırasıyla takip eder. Alt bölümde **ilk kurulum anlık görüntüsü (baseline)** yer alır; üstteki girdiler zamanla güncellenir.

---

## Son güncellemeler

### 2026-06-05 — Document Intelligence: manager yetkisi ve System klasörü

- **Manager bypass (MngDocument):** Mirası kırık (kısıtlı) klasörlerde `isManager` kullanıcıları, görüntüleme yetkisi varsa tam CRUD ve menü aksiyonları alır — admin bypass ile aynı mantık, kapsam dar.
- **Öğreticiler:** `MonitraNG / Öğreticiler` altında Operasyon Merkezi kullanıcı ve yönetici rehberleri yüklendi; yönetici rehberi `Manager` klasöründe (`MonitraNG Users` görünürlüğü).
- **System:** Kök seviyede `System` klasörü ve bu sürüm notları dokümanı eklendi.

### 2026-06-04 — Operasyon Merkezi düzeltmeleri ve öğreticiler

- **Yorum editörü:** İş profili → Yorumlar sekmesinde zengin metin editörü prod/test ortamında düzeltildi (`vue-i18n` `@` kaçışı, istemci bileşeni).
- **MonitraNG Geri Bildirim:** Kapanış geçişlerinde zorunlu **çözüm özeti** alanı forma, akışa ve doğrulama kurallarına eklendi.
- **Document Intelligence öğreticileri:** OC kullanıcı/yönetici rehberleri repo ve seed script ile hazırlandı.

### 2026-06-03 — Document Intelligence performans

- İlk açılış ve klasör gezinme hızlandırıldı: `GET /resources/bootstrap`, `GET /resources/browse`, istek başına permission snapshot önbelleği.

### 2026-06-01 — Document Intelligence Faz 1 tamamlandı

- Klasör ağacı, markdown editör/önizleme, dosya yükleme/indirme/inline önizleme, arama, sürüm geçmişi, grup bazlı yetkilendirme + miras, taslak/yayınla — prod ve test ortamında canlı.

---

## Platform özeti (baseline — Haziran 2026)

Bu bölüm, MonitraNG platformunun **ilk yayın kapsamını** özetler. Büyük sürüm değişikliklerinde güncellenir.

### Ortamlar

| Ortam | Gateway | Not |
| --- | --- | --- |
| **Üretim (prod)** | `http://192.168.20.8:5040` | Odak üretim VM |
| **Test / geliştirme** | `http://192.168.20.20:5040` | Odak test VM |

UI: `:3000` · Keeper: `:5001` · Data Gateway: `:5010` · Document Intelligence: `/documents/api/v1/...`

### Aktif modüller (özet)

| Modül | Kısa açıklama | Durum |
| --- | --- | --- |
| **Operasyon Merkezi** | Workspace, iş öğeleri, board, profil, yorumlar, onaylar, tanımlamalar | Canlı |
| **Document Intelligence** | Klasör/doküman yönetimi, markdown, dosya, yetkilendirme | Faz 1 canlı |
| **Task Manager** | Proje/board, iş profili, etiketler | Canlı |
| **Keeper** | Kimlik, grup, token (`isAdmin` / `isManager`) | Canlı |
| **Data Gateway** | Dataset CRUD, sorgu, dosya depolama | Canlı |
| **Gateway** | Tek giriş noktası, servis yönlendirme | Canlı |
| **Workflow / Scheduler / Alarm** | Otomasyon ve zamanlama altyapısı | Canlı |
| **Monitoring / Engine / Reactor** | İzleme ve olay işleme | Canlı |

### Önemli çalışma alanları (Operasyon Merkezi)

| Workspace | Amaç | Erişim |
| --- | --- | --- |
| **MonitraNG Geri Bildirim** | Platform hata ve öneri kayıtları | Yalnızca `MonitraNG Users` |
| **IT Destek** | Kurumsal help desk; kullanıcılar talep açar | `users` açar; `MonitraNG Users` triyaj |

### Dokümantasyon (Document Intelligence)

| Konum | İçerik | Görünürlük |
| --- | --- | --- |
| `MonitraNG / Öğreticiler` | Operasyon Merkezi — Kullanıcı Rehberi | Genel |
| `MonitraNG / Öğreticiler / Manager` | Operasyon Merkezi — Yönetici Rehberi | `MonitraNG Users` |
| `System` | Sürüm Notları (bu doküman) | `MonitraNG Users` |
| `System` | Diagnostic Raporu | `MonitraNG Users` |

### Bilinen sınırlamalar

- Document Intelligence **Faz 2** (Operasyon Merkezi iş öğesi ↔ doküman bağlantısı) henüz yapılmadı.
- Kısıtlı klasörlerde **admin olmayan** kullanıcılarla canlı tree/403 doğrulaması tamamlanmadı (admin bypass testleri ağırlıklı).
- Office dosyaları (docx/xlsx) için inline önizleme yok; indirme gerekir.
- `System` altındaki **ikinci manager dokümanı** planlanıyor (bu doküman tamamlandıktan sonra).

### Sıradaki (kısa yol haritası)

1. `System` altında ikinci manager dokümanı (konu birlikte belirlenecek).
2. Document Intelligence Faz 2 — WorkItem ↔ doküman ilişkisi.
3. Öğretici ve sürüm notlarının prod deploy sonrası düzenli güncellenmesi.

---

## Güncelleme rehberi (ekip içi)

Yeni bir prod/test yayınından sonra:

1. **Son güncellemeler** bölümünün en üstüne tarihli bir başlık ekleyin (`### YYYY-AA-GG — Kısa başlık`).
2. 3–7 madde: ne değişti, hangi modül, kullanıcıya etkisi.
3. **Son güncelleme** satırındaki tarihi güncelleyin.
4. Büyük platform değişikliğinde **Platform özeti** tablolarını gözden geçirin.

Teknik detay için repo: `docs/odak/document_intelligence/DEVAM.md`, seed script'leri `docs/odak/document_intelligence/scripts/`.
