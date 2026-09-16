# Süreç

**Süreç haritası**, resmi süreç gerçeğinin bir draw.io / görsel belge olduğunun kaydıdır. Bu sekmede editör, BPMN motoru ve otomatik senkron yoktur.

> Bu sekmenin ekran turu (seçici, modal, tablo) Yükümlülük / Denetim / Paydaş ile aynı yoldan **gitmeyecek**. Kavram aynı kalır; UX sonraki oturumda ayrı tasarlanır.

## Ekranda ne vardır?

Ad, tür (prosedür, iş akışı, organizasyon, diğer), DI belge (draw.io), durum (taslak / resmi / yürürlükten kalktı), not.

**Resmi yap** bir haritayı yürürlükteki süreç yapar. Yürürlükten kaldırmak not ister. Taslak ve belgesiz kayıtlar Durum’a yansır.

Kayıt silmek draw.io dosyasını silmez.

## Örnek

Kütüphane’ye `Saha kabul akışı.drawio` yükleyin. Süreç sekmesinde kayıt açın, belgeyi bağlayın, **Resmi yap**.

Akış değişince yeni sürüm yükleyin (DI dosya sürümü) veya yeni kayıt açıp eskisini yürürlükten kaldırın. Bu sekme BPMN çalıştırmaz; OC geçişleri hâlâ OC kurallarıdır.

## Ne değildir?

- Draw.io editörü değildir (dosya Kütüphane / DI’dadır)
- Gantt değildir
- Kapı listesi değildir; resmi süreci kapı kriterine referans verebilirsiniz

## Bu eğitim setini bitirmek

Plan (Genel, Gantt, WBS, FS, Durum) → Teslimat (Karar, Kapı, Paket, Kütüphane) → Kontrol ve Taraflar yardımcı kayıtlardır.

Boş bir projede pratik sıra: Paketler veya WBS → Kütüphane → bağla → Kararlar/Kapılar → Durum.
