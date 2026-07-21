# Platform omurgası

**Tek giriş · tek veri · tek bildirim**

---

## Tek cümle

Tüm MonitraNG modülleri aynı **kimlik**, **veri** ve **bildirim** altyapısını paylaşır — kurumunuz modül modül büyürken kullanıcı listesi ve mail sunucusu kurulumunu tekrarlamaz.

---

## Bileşenler

| Bileşen | Müşteri dili |
|---------|--------------|
| **Keeper** | Kurumsal giriş, kullanıcı, grup, yetki |
| **DataGateway** | Merkezi veri — dataset, validation, dosya depolama |
| **Notifier** | E-posta, Telegram, uygulama içi bildirim |
| **Scheduler** | Zamanlanmış işler — tüm modüllere tetik |
| **Gateway** | Güvenli API geçidi |

---

## Fayda

- Kullanıcı **bir kez** oturum açar.
- Veri **tenant (domain) izolasyonlu** saklanır.
- Yeni modül = aynı kullanıcılar, aynı gruplar, aynı bildirim politikaları.
- OC ataması, rapor yetkisi, belge klasörü izni — hepsi aynı dizinden.

**Broşür cümlesi:** *«Tek omurga — modüller bu temelin üzerinde birleşir.»*

---

*MonitraNG Platform Broşürü · Modüller*
