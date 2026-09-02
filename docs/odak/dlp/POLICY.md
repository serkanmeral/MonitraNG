# DLP — Servis yerleşimi, politika nesnesi, evaluate sözleşmesi

**Durum:** Dilim 0 canlı şema + Dilim 1 motor kodlandı (2 Eylül 2026)  
**Ana plan:** [DLP_PLANNING.md](./DLP_PLANNING.md) · Lab: [LAB.md](./LAB.md)

Bu belge `DLP_PLANNING` §15’teki açık konuları (servis, JSON, Outlook↔agent) kilitler.

---

## 1. Servis yerleşimi (K7)

Yeni `MngDlp` servisi **ilk dilimde yok**. Sahiplik üçe bölünür; dağıtım mevcut agent kanalına biner.

```text
MngDocument          sınıflandırma kataloğu (dm_tags.kind) + damga (üretim/indirme)
                     + DI email.share kesicisi (sunucu)
MngKeeper            kullanıcı / grup gerçeği (eşleme kaynağı)
MngLogCollector      DLP kuralı yazar, yayınlar, agent’a derlenmiş politika verir
                     GET /api/v1/policy/dlp  (eventlog-packages ile aynı aile)
MngLogsAgent         motor + localhost evaluate (Outlook burayı çağırır)
Mng.Ui               config: SIEM/agent ailesinde kural+sözlük+yayın
                     DI belge ekranı: yalnızca sınıf seçimi (kataloga bağlanır)
```

| Aday | Neden değil / neden evet |
|------|---------------------------|
| **MngLogCollector** (kural + yayın) | Agent zaten `GET /api/v1/policy/*` + API key + ETag + publish çekiyor. Event log paketleriyle aynı operasyon modeli. |
| **MngDocument** (sınıf + damga) | `dm_tags` ve indirme zaten burada. Agent bugün Document’a konuşmaz; WhatsApp/USB listesi Document’a ait değil. |
| Yeni MngDlp servisi | Deploy, gateway, health maliyeti. Collector şişerse sonra ayrılır. |
| MngKeeper | Yalnızca kimlik. |
| MngAlarm | Tespit/korelasyon; önleme politikası değil. |

**UI:** kural ekranları `/apps/siem-center` yanında veya `/apps/dlp` — güvenlik ailesi. Sınıflandırma adı/rengi DI etiket UI’sında kalır; DLP ekranı sınıfları **seçer**, yeniden tanımlamaz.

**Yayın modeli** (eventlog paketleri ile aynı): taslak Mongo’da → `POST .../publish` → `version` + ETag → agent pull. Taslak sahada geçerli değildir.

**Kimlik derlemesi:** tam kullanıcı kataloğu politika dosyasına gömülmez. Agent evaluate anında cache’e bakar; miss’te collector üzerinden Keeper çözümü (asenkron doldurma). Gönderim anında Outlook **collector’a gitmez** (K9).

---

## 2. Kural çatışması (K8)

**Sıralı liste, ilk eşleşen kazanır.** `priority` küçük sayı = önce. UI sürüklenebilir liste; en altta catch-all audit önerilir.

Aynı `priority` değerinde sıra belirsizdir — yayın öncesi benzersiz priority doğrulanır.

---

## 3. Derlenmiş politika JSON (`schemaVersion: 1`)

Collector `GET /api/v1/policy/dlp` gövdesi. Agent `policy.json` içinde `Policy.Dlp` olarak saklar. Belge içeriği / hash listesi **yoktur**.

```json
{
  "schemaVersion": 1,
  "policyId": "odak-default",
  "version": "3",
  "publishedUtc": "2026-08-31T16:00:00Z",
  "unclassified": {
    "allow": true,
    "effect": "audit"
  },
  "classifications": [
    {
      "id": "cl-dahili",
      "name": "dahili",
      "sensitivity": 1,
      "persistToFile": true
    },
    {
      "id": "cl-gizli",
      "name": "gizli",
      "sensitivity": 3,
      "persistToFile": true
    }
  ],
  "dictionaries": {
    "internalEmailDomains": ["odak.local", "odak.com.tr", "dlp.internal"],
    "sanctionedProcesses": ["OUTLOOK.EXE", "WINWORD.EXE", "EXCEL.EXE", "EXPLORER.EXE"],
    "unsanctionedProcesses": ["WhatsApp.exe", "chrome.exe", "msedge.exe"]
  },
  "rules": [
    {
      "id": "r-gizli-email-external-block",
      "name": "Gizli - dış e-posta",
      "enabled": true,
      "priority": 100,
      "classificationIds": ["cl-gizli"],
      "actions": ["email.send"],
      "destination": { "emailScope": "external" },
      "exceptGroupIds": ["keeper-group-id-finans-yoneticiler"],
      "effect": "block"
    },
    {
      "id": "r-gizli-email-internal-audit",
      "name": "Gizli - iç e-posta",
      "enabled": true,
      "priority": 200,
      "classificationIds": ["cl-gizli"],
      "actions": ["email.send"],
      "destination": { "emailScope": "internal" },
      "exceptGroupIds": [],
      "effect": "audit"
    },
    {
      "id": "r-any-email-audit",
      "name": "Diğer e-posta (catch-all)",
      "enabled": true,
      "priority": 900,
      "classificationIds": ["*"],
      "actions": ["email.send"],
      "destination": { "emailScope": "any" },
      "exceptGroupIds": [],
      "effect": "audit"
    }
  ]
}
```

### 3.1 Alanlar

| Alan | Anlam |
|------|--------|
| `unclassified` | Damgasız ek. Dilim 1: `allow: true`, `effect: audit`. |
| `classifications[].id` | `dm_tags.__dataId` (yayın anında dondurulur). |
| `actions[]` | Kanal. Dilim 1: `email.send`. Rezerve: `email.share`, `usb.copy`, `browser.upload`, `unsanctioned.appRead`. |
| `destination.emailScope` | `internal` \| `external` \| `any`. İç = tüm alıcıların domain’i `internalEmailDomains` içinde (case-insensitive). **Bir dış alıcı varsa** scope `external`. |
| `exceptGroupIds` | Bu Keeper gruplarından **en az birinde** olan kullanıcı kuralı **atlar** (sonraki kurala geçilir). |
| `classificationIds: ["*"]` | Sınıfı olan her dosya; unclassified bu kurala girmez (`unclassified` bloğu ayrıca). |
| `effect` | `audit` \| `warn` \| `block` |

Dilim 1’de `exceptGroupIds` boş + `effect: audit` ile block kuralları **yayınlanabilir ama agent enforce kapalı** tutulabilir (`dlp.enforcementMode: auditOnly`). Böylece kural gerçek hayatta yazılır, kesilmez.

```json
"enforcementMode": "auditOnly"
```

`enforce` olunca warn/block uygulanır. Plan K5 ile uyumlu.

### 3.2 Motor eşleşme sırası

1. Eklerin damgasından sınıfları oku; **max sensitivity** birincil sınıf olsun. Hiç damga yoksa `unclassified` → karar; kurallara girme.
2. `emailScope` hesapla.
3. `windowsUser` → Keeper `groupIds` (cache). Çözülemezse grup yok sayılır (`exceptGroupIds` tutmaz), `identitySource: unresolved`.
4. `enabled` kuralları `priority` artan sırada gez.
5. İlk tam eşleşme (sınıf, action, destination, except değil) kazanır.
6. `auditOnly` ise effect ne olursa olsun gönderime izin var; olay yine gerçek effect’i taşır (`would_block`).

---

## 4. Outlook ↔ agent evaluate (K9, K10)

**Kesici:** Classic Outlook COM/VSTO eklentisi (K11). Office.js / Yeni Outlook sonraki faz.

**Gönderim anında** eklenti yalnızca localhost agent’ı çağırır. Collector / Keeper / UI yok.

```text
POST http://127.0.0.1:{LocalUiPort}/dlp/evaluate
Port varsayılan 5092 (mevcut local UI ile aynı Kestrel, ayrı auth).
```

Local UI PIN’i **kullanılmaz** (Outlook oturumunda insan PIN girmez). Ayrı **makine anahtarı**: `%ProgramData%\MngLogs\Agent\dlp-local.key` (ACL: LocalSystem + Administrators + eklentinin çalıştığı kullanıcı okuyabilir). Header: `X-MngLogs-DlpKey`. Loopback dışında bind yok.

### 4.1 İstek

```json
{
  "action": "email.send",
  "windowsUser": "ODAK\\ali",
  "recipients": ["ali@dlp.internal", "dis@gmail.com"],
  "attachments": [
    { "path": "C:\\Users\\ali\\AppData\\Local\\Temp\\teklif.docx" }
  ],
  "client": { "kind": "outlook-addin", "version": "0.1.0" }
}
```

| Alan | Zorunlu | Not |
|------|---------|-----|
| `action` | evet | Dilim 1: `email.send` |
| `windowsUser` | evet | Eklenti oturum kullanıcısını verir; agent doğrulayabilir (WTS) |
| `recipients` | evet | To+Cc+Bcc SMTP adresleri |
| `attachments[].path` | ek varsa | Outlook temp kopyası olabilir; damga dosyada durduğu sürece yeter |
| `attachments[].classificationId` | hayır | Test/simülasyon: damga okumadan sınıf enjekte (`LAB` Katman A) |

Ek yok + sınıflı gövde: Dilim 1 **kapsam dışı** → unclassified / allow+audit.

### 4.2 Yanıt

```json
{
  "correlationId": "8f3c2e1a-...",
  "policyVersion": "3",
  "enforcementMode": "auditOnly",
  "decision": "allow",
  "effect": "block",
  "allowSend": true,
  "wouldBlock": true,
  "classification": {
    "id": "cl-gizli",
    "name": "gizli",
    "sensitivity": 3,
    "source": "embedded"
  },
  "emailScope": "external",
  "identity": {
    "windowsUser": "ODAK\\ali",
    "keeperUserId": "66ab...",
    "groupIds": ["keeper-group-id-users"],
    "source": "cache"
  },
  "matchedRuleId": "r-gizli-email-external-block",
  "matchedRuleName": "Gizli - dış e-posta",
  "prompt": { "kind": "none" },
  "message": null
}
```

| `decision` | `allowSend` | Eklenti |
|------------|-------------|---------|
| `allow` | true | Gönder; olay audit ise yine yazılır |
| `warn` | false (henüz) | Gerekçe UI; sonra `POST /dlp/justify` |
| `block` | false | İptal; kullanıcıya `message` |

Dilim 1 `auditOnly`: her zaman `decision: allow`, `allowSend: true`; `effect` ve `wouldBlock` gerçek kuralı gösterir.

`classification.source`: `embedded` \| `override` (simülasyon) \| `none`.

`identity.source`: `cache` \| `unresolved`.

Hata (agent kapalı, kötü anahtar, dosya kilitli): eklenti **fail-open + yerel uyarı** (Dilim 1) veya fail-closed (sonra, politika bayrağı). Dilim 1 fail-open: gönderim düşmesin, olay `dlp.evaluate.unavailable`.

### 4.3 Warn (Dilim 2)

```http
POST /dlp/justify
```

```json
{
  "correlationId": "8f3c2e1a-...",
  "justification": "Müşteri teklif talebi, sipariş 12345"
}
```

Yanıt: `{ "allowSend": true }`. Agent olayı `effect: warn` + gerekçe ile SIEM’e yollar.

### 4.4 Simülasyon (Katman A, Outlook yok)

Aynı `POST /dlp/evaluate`; `attachments: [{ "classificationId": "cl-gizli" }]`, `path` yok. Collector veya agent local UI “DLP dene” bunu kullanır. Sunucu simülasyonu (config UI) **aynı JSON’u** collector’da çalıştırır (agent’sız); motor kodu paylaşılmalı (ortak kütüphane veya collector içi kopya — implementasyonda `MngLogs.Agent.Core` / küçük `DlpEngine`).

---

## 5. Olay (SIEM)

`source`: `mnglogs.dlp` (taslak).

Asgari alanlar: `correlationId`, `action`, `decision`, `effect`, `wouldBlock`, `allowSend`, `classificationId`, `emailScope`, `recipientDomains` (tam adres şart değil, KVKK), `matchedRuleId`, `windowsUser`, `keeperUserId`, `identitySource`, `policyVersion`, `enforcementMode`, `client.kind`.

Tam alıcı listesi ve dosya yolu varsayılan **yok** (log sızıntısı). İsteğe bağlı ayrıntı bayrağı sonra.

---

## 6. Dilim 1 kabul kriterleri (sözleşme)

- [x] Collector taslak kural + publish + `GET /policy/dlp` ETag (kod; sahada deploy bekler)
- [x] Agent pull → `dlp-policy.json` (`Policy.Dlp` local override = `enforcementMode`)
- [x] `POST /dlp/evaluate` loopback + `X-MngLogs-DlpKey`
- [x] Outlook yokken classify override ile iç/dış senaryosu (unit test + lab script)
- [x] `auditOnly`: gerçek `effect: block` bile `allowSend: true` + `wouldBlock: true`
- [ ] Agent down: Dilim 1 fail-open (evaluate exception fail-open; agent process down = Outlook henüz yok)
- [ ] Outlook eklentisi

Outlook eklentisi Dilim 1’in **son** adımıdır; motor + lab (smtp4dev) eklentiden önce yeşil olur.

---

## 7. Damga şeması (Dilim 0, kilit)

Sınıf belgede `dm_resources.classificationTagId` olarak durur. Binary indirme / üretimde dosyaya yazılır (`persistToFile`). Agent Dilim 1’de **yalnız dosyayı** okur; Mongo’ya gitmez.

| Biçim | Yer | Anahtarlar |
|-------|-----|------------|
| DOCX / XLSX / PPTX | OPC `/docProps/custom.xml` (Office custom properties) | `MngDlp.ClassificationId`, `MngDlp.ClassificationName`, `MngDlp.Sensitivity`, `MngDlp.SchemaVersion` |
| PDF | `%%EOF` öncesi yorum satırı | `% MngDlp:1\|cl-gizli\|gizli\|3` |

- `SchemaVersion` = `1`.
- Damgasız / okunamayan / `persistToFile: false` → Dilim 1 `unclassified`.
- Collabora custom prop silerse **indirme** Mongo’daki id’den yeniden yazar.
- Markdown / klasör: id var, binary damga yok.
- Geriye dönük: başka namespace yok; v1 tek şema.

