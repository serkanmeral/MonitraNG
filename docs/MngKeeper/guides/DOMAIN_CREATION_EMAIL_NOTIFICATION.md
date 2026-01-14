# Domain Oluşturma Email Bildirimi

**Tarih:** 15 Ocak 2026  
**Versiyon:** 1.0.0  
**Durum:** ✅ Tamamlandı ve Test Edildi

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Özellikler](#özellikler)
3. [İmplementasyon Detayları](#implementasyon-detayları)
4. [Kullanım](#kullanım)
5. [Teknik Detaylar](#teknik-detaylar)

---

## 🎯 GENEL BAKIŞ

Domain oluşturulduğunda, ilgili kişiye (related person) otomatik olarak bir email bildirimi gönderilir. Bu bildirim, domain bilgilerini ve admin kullanıcı bilgilerini içerir.

### Senaryo

1. Kullanıcı MngDomainUI üzerinden yeni bir domain oluşturur
2. `RelatedPersonEmail` alanına email adresi girilir (opsiyonel)
3. Domain başarıyla oluşturulduktan sonra pipeline'ın son adımı olarak email gönderilir
4. Email MngNotifier servisi üzerinden gönderilir

---

## ✅ ÖZELLİKLER

- ✅ Domain oluşturulduğunda otomatik email gönderimi
- ✅ HTML formatında email
- ✅ Domain bilgileri (Domain Name, Display Name, Created Date)
- ✅ Admin kullanıcı bilgileri (Username, Email, Password)
- ✅ Non-critical step: Mail gönderilemezse pipeline başarısız olmaz
- ✅ Email formatı: HTML (responsive design)
- ✅ MngDomainUI formunda `RelatedPersonEmail` alanı

---

## 🔧 İMPLEMENTASYON DETAYLARI

### 1. Pipeline Step: `SendDomainCreatedEmailStep`

**Konum:** `MngKeeper/Core/MngKeeper.Application/Pipelines/DomainCreation/Steps/SendDomainCreatedEmailStep.cs`

**Özellikler:**
- Domain creation pipeline'ının son adımı (Step 12)
- `ActivateDomainStep`'ten sonra çalışır
- Non-critical: Mail gönderilemezse pipeline başarısız olmaz (log + warning)
- `RelatedPersonEmail` yoksa veya geçersizse step skip edilir

**Akış:**
```
Domain Creation Pipeline:
  1. ValidateDomainStep
  2. CreateDomainEntityStep
  3. CreateDatabaseStep
  4. InitializeDatabaseCollectionsStep
  5. InitializeDataGatewayCollectionsStep
  6. CreateIndexesStep
  7. CreateKeycloakRealmStep
  8. CreateDefaultGroupsStep
  9. CreateAdminUserStep
  10. PublishDomainCreatedEventStep
  11. InitializeDomainCacheStep
  12. CreateMinIOBucketStep
  13. ActivateDomainStep
  14. SendDomainCreatedEmailStep ← Email gönderimi
```

### 2. Email Template

**Konum:** `MngNotifier/Infrastructure/MngNotifier.Infrastructure/Templates/Email/domain-created.html`

**Durum:** Template hazır, ancak şu anda kullanılmıyor. Step içinde basit HTML email oluşturuluyor.

**Placeholder'lar (Gelecekte kullanılacak):**
- `{{DomainName}}` - Domain adı
- `{{DisplayName}}` - Domain görünen adı
- `{{CreatedAt}}` - Oluşturulma tarihi/saati
- `{{RelatedPersonName}}` - İlgili kişinin adı
- `{{AdminEmail}}` - Admin kullanıcı e-posta adresi
- `{{AdminUsername}}` - Admin kullanıcı adı
- `{{AdminPassword}}` - Admin şifresi

**Not:** Şu an template service entegre edilmemiş. Gelecekte `IEmailTemplateService` kullanılarak template'ten email oluşturulabilir.

### 3. Servisler

**MngKeeper:**
- `INotifierService` - MngNotifier API'sine HTTP istek gönderir
- `NotifierService` - Implementation (HttpClient kullanır)

**MngNotifier:**
- `IEmailTemplateService` - Template okuma ve placeholder replacement (hazır, şu an kullanılmıyor)
- `EmailTemplateService` - Implementation
- `IMailProvider` - Mail gönderme
- `SmtpMailProvider` - SMTP implementation

### 4. Domain Model Güncellemeleri

**DomainCreationContext:**
- `RelatedPersonEmail` field'ı eklendi (nullable)

**CreateDomainCommand:**
- `RelatedPersonEmail` field'ı eklendi (optional)

**MngDomainUI:**
- `DomainForm.vue` - `RelatedPersonEmail` input field eklendi
- `CreateDomainRequest` type - `relatedPersonEmail` field'ı eklendi

---

## 📧 KULLANIM

### MngDomainUI'da Domain Oluşturma

1. Domain oluşturma formunu açın
2. `Related Person Email` alanına email adresi girin (opsiyonel)
3. Diğer gerekli alanları doldurun
4. "Create Domain" butonuna tıklayın
5. Domain oluşturulduktan sonra, `RelatedPersonEmail` varsa otomatik olarak email gönderilir

### Email İçeriği

Email şu bilgileri içerir:
- Domain bilgileri:
  - Domain Adı
  - Görünen Ad
  - Oluşturulma Tarihi
- Yönetici hesap bilgileri:
  - Kullanıcı Adı
  - E-posta
  - Şifre (güvenlik uyarısı ile)
- Güvenlik uyarısı: Şifrenin güvenli saklanması gerektiği

---

## 🔧 TEKNİK DETAYLAR

### Konfigürasyon

**MngKeeper Settings:**
```json
{
  "MngKeeperSettings": {
    "Notifier": {
      "BaseUrl": "http://mngnotifier:5070",
      "ApiVersion": "v1"
    }
  }
}
```

**Docker Compose:**
```yaml
environment:
  - MngKeeperSettings__Notifier__BaseUrl=http://mngnotifier:5070
  - MngKeeperSettings__Notifier__ApiVersion=v1
```

**MngNotifier Settings:**
```json
{
  "MngNotifierSettings": {
    "Mail": {
      "Provider": "SMTP",
      "DefaultFrom": {
        "email": "noreply@monitrang.com",
        "name": "MonitraNG"
      },
      "Smtp": {
        "Host": "mail.monitrang.com",
        "Port": 587,
        "Username": "noreply@monitrang.com",
        "Password": "***",
        "EnableSsl": true
      }
    }
  }
}
```

### API Endpoint

**MngNotifier API:**
- `POST /api/v1/notifications/mail`
- Authentication: ❌ No authentication required (AllowAnonymous)
- Request body:
  ```json
  {
    "to": ["recipient@example.com"],
    "subject": "Domain Oluşturuldu: {DisplayName}",
    "body": "<html>...</html>",
    "isHtml": true
  }
  ```

### Hata Yönetimi

- Email gönderilemezse pipeline başarısız olmaz
- Hata durumunda log kaydı yapılır ve warning döner
- `RelatedPersonEmail` yoksa veya geçersizse step skip edilir

### Logging

- Email gönderme başarılı olduğunda: Information level log
- Email gönderme başarısız olduğunda: Error level log (non-critical)
- Step skip edildiğinde: Information level log

---

## 📝 NOTLAR

1. **Template Service:** Şu an template service hazır ancak kullanılmıyor. Step içinde basit HTML email oluşturuluyor. Gelecekte template service entegre edilebilir.

2. **HTML Hatalar:** Mail içeriğinde bazı küçük HTML formatlama hataları olabilir, ancak email'in görüntülenmesi ve okunması için kritik değildir.

3. **Güvenlik:** Admin şifresi email içinde gönderiliyor. Gelecekte şifre sıfırlama linki gönderilmesi düşünülebilir.

4. **Template Yönetimi:** `domain-created.html` template dosyası hazır ancak şu an kullanılmıyor. Template service entegre edildiğinde kullanılabilir.

---

## 🔄 GELECEK GELİŞTİRMELER

1. **Template Service Entegrasyonu:**
   - `IEmailTemplateService` kullanılarak template'ten email oluşturma
   - Placeholder replacement işlemi
   - Template yönetimi (MngDataGateway üzerinden)

2. **Email İyileştirmeleri:**
   - HTML formatlama hatalarının düzeltilmesi
   - Responsive design iyileştirmeleri
   - Logo desteği (domain logosu veya sistem logosu)

3. **Güvenlik:**
   - Şifre sıfırlama linki gönderme seçeneği
   - Email içinde şifre göndermeme seçeneği

4. **Diğer Mail Türleri:**
   - Domain güncellendiğinde email
   - Domain silindiğinde email
   - Kullanıcı daveti email'i
   - Şifre sıfırlama email'i

---

**Son Güncelleme:** 15 Ocak 2026  
**Test Durumu:** ✅ Başarıyla test edildi  
**Production Durumu:** ✅ Production'da çalışıyor
