// Export Sidebar Menu Items from TypeScript to JSON
// This script uses a simpler approach: reads the TypeScript file and converts icon components to strings

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Paths
const projectRoot = path.resolve(__dirname, '../../../../');
const sidebarItemPath = path.join(projectRoot, 'Mng.Ui/components/lc/Full/vertical-sidebar/sidebarItem.ts');
const outputPath = path.join(__dirname, 'menu-items-export.json');

console.log(`📂 Project Root: ${projectRoot}`);
console.log(`📄 Sidebar Item Path: ${sidebarItemPath}`);
console.log('');

// Check if sidebarItem.ts exists
if (!fs.existsSync(sidebarItemPath)) {
    console.error(`❌ sidebarItem.ts dosyası bulunamadı: ${sidebarItemPath}`);
    process.exit(1);
}

console.log('✅ sidebarItem.ts dosyası bulundu');
console.log('');

// Read TypeScript file
const fileContent = fs.readFileSync(sidebarItemPath, 'utf-8');

// Extract icon imports and create mapping
const iconMap = new Map();
const iconImportMatch = fileContent.match(/import\s*\{([^}]+)\}\s*from\s*["']vue-tabler-icons["']/);
if (iconImportMatch) {
    const iconNames = iconImportMatch[1].split(',').map(name => name.trim());
    iconNames.forEach(iconName => {
        iconMap.set(iconName, iconName);
    });
    console.log(`✅ Icon mapping oluşturuldu: ${iconMap.size} icon bulundu`);
}

// Simple parsing: Convert TypeScript array to JavaScript array
// We'll use eval in a safe way by preprocessing the file
// Replace icon components with strings
let processedContent = fileContent;

// Replace icon component references with strings
iconMap.forEach((iconName) => {
    // Match: icon: IconName (but not icon: "IconName" or icon: 'IconName')
    const regex = new RegExp(`(icon\\s*:\\s*)${iconName}(?=[,\\s}])`, 'g');
    processedContent = processedContent.replace(regex, `$1"${iconName}"`);
});

// Extract just the array content
const arrayStartMatch = processedContent.match(/const\s+sidebarItem[^=]*=\s*(\[)/);
if (!arrayStartMatch) {
    console.error('❌ sidebarItem array başlangıcı bulunamadı!');
    process.exit(1);
}

const arrayStartIndex = arrayStartMatch.index + arrayStartMatch[0].length - 1;

// Find matching closing bracket
let bracketCount = 0;
let inString = false;
let stringChar = null;
let escapeNext = false;
let arrayEndIndex = -1;

for (let i = arrayStartIndex; i < processedContent.length; i++) {
    const char = processedContent[i];
    
    if (escapeNext) {
        escapeNext = false;
        continue;
    }
    
    if (char === '\\') {
        escapeNext = true;
        continue;
    }
    
    if (!inString && (char === '"' || char === "'" || char === '`')) {
        inString = true;
        stringChar = char;
        continue;
    }
    
    if (inString && char === stringChar) {
        inString = false;
        stringChar = null;
        continue;
    }
    
    if (inString) {
        continue;
    }
    
    if (char === '[') {
        bracketCount++;
    } else if (char === ']') {
        bracketCount--;
        if (bracketCount === 0) {
            arrayEndIndex = i + 1;
            break;
        }
    }
}

if (arrayEndIndex === -1) {
    console.error('❌ sidebarItem array sonu bulunamadı!');
    process.exit(1);
}

const arrayContent = processedContent.substring(arrayStartIndex, arrayEndIndex);

// Evaluate the array (safe because we control the content)
// Remove export and const declarations, keep only the array
let evalCode = arrayContent;

// Fix TypeScript-specific syntax for JavaScript
evalCode = evalCode.replace(/:\s*menu\[\]/g, ''); // Remove type annotation
evalCode = evalCode.replace(/header\s*:\s*"([^"]+)"/g, 'header: "$1"'); // Ensure strings
evalCode = evalCode.replace(/title\s*:\s*"([^"]+)"/g, 'title: "$1"'); // Ensure strings
evalCode = evalCode.replace(/to\s*:\s*"([^"]+)"/g, 'to: "$1"'); // Ensure strings

try {
    // Use Function constructor for safer eval
    const sidebarItems = new Function(`return ${evalCode}`)();
    
    console.log(`✅ Parsed ${sidebarItems.length} menu items`);
    console.log('');
    
    // Convert to MongoDB format
    const mongodbItems = [];
    let order = 0;
    let currentLevel = 0;
    const parentStack = [];
    let currentParentId = null;
    
    function processMenuItem(item, level, parentId) {
        const dataId = crypto.randomUUID();
        
        const mongodbItem = {
            order: order++,
            itemType: item.header ? 'header' : 'item',
            level: level,
            parentId: parentId || null,
        };
        
        if (item.header) {
            mongodbItem.header = item.header;
            // Headers reset the level
            currentLevel = 0;
            currentParentId = null;
            parentStack.length = 0;
        }
        
        if (item.title) {
            mongodbItem.title = item.title;
        }
        
        if (item.icon) {
            // Icon is now a string
            mongodbItem.icon = typeof item.icon === 'string' ? item.icon : item.icon.name || 'ChartPieIcon';
            mongodbItem.iconType = 'tabler';
        }
        
        if (item.to) {
            mongodbItem.to = item.to;
            mongodbItem.type = item.type || 'internal';
        }
        
        if (item.chip) {
            mongodbItem.chip = item.chip;
        }
        
        if (item.chipColor) {
            mongodbItem.chipColor = item.chipColor;
        }
        
        if (item.chipBgColor) {
            mongodbItem.chipBgColor = item.chipBgColor;
        }
        
        if (item.chipVariant) {
            mongodbItem.chipVariant = item.chipVariant;
        }
        
        if (item.chipIcon) {
            mongodbItem.chipIcon = item.chipIcon;
        }
        
        if (item.disabled !== undefined) {
            mongodbItem.disabled = item.disabled;
        }
        
        if (item.subCaption) {
            mongodbItem.subCaption = item.subCaption;
        }
        
        // Default pageType is 'user'
        mongodbItem.pageType = 'user';
        
        mongodbItems.push({ ...mongodbItem, __tempId: dataId });
        
        // Process children
        if (item.children && Array.isArray(item.children)) {
            parentStack.push(dataId);
            item.children.forEach((child) => {
                processMenuItem(child, level + 1, dataId);
            });
            parentStack.pop();
        }
        
        return dataId;
    }
    
    // Process all items
    sidebarItems.forEach((item) => {
        if (item.header) {
            // Header resets hierarchy
            currentLevel = 0;
            currentParentId = null;
            parentStack.length = 0;
        }
        processMenuItem(item, currentLevel, currentParentId);
    });
    
    // Second pass: replace __tempId with actual parentId references
    const idMap = new Map();
    mongodbItems.forEach((item, index) => {
        if (item.__tempId) {
            idMap.set(item.__tempId, index);
        }
    });
    
    const finalItems = mongodbItems.map((item, index) => {
        const finalItem = { ...item };
        delete finalItem.__tempId;
        
        if (item.parentId && typeof item.parentId === 'string') {
            // parentId is a __tempId, find the actual item index
            const parentIndex = idMap.get(item.parentId);
            if (parentIndex !== undefined) {
                // We need to use actual dataIds, but for now we'll use index-based references
                // This will be fixed when we load to MongoDB
                finalItem.parentId = mongodbItems[parentIndex].__tempId;
            }
        }
        
        return finalItem;
    });
    
    // Actually, let's simplify: use null for parentId and fix hierarchy in load script
    // For now, just use null and let the load script handle it
    
    const simplifiedItems = mongodbItems.map((item) => {
        const { __tempId, ...rest } = item;
        // parentId will be set correctly in the load script based on hierarchy
        if (rest.itemType === 'header') {
            rest.parentId = null;
        }
        return rest;
    });
    
    // Save to JSON
    fs.writeFileSync(outputPath, JSON.stringify(simplifiedItems, null, 2), 'utf-8');
    
    console.log(`✅ Menu items exported to: ${outputPath}`);
    console.log(`📊 Total items: ${simplifiedItems.length}`);
    console.log('');
    
    // Show summary
    const headerCount = simplifiedItems.filter(item => item.itemType === 'header').length;
    const itemCount = simplifiedItems.filter(item => item.itemType === 'item').length;
    
    console.log('📈 Summary:');
    console.log(`   Headers: ${headerCount}`);
    console.log(`   Items: ${itemCount}`);
    console.log('');
    console.log('✅ Export completed successfully!');
    console.log('');
    console.log('📝 Next steps:');
    console.log(`   1. Review the exported JSON file: ${outputPath}`);
    console.log('   2. Run load-menu-items.ps1 to import to MongoDB');
    console.log('');
    
} catch (error) {
    console.error('❌ Error parsing menu items:', error.message);
    console.error(error.stack);
    process.exit(1);
}
