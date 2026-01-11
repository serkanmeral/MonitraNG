# Side Menu Implementation Scripts

Bu klasör, Side Menu implementasyonu için Faz 1 (Dataset ve Temel Altyapı) script'lerini içerir.

## Script'ler

### 1. `create-system-datasets-category.ps1`
System Datasets kategorisini oluşturur.

**Kullanım:**
```powershell
.\create-system-datasets-category.ps1
```

**Çıktı:**
- `system-datasets-category-id.txt`: Oluşturulan kategori ID'si (sonraki script'ler için)

---

### 2. `create-side-menu-dataset.ps1`
@side_menu dataset'ini oluşturur veya günceller.

**Ön Koşul:**
- System Datasets kategorisi oluşturulmuş olmalı
- `system-datasets-category-id.txt` dosyası mevcut olmalı

**Kullanım:**
```powershell
.\create-side-menu-dataset.ps1
```

**Çıktı:**
- Dataset başarıyla oluşturulur veya güncellenir

---

### 3. `export-sidebar-menu.ps1` / `export-menu-items.js`
Hard-coded menu verilerini export eder.

**⚠️ NOT:** TypeScript dosyasını parse etmek karmaşık olduğu için, şu an için placeholder script'ler var.

**Kullanım (PowerShell):**
```powershell
.\export-sidebar-menu.ps1
```

**Kullanım (Node.js):**
```bash
cd Mng.Ui
node ../scripts/tests/MngDataGateway/side-menu/export-menu-items.js
```

**Manuel Export:**
1. `sidebarItem.ts` dosyasını açın
2. Menu item'ları manuel olarak JSON formatına çevirin
3. `menu-items.json` dosyası oluşturun

**JSON Formatı:**
```json
[
  {
    "order": 0,
    "itemType": "header",
    "header": "Home",
    "level": 0,
    "parentId": null
  },
  {
    "order": 1,
    "itemType": "item",
    "title": "Analytical",
    "icon": "ChartPieIcon",
    "iconType": "tabler",
    "to": "/dashboards/analytical",
    "type": "internal",
    "pageType": "user",
    "level": 0,
    "parentId": null,
    "disabled": false
  }
]
```

---

### 4. `load-menu-items.ps1`
Menu verilerini veritabanına yükler (bulk insert).

**Ön Koşul:**
- @side_menu dataset'i oluşturulmuş olmalı
- `menu-items.json` dosyası mevcut olmalı

**Kullanım:**
```powershell
.\load-menu-items.ps1
```

**Çıktı:**
- Menu items başarıyla yüklenir
- `menu-items-dataids.json`: Insert edilen item'ların __dataId'leri (parentId güncellemesi için)

---

## Çalıştırma Sırası

1. **Kategori Oluştur:**
   ```powershell
   .\create-system-datasets-category.ps1
   ```

2. **Dataset Oluştur:**
   ```powershell
   .\create-side-menu-dataset.ps1
   ```

3. **Menu Export (Manuel):**
   - `sidebarItem.ts` dosyasını açın
   - Menu item'ları JSON formatına çevirin
   - `menu-items.json` dosyası oluşturun

4. **Menu Items Yükle:**
   ```powershell
   .\load-menu-items.ps1
   ```

---

## Notlar

- Tüm script'ler MngDataGateway API'sine bağlanır (`https://localhost:5010`)
- Token yönetimi için `../auth/load-token.ps1` script'i kullanılır
- SSL sertifika kontrolü devre dışı (development)

---

## Sonraki Adımlar

Faz 1 tamamlandıktan sonra:
- Faz 2: Frontend Store ve Composable'lar
- Faz 3: Sidebar Entegrasyonu
