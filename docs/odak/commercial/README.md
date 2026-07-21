# Odak Kompozit — Ticari teklif dosyaları

| Dosya | Kime | Açıklama |
|:---|:---|:---|
| [Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md](./Odak_Kompozit_Fiyat_Teklifi_MUSTERI.md) | **Müşteri** | Kapaklı teklif (PDF kaynağı) — fiyat/ödeme boş |
| [Odak_Kompozit_Teklif_IC_CALISMA_NOTLARI.md](./Odak_Kompozit_Teklif_IC_CALISMA_NOTLARI.md) | **İç** | Konuşma kararları |
| [Odak_Kompozit_Fiyat_Teklifi.md](./Odak_Kompozit_Fiyat_Teklifi.md) | **İç** | Detaylı çalışma taslağı |
| [export-teklif-pdf.ps1](./export-teklif-pdf.ps1) | Araç | PDF export |

## Paketler (müşteri MD)

1. Döküman Zekası  
2. Raporlama  
3. İzleme  
4. Üretim Operasyonu *(ayrı)*  
5. Anket (host: kurum **veya** monitrang.com)  
6. İş Paketleri — sürekli güncelleme  

Fiyat/ödeme: toplantıda doldurulacak.

```powershell
pwsh -File .\docs\odak\commercial\export-teklif-pdf.ps1 -OpenAfter
```
