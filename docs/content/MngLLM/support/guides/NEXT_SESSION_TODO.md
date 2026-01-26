# Chatbot - Yarınki Oturum İçin Yapılacaklar

**Tarih:** 15 Ocak 2026  
**Sonraki Oturum:** 16 Ocak 2026  
**Durum:** 📋 Planlama Tamamlandı - Implementasyona Hazır

---

## ✅ Bugün Tamamlananlar

### 1. Chatbot Planlama
- ✅ Kapsamlı chatbot planlama dokümanı oluşturuldu (`CHATBOT_PLANNING.md`)
- ✅ Kullanım senaryoları belirlendi (NLQ, Docs, Guide, General)
- ✅ Mimari tasarım hazırlandı
- ✅ Faz bazlı implementasyon planı oluşturuldu

### 2. Dokümantasyon Hazırlık Stratejisi
- ✅ Dokümantasyon hazırlık stratejisi dokümanı (`DOCUMENTATION_PREPARATION_STRATEGY.md`)
- ✅ MkDocs markdown ve OpenAPI JSON kullanımı planlandı
- ✅ İndeksleme stratejisi belirlendi (basit keyword search → vector search)
- ✅ LLM context hazırlama stratejisi hazırlandı

### 3. Çoklu Dil Desteği
- ✅ Çoklu dil desteği planı (`MULTILINGUAL_SUPPORT.md`)
- ✅ 5 dil desteği planlandı (tr, en, fr, ar, zh)
- ✅ UI çevirileri ve LLM dil algılama stratejisi hazırlandı

### 4. UI Rehber Desteği
- ✅ UI rehber desteği stratejisi (`UI_GUIDE_STRATEGY.md`)
- ✅ Chatbot-optimized rehber formatı belirlendi
- ✅ Mevcut rehberler vs. ayrı chatbot klasörü analizi yapıldı
- ✅ Hibrit yaklaşım önerildi (`docs/Mng.Ui/guides/chatbot/` klasörü)

### 5. Chatbot İsmi
- ✅ Chatbot ismi belirlendi: **Moni**
- ✅ İsim belirleme dokümanı oluşturuldu (`CHATBOT_NAME.md`)
- ✅ Kullanım senaryoları ve implementasyon notları hazırlandı

### 6. Implementasyon Planı
- ✅ Detaylı implementasyon planı (`IMPLEMENTATION_PLAN.md`)
- ✅ Faz bazlı görev listesi hazırlandı
- ✅ Token yönetimi stratejisi planlandı

---

## 📋 Yarın Yapılacaklar

### 1. MkDocs Dokümantasyon Planlaması (Öncelik)

**Amaç:** MkDocs dosyalarını hem chatbot hem de insanlar için uygun hale getirme planı

**Görevler:**
- [ ] MkDocs dokümantasyon yapısını analiz et
- [ ] Chatbot için optimize edilmiş format belirle
- [ ] Front matter (YAML metadata) standardı oluştur
- [ ] İnsan okunabilirliği ile chatbot parse edilebilirliği arasındaki dengeyi planla
- [ ] Markdown + Metadata hybrid format stratejisi
- [ ] Rehber template'i oluştur
- [ ] İlk örnek rehberleri hazırla (template kullanarak)

**Hedef:** 
- Chatbot'un anlayabileceği structured format
- İnsanların okuyabileceği markdown formatı
- İkisini de destekleyen hibrit yapı

**Sorular:**
1. Mevcut MkDocs dosyalarına metadata ekleyelim mi?
2. Ayrı chatbot klasörü mü oluşturalım?
3. Front matter standardı nasıl olsun?
4. Adım adım talimatlar nasıl formatlanmalı?
5. Route linkleri nasıl yapılandırılmalı?

### 2. İlk Rehber Hazırlama (Olası)

**Eğer MkDocs planlaması biterse:**
- [ ] İlk örnek rehberi hazırla (örn: Dataset Oluşturma)
- [ ] Template kullanarak test et
- [ ] Chatbot parse edilebilirliğini kontrol et
- [ ] İnsan okunabilirliğini kontrol et

---

## 📝 Planlama Notları

### MkDocs Dokümantasyon Hibrit Format Stratejisi

**Hedef:** Aynı dosya hem insanlar hem chatbot tarafından kullanılabilmeli

**Yaklaşım:**
1. **Front Matter (YAML):** Chatbot için metadata
2. **Markdown Content:** İnsanlar için içerik
3. **Structured Sections:** Chatbot parse edebilir yapı

**Örnek Format:**
```yaml
---
title: "Dataset Oluşturma"
category: "datasets"
tags: ["dataset", "create"]
route: "/apps/datasets/create"
language: "tr"
steps:
  - order: 1
    title: "Dataset Management Sayfasına Git"
    route: "/apps/datasets"
    action: "Menüden 'Datasets' tıkla"
---
# Dataset Oluşturma

[Markdown content - normal rehber metni]

## Adımlar

1. ...
2. ...
```

**Avantajlar:**
- ✅ Tek dokümantasyon (maintainability)
- ✅ Hem insan hem chatbot tarafından kullanılabilir
- ✅ MkDocs ile render edilebilir
- ✅ Chatbot parse edebilir

**Dikkat Edilmesi Gerekenler:**
- Front matter standardı belirlenmeli
- Structured sections (adımlar, route'lar) tutarlı olmalı
- Metadata ile content senkronize kalmalı

---

## 🔗 İlgili Dokümanlar

1. `CHATBOT_PLANNING.md` - Genel chatbot planlaması
2. `DOCUMENTATION_PREPARATION_STRATEGY.md` - Dokümantasyon hazırlık stratejisi
3. `UI_GUIDE_STRATEGY.md` - UI rehber desteği stratejisi
4. `IMPLEMENTATION_PLAN.md` - Detaylı implementasyon planı
5. `MULTILINGUAL_SUPPORT.md` - Çoklu dil desteği planı
6. `CHATBOT_NAME.md` - Chatbot ismi belirleme

---

## 💡 Yarınki Oturum İçin Hatırlatmalar

1. **MkDocs Dokümantasyon Planlaması** - En yüksek öncelik
2. Mevcut MkDocs yapısını incele (`docs/content/`, `mkdocs.yml`)
3. Chatbot parse edilebilirliği vs. insan okunabilirliği dengesini düşün
4. Front matter standardı belirle
5. Template oluştur
6. İlk örnek rehberi hazırla (test için)

---

**Son Güncelleme:** 15 Ocak 2026
