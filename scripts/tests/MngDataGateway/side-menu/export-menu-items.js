// Export Sidebar Menu Items (Node.js/JavaScript)
// Bu script'i Mng.Ui klasöründe çalıştırın: node scripts/tests/MngDataGateway/side-menu/export-menu-items.js

const fs = require('fs');
const path = require('path');

// sidebarItem.ts dosyası path'i
const projectRoot = path.join(__dirname, '../../../../..');
const sidebarItemPath = path.join(projectRoot, 'Mng.Ui/components/lc/Full/vertical-sidebar/sidebarItem.ts');

if (!fs.existsSync(sidebarItemPath)) {
    console.error(`❌ sidebarItem.ts dosyası bulunamadı: ${sidebarItemPath}`);
    process.exit(1);
}

// TypeScript dosyasını oku
const fileContent = fs.readFileSync(sidebarItemPath, 'utf-8');

// Icon component mapping'i oluştur (import statements'tan)
const iconMap = {};
const iconImportMatch = fileContent.match(/import\s*\{([^}]+)\}\s*from\s*["']vue-tabler-icons["']/);
if (iconImportMatch) {
    const iconNames = iconImportMatch[1].split(',').map(name => name.trim());
    iconNames.forEach(iconName => {
        iconMap[iconName] = iconName;
    });
}

console.log(`✅ Icon mapping oluşturuldu: ${Object.keys(iconMap).length} icon bulundu`);

// sidebarItem array'ini extract et (basit parsing)
// TypeScript object literal'ını parse etmek için daha gelişmiş bir parser gerekiyor
// Şimdilik basit regex ile yapıyoruz

// sidebarItem array'ini bul
const sidebarItemMatch = fileContent.match(/const\s+sidebarItem[^=]*=\s*(\[[\s\S]*\]);/);
if (!sidebarItemMatch) {
    console.error('❌ sidebarItem array bulunamadı!');
    process.exit(1);
}

// TypeScript object literal'ını JavaScript object'e çevirmek için eval kullanabiliriz
// Ama güvenlik riski var, alternatif: TypeScript compiler kullanmak veya manual parse

// Alternatif yaklaşım: TypeScript dosyasını compile edip require etmek
// Ama şimdilik basit bir yaklaşım: Object literal'ı parse et

// Geçici çözüm: sidebarItem.ts dosyasını manuel olarak JSON'a çevirmek gerekiyor
// Veya TypeScript compiler ile JavaScript'e compile edip require etmek

console.log('⚠️  TypeScript dosyasını parse etmek için TypeScript compiler veya manual export gerekiyor.');
console.log('   Şimdilik template JSON oluşturuluyor...');

// Template menu items (ilk birkaç örnek)
const templateMenuItems = [
    {
        order: 0,
        itemType: 'header',
        header: 'Home',
        level: 0,
        parentId: null
    },
    {
        order: 1,
        itemType: 'item',
        title: 'Analytical',
        icon: 'ChartPieIcon',
        iconType: 'tabler',
        to: '/dashboards/analytical',
        type: 'internal',
        pageType: 'user',
        level: 0,
        parentId: null,
        disabled: false
    }
];

// Template JSON dosyası oluştur
const templatePath = path.join(__dirname, 'menu-items-template.json');
fs.writeFileSync(templatePath, JSON.stringify(templateMenuItems, null, 2), 'utf-8');

console.log(`📄 Template JSON dosyası oluşturuldu: ${templatePath}`);
console.log('');
console.log('⚠️  NOT: Gerçek menu verilerini export etmek için:');
console.log('   1. sidebarItem.ts dosyasını manuel olarak JSON\'a çevirin');
console.log('   2. Veya TypeScript compiler ile compile edip require edin');
console.log('   3. menu-items-template.json dosyasını doldurun');
console.log('');
