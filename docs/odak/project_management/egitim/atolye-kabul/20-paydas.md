# Paydaş

**Paydaş**, dış tarafın hangi belgelere bakabileceğinin kaydıdır. Ayrı kiracı, müşteri portalı, e-posta gönderimi ve tek oturum açma yoktur. E-posta alanı giriş hesabı değildir.

Soldaki menüde **Taraflar → Paydaş**.

Kişi Monitra’ya girmez. Siz belgelerini kütüphaneden alıp kendi yolunuzla iletirsiniz. Kayıt, “ne paylaştık” gerçeğidir.

Okundu kaydı ayrıdır. Dış kişinin adı oraya da yazılabilir. Okundu “okudu mu” der. Paydaş “hangi belge onun listesinde” der.

Denetim paketi de ayrıdır. O, resmi kanıt setidir. Paydaş, kişi kaydıdır.

## Liste

Liste boşsa “Henüz paydaş yok” yazar. Arama ve **Tümü** süzgeci vardır.

| Kolon | Ne işe yarar |
|---|---|
| Ad | Kişinin veya rolün adı. |
| Kuruluş | Şirket veya birim. |
| Tür | Müşteri, tedarikçi, danışman, denetçi, sponsor veya diğer. |
| E-posta | Bilgi. Giriş açmaz. Posta gitmez. |
| Paylaşım | Seçilen belge sayısı. Yoksa paylaşımsız sayılır. |
| Erişim sonu | Bu günden sonra kayıt süresi bitmiş sayılır. |
| Durum | Davetli, etkin veya geri alındı. |
| WBS | Boşsa proje düzeyidir. |

Paylaşımsız ve süresi bitmiş kayıtlar Durum’a yansır. Kapatma kilidi değildir.

## Pencere

| Alan | Ne işe yarar |
|---|---|
| Ad | Örnek: `Müşteri saha müdürü`. |
| Kuruluş | Örnek: atölyenin sahibi. |
| Tür | **Müşteri** saha tarafı. **Tedarikçi** yüklenici. **Danışman** uzman. **Denetçi** resmi otorite. **Sponsor** yönetim. **Diğer** bunlara sığmayan. |
| E-posta | Bilgi alanı. |
| Paylaşılan belgeler | **Kütüphaneden seç**. Birden fazla belge. Çarpı, belgeyi listeden çıkarır. Belge silinmez. Seçilmezse “Paylaşılan belge seçilmedi” yazar ve Durum’da **Paylaşımsız paydaş** olur. |
| Erişim sonu | Boşsa süre bitmez. Geçmişse “erişim sonu geçmiş” uyarısı ve **Süresi biten paydaş** çıkar. |
| WBS | İsteğe bağlı. |
| Durum | **Davetli**: kayıt açıldı, paylaşım henüz etkin değil. **Etkin**: paylaşım kaydı yürürlükte. **Geri alındı**: paylaşım kapatıldı, not zorunlu. |
| Not | Geri almakta zorunlu. |

**Etkinleştir**, davetliyi etkin yapar. Giriş hesabı açmaz.

Silmek paydaş kaydını siler. Belgeler durur. Silinecek giriş hesabı yoktur. Çünkü hiç açılmamıştır.

## Örnek

| Alan | Değer |
|---|---|
| Ad | Müşteri saha müdürü |
| Tür | Müşteri |
| Belgeler | Kontrol listesi, güvenlik talimatı |
| Erişim sonu | 31 Aralık 2026 |
| Durum | Etkin |

Bu kişi sisteme girmez. Kayıt, o iki belgenin onunla paylaşıldığını söyler.

## Adımlar

1. Paylaşılacak sayfalar kütüphanede olsun.
2. **Paydaş ekle**. Ad, tür ve iki belgeyi seçin. Erişim sonunu 31 Aralık yapın.
3. Durumu **Etkin** yapıp kaydedin.
4. Belgeyi kaldırıp kaydedin. Durum’da paylaşımsız satırın çıktığını görün. Sonra belgeleri geri koyun.
5. Deneme kaydını silin. Sayfalar kütüphanede kalır.

## Ne değildir?

- Kullanıcı yönetimi değildir. Giriş hesapları başka yerdedir.
- Denetim paketi gönderimi değildir.
- Okundu kaydı değildir.
- E-posta göndermez ve oturum açtırmaz.

Sıradaki: [Süreç](./21-surec.md)
