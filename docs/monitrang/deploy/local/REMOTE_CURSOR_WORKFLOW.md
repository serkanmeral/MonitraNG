# Müşteri terminali — uzaktan Cursor iş akışı

## Kısıt

Bu lokal geliştirme PC’sinden müşteri **test** (`192.168.20.20`) veya **prod** (`192.168.20.8`) ortamlarına **doğrudan erişim yok**.

| Yol | Durum |
|-----|--------|
| Lokal → test/prod API / SSH / Mongo | **Yok** |
| Lokal → VPN → RDP → müşteri **terminal PC** | **Var** |
| Terminal PC → test/prod (Cursor, script, browser) | **Var** (terminal’in ağında) |

Dolayısıyla dump, kullanıcı listesi, DI export paketi vb. işlemler **bu makineden çalıştırılmaz**. Terminaldeki Cursor’da yürütülür; üretilen artefaktlar buraya taşınır.

---

## Standart döngü

```text
1. BURADA (lokal Cursor)
   - İhtiyacı netleştir
   - Terminal Cursor için PROMPT + kontrol listesi üret
   - Beklenen çıktı formatını / dosya yapısını tanımla
   - Prompt’u docs/monitrang/deploy/local/remote_prompts/ altına kaydet

2. MÜŞTERİ TERMİNALİ (RDP + Cursor)
   - Prompt’u yapıştır / ilgili .md’yi aç
   - Agent’ın ürettiği script’leri çalıştır (veya agent çalıştırsın)
   - Çıktıyı paketle (zip / klasör) — secret’ları ayır

3. BURAYA GERİ
   - Paketi USB / paylaşım / izinli kanal ile al
   - gitignore path’e koy (dump, DOCX pack, user export)
   - Lokal Cursor ile import / normalize / doğrulama
```

---

## Prompt dosyası kuralları

Konum: `docs/monitrang/deploy/local/remote_prompts/`

| Kural | Açıklama |
|-------|----------|
| Tek iş / tek prompt | Örn. yalnız DI export, yalnız Mongo dump |
| Ortam | Prompt’ta açık yaz: **test 20.20** (veya prod — bilinçli) |
| Çıktı sözleşmesi | Klasör ağacı, dosya adları, başarı kriteri |
| Secret | Parola / token prompt’a **yazılmaz**; terminaldeki mevcut `.env` / token script’lerine işaret |
| Git | Ham dump / DOCX pack **commit edilmez**; prompt metni commit edilebilir |

İsim örneği: `RP01_mongo_dump_odak_test.md`, `RP02_di_templates_export_test.md`.

---

## Bu taşıma ile ilgili uzak işler (plan)

| ID | İş | Kaynak | Lokal’de sonraki |
|----|-----|--------|------------------|
| RP — dump | `mng_odak` mongodump (veya eşdeğeri) | Test | Restore + user/group Local normalize |
| RP — DI pack | Designer + letterhead + cover API export | Test | from-reference / katalog import |
| RP — (opsiyonel) env envanteri | Port, compose override, domain adı doğrulama | Test | INVENTORY doldurma |

Prompt’lar ihtiyaç anında buradan üretilir; terminal Cursor çalıştırır; paket geri gelir.

---

## Güvenlik

- RDP oturumunda üretilen token dosyaları terminale özgü kalabilir; lokal’e taşınırken gerekmeyenleri alma.
- Müşteri verisi içeren paketler yalnızca lokal ignore path’te.
- Prompt’larda production’a yanlışlıkla yazma komutu olmamalı; varsayılan **test**.
