# MonitraNG - Test Ortamı Kaynak Gereksinimleri

**Tarih:** 15 Ocak 2025  
**Ortam:** Test/Development  
**Durum:** Tüm bileşenler dahil (AI Chat Bot ile)

---

## 📊 Toplam Kaynak Gereksinimleri

### Minimum Konfigürasyon (Küçük Test)

| Kaynak | Miktar | Notlar |
|--------|--------|--------|
| **RAM** | 16 GB | Kritik minimum |
| **CPU** | 4 Core | 2.0 GHz+ |
| **Disk** | 100 GB SSD | Sistem + veri |
| **Network** | 100 Mbps | İç ağ yeterli |

**Kullanım Senaryosu:**
- 1-5 domain
- 10-50 kullanıcı
- Düşük trafik
- AI Chat Bot (küçük model: rn_tr_r1)

---

### Önerilen Konfigürasyon (Orta Test)

| Kaynak | Miktar | Notlar |
|--------|--------|--------|
| **RAM** | 32 GB | Rahat çalışma |
| **CPU** | 8 Core | 2.4 GHz+ |
| **Disk** | 200 GB SSD | Sistem + veri + modeller |
| **Network** | 1 Gbps | İç ağ |

**Kullanım Senaryosu:**
- 5-20 domain
- 50-200 kullanıcı
- Orta trafik
- AI Chat Bot (turkcell-llm-7b-v1)

---

### İdeal Konfigürasyon (Büyük Test)

| Kaynak | Miktar | Notlar |
|--------|--------|--------|
| **RAM** | 64 GB | Yüksek performans |
| **CPU** | 16 Core | 2.8 GHz+ |
| **Disk** | 500 GB SSD | Sistem + veri + modeller + logs |
| **Network** | 1 Gbps | İç ağ |
| **GPU** | 1x NVIDIA (Opsiyonel) | AI Chat Bot için hızlandırma |

**Kullanım Senaryosu:**
- 20+ domain
- 200+ kullanıcı
- Yüksek trafik
- AI Chat Bot (turkcell-llm-7b-v1 + GPU)

---

## 🔧 Bileşen Bazlı Kaynak Dağılımı

### 1. Infrastructure Services (mng_common)

| Servis | RAM | CPU | Disk | Notlar |
|--------|-----|-----|------|--------|
| **MongoDB** | 2 GB | 1 Core | 20 GB | Veri + index'ler |
| **PostgreSQL** | 1 GB | 1 Core | 5 GB | Keycloak için |
| **Keycloak** | 1.5 GB | 1 Core | 2 GB | Identity management |
| **Redis** | 512 MB | 0.5 Core | 1 GB | Cache (maxmemory: 512MB) |
| **RabbitMQ** | 512 MB | 1 Core | 5 GB | Message queue |
| **MinIO** | 512 MB | 1 Core | 50 GB | Object storage (büyüyebilir) |
| **Mosquitto** | 128 MB | 0.5 Core | 1 GB | MQTT broker |
| **Seq** | 512 MB | 1 Core | 10 GB | Log aggregation |
| **Mongo Express** | 256 MB | 0.5 Core | - | Web UI |
| **Redis Commander** | 256 MB | 0.5 Core | - | Web UI |
| **Portainer** | 256 MB | 0.5 Core | 1 GB | Container management |
| **Node-RED** | 512 MB | 1 Core | 2 GB | Workflow automation |
| **TOPLAM** | **~8 GB** | **~9 Core** | **~97 GB** | |

---

### 2. Application Services (mng_apps)

| Servis | RAM | CPU | Disk | Notlar |
|--------|-----|-----|------|--------|
| **MngGateway** | 512 MB | 1 Core | 1 GB | API Gateway |
| **MngKeeper** | 1 GB | 1 Core | 2 GB | Identity & Access |
| **MngDataGateway** | 1 GB | 1 Core | 2 GB | Data operations |
| **MngHub** | 512 MB | 1 Core | 1 GB | SignalR Hub |
| **MngReactor** | 1 GB | 1 Core | 2 GB | Business logic (varsa) |
| **TOPLAM** | **~4 GB** | **~5 Core** | **~8 GB** | |

---

### 3. AI Chat Bot Services (yeni)

| Servis | RAM | CPU | Disk | Notlar |
|--------|-----|-----|------|--------|
| **MngChatBot** | 512 MB | 1 Core | 1 GB | Chat API |
| **Qdrant** | 1 GB | 1 Core | 5 GB | Vector database |
| **Ollama (Küçük)** | 6 GB | 2 Core | 10 GB | rn_tr_r1 model (~3B) |
| **Ollama (Orta)** | 10 GB | 4 Core | 15 GB | turkcell-llm-7b-v1 (~7B) |
| **Ollama (Büyük)** | 16 GB | 6 Core | 20 GB | turkcell-llm-7b-v1 + GPU |
| **TOPLAM (Küçük)** | **~7.5 GB** | **~4 Core** | **~16 GB** | |
| **TOPLAM (Orta)** | **~11.5 GB** | **~6 Core** | **~21 GB** | |
| **TOPLAM (Büyük)** | **~17.5 GB** | **~8 Core** | **~26 GB** | |

---

## 📈 Toplam Kaynak Özeti

### Senaryo 1: Minimum (Küçük Test)

```
Infrastructure:     8 GB RAM,  9 Core,  97 GB Disk
Application:       4 GB RAM,  5 Core,   8 GB Disk
AI Chat Bot:       7.5 GB RAM, 4 Core,  16 GB Disk
Sistem Overhead:   1 GB RAM,  1 Core,   5 GB Disk
─────────────────────────────────────────────────
TOPLAM:           20.5 GB RAM, 19 Core, 126 GB Disk
```

**Önerilen Hosting:**
- **VPS:** 24 GB RAM, 4-6 Core, 150 GB SSD
- **Bulut:** AWS t3.xlarge (16 GB RAM, 4 vCPU) + 150 GB EBS
- **Fiyat:** ~$50-80/ay

---

### Senaryo 2: Önerilen (Orta Test)

```
Infrastructure:     8 GB RAM,  9 Core,  97 GB Disk
Application:       4 GB RAM,  5 Core,   8 GB Disk
AI Chat Bot:      11.5 GB RAM, 6 Core,  21 GB Disk
Sistem Overhead:   2 GB RAM,  2 Core,  10 GB Disk
─────────────────────────────────────────────────
TOPLAM:           25.5 GB RAM, 22 Core, 136 GB Disk
```

**Önerilen Hosting:**
- **VPS:** 32 GB RAM, 8 Core, 200 GB SSD
- **Bulut:** AWS t3.2xlarge (32 GB RAM, 8 vCPU) + 200 GB EBS
- **Fiyat:** ~$100-150/ay

---

### Senaryo 3: İdeal (Büyük Test)

```
Infrastructure:     8 GB RAM,  9 Core,  97 GB Disk
Application:       4 GB RAM,  5 Core,   8 GB Disk
AI Chat Bot:      17.5 GB RAM, 8 Core,  26 GB Disk
Sistem Overhead:   4 GB RAM,  2 Core,  20 GB Disk
─────────────────────────────────────────────────
TOPLAM:           33.5 GB RAM, 24 Core, 151 GB Disk
```

**Önerilen Hosting:**
- **VPS:** 64 GB RAM, 16 Core, 500 GB SSD
- **Bulut:** AWS t3.4xlarge (64 GB RAM, 16 vCPU) + 500 GB EBS
- **GPU:** NVIDIA T4 (opsiyonel, AI hızlandırma için)
- **Fiyat:** ~$200-300/ay (GPU olmadan), ~$500-700/ay (GPU ile)

---

## 💰 Hosting Seçenekleri ve Fiyatlandırma

### 1. VPS (Virtual Private Server)

**Hetzner (Önerilen - Avrupa):**
- **CX41:** 16 GB RAM, 4 Core, 160 GB SSD - €25/ay (~$27)
- **CPX41:** 16 GB RAM, 8 Core, 240 GB SSD - €35/ay (~$38)
- **CCX33:** 64 GB RAM, 16 Core, 640 GB SSD - €95/ay (~$103)

**DigitalOcean:**
- **16 GB RAM, 4 vCPU, 320 GB SSD:** $96/ay
- **32 GB RAM, 8 vCPU, 640 GB SSD:** $192/ay
- **64 GB RAM, 16 vCPU, 1.2 TB SSD:** $384/ay

**Linode:**
- **16 GB RAM, 4 vCPU, 320 GB SSD:** $80/ay
- **32 GB RAM, 8 vCPU, 640 GB SSD:** $160/ay
- **64 GB RAM, 16 vCPU, 1.2 TB SSD:** $320/ay

---

### 2. Bulut (AWS/Azure/GCP)

**AWS EC2:**
- **t3.xlarge:** 16 GB RAM, 4 vCPU - $0.1664/saat (~$120/ay)
- **t3.2xlarge:** 32 GB RAM, 8 vCPU - $0.3328/saat (~$240/ay)
- **t3.4xlarge:** 64 GB RAM, 16 vCPU - $0.6656/saat (~$480/ay)
- **EBS Storage:** $0.10/GB/ay (100 GB = $10/ay)

**Azure VM:**
- **Standard_D4s_v3:** 16 GB RAM, 4 vCPU - ~$150/ay
- **Standard_D8s_v3:** 32 GB RAM, 8 vCPU - ~$300/ay
- **Standard_D16s_v3:** 64 GB RAM, 16 vCPU - ~$600/ay

**Google Cloud:**
- **n1-standard-4:** 15 GB RAM, 4 vCPU - ~$140/ay
- **n1-standard-8:** 30 GB RAM, 8 vCPU - ~$280/ay
- **n1-standard-16:** 60 GB RAM, 16 vCPU - ~$560/ay

---

### 3. Dedicated Server (Fiziksel)

**Hetzner Dedicated:**
- **EX42:** 64 GB RAM, Intel i7, 2x 512 GB SSD - €49/ay (~$53)
- **AX161:** 128 GB RAM, AMD EPYC, 2x 1.92 TB NVMe - €199/ay (~$216)

**OVH:**
- **Rise-1:** 32 GB RAM, 4 Core, 2x 500 GB SSD - €50/ay (~$54)
- **Rise-2:** 64 GB RAM, 8 Core, 2x 1 TB SSD - €100/ay (~$108)

---

## 🎯 Öneriler

### Test Ortamı İçin En İyi Seçenek

**1. Küçük Test (1-5 domain):**
- **Hetzner CPX41:** 16 GB RAM, 8 Core, 240 GB SSD - €35/ay (~$38)
- **Toplam:** ~$40/ay
- **Yeterli:** Minimum konfigürasyon için

**2. Orta Test (5-20 domain):**
- **Hetzner CCX33:** 64 GB RAM, 16 Core, 640 GB SSD - €95/ay (~$103)
- **Toplam:** ~$110/ay
- **Yeterli:** Önerilen konfigürasyon için

**3. Büyük Test (20+ domain):**
- **Hetzner Dedicated EX42:** 64 GB RAM, Intel i7, 2x 512 GB SSD - €49/ay (~$53)
- **Toplam:** ~$60/ay
- **Yeterli:** İdeal konfigürasyon için (GPU olmadan)

---

## ⚠️ Önemli Notlar

### Disk Kullanımı

1. **MongoDB:** Veri büyüdükçe artar (domain başına ~1-5 GB)
2. **MinIO:** Dosya storage (kullanıma göre değişir)
3. **Ollama Modelleri:** 
   - rn_tr_r1: ~3 GB
   - turkcell-llm-7b-v1: ~4.5 GB
   - nomic-embed-text: ~137 MB
4. **Qdrant:** Vector embeddings (dokümantasyon başına ~10-50 MB)
5. **Logs (Seq):** Günlük log boyutuna göre artar

### RAM Kullanımı

1. **Ollama en büyük RAM tüketicisi:** Model boyutuna göre 6-16 GB
2. **MongoDB:** Veri ve index'ler için 2-4 GB
3. **Diğer servisler:** Nispeten sabit

### CPU Kullanımı

1. **Ollama:** AI işlemleri CPU-intensive (2-6 core)
2. **MongoDB:** Query'ler için 1-2 core
3. **Diğer servisler:** Düşük CPU kullanımı

### Ölçeklenebilirlik

- **Dikey Ölçekleme:** RAM/CPU artırma (kolay)
- **Yatay Ölçekleme:** Servis replikasyonu (daha karmaşık)
- **AI Chat Bot:** Tek instance yeterli (test için)

---

## 🔄 Optimizasyon Önerileri

### 1. Kaynak Tasarrufu

- **Küçük AI Model:** rn_tr_r1 kullan (6 GB RAM yerine 10 GB)
- **Redis Memory Limit:** 512 MB yeterli (test için)
- **MinIO:** Sadece gerekli dosyalar
- **Seq Log Retention:** 7 gün (test için yeterli)

### 2. Performans İyileştirme

- **SSD Disk:** Mutlaka SSD kullan (MongoDB, Qdrant için kritik)
- **RAM:** Yeterli RAM (swap kullanımından kaçın)
- **CPU:** 2.4 GHz+ önerilir (AI işlemleri için)

### 3. Maliyet Optimizasyonu

- **Hetzner:** En uygun fiyat/performans (Avrupa)
- **Reserved Instances:** AWS/Azure'da %30-50 indirim
- **Spot Instances:** Test için uygun (AWS)

---

## 📋 Checklist

Test ortamı kurulumu için:

- [ ] Minimum 16 GB RAM (önerilen: 32 GB)
- [ ] Minimum 4 Core CPU (önerilen: 8 Core)
- [ ] Minimum 150 GB SSD (önerilen: 200 GB)
- [ ] Docker & Docker Compose kurulu
- [ ] Port açıklıkları (80, 443, 27017, 5672, vb.)
- [ ] Firewall konfigürasyonu
- [ ] Backup stratejisi (opsiyonel test için)

---

**Son Güncelleme:** 15 Ocak 2025  
**Hazırlayan:** AI Assistant

