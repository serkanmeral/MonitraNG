# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 3 Ağustos 2026 (P5 Parse Rules + Settings Event Log IA)  
**Ortam notu:** Odak production; Collector `:5091`; UI çoğu zaman local Nuxt → prod API.  
**Reactor:** parse katalog seed + sample API Odak’a deploy edildi (`SeedRevision` 5).  
**Mng.Ui:** local (parse sihirbazları / Settings IA henüz prod `mngui` deploy edilmedi — hard refresh local).

## Çalışma kuralı

Kapsam → kazanım → onay → kod.  
**Park:**  
- Host Analytics L3 / genel Analytics dönüşü  
- Ajansız host aksiyonları (Discovery)  
- Firewall parse kurallarının katalog seed’e taşınması (hâlâ C# parser)  
- Periyodik discovery scan · Hard publish · Host paket ataması (E3)  
- UI’den parametreli agent indir  
**Freeze:** Eski SIEM security paneli.

---

## Son çalışılan konu

**P5 — Parse Rules kataloğu + Windows/Linux sihirbazları + Settings Event Log IA**

Detay: [PARSE_RULES_CATALOG.md](./PARSE_RULES_CATALOG.md)

---

## Tamamlananlar (bu oturum / P5)

### Parse kuralları (Reactor + UI) ✓

- Mongo katalog + manage/publish API + `SecEventCatalogParseEngine` (C# fallback)
- Builtin seed **SeedRevision 5:** Security hizası (4624/4625/4634/4648/4672/4720/4722/4726/4728/4732/4738/4740 + 5136/5137/5139) + RDP 21–25 + Linux sshd/sudo
- Seed sync: revision bump’ta eski builtin silinir (örn. 65002 Application builtin kaldırıldı)
- `custom.*` alanlar + `sec_event_custom_fields` + ExtraFields → `fields`
- Agent `mnglogs-agent` + `linux-journal` → katalog `sourceProduct` eşlemesi
- `when.op = contains`; journal `MESSAGE` → `message`

### Sihirbazlar ✓

- **Windows:** Event ID örnek + Tanımlı Alanlar | Custom Regex sekmeleri  
- **Linux:** paket/query örnek API + Custom Regex odaklı; package `when.eq` + family/contains zorunluluğu  
- «Kural oluştur» kaldırıldı → yalnızca Windows / Linux sihirbazı

### Settings IA ✓

Üst: **Event Log · Keşif · Referans**  
Event Log alt: **Paket kataloğu · Parse kuralları · Alan kataloğu**  
Parse + Alan listeleri: sayfalama / sıralama / filtre

### Toplama seed notu

`security-auth` Event ID listesi seed’de genişletildi; **mevcut Odak katalog otomatik güncellenmez** — Settings → Paket kataloğu’ndan elle ID ekleyip Yayınla gerekir.

---

## Sıradaki adım

1. **Firewall / bastion** parse kurallarını katalog seed + message family’ye taşı (opsiyonel dilim)  
2. **Prod `mngui` deploy** (sihirbaz + Settings IA) — kullanıcı isterse  
3. Odak **security-auth** paket ID’lerini yayınla (yeni Security Event ID’ler için)  
4. Park: Host Analytics L3 · Discovery ajansız host aksiyonları  

---

## Nerede kalmıştık

P5 parse/settings dilimi kod + Reactor deploy + doküman ile kapatıldı.  
**Kaldığımız nokta:** firewall katalog taşıma ve/veya mngui deploy; Analytics L3 / Discovery park duruyor.
