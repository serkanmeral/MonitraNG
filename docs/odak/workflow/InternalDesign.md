# MonitraNG Workflow Runtime Internal Design v1

## Doküman Durumu

* Durum: Tasarım Aşaması
* Versiyon: 1.0
* Kapsam: Workflow Runtime Engine
* Bağımlılık: Workflow Engine Planı v1.1

---

# 1. Amaç

Bu doküman Workflow Engine'in iç çalışma mekanizmasını tanımlar.

Workflow Engine Planı dokümanı sistemin ne yapacağını açıklarken, bu doküman runtime'ın bunu nasıl gerçekleştireceğini tanımlar.

---

# 2. Temel Runtime Yaklaşımı

Runtime aşağıdaki prensipler üzerine kurulacaktır:

* Queue Based Execution
* Persistent State
* Long Running Workflow
* Distributed Execution
* Resume Support
* Version Based Execution

Runtime hiçbir node'u doğrudan çağırmaz.

Tüm node çalıştırmaları Worker'lar tarafından gerçekleştirilir.

---

# 3. Runtime Akışı

```text
Workflow Trigger
↓
Workflow Instance Create
↓
Execution Queue
↓
Worker
↓
Node Execute
↓
Execution Log
↓
Next Node Queue
↓
Workflow Complete
```

---

# 4. Node Standardı

Tüm node'lar ortak veri modelini kullanacaktır.

Örnek:

```json
{
  "id": "node_1",
  "type": "http.request",
  "name": "Firewall Request",
  "version": 1,
  "position": {
    "x": 120,
    "y": 240
  },
  "config": {},
  "inputMappings": [],
  "outputMappings": [],
  "retryPolicy": {
    "enabled": true,
    "maxAttempts": 3,
    "delaySeconds": 5
  },
  "timeoutSeconds": 30,
  "continueOnError": false
}
```

---

# 5. Edge Standardı

Node bağlantıları tipli olacaktır.

```json
{
  "id": "edge_1",
  "sourceNodeId": "if_1",
  "targetNodeId": "block_ip_1",
  "type": "true"
}
```

Desteklenen edge tipleri:

* success
* error
* true
* false
* default
* timeout
* approved
* rejected

---

# 6. Node Contract

Tüm node implementasyonları aşağıdaki sözleşmeyi uygulayacaktır.

```csharp
public interface IWorkflowNode
{
    Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken);
}
```

---

# 7. Node Registry

Runtime node tiplerini bir registry üzerinden çözecektir.

Örnek:

```text
http.request
if
switch
delay
approval.wait
create.workitem
block.ip
email.send
```

Runtime node tipi üzerinden ilgili implementasyonu bulacaktır.

---

# 8. Execution Context

Workflow boyunca tek bir context taşınacaktır.

```json
{
  "event": {},
  "variables": {},
  "outputs": {},
  "user": {},
  "system": {}
}
```

Node'lar birbirleriyle doğrudan haberleşmeyecektir.

Tüm veri paylaşımı context üzerinden yapılacaktır.

---

# 9. Output Modeli

Her node kendi çıktısını aşağıdaki yapıda yazacaktır.

```json
{
  "outputs": {
    "firewall_request": {
      "statusCode": 200,
      "success": true
    }
  }
}
```

Bu yaklaşım node'lar arasında gevşek bağlılık sağlar.

---

# 10. Node Execution Result

```csharp
public class NodeExecutionResult
{
    public bool Success { get; set; }

    public string? NextEdgeType { get; set; }

    public Dictionary<string, object?> Output { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public bool ShouldWait { get; set; }

    public string? WaitingType { get; set; }
}
```

---

# 11. Waiting State Modeli

Runtime aşağıdaki bekleme durumlarını destekleyecektir.

```text
WaitingApproval
WaitingDelay
WaitingEvent
WaitingManualResume
```

Worker bu node'ları tekrar çalıştırmaz.

Resume işlemi beklenir.

---

# 12. Workflow Instance Modeli

```json
{
  "__dataId": "guid",

  "workflowId": "guid",

  "workflowVersionId": "guid",

  "status": "Running",

  "currentNodes": [
    "node_5"
  ],

  "executionContext": {},

  "startedAt": "",

  "finishedAt": "",

  "triggerType": "event",

  "triggerData": {}
}
```

---

# 13. Runtime Worker Akışı

```text
Queue Message Al
↓
Workflow Instance Yükle
↓
Workflow Version Yükle
↓
Node Bul
↓
Node Execute
↓
Context Güncelle
↓
Log Yaz
↓
Next Node Queue
```

---

# 14. Retry Politikası

Her node kendi retry politikasını tanımlayabilir.

```json
{
  "enabled": true,
  "maxAttempts": 3,
  "delaySeconds": 5
}
```

---

# 15. Timeout Politikası

Her node bağımsız timeout süresine sahip olabilir.

```json
{
  "timeoutSeconds": 30
}
```

Timeout durumunda edge tipi:

```text
timeout
```

olarak değerlendirilir.

---

# 16. Expression Engine

Workflow koşulları expression tabanlı olacaktır.

Örnek:

```text
event.riskScore > 70
```

```text
event.country != "TR"
```

```text
outputs.ai.score > 90
```

Basit field/operator yapısı kullanılmayacaktır.

---

# 17. Approval Modeli

Approval node aşağıdaki hedef tiplerini destekleyecektir.

* User
* Group
* Expression

Örnek:

```text
event.owner
```

```text
workitem.assignee
```

---

# 18. Parallel Execution Hazırlığı

Faz 1 içerisinde kullanılmayacaktır.

Ancak veri modeli aşağıdaki senaryoyu destekleyecek şekilde tasarlanacaktır.

```text
Alarm
↓
Parallel
├─ Firewall
├─ WorkItem
└─ Notification
↓
Join
```

Bu nedenle runtime tek node yerine birden fazla aktif node taşıyabilecek şekilde tasarlanacaktır.

---

# 19. İlk Teknik Hedef

UI geliştirmeden önce aşağıdaki senaryo çalışmalıdır.

```text
Manual Trigger
↓
If
↓
HTTP Request
↓
Write Log
```

Bu senaryo başarıyla çalıştırıldığında Runtime Core tamamlanmış kabul edilir.

---

# 20. Sonuç

Workflow Runtime;

* Ölçeklenebilir
* Dağıtık çalışabilir
* Long Running Workflow destekler
* Persistent State kullanır
* Resume mekanizmasına sahiptir

Bu tasarım MonitraNG Workflow Engine Planı v1.1 ile tam uyumludur.
