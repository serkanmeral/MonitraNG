# MonitraNG Icon Set

IoT Monitoring platformu için özel olarak tasarlanmış icon seti.

## Icon Dosyaları

### SVG Formatı (Önerilen)
- **`favicon.svg`** - Favicon için optimize edilmiş, basit versiyon
- **`icon.svg`** - Detaylı versiyon, genel kullanım için
- **`icon-simple.svg`** - Minimalist alternatif versiyon

## Tasarım Açıklaması

Icon, IoT Monitoring konseptini temsil eder:
- **Merkezi Hub**: Mavi renkli dashboard/monitoring merkezi
- **IoT Sensörler**: Yeşil renkli, hub'a bağlı 4 sensör (üst, sağ, alt, sol)
- **Bağlantı Hatları**: Sensörlerden hub'a veri akışını gösteren mavi çizgiler
- **Monitoring Ekranı**: Hub içindeki grid çizgileri, veri görselleştirmesini temsil eder

## Renkler

- **Mavi (#3b82f6)**: Monitoring hub, bağlantı hatları
- **Yeşil (#10b981)**: IoT sensörler
- **Beyaz**: Dashboard grid çizgileri, kontrast için

## Kullanım

### Favicon Olarak
Nuxt.config.ts'de zaten yapılandırılmış. Tarayıcı otomatik olarak `/favicon.svg` dosyasını kullanacak.

### PNG/ICO Formatına Dönüştürme

SVG dosyalarını PNG veya ICO formatına dönüştürmek için:

1. **Online Araçlar:**
   - [favicon.io](https://favicon.io/favicon-converter/) - SVG'den ICO/PNG dönüştürme
   - [realfavicongenerator.net](https://realfavicongenerator.net/) - Tüm platformlar için favicon seti
   - [convertio.co](https://convertio.co/svg-ico/) - SVG to ICO converter

2. **Komut Satırı (ImageMagick):**
   ```bash
   # PNG oluştur (32x32)
   magick icon.svg -resize 32x32 icon.png
   
   # ICO oluştur (çoklu boyut)
   magick icon.svg -resize 16x16 -resize 32x32 -resize 48x48 icon.ico
   ```

3. **Node.js (sharp):**
   ```bash
   npm install sharp
   ```
   ```javascript
   const sharp = require('sharp');
   await sharp('icon.svg').resize(32, 32).png().toFile('icon.png');
   ```

## Önerilen Boyutlar

- **Favicon**: 32x32px (veya 16x16px)
- **Apple Touch Icon**: 180x180px
- **Android Icon**: 192x192px, 512x512px
- **Windows Tile**: 144x144px

## Güncelleme

Icon'ları güncellemek için:
1. SVG dosyalarını düzenleyin
2. Gerekirse PNG/ICO formatlarına dönüştürün
3. `public/` klasörüne ekleyin
4. Nuxt uygulamasını yeniden build edin
