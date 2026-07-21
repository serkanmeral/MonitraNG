# GitLab Runner — kaynak tüketimi (monitrang.com)

**Tarih:** 20 Temmuz 2026  
**Kapsam:** Yalnızca **GitLab Runner** (CI job executor). GitLab uygulamasının kendisi ayrı tutulur.  
**Bağlam:** PC-driven deploy’a geçince Runner’ın idle/aktif maliyeti ve kaldırma seçenekleri.

---

## Ölçüm özeti (canlı sunucu)

| Bileşen | Idle CPU | Idle RAM | Not |
|---------|----------|----------|-----|
| **gitlab-runner** container | ~%0 | **~54 MiB** (~%0.3) | 8 haftadır Up; süreç 59 gündür ayakta |
| Job container’ları | — | 0 (çalışan yok) | Son job container’lar **~8 hafta** önce Exited |
| **gitlab** (uygulama, karşılaştırma) | ~%7–11 | **~4.9 GiB** (~%26) | Runner değil; UI/API/CI metadata |

**Sonuç (idle):** Runner’ın sürekli maliyeti çok düşük (~50–60 MiB). Asıl ağırlık **GitLab uygulaması** ve (job çalışınca) **geçici build container’ları + Docker image/cache**.

---

## Yapılandırma (özet, secret yok)

- `concurrent = 1` (aynı anda tek job)
- Executor: **docker** (`privileged = true`, `network_mode = host`)
- Varsayılan image: `mcr.microsoft.com/dotnet/sdk:9.0`
- Docker socket bind: host Docker’ı kullanır → job sırasında host CPU/RAM/disk’i paylaşır

Job çalışırken tüketim idle ile kıyaslanamaz: .NET/Node build’ler kısa süreliğine **birkaç GB RAM + yüksek CPU** açabilir (`concurrent=1` üst sınırı bir job).

---

## Disk / cache (Runner ile ilişkili)

| Kalem | Yaklaşık boyut | Not |
|-------|----------------|-----|
| Runner cache volume’ları (4 adet) | **~1.4 GiB** | `runner-*-cache-*` |
| `gitlab-runner` image | ~452 MiB | |
| Helper image | ~142 MiB | |
| Tipik job base image’lar (ör. sdk:9.0, node:20) | ~1.2–1.6 GiB / image | Job’larda kullanılır; başka build’lerle paylaşılabilir |
| Docker **Build Cache** (tüm host) | **~33 GiB** (reclaimable ~18 GiB) | Sadece runner değil; compose/build birikimi |
| Eski exited job container’lar | birkaç adet, 6 ay–8 hafta | Temizlenebilir |

`/var/lib/docker` toplam ~27G (docker data kökü; tüm stack).

---

## Kullanım sinyali

- Şu an **running / pending job yok** (idle).
- Son runner job container’ları **~8 haftadır** bitmiş durumda → production deploy için Runner fiilen kullanılmıyor.
- Pipeline metadata GitLab DB’de kalır; Runner kapansa bile GitLab UI çalışır.

---

## Ne kaldırılabilir / ne kalmalı?

| Seçenek | Ne olur | Kaynak etkisi |
|---------|---------|---------------|
| **A. Hiçbir şey** | Runner idle kalsın | ~54 MiB; ihmal edilebilir |
| **B. Runner’ı durdur** (`docker stop gitlab-runner`) | CI job çalışmaz; GitLab UI/repo kalır | ~54 MiB + job spike riski gider; cache disk kalır |
| **C. Runner’ı compose’dan çıkar / kaldır** | Kalıcı; tekrar register gerekir | B + image/volume temizliği ile disk de açılır |
| **D. Sadece cache/image temizliği** | Runner açık kalır | ~1.4 GiB+ reclaimable build cache |
| **E. `.gitlab-ci.yml` sadeleştir** | Job tanımları azalır; Runner yoksa etkisi yok | Pipeline gürültüsü azalır |

**Öneri (PC-driven deploy ile uyumlu):**

1. Kısa vadede **B**: Runner’ı stop et (veya `mng_common` içinde profil dışı bırak) — GitLab kalsın.  
2. İleride CI (build/test) tekrar istenirse Runner’ı start/register.  
3. Disk için ara sıra `docker builder prune` / eski runner cache volume temizliği (dikkatli).  
4. **GitLab container’ını kapatmayın** — asıl RAM orada; bu dokümanın konusu değil.

---

## Deploy stratejisi ile ilişki

PC-driven sync/deploy ([DEPLOY_STRATEGY.md](./DEPLOY_STRATEGY.md)) birincil yol olduktan sonra Runner **zorunlu değil**.  
Idle maliyeti düşük olduğu için “hemen sil” acil değil; **işe yaramayan CI job’ları ve deploy-services hattı** asıl sürtünme kaynağıydı — kaynak açısından kritik olan idle Runner değil, **geçmişte job anındaki spike + bakım maliyeti**.

Karar kaydı (doldurulacak):

- [ ] Runner idle bırakılacak  
- [ ] Runner stop edilecek  
- [ ] Runner kaldırılacak + cache temizlenecek  
- [ ] Karar ertelendi  

---

## Güvenlik notu

`config.toml` içinde runner token vardır. Bu dosyayı chat/log’a yapıştırmayın; rotate gerekirse GitLab UI → Settings → CI/CD → Runners.
