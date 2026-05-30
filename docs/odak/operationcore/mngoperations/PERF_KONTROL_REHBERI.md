# PERF Oturumu — Kontrol Rehberi (30 May 2026)

Bu oturumda board liste + profil için **davranış-koruyan, ölçüm-öncelikli** performans optimizasyonu yapıldı.
Aşağıdaki adımlarla *hiçbir şeyin bozulmadığını* ve kazanımın yerinde olduğunu doğrulayabilirsiniz.

- **Branch/commit:** `main` (merge commit `eebdbaa`; perf commit'leri `33cf2ad` backend, `60e1c45` UI).
- **Deploy:** `mngoperations` + `mngui` Odak'ta canlı (healthy). Ölçüm bayrağı **kapalı**.
- **Detay:** `PERF_OPTIMIZATION.md`.

---

## A. Neler değişti (gözle/kodla kontrol)

### Backend (MngOperations)
| Dosya | Değişiklik | Risk notu |
|---|---|---|
| `Core/.../Diagnostics/OcCallStats.cs` (yeni) | İstek başına DG/Keeper çağrı sayacı (scoped) | Sadece sayaç; iş mantığı yok |
| `Core/.../Configuration/MngOperationsSettings.cs` | `PerfDiagnostics` bayrağı (default `false`) | Kapalıyken etkisiz |
| `Core/.../ServiceRegistration.cs` | `PerfDiagnostics`'i IOptions'a kopyala | — |
| `Infrastructure/.../ServiceRegistration.cs` | `OcCallStats` scoped kaydı | — |
| `Infrastructure/.../Clients/MngDataGatewayClient.cs` | `SendWithRetryAsync`'e op etiketi + süre ölçümü | Davranış aynı, sadece sarmalama |
| `Infrastructure/.../Services/PersonDirectoryService.cs` | Keeper N+1 ölçümü | Davranış aynı |
| `Infrastructure/.../Services/FieldBehaviorResolverService.cs` | **İstek başına tek `key→record` map** + kurallar bir kez | **Çözülen alan/kural çıktısı birebir aynı** (ilk-gelen-kazanır korundu) |
| `Infrastructure/.../Services/RuntimeContextService.cs` | Profilde `op_links`/`timeline` **erken paralel**; timeline `limit=200→sort=-enteredAt&limit=5`; OC_PERF log | Sonuç kümesi birebir (DG sort doğrulandı) |

### UI (Mng.Ui)
| Dosya | Değişiklik | Risk notu |
|---|---|---|
| `utils/ocColumnFormat.ts` | `Intl` formatter memoize (locale/currency anahtarlı) | Format çıktısı aynı |
| `composables/useSharedNow.ts` (yeni) | Tek global "now" ticker (refcount'lu) | — |
| `components/.../OcSlaStatusChip.vue` | Satır başına `setInterval` → paylaşılan ticker | Davranış aynı (60 sn güncelleme) |
| `composables/useOcBoardListLookups.ts` | Context map'leri tek kez kur | Çözüm çıktısı aynı |
| `pages/.../boards/[boardId]/index.vue` | `listRows`'ta state/priority/type/assignee önceden çöz; `OcBoardKanban` lazy | Şablon çıktısı birebir (fallback dahil) |
| `services/apiService.ts` | `localStorage.OC_PERF='1'` iken çağrı süresi log'u | Kapalıyken etkisiz |

---

## B. Manuel smoke (tarayıcıda — sizin yapacağınız toplu kontrol)

### Board liste (`/apps/operation-core/boards/{boardId}`)
- [ ] Liste açılıyor, satırlar + toplam sayı doğru.
- [ ] Sıralama (sortable başlık) çalışıyor, yön değişiyor.
- [ ] Hızlı filtre (durum/öncelik/tip) + **gelişmiş arama** (gt/gte/lt/lte, in/nin) doğru sonuç.
- [ ] Serbest metin arama (debounce) çalışıyor.
- [ ] Relation/select sütun ve filtrelerde **etiket** görünüyor (ham id değil).
- [ ] **Computed sütun** değeri doğru.
- [ ] **SLA chip** doğru faz/renk; sayaç ilerliyor (1 dk).
- [ ] Audit sütunları (createdAt/createdBy/age) + sticky actions doğru.
- [ ] Actions: profil/düzenle/sil + yeni iş modalı açılıyor.
- [ ] Kanban moduna geçiş (varsa) çalışıyor → **lazy yüklenir** (ilk geçişte minik gecikme normal).

### Work item profil (`/apps/operation-core/work-items/{id}/profile`)
- [ ] Profil açılıyor; başlık/key/durum doğru (**hız: ~1.2 sn warm**).
- [ ] Detay sekmesi: form salt-okunur, alan + label'lar doğru.
- [ ] Aktivite: timeline + yorum gönder (+ mention) çalışıyor.
- [ ] Ekler: yükle/indir/kaldır çalışıyor.
- [ ] **Durum geçişi** butonları (akışta tanımlıysa) → uygulanınca durum + timeline yenileniyor.
- [ ] Sidebar: SLA paneli, politikalar, meta, izleyenler, bağlılar doğru.

### Konsol/ağ (opsiyonel)
- [ ] Tarayıcı konsolu: `localStorage.setItem('OC_PERF','1')` → board/profil açınca `[OC_PERF] GET ... XXms` log'u görünür.

---

## C. Otomatik smoke (repo kökünden — hızlı API doğrulaması)

```powershell
# Token + oluştur + profil/SLA + DG snapshot (+ varsa transition)
pwsh -NoProfile -File ".\docs\odak\operationcore\scripts\smoke-sla-faz1.ps1" -WithTransition
```
Beklenen: `SLA-1 smoke tamam - OCD-xxxx`, profil `sla` DTO dolu, DG policy id eşleşir.
> Bu oturumda çalıştırıldı → **yeşil** (transition adımı demo akışta geçiş tanımı olmadığından atlandı = normal).

---

## D. Performans ölçümünü tekrar açmak (gerekirse)

1. `ApplicationResources/mng_apps/docker-compose.odak.yml` → `MngOperationsSettings__PerfDiagnostics=true`.
2. `pwsh scripts/odak/sync-odak-source.ps1 -Paths ApplicationResources/mng_apps` + `deploy-odak-apps.ps1 -Services mngoperations`.
3. Board/profil kullan → MO logunda OC_PERF satırları:
   ```
   docker logs --since 5m mngoperations | grep OC_PERF
   ```
   Satır formatı: `OC_PERF {endpoint} ... totalMs=.. dgCalls=.. dgMs=.. keeperCalls=.. ops=[..]`.
4. **Ölçüm bitince bayrağı tekrar `false` yapıp deploy edin** (üretimde kapalı kalmalı).

### Baseline → sonrası (bu oturum)
| Endpoint | Önce | Sonra |
|---|---|---|
| profile (warm) | ~1575-1822 ms, 4 DG | **~1218 ms (~%30)** |
| board_list (warm) | ~330 ms, 1 DG | değişmedi (zaten optimal) |

---

## E. Geri alma (gerekirse)
- Tüm perf işi `main`'de tek merge commit'i (`eebdbaa`) + iki perf commit'i (`33cf2ad`, `60e1c45`).
- Sorun çıkarsa: `git revert -m 1 eebdbaa` (merge'i geri al) veya tekil commit revert.
- Enstrümantasyon kalıcı ve flag-gated; üretimde kapalı olduğu için geri alma gerektirmez.

---

## Açık kalemler (mola sonrası — isteğe bağlı)
- ⬜ **Manuel toplu kontrol** (B bölümü).
- ⬜ **Faz-2:** CC computed sütunlarda sıralama/filtre; F transition `requiredFields` ön-toplama.
- ⬜ **Faz-4 (kapıda, ayrı onay):** tablo sanallaştırma; büyük dosya bölme refactor — ölçüm gerektirmedi.
