# MonitraNG Workflow Runtime Internal Design v1.1

## Doküman Durumu

* Durum: Tasarım Tamamlandı
* Versiyon: 1.1
* Kapsam: Workflow Runtime Genişletilmiş Mimari Tasarımı
* Bağımlılık:

  * Workflow Engine Planı v1.1
  * Workflow Runtime Internal Design v1.0

---

# 1. Amaç

Bu doküman Workflow Runtime'ın operasyonel çalışma modelini detaylandırır.

v1.0 dokümanında tanımlanan:

* Node Modeli
* Edge Modeli
* Execution Context
* Runtime Akışı
* Retry ve Timeout Yapısı

üzerine aşağıdaki konular eklenmiştir:

* Queue Architecture
* Worker Architecture
* Publish Lifecycle
* Node Registry
* Debug Mode
* Replay Mechanism
* Dead Letter Handling
* Service Boundaries

---

# 2. Queue Architecture

Workflow Runtime tamamen Queue Based Execution yaklaşımıyla çalışacaktır.

Her node çalıştırması bağımsız bir queue mesajı olarak ele alınacaktır.

---

## Queue Listesi

```text
workflow.execution.queue
workflow.retry.queue
workflow.delay.queue
workflow.deadletter.queue
workflow.event.queue
```

---

## workflow.execution.queue

Normal node çalıştırmalarını taşır.

Örnek:

```json
{
  "instanceId": "guid",
  "workflowVersionId": "guid",
  "nodeId": "http_request_1",
  "attempt": 1,
  "correlationId": "guid"
}
```

---

## workflow.retry.queue

Retry gerektiren node'lar için kullanılır.

Örnek:

```text
HTTP Timeout
↓
Retry Queue
↓
5 saniye sonra tekrar çalıştır
```

---

## workflow.delay.queue

Delay Node tarafından kullanılır.

Örnek:

```text
Delay 2 Saat
↓
workflow.delay.queue
↓
2 Saat Sonra Resume
```

---

## workflow.deadletter.queue

Retry sınırını aşan node'lar buraya gönderilir.

Örnek:

```text
HTTP Request
↓
3 Retry
↓
Başarısız
↓
Dead Letter Queue
```

---

## workflow.event.queue

Harici event'lerin runtime'a aktarılması için kullanılır.

Örnek:

```text
AlarmRaised
DocumentUploaded
WorkItemCreated
SecurityThreatDetected
```

---

# 3. Worker Architecture

Workflow çalıştırma işlemleri Worker Servisi tarafından gerçekleştirilir.

---

## Worker Sorumlulukları

```text
Queue Mesajı Al
↓
Workflow Instance Yükle
↓
Workflow Version Yükle
↓
Node Çalıştır
↓
Execution Context Güncelle
↓
Execution Log Yaz
↓
Next Node Queue
```

---

## Worker Ölçeklendirme

Worker stateless tasarlanacaktır.

Avantajları:

* Horizontal Scale
* Kubernetes Uyumlu
* Docker Uyumlu
* Restart Güvenli

---

## Worker Sayısı

İlk Faz:

```text
1-3 Worker
```

İleri Faz:

```text
N Worker
```

yük paylaşımı desteklenecektir.

---

# 4. Service Boundaries

Workflow sistemi başlangıçta iki servis olarak tasarlanacaktır.

---

## MngWorkflowApi

Görevleri:

```text
Workflow CRUD
Version Yönetimi
Publish İşlemleri
Approval İşlemleri
Workflow Listeleri
Run History
Debug Görüntüleme
```

---

## MngWorkflowWorker

Görevleri:

```text
Workflow Çalıştırma
Queue Tüketme
Retry Yönetimi
Resume İşlemleri
Node Çalıştırma
```

---

## Gelecekte Ayrılabilecek Servisler

```text
MngWorkflowScheduler
MngWorkflowEventListener
```

İlk fazda ayrı servis olmayacaktır.

---

# 5. Publish Lifecycle

Workflow tasarımları versiyon bazlı yönetilecektir.

---

## Draft

Özellikleri:

* Düzenlenebilir
* Çalıştırılamaz
* Test Edilebilir

---

## Published

Özellikleri:

* Aktif Versiyon
* Çalıştırılabilir
* Değiştirilemez

---

## Archived

Özellikleri:

* Pasif
* Yeni Instance Başlatamaz
* Geçmiş Referansı Olarak Saklanır

---

# 6. Version Isolation

Önemli karar:

Çalışan Workflow Instance her zaman belirli bir Workflow Version üzerinden çalışacaktır.

Örnek:

```text
Workflow v3 Publish
↓
100 Instance Çalışıyor
↓
Workflow v4 Publish
```

Sonuç:

```text
Mevcut 100 Instance
→ v3 ile devam eder

Yeni Instance
→ v4 ile başlar
```

Bu yaklaşım süreç tutarlılığı sağlar.

---

# 7. Node Registry

Runtime Node Tiplerini Registry üzerinden çözecektir.

---

## İlk Faz Yaklaşımı

```csharp
services.AddScoped<HttpRequestNode>();
services.AddScoped<IfNode>();
services.AddScoped<DelayNode>();
services.AddScoped<ApprovalNode>();
```

Node tipi:

```text
http.request
```

ilgili implementasyona yönlendirilir.

---

## Plugin Sistemi

İlk faz kapsamına alınmamıştır.

İleri fazlarda:

```text
Node Package
Node Marketplace
External Node Library
```

desteklenebilir.

---

# 8. Debug Mode

Workflow geliştirme sürecinde kullanılacaktır.

---

## Amaç

Gerçek sisteme deploy etmeden workflow'u test edebilmek.

---

## Desteklenecek Özellikler

```text
Test Run
Input Context
Output Context
Execution Time
Error Detayları
Node Sonuçları
```

---

## Desteklenmeyecek Özellikler

İlk Faz:

```text
Breakpoint
Step-by-Step Execute
Live Edit
```

---

# 9. Replay Mechanism

Replay geçmiş bir workflow çalışmasını yeniden başlatmayı sağlar.

---

## Amaç

* Hata Analizi
* Test
* Debug
* Güvenlik Olay İncelemesi

---

## Replay Yapısı

Aşağıdaki bilgiler kullanılır:

```json
{
  "workflowVersionId": "guid",
  "triggerData": {}
}
```

Runtime aynı workflow'u yeniden başlatır.

---

# 10. Dead Letter Handling

Retry sonrasında başarısız olan node'lar Dead Letter Queue'ya aktarılır.

---

## Akış

```text
Node Execute
↓
Başarısız
↓
Retry
↓
Retry
↓
Retry
↓
Dead Letter
```

---

## Workflow Durumu

```text
Failed
```

olarak işaretlenir.

---

## Gelecekte Desteklenecek Operasyonlar

```text
Retry Failed Node
Resume From Node
Restart Instance
Cancel Instance
```

---

# 11. Correlation Modeli

Her workflow instance benzersiz bir CorrelationId taşıyacaktır.

Amaç:

* Log Korelasyonu
* Distributed Tracing
* Debug
* Replay

Örnek:

```json
{
  "instanceId": "guid",
  "correlationId": "guid"
}
```

Tüm loglar bu kimlik ile ilişkilendirilir.

---

# 12. Runtime Monitoring

Workflow Runtime kendi sağlık metriklerini yayınlayacaktır.

Örnek:

```text
Active Workflows
Completed Workflows
Failed Workflows
Average Execution Time
Retry Count
Queue Length
Dead Letter Count
```

Bu metrikler Monitoring modülüne gönderilecektir.

---

# 13. Güvenlik İlkeleri

Workflow Runtime aşağıdaki bilgileri loglamayacaktır:

* Şifreler
* API Key'ler
* Access Token'lar
* Secret Değerleri

Secret çözümleme yalnızca çalışma anında yapılacaktır.

---

# 14. Sonuç

Workflow Runtime v1.1 ile birlikte:

* Queue Based Execution
* Worker Architecture
* Publish Lifecycle
* Version Isolation
* Debug Mode
* Replay Mechanism
* Dead Letter Handling
* Runtime Monitoring

kararları netleştirilmiştir.

Bu doküman sonrasında Workflow Engine tarafında büyük mimari kararların tamamlandığı kabul edilir.

Bir sonraki aşama:

```text
Workflow Backend Implementation Plan v1
```

dokümanının hazırlanmasıdır.

Bu doküman artık mimariyi değil, doğrudan .NET Core proje yapısını, servisleri, MongoDB koleksiyonlarını, RabbitMQ topology'sini, worker sınıflarını ve geliştirme sıralamasını tanımlayacaktır.
