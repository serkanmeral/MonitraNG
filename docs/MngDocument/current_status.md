# MngDocument — Oturum durumu

## Son Çalışılan Konu

Proje kütüphanesindeki belgeler için düz metin indeks, özet ve paragraf farkı. İndeks OpenSearch’e yazar; model indeks adımında yoktur. Özet yalnızca indeksi tamam belgelerde, `qwen2.5:3b` ile üretilir. Fark model kullanmaz; açık belge tabandır, en fazla dört indeksli belge onunla kıyaslanır.

## Tamamlanan İşler

- Belge alanları: indeks ve özet durumu (`none`, `running`, `done`, `failed`; `stale` sürüm uyuşmazlığında hesaplanır).
- `POST /api/v1/resources/{id}/index`, özet ve `POST /api/v1/resources/{id}/diff`.
- İndeks ve özet aynı anda tek iş (`DocumentJobGate`). Fark bu kapıyı almaz.
- docx (tablolar dahil) ve markdown düz metin çıkarımı. Metin çıkmazsa durum `failed`.
- İçerik değişince indeks ve özet alanları temizlenir, OpenSearch kaydı silinir.
- Uzun özet parçalara bölünür. Son çağrı modeli bellekten indirir.
- Fark: paragraf eklenen / çıkan / değişen. İçindekiler ve yalnız numara satırları ayrıntıdadır. Sonuç markdown belge olarak kaydedilir.
- Proje kütüphanesi listesi: oluşturulma, ekleyen, sağa sabit işlem menüsü. Yapay zeka maddeleri menüde kendi başlığı altındadır. Silme mevcut silme iznine bağlıdır.

## Devam Eden İşler

- Özet metninin kalitesi (olumsuzlama, kesilme, yönerge tekrarı) ertelendi. Prompt ve sıcaklık değiştirilmedi.
- Şartnameden iş kırılımı, ilgili belgeler, durum paketi ve tutanaktan toplantı bu hatta yok.

## Sonraki Adımlar

- Özet kalitesi ayrıca konuşulacak.
- Birden fazla belge farkı: taban Analiz V1.0, karşı belgeler V2.0 ve PYP. Karşılaştır yeniden çalıştırılmadan eski sonuç yeni süzgeci göstermez.

## Önemli Notlar

- Yerel yığın: Docker Desktop, ağ geçidi `https://localhost:5040`. OpenSearch `mng-{domain}-di-docs`. Özet Ollama `http://ollama:11434`.
- Tam metin Mongo’da durmaz.
- Rar, zip ve xlsx farklanmaz. Fark docx ve markdown içindir.
- `docker-compose.yml` içinde OpenSearch ve özet model adresleri mngdocument servisine eklendi. Ollama `gpus: all`, `OLLAMA_KEEP_ALIVE=30m`.

## Son Güncelleme

**13 Ekim 2026** — İndeks, özet, fark ve kütüphane işlem menüsü yerel ortamda duruyor. Üretime alınmadı.

## Nerede Kalmıştık

Kütüphane listesinde işlem sütunu sağa sabitlendi; menüde aç, indir, bağla, yapay zeka ve sil var. Fark ekranında birden fazla belge sekme sekme açılır.
