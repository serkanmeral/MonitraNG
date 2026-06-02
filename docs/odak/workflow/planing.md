# MonitraNG Workflow Engine Planı v1.1

## Doküman Durumu

* Durum: Tasarım Tamamlandı
* Versiyon: 1.1
* Kapsam: Workflow Engine
* İlişkili Modüller:

  * Monitoring
  * Operation Core
  * Document Intelligence
  * AI Services
  * Security Services
  * Notification Services

---

# 1. Amaç

Workflow Engine, MonitraNG platformunun merkezi orkestrasyon katmanıdır.

Görevi:

* Olayları dinlemek
* Süreçleri başlatmak
* Şartları değerlendirmek
* Otomatik aksiyonları çalıştırmak
* İnsan onaylarını yönetmek
* Harici sistemlerle entegrasyon sağlamak
* Tüm işlemleri kayıt altına almak

Workflow Engine;

* Monitoring
* Security
* Operation Core
* Document Intelligence
* Notification
* AI

modüllerini birbirine bağlayan çekirdek bileşendir.

---

# 2. Temel Yaklaşım

Workflow Engine alarm üretmez.

Alarm üretimi ve alarm doğrulama Monitoring / Alarm Engine katmanının sorumluluğundadır.

Workflow Engine yalnızca oluşan olaylara tepki verir.

Örnek:

```text
AlarmRaised
↓
Risk Kontrolü
↓
Firewall Kuralı
↓
WorkItem Aç
↓
Bildirim Gönder
↓
Logla
```

---

# 3. Mimari Yaklaşım

## Temel Akış

```text
Event Source
↓
Trigger
↓
Workflow Runtime
↓
Node Executor
↓
Actions
↓
Execution Log
```

---

## Ana Bileşenler

### Workflow Definition Service

Workflow tanımlarını yönetir.

### Workflow Runtime Service

Workflow çalıştırma motorudur.

### Workflow Trigger Listener

Event, HTTP ve Schedule tetiklemelerini dinler.

### Workflow Version Service

Workflow versiyonlarını yönetir.

### Workflow Instance Service

Çalışan workflow örneklerini yönetir.

### Workflow Log Service

Execution loglarını yönetir.

### Secret Resolver

Workflow secretlarını çözer.

### Workflow UI

Workflow tasarım ekranlarını sağlar.

---

# 4. Long Running Workflow Kararı

Bu proje kapsamında aşağıdaki karar alınmıştır:

> Workflow Engine baştan Long Running Workflow destekleyecektir.

Workflow'lar;

* Saniyeler
* Dakikalar
* Saatler
* Günler
* Haftalar

boyunca çalışabilir.

Örnek:

```text
Doküman Yüklendi
↓
Yönetici Onayı Bekle
↓
2 Gün Bekle
↓
Yayınla
```

Bu nedenle workflow state bilgisi kalıcı olarak saklanmalıdır.

---

# 5. Persistent State Kararı

Workflow state bilgisi RAM üzerinde tutulamaz.

Tüm çalışma durumu MongoDB üzerinde saklanacaktır.

Avantajları:

* Uygulama restart olsa bile süreç devam eder.
* Runtime yeniden ayağa kalkabilir.
* Cluster desteği sağlanabilir.
* Dağıtık mimariye hazır olur.

---

# 6. Workflow Resume Kararı

Workflow aşağıdaki durumlarda durdurulabilir:

* Approval
* Delay
* External Event
* Manual Resume

Örnek:

```text
Approval Node
↓
WaitingApproval
↓
Admin Onayladı
↓
Resume
↓
Sonraki Node
```

Workflow kaldığı node'dan devam eder.

---

# 7. Queue Based Runtime Kararı

Workflow Runtime queue tabanlı çalışacaktır.

Temel model:

```text
Workflow Instance
↓
Execution Queue
↓
Worker
↓
Node Execute
↓
Queue Next Nodes
```

Bu yaklaşım:

* Ölçeklenebilir
* Dağıtılabilir
* Retry destekli
* Dayanıklı

bir yapı sağlar.

---

# 8. Workflow Trigger Tipleri

## Manual Trigger

UI üzerinden çalıştırılır.

## HTTP Trigger

Webhook benzeri çalışır.

Örnek:

```http
POST /api/workflows/hooks/{workflowKey}
```

## Schedule Trigger

Quartz üzerinden çalışır.

Örnek:

* Her gece
* Her saat
* Cron tabanlı

## Event Trigger

RabbitMQ üzerinden çalışır.

Örnek Eventler:

```text
AlarmRaised
AlarmResolved
SecurityThreatDetected
DocumentUploaded
WorkItemCreated
UserLoginFailed
```

---

# 9. Node Kategorileri

## Trigger Nodes

Workflow başlangıç noktaları.

## Condition Nodes

Karar mekanizmaları.

## Action Nodes

İşlem yapan düğümler.

## Integration Nodes

Harici sistem entegrasyonları.

## Approval Nodes

İnsan onayı süreçleri.

## AI Nodes

Yapay zeka servisleri.

## Security Nodes

Güvenlik aksiyonları.

## Operation Core Nodes

İş yönetimi aksiyonları.

## Notification Nodes

Bildirim işlemleri.

## Utility Nodes

Genel yardımcı node'lar.

---

# 10. MVP Node Listesi

## Trigger

* Manual Trigger
* HTTP Trigger
* Schedule Trigger
* Event Trigger

## Condition

* If
* Compare
* Expression
* Switch

## Action

* HTTP Request
* Create Alarm
* Create WorkItem
* Update WorkItem
* Write Log

## Security

* Block IP
* Unblock IP
* Firewall Request

## Notification

* Email
* Internal Notification
* Chat Message
* Webhook

## Utility

* Delay
* Set Variable
* Transform Data
* Stop Workflow

---

# 11. Execution Context Kararı

Workflow boyunca tek bir Execution Context taşınacaktır.

Örnek:

```json
{
  "event": {
    "sourceIp": "1.2.3.4",
    "riskScore": 95
  },
  "variables": {},
  "outputs": {}
}
```

Node'lar birbirleriyle bu context üzerinden haberleşecektir.

Bu yapı:

* AI Node
* Security Node
* Approval Node
* Integration Node

gibi tüm node'lar için ortak çalışma modeli sağlar.

---

# 12. Veri Modeli

## @workflow_definitions

Workflow kimliği.

```json
{
  "__dataId": "guid",
  "key": "ddos_response",
  "name": "DDoS Response",
  "category": "Security",
  "currentVersion": 1
}
```

---

## @workflow_versions

Workflow tasarımının saklandığı yer.

```json
{
  "__dataId": "guid",
  "workflowId": "guid",
  "version": 1,
  "status": "Published",
  "nodes": [],
  "edges": []
}
```

---

## @workflow_instances

Workflow çalışma örnekleri.

```json
{
  "__dataId": "guid",
  "workflowId": "guid",
  "workflowVersionId": "guid",
  "status": "Running"
}
```

---

## @workflow_node_executions

Node çalışma logları.

```json
{
  "__dataId": "guid",
  "instanceId": "guid",
  "nodeId": "node_1",
  "status": "Success"
}
```

---

## @workflow_secrets

Workflow secretları.

```json
{
  "__dataId": "guid",
  "key": "firewallToken",
  "value": "encrypted"
}
```

---

## @workflow_approvals

Bekleyen onaylar.

```json
{
  "__dataId": "guid",
  "instanceId": "guid",
  "nodeId": "approval_1",
  "status": "Waiting"
}
```

---

# 13. Waiting State Türleri

Workflow aşağıdaki bekleme durumlarını destekleyecektir.

```text
WaitingApproval
WaitingDelay
WaitingEvent
WaitingManualResume
```

---

# 14. Error Handling

İlk Faz:

* Retry
* Timeout
* Continue On Error
* Error Log

İleri Faz:

* Compensation
* Rollback
* Dead Letter Workflow
* Error Workflow

---

# 15. Approval Modeli

Örnek:

```text
Risk Score 70
↓
Approval Bekle
↓
Onaylandı
↓
Firewall Rule
```

Approval Node:

```json
{
  "type": "approval.wait",
  "approverGroup": "SecurityAdmins"
}
```

---

# 16. Secret Yönetimi

Workflow JSON içerisinde:

* Token
* API Key
* Şifre

saklanmayacaktır.

Örnek:

```json
{
  "Authorization": "Bearer {{secrets.firewallToken}}"
}
```

---

# 17. Yetkilendirme

```text
workflow.view
workflow.create
workflow.update
workflow.delete
workflow.publish
workflow.run
workflow.cancel
workflow.approve
workflow.viewLogs
workflow.manageSecrets
```

---

# 18. UI Yaklaşımı

Referanslar:

* Node-RED
* n8n
* Power Automate

---

## Workflow Listesi

* Name
* Category
* Version
* Last Run
* Status

---

## Workflow Designer

```text
Node Library
↓
Canvas
↓
Properties Panel
```

---

## Run History

Workflow geçmişi.

---

## Debug View

Node bazlı çalıştırma sonuçları.

---

# 19. MonitraNG Entegrasyonları

## Monitoring

* AlarmRaised
* AlarmResolved
* MetricThresholdExceeded

## Operation Core

* Create WorkItem
* Update WorkItem
* Change State

## Document Intelligence

* Document Uploaded
* Document Approved
* Document Published

## AI

* Analyze
* Summarize
* Classify
* Recommendation

## Security

* Block IP
* Unblock IP
* Disable User

---

# 20. Fazlar

## Faz 1

Workflow Core

* Definitions
* Versions
* Instances
* Manual Trigger
* If Node
* HTTP Request Node

## Faz 2

Trigger Sistemi

* Event Trigger
* Schedule Trigger
* RabbitMQ
* Quartz

## Faz 3

MonitraNG Entegrasyonları

* Alarm Node
* WorkItem Node
* Notification Node

## Faz 4

Security Automation

* Block IP
* Firewall Integration
* Approval

## Faz 5

AI & Document Intelligence

* AI Nodes
* Document Approval
* Document Publishing

## Faz 6

Advanced Runtime

* Parallel Execution
* ForEach
* Sub Workflow
* Workflow Templates
* Compensation Actions

---

# 21. İlk Teknik Hedef

UI geliştirmeden önce:

```text
Manual Trigger
↓
If Condition
↓
HTTP Request
↓
Write Log
```

çalıştırılmalıdır.

Backend Runtime tamamlandıktan sonra UI geliştirmesine başlanmalıdır.

---

# 22. Sonuç

Workflow Engine;

* Monitoring
* Security
* Operation Core
* Document Intelligence
* AI
* Notification

modüllerini birbirine bağlayan merkezi orkestrasyon katmanı olacaktır.

Bu planlama MonitraNG Major Roadmap ile uyumludur ve Workflow Engine tarafındaki tasarım aşaması tamamlanmış kabul edilir.
