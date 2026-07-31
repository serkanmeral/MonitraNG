# SIEM / Siber güvenlik platformu — mevcut durum

**Son güncelleme:** 31 Temmuz 2026 (Windows Host Analytics MVP + sec-events by-id)  
**Ortam notu:** Odak production `odak@192.168.20.8`; `mngreactor` + `mngui` deploy edildi (Host Analytics + by-id).  
**Canlı pilot:** `MngLogs.Agent` **v1.0.4** → collector `http://192.168.20.8:5091`; Local UI `:5092`; hostId=`TERMINAL-pilot`.

## Çalışma kuralı

Kapsam → kazanım → onay → kod.  
**Park:** P5 parser · Alarm/Notifier · Hard publish · Settings iskelet redesign · Host paket ataması (E3).  
**Freeze:** Eski SIEM security paneli.

---

## Son çalışılan konu

**Windows Host Analytics** — `/apps/siem-center/hosts/[hostname]`: zaman aralığı, KPI, chart, oturum (Security+RDP), watch hedefler+aktivite, Event Log özeti (sayfalı tablo + pasta kanal filtresi + detay).

---

## Tamamlananlar

### Host Analytics ✓ (31 Tem — UI + Reactor prod)

- Tek sayfa host paneli; Discovery modal CTA
- Oturum geçmişi: 4624/4634/4625/4647 + RDP 21/23/24/25; kullanıcı filtresi; sayfalama/detay
- Watch: tanımlı hedeflerin son inventory durumu + aralık aktivitesi
- Event Log: sayfalı/sıralanabilir tablo, pasta→kanal filtresi, detay
- `sec-events/by-id` + `{**id}` (slash’li Windows id 404 düzeltmesi)
- Doküman: [HOST_ANALYTICS.md](./HOST_ANALYTICS.md)

### E1 Event Log paket ayarları ✓ (kod; Collector prod deploy ayrı)

- Mongo katalog + Settings Catalog + soft Yayınla

### Önceki ✓

- Discovery A1 prod · host modal · ajan 1.0.4 watch prune · Reactor Fields

---

## Sıradaki adım

1. Host Analytics kullanıcı doğrulama / ince ayar (isteğe bağlı)  
2. **Collector Odak prod deploy** + Settings Catalog E1 doğrula  
3. E3 host paket ataması (ayrı onay)  
4. P5 parser (ayrı onay)

---

## Nerede kalmıştık

Host Analytics MVP prod’da. Bir sonraki oturum: ince ayarlar veya E1 Collector deploy / E3 / P5 (onaylı kapsam).
