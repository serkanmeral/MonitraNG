# Teslimat Omurgası — Oturum durumu

## Son Çalışılan Konu

Proje kütüphanesinde belge indeks, özet ve fark. Toplantı aksiyonuna kod ve birden fazla hedef (kurum kullanıcısı, kurum dışı kişi, firma).

## Tamamlanan İşler

- Kütüphane listesi: oluşturulma, ekleyen, güncelleme. İşlemler sağa sabit menü. Yapay zeka maddeleri aynı menüde.
- Fark: indeksli belgeler, belge seçici, sekme başına bir karşı belge, sonuç markdown olarak kaydedilir.
- Toplantı aksiyonu `code` ve `targets`. Durum paketinden bağlı olmayan aksiyon kartı çıkarıldı.
- Manifest `pm_meeting_actions` alan listesine `code` ve `targets` eklendi.

## Devam Eden İşler

- Özet metin kalitesi ertelendi.
- Şartnameden proje kuran yapı, hakediş ve tutanaktan otomatik toplantı yok.

## Sonraki Adımlar

- Kütüphane listesini giriş yapılmış oturumda yatay kaydırarak işlem sütununu doğrula.
- Çoklu fark denemesi: taban Analiz V1.0, karşı belgeler V2.0 ve PYP.

## Önemli Notlar

- Belge işinin servis notu: [docs/MngDocument/current_status.md](../../MngDocument/current_status.md).
- Ürün planı: [PLAN.md](./PLAN.md). Bu oturum PLAN bölüm 13’ü (hakediş, toplantı görevlendirmesi tasarımı, şartname yapay zekâsı) değiştirmedi. Toplantı aksiyon hedefleri mevcut toplantı kaydına eklendi.
- Önceki kilit (1 Ekim 2026) duruyor: açılış Gantt, dashboard grafik duvarı, sekmeler solda, pulse belgesiz tarama yapmaz.
- UI yerelde `npm run dev`. Bu oturumda imaj üretilip Odak’a gönderilmedi.

## Son Güncelleme

**13 Ekim 2026** — Kütüphane yapay zeka hattı ve toplantı aksiyon hedefleri commit’e hazır. Üretim yok.

## Nerede Kalmıştık

Kütüphane işlem menüsü ve sağa sabit sütun bitti. Özet kalitesi ve şartname→iş kırılımı sıradaki konuşma değil; kullanıcı isterse açılır.
