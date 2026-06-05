# Linux rsyslog / auth forwarder — müşteri ops şablonu

**Durum:** ✅ Pilot (Debian 13 / imjournal) · 5 Haz 2026 güncellendi  
**Müşteri wiki (Document Intelligence):** [../document_intelligence/tutorials/guvenlik-merkezi-linux-rsyslog-kurulumu.md](../document_intelligence/tutorials/guvenlik-merkezi-linux-rsyslog-kurulumu.md)  
**İlişkili:** [SIEM_PLANNING.md §5.2](./SIEM_PLANNING.md#52-kaynak--toplama-karar-matrisi) · [SIEM_PARSER_PLAN.md §7](./SIEM_PARSER_PLAN.md#7-linux-auth-parser-linuxauthv1)

Linux sunucularda **OpenSSH (sshd)** olayları **systemd journal** (`ssh.service`) üzerinden gelir. **Debian 13+** da klasik `auth,authpriv.*` forward **gürültü üretir**; pilot yapılandırma **imjournal** + dar filtre kullanır.

Windows WEF/NxLog akışının Linux karşılığıdır (Faz 2.5).

---

## 1. Mimari özeti

```mermaid
flowchart LR
    LIN[Linux sunucu] -->|auth.log / journal| RSYS[rsyslog]
    RSYS -->|UDP/TCP syslog| ENG[MngEngine :5514/:514]
    ENG --> REA[MngReactor]
    RELAY[Opsiyonel rsyslog relay] --> ENG
    LIN --> RELAY
```

| Rol | Sorumlu | Not |
|-----|---------|-----|
| sshd / sudo log üretimi | Linux OS | `/var/log/auth.log` veya journal |
| rsyslog forwarder | Müşteri IT | Şablon: [templates/rsyslog-linux-auth-to-engine.conf](./templates/rsyslog-linux-auth-to-engine.conf) (`51-monitrang-siem-journal-sshd.conf`) |
| Engine syslog listener | MonitraNG | ✅ UDP (Odak `:5514`) |
| Parser `linux.auth.v1` | MonitraNG | ✅ sshd / sshd-session |
| U1 korelasyon | MonitraNG | ✅ lab E2E |

**Pilot filtre:** yalnızca `Failed password` ve `Accepted password` (SIEM gürültüsünü keser).

**“Agent” notu:** Ayrı MonitraNG Linux agent binary’si yok. Birincil yol **rsyslog/syslog-ng forward**; alternatif NXLog/Filebeat push (müşteri tercihi).

---

## 2. Ön koşullar

| # | Kontrol |
|---|---------|
| 1 | Linux sunucuda `rsyslog` veya `syslog-ng` çalışıyor |
| 2 | `sshd` başarısız/başarılı oturum logları auth kanalında görünüyor |
| 3 | Engine ↔ Linux arası UDP/TCP **5514** (lab) veya **514** (prod) firewall açık |
| 4 | Engine `MngEngine:SecEventQueue:Enabled=true` |
| 5 | Reactor token — Engine `config.txt` |

**Odak lab:** Engine `http://192.168.20.20:5037`, syslog UDP `:5514`.

---

## 3. rsyslog kurulumu (pilot)

### 3.1 Şablonu kopyala

```bash
# 50: genis auth forward YOK (placeholder)
sudo tee /etc/rsyslog.d/50-monitrang-siem.conf <<'EOF'
# MonitraNG SIEM — genis auth forward yok. SSH: 51-monitrang-siem-journal-sshd.conf
EOF

# 51: imjournal — ENGINE_HOST / port duzenleyin
sudo cp rsyslog-linux-auth-to-engine.conf /etc/rsyslog.d/51-monitrang-siem-journal-sshd.conf
sudo sed -i 's/MONITRA_ENGINE_HOST/192.168.20.20/g' /etc/rsyslog.d/51-monitrang-siem-journal-sshd.conf

sudo sed -i 's/^#\?ForwardToSyslog=.*/ForwardToSyslog=no/' /etc/systemd/journald.conf
sudo systemctl restart systemd-journald
sudo rsyslogd -N1
sudo systemctl restart rsyslog
```

Otomasyon (Odak): `scripts/odak/install-rsyslog-siem-odak.ps1 -Apply`

**İletilen satırlar:** `Failed password`, `Accepted password` only.

### 3.2 Doğrulama (sunucu tarafı)

```bash
logger -p auth.info "MonitraNG SIEM probe"
sudo tail -f /var/log/auth.log
```

---

## 4. Prod hardening checklist

| # | Konu | Öneri |
|---|------|--------|
| 1 | **Transport** | UDP yerine **TCP** `omfwd` (kayıp riski) |
| 2 | **Queue** | `queue.type=LinkedList`, `action.resumeRetryCount=-1` |
| 3 | **Filtre** | Yalnızca auth/authpriv; uygulama loglarını ayır |
| 4 | **Relay** | 50+ sunucu → merkezi rsyslog relay → Engine ([SIEM_THROUGHPUT_AND_QUEUES.md](./SIEM_THROUGHPUT_AND_QUEUES.md)) |
| 5 | **TLS** | İnternet üzerinden iletimde stunnel/TLS syslog-ng |
| 6 | **Hostname** | Syslog satırında hostname doğru (Reactor `source.host`) |
| 7 | **Metadata** | Engine sshd/sudo satırlarında otomatik `endpoint` / `linux-syslog` atar |

---

## 5. Opsiyonel relay (yüksek hacim)

```text
[app01..N] --rsyslog--> [relay01] --TCP--> [MngEngine]
```

Relay sunucusunda aynı şablon; kaynak IP `__FROMHOST__` ile hostname korunur. Rate limit ve yerel disk arşivi relay’de tutulabilir — Engine tam arşiv sunucusu değildir ([SIEM_PLANNING.md §5.1](./SIEM_PLANNING.md)).

---

## 6. Lab smoke (Odak)

Repo kökünden:

```powershell
pwsh scripts/odak/test-linux-rsyslog-auth-e2e.ps1
```

Akış: UDP fixture → Engine flush → Reactor sorgu (`linux.auth.v1`, `sourceHost=app01`).

Tam paket: `run-siem-quick-regression.ps1` (E2E suite içinde).

**Engine deploy:** `SecEventSyslogItemBuilder` güncellemesi sonrası `mngengine` recreate gerekebilir.

---

## 7. Sorun giderme

| Belirti | Olası neden | Çözüm |
|---------|-------------|--------|
| Engine kuyruk dolu | Yoğun ingest | `POST /api/SecEvents/flush`, relay queue |
| `linux.auth.v1` yok | Yanlış kanal / parser | auth satırında `sshd[` veya `sudo:` var mı? |
| `source.host=unknown` | Hostname syslog formatı | BSD `MMM dd HH:MM:SS host` formatı kullanın |
| Firewall parser | Eski Engine | Engine deploy — sshd otomatik `linux-syslog` sınıflandırması |
| U1 alarm yok | Korelasyon eşiği | `test-siem-linux-auth-u1-alarm-e2e.ps1` |

---

## 8. İlgili dokümanlar

- [SIEM_WEF_WEC_FORWARDER.md](./SIEM_WEF_WEC_FORWARDER.md) — Windows toplama
- [templates/nxlog-wec-to-engine.conf](./templates/nxlog-wec-to-engine.conf)
- [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md)
