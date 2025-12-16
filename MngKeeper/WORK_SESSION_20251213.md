# MngKeeper - Çalışma Oturumu Özeti
**Tarih:** 13 Aralık 2025  
**Konu:** Kullanıcıların Varsayılan "users" Grubuna Otomatik Eklenmesi ve Token'da user_groups Claim'i

---

## ✅ Tamamlanan İşler

### 1. CreateDefaultGroupsStep Güncellemesi
**Dosya:** `Core/MngKeeper.Application/Pipelines/DomainCreation/Steps/CreateDefaultGroupsStep.cs`

**Yapılan Değişiklikler:**
- Gruplar artık hem Keycloak'ta hem de MngKeeper veritabanına kaydediliyor
- `IGroupRepository` ve `IDataGatewaySyncService` inject edildi
- Her grup (admins, managers, users, guests) oluşturulurken:
  1. Keycloak'ta oluşturuluyor
  2. MngKeeper MongoDB'ye kaydediliyor
  3. DataGateway MongoDB'ye sync ediliyor

**Önemi:** Artık domain oluşturulurken "users" grubu MngKeeper veritabanında da mevcut, böylece `GetByDomainIdAsync` ile bulunabiliyor.

---

### 2. CreateUserCommandHandler Güncellemesi
**Dosya:** `Core/MngKeeper.Application/Features/User/Commands/CreateUser/CreateUserCommandHandler.cs`

**Yapılan Değişiklikler:**
- Yeni kullanıcılar otomatik olarak "users" grubuna ekleniyor
- `GetByDomainIdAsync` kullanılarak domain'e özel grup araması yapılıyor
- Group ID'lerden group name'lere dönüşüm yapılıyor (Keycloak API'si için)
- `AddUserToGroupAsync` metoduna group name gönderiliyor (group ID değil)

**Kod Mantığı:**
```csharp
// 1. "users" grubunu domain'de bul
var domainGroups = await _groupRepository.GetByDomainIdAsync(claims.DomainId);
var usersGroup = domainGroups.FirstOrDefault(g => g.Name == "users" && g.DomainId == claims.DomainId);

// 2. Eğer kullanıcı "users" grubunda değilse ekle
if (usersGroup != null && !finalGroupIds.Contains(usersGroup.Id))
{
    finalGroupIds.Add(usersGroup.Id);
}

// 3. Group ID'lerden group name'lere dönüştür (Keycloak için)
var groupNames = new List<string>();
foreach (var groupId in finalGroupIds)
{
    var group = await _groupRepository.GetByIdAsync(groupId);
    if (group != null && group.DomainId == claims.DomainId)
    {
        groupNames.Add(group.Name);
    }
}

// 4. Keycloak'ta kullanıcıyı gruplara ekle
foreach (var groupId in finalGroupIds)
{
    var group = await _groupRepository.GetByIdAsync(groupId);
    if (group != null && group.DomainId == claims.DomainId)
    {
        await _keycloakService.AddUserToGroupAsync(domainValue.RealmName, keycloakUser.Id, group.Name);
    }
}
```

---

### 3. Test Script'i
**Dosya:** `tests/create-testdomain-and-test.ps1`

**Test Senaryosu:**
1. Yeni bir domain oluştur (timestamp ile benzersiz isim)
2. Keycloak protocol mapper'larını yapılandır
3. "users" grubunun domain'de mevcut olduğunu doğrula
4. Grup belirtmeden yeni bir kullanıcı oluştur
5. Kullanıcının otomatik olarak "users" grubuna eklendiğini doğrula
6. Token'ı parse et ve `user_groups` claim'inin varlığını kontrol et
7. MngKeeper DB'de kullanıcının "users" grubunda olduğunu doğrula

**Test Sonucu:** ✅ BAŞARILI
- Domain oluşturuldu
- "users" grubu hem Keycloak'ta hem MngKeeper DB'de mevcut
- Kullanıcı otomatik olarak "users" grubuna eklendi
- Token'da `user_groups` claim'i var ve "users" grubunu içeriyor
- MngKeeper DB'de kullanıcı "users" grubunda

---

## 📝 Önemli Notlar

### Keycloak API Kullanımı
- `AddUserToGroupAsync` metodu **group name** bekliyor, **group ID** değil
- Bu yüzden MngKeeper Group ID'sinden group name'e dönüşüm yapılıyor

### Domain'e Özel Grup Araması
- `GetByNameAsync` domain'e göre filtreleme yapmıyor
- Bu yüzden `GetByDomainIdAsync` kullanılıp sonra name ile filtreleme yapılıyor

### Protocol Mapper Yapılandırması
- Yeni domain oluşturulduktan sonra Keycloak protocol mapper'ları yapılandırılmalı
- Endpoint: `POST /api/admin/realms/{realmName}/configure-mappers`
- Mapper'lar yapılandırıldıktan sonra yeni token alınmalı (eski token'da claim'ler olmayabilir)

---

## 🔄 Sonraki Adımlar (Opsiyonel)

1. **Mevcut Domain'ler için "users" Grubu Oluşturma:**
   - "ebebek", "proline" gibi mevcut domain'ler için "users" grubunu manuel oluşturma script'i
   - Veya migration script'i ile tüm mevcut domain'ler için otomatik oluşturma

2. **Kullanıcı Güncelleme Senaryosu:**
   - Kullanıcı güncellendiğinde "users" grubundan çıkarılırsa tekrar eklenmesi gerekir mi?
   - Şu an sadece yeni kullanıcı oluşturulurken otomatik ekleniyor

3. **Test Coverage:**
   - Farklı senaryolar için test script'leri (kullanıcı zaten bir grup belirtmişse, "users" grubu yoksa vb.)

---

## 📂 Değiştirilen Dosyalar

1. `Core/MngKeeper.Application/Pipelines/DomainCreation/Steps/CreateDefaultGroupsStep.cs`
2. `Core/MngKeeper.Application/Features/User/Commands/CreateUser/CreateUserCommandHandler.cs`
3. `tests/create-testdomain-and-test.ps1` (yeni)

---

## ✅ Test Sonuçları

**Test Domain:** `testdomain20251213161033`  
**Test Kullanıcı:** `test.user.20251213161033`  
**Sonuç:** Tüm kontroller başarılı ✅

- ✅ Domain oluşturuldu
- ✅ "users" grubu domain'de mevcut
- ✅ Kullanıcı otomatik olarak "users" grubuna eklendi
- ✅ Token'da `user_groups` claim'i var: `["users"]`
- ✅ MngKeeper DB'de kullanıcı "users" grubunda

---

## 🎯 Özet

Artık her yeni kullanıcı otomatik olarak "users" grubuna ekleniyor ve bu grup bilgisi JWT token'ında `user_groups` claim'i olarak görünüyor. Domain oluşturulurken varsayılan gruplar hem Keycloak'ta hem de MngKeeper veritabanında oluşturuluyor.

---

**Not:** Uygulama derlenmiş ve test edilmiş durumda. Yarın kaldığımız yerden devam edebiliriz.

