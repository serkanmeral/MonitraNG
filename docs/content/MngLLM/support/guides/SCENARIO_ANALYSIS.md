# LLM Senaryoları Analiz Raporu

**Tarih:** 3 Ocak 2026  
**Servis:** MngLLM  
**Amaç:** Kullanıcı sorularına göre LLM yetenekleri değerlendirmesi

---

## Senaryo Analiz Tablosu

| Senaryo | Durum | Zorluk | LLM Katkısı | Backend Katkısı | Öncelik |
|---------|-------|--------|-------------|-----------------|---------|
| 1. Dataset Sorgulama (NLQ) | ✅ Mümkün | Orta-Yüksek | Doğal dil → Query dönüşümü | API çağrısı, Query execution | Yüksek |
| 2. Dil Dosyası Güncelleme | ✅ Mümkün | Orta | Çoklu dil çevirisi | JSON parsing, File operations | Yüksek |
| 3. Validasyon Dokümantasyonu | ✅ Mümkün | Düşük | Dokümantasyon analizi, Örnek üretme | - | Orta |
| 4. Kullanıcı Rehberi | ✅ Mümkün | Düşük | Adım adım talimatlar | - | Düşük |

---

## Detaylı Analiz

### Senaryo 1: Dataset Sorgulama (NLQ - Natural Language Query)

**Soru Örneği:** "Sayfa sayısı 50'den fazla kaç kitap var?"

**Durum:** ✅ **Mümkün** (Orta-Yüksek Zorluk)

**Nasıl Çalışır:**
1. LLM doğal dili anlar ve MongoDB query/API parametrelerine dönüştürür
2. Backend API çağrısı yapılır (LLM direkt veritabanı erişimi yapmaz)
3. Sonuç kullanıcıya döner

**Mimari:**
```
Kullanıcı → Chatbot UI → MngLLM Service → MngDataGateway API → MongoDB
                  ↓
         Doğal Dil → MongoDB Query/Filters
```

**Gereksinimler:**
- ✅ LLM → Query dönüştürme mantığı (Natural Language to Query)
- ✅ Dataset schema bilgisi (field isimleri, tipleri)
- ✅ API entegrasyonu (MngDataGateway)

**Zorluklar:**
- Dataset schema'larını LLM'e sağlamak (context management)
- Karmaşık sorguları doğru dönüştürmek
- Hata durumlarını yönetmek

**Öncelik:** Yüksek (Kullanıcı değeri çok yüksek)

---

### Senaryo 2: Dil Dosyası Güncelleme

**Soru Örneği:** Side menu için "Dil Dosyalarını Güncelle" butonu ile çoklu dil dosyalarını güncelleme

**Durum:** ✅ **Mümkün** (Orta Zorluk)

**Nasıl Çalışır:**
1. LLM çeviri üretir (Türkçe → İngilizce, Fransızca, Arapça, Çince)
2. JSON dosyaları parse edilir ve güncellenir
3. Güvenli dosya yazma işlemi yapılır

**Gereksinimler:**
- ✅ Çoklu dil çevirisi
- ✅ JSON dosya parsing ve yazma
- ✅ Güvenlik (validation, backup)
- ✅ Frontend entegrasyonu (mevcut "Dil Dosyalarını Güncelle" butonu)

**Zorluklar:**
- Arapça ve Çince çeviri kalitesi (Ollama küçük modellerde sınırlı olabilir)
- JSON structure'ı korumak
- Güvenli dosya yazma

**Öncelik:** Yüksek (Zaten planlanmış, RoadMap'te var)

---

### Senaryo 3: Validasyon Dokümantasyonu

**Soru Örneği:** "Dataset fieldları için validasyon nasıl yapılır?"

**Durum:** ✅ **Mümkün** (Düşük Zorluk)

**Nasıl Çalışır:**
1. LLM mevcut dokümantasyonu/kodu analiz eder
2. Kullanıcı dostu açıklama üretir
3. Örnekler verir

**Gereksinimler:**
- ✅ Dokümantasyon erişimi (veya kod analizi)
- ✅ Context management (hangi konuda soru sorulduğu)
- ✅ Örnek üretme

**Zorluklar:**
- Dokümantasyon güncel tutmak
- Context window limitleri (uzun dokümantasyon)

**Öncelik:** Orta (Yardımcı özellik, ana özellik değil)

---

### Senaryo 4: Kullanıcı Rehberi

**Soru Örneği:** "Şifremi nasıl değiştiririm?"

**Durum:** ✅ **Mümkün** (Düşük Zorluk)

**Nasıl Çalışır:**
1. LLM mevcut kullanım kılavuzunu/özelliklerini analiz eder
2. Adım adım talimatlar verir

**Gereksinimler:**
- ✅ UI özelliklerini bilme (veya kullanım kılavuzu)
- ✅ Adım adım talimat üretme
- ✅ Dinamik içerik (UI değişirse güncellenebilmeli)

**Zorluklar:**
- UI değişikliklerini takip etmek
- Kullanıcı bağlamını anlamak

**Öncelik:** Düşük (Güzel bir özellik ama kritik değil)

---

## Öncelik Sıralaması

### Faz 1: Yüksek Öncelik (MVP)

1. **Dil Dosyası Güncelleme** (Senaryo 2)
   - Zaten RoadMap'te planlanmış
   - Side Menu Manager'da buton mevcut
   - Hızlı değer üretir

2. **Dataset Sorgulama (NLQ)** (Senaryo 1)
   - Yüksek kullanıcı değeri
   - Fark yaratan özellik
   - Orta-yüksek zorluk

### Faz 2: Orta Öncelik

3. **Validasyon Dokümantasyonu** (Senaryo 3)
   - Yardımcı özellik
   - Düşük zorluk
   - Hızlı implement edilebilir

### Faz 3: Düşük Öncelik

4. **Kullanıcı Rehberi** (Senaryo 4)
   - Güzel bir özellik
   - Düşük zorluk
   - İleride eklenebilir

---

## Teknik Notlar

### NLQ (Natural Language Query) için:

**Önerilen Yaklaşım:**
1. LLM'e dataset schema bilgisi sağlamak (context olarak)
2. Doğal dili MongoDB filter formatına dönüştürmek
3. MngDataGateway API'sine göndermek
4. Sonucu kullanıcı dostu formatta döndürmek

**Örnek Prompt:**
```
Dataset: tst_books
Fields:
- title (text)
- pageCount (number)
- author (relation)
- genres (array)
- publishDate (datetime)

Kullanıcı Sorusu: "sayfa sayısı 50'den fazla kaç kitap var?"

MongoDB filter oluştur ve MngDataGateway API formatına çevir.
```

### Dil Dosyası Güncelleme için:

**Önerilen Yaklaşım:**
1. Türkçe text'i LLM'e göndermek
2. Çoklu dil çevirisi almak
3. JSON dosyalarını güncellemek (backend API)
4. Frontend'e sonucu döndürmek

**Not:** Arapça ve Çince çeviriler için daha büyük model gerekebilir (Qwen2.5 7B veya RN_TR_R1).

---

## Sonuç

Tüm senaryolar **mümkün**. Öncelik sırası:

1. ✅ **Dil Dosyası Güncelleme** - Zaten planlanmış, hızlı implement
2. ✅ **Dataset Sorgulama (NLQ)** - Yüksek değer, orta-yüksek zorluk
3. ✅ **Validasyon Dokümantasyonu** - Yardımcı özellik
4. ✅ **Kullanıcı Rehberi** - Güzel bir özellik
