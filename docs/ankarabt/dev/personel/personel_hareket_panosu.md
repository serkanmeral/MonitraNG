# Personel hareketi panosu

Son güncelleme: 16 Kasım 2026, akşam.

Durum: kimlik şeridi, açık iş bağlantısı ve haftalık toplantı takvimi kodlandı. Pano sürümü 8. Zimmet bu dilime girmedi.

Pano slug `personel`. Menüdeki Kişisel ve Personel hareketi aynı kaydı açar. Kişisel sayfa kişiyi kendine kilitler. Yönetici ve admin başkasını seçer. Yönetici olmayan kişi `/dashboards/personel` adresine girmez.

Kod: `Mng.Ui/services/personnelDashboard.ts`, `Mng.Ui/services/personnelActivityFetch.ts`, `Mng.Ui/components/welcome/PersonnelIdentityStrip.vue`, `Mng.Ui/components/welcome/PersonnelMeetingWeek.vue`.

## Kimlik şeridi

Sayıların üstündedir. Hafta kayınca yenilenmez. Kişi değişince yenilenir.

| Parça | Ne görünür | Kaynak |
|---|---|---|
| Kişi | Ad ve fotoğraf. Fotoğraf yoksa baş harf | Keeper kullanıcısı |
| Departman | Tek satır | Keeper `department` |
| Yönetici | Şemadaki oturan. Kişi çalışansa koltuğun oturanı. O boşsa bir üst koltuğun oturanı | `org_mevkiler` |
| Yıllık izin | Açık dönemin bakiyesi. Sayı yoksa çizgi | `izin_personel.keeperKisiId`, sonra `izin_ozet` içinde `yillik` ve `acikDonem` |

Cinsiyet, kullanıcı kimliği ve grup düzenleme şeritte yoktur. Zimmet yoktur. Bakiye haftaya bağlı değildir.

Çizgi, bakiyenin gizlendiği anlamına gelmez. Kart kullanıcıya bağlı değilse veya açık yıllık satırda sayı yoksa çizgi görünür. 16 Kasım akşamı sicili kullanıcı adıyla tutan 14 kart bağlandı. Bağı olmayan 31 kart çizgi gösterir.

## Tablolar

Satıra tıklamak panodan çıkmaz. Kod, iş profilini yeni sekmede açar.

| Tablo | Sütunlar |
|---|---|
| Açık iş | Kod, iş, çalışma alanı, durum, açılış, vade |
| Kapanan iş | Kod, iş, çalışma alanı, puan, kapandı |

Açılış ve vade gün.ay.yıl yazılır. Açık iş haftadan bağımsızdır. En fazla 80 açık satır çizilir.

## Toplantılar

Seçilen pazartesi–pazar haftası takvimdedir. Ay çubuğu, yeni toplantı ve seri yoktur. Toplantıya tıklamak `/apps/meetings?open=` adresini yeni sekmede açar. İzin günü ve kapanan iş takvime konmaz.

## Hız

İş paketi belgeyi ve toplantıyı beklemez. Toplantıyı takvim bileşeni çeker. Belgeler ayrı gelir. Geliştirme ortamında pano parçaları geldikçe çizilir. Hepsi bitene kadar beklenmez.

## Bu dilimin dışında

| İş | Neden |
|---|---|
| Zimmet | Konuşulmadı |
| İzin gününün takvime konması | Toplantı takvimi yeterli görüldü |
| İzin hesabı | Pano bakiyeyi okur. Dönem açılışı izin planındadır |
| Fotoğrafın diğer ekranlara yayılması | Şerit Keeper `photoUrl` gösterir. Diğer ekranlar ayrı iş |
