# MngLLM Roadmap

Yaptıklarımız, yapacaklarımız ve kararlarımız bu dosyada güncellenecektir.

## Yapılanlar

- **Faz 1: Çoklu dil çevirisi** — Translation API (`POST /api/v1/llm/translate`), Ollama entegrasyonu (Qwen2.5 3B), TR→EN/FR/AR/ZH; MngKeeper system/locales + MinIO; Side Menu Manager “Dil Dosyalarını Güncelle” ile LLM entegrasyonu, fallback (LLM yoksa placeholder).
- **Proje yapısı** — Clean Architecture; LLMController, Health, Version; Swagger/Scalar, API versioning.
- **API Gateway** — SSL termination Gateway’de, CORS merkezi, internal HTTP; `/llm/api/v1/*`.
- **Docker** — Dockerfile, docker-compose, port 5030.

Detaylı sürüm geçmişi için [Changelog](CHANGELOG.md) dosyasına bakınız.

## Yapılacaklar

- **Faz 2: NLQ (Natural Language Query)** — Doğal dille dataset sorgulama; MngDataGateway ile entegrasyon (yüksek öncelik).
- **Faz 3: Dokümantasyon & Yardım** — Platform kullanım rehberi, dokümantasyon yardımı (orta öncelik).
- **Faz 4: Kullanıcı Rehberi** — Adım adım talimatlar (düşük öncelik).
- **Faz 5: Chatbot iyileştirmesi** — Cevapların kalitesi ve bağlam kullanımı (orta öncelik).
- **Çeviri cache** — Translation cache (opsiyonel, performans).

## Kararlar

- **Ollama** — Test için Qwen2.5 3B; production için ayrı sunucu / daha büyük model (örn. 7B) düşünülebilir.
- **SSL** — TLS Gateway’de; MngLLM internal HTTP.
- **Dil dosyaları** — MngKeeper `/system/locales` + MinIO `System/locales/`; LLM çevirisi Side Menu Manager üzerinden tetiklenir.

---

Detaylı fazlar ve teknik plan için [Roadmap (ek)](../support/guides/ROADMAP.md) dosyasına bakılabilir.
