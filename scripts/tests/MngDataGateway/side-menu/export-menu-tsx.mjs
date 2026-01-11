// Export using tsx to import TypeScript directly
import { spawn } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const projectRoot = path.resolve(__dirname, '../../../../');
const sidebarItemPath = path.join(projectRoot, 'Mng.Ui/components/lc/Full/vertical-sidebar/sidebarItem.ts');
const outputPath = path.join(__dirname, 'menu-items-export.json');

console.log(`📂 Reading: ${sidebarItemPath}`);
console.log('');

// Alternative: Use a simpler manual conversion approach
// Read the TypeScript file and do a more careful replacement

let content = fs.readFileSync(sidebarItemPath, 'utf-8');

// Step 1: Replace icon component references with strings more carefully
const iconNames = [];
const iconImportMatch = content.match(/import\s*\{([^}]+)\}\s*from\s*["']vue-tabler-icons["']/);
if (iconImportMatch) {
    iconImportMatch[1].split(',').forEach(name => {
        iconNames.push(name.trim());
    });
}
console.log(`✅ Found ${iconNames.length} icon imports`);

// Replace icon: IconName with icon: "IconName" (more carefully)
iconNames.forEach(iconName => {
    // Match: icon: IconName (not in quotes, followed by comma, closing brace, or newline)
    const regex = new RegExp(`(icon\\s*:\\s*)${iconName}(?=\\s*[,}])`, 'g');
    content = content.replace(regex, `$1"${iconName}"`);
});

// Step 2: Extract array content more carefully
// Find: const sidebarItem: menu[] = [
const arrayStartPattern = /const\s+sidebarItem[^=]*=\s*\[/;
const match = content.match(arrayStartPattern);
if (!match) {
    console.error('❌ sidebarItem array başlangıcı bulunamadı!');
    process.exit(1);
}

const startIndex = match.index + match[0].length;

// Find matching closing bracket
let bracketCount = 1;
let inString = false;
let stringChar = null;
let escapeNext = false;
let endIndex = -1;

for (let i = startIndex; i < content.length; i++) {
    const char = content[i];
    
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
            endIndex = i;
            break;
        }
    }
}

if (endIndex === -1) {
    console.error('❌ sidebarItem array sonu bulunamadı!');
    process.exit(1);
}

let arrayContent = content.substring(startIndex - 1, endIndex + 1); // Include brackets

// Remove type annotation
arrayContent = arrayContent.replace(/:\s*menu\[\]/g, '');

// Now try to evaluate - but first, fix any remaining TypeScript-specific syntax
// Remove interface/type references, fix any remaining issues

// Try to create a valid JavaScript array
// Wrap in function to avoid global scope issues
const evalWrapper = `
(function() {
    const menu = [];
    return ${arrayContent};
})();
`;

let sidebarItems;
try {
    sidebarItems = eval(evalWrapper);
    console.log(`✅ Successfully parsed ${sidebarItems.length} menu items`);
} catch (error) {
    console.error('❌ Error evaluating:', error.message);
    console.error('Trying manual parsing approach...');
    
    // Fallback: Manual parsing with regex
    sidebarItems = [];
    const itemPattern = /\{\s*(?:header|title)\s*:[^}]+\}/g;
    // This is too simplistic, won't work for nested objects
    // We need a better approach
    
    console.error('Manual parsing not implemented yet');
    process.exit(1);
}

// Convert to MongoDB format (same as before)
const mongodbItems = [];
let order = 0;
let currentLevel = 0;
let currentParentId = null;
const parentStack = [];

// Helper function to generate pageCode from title or to
let pageCodeCounter = 0;
const usedPageCodes = new Set();

function generatePageCode(item, order) {
    let pageCode = null;
    
    if (item.to && item.to.startsWith('/')) {
        // Route'tan pageCode oluştur: /dashboards/analytical -> dashboards-analytical
        pageCode = item.to.substring(1).replace(/\//g, '-').replace(/[^a-zA-Z0-9-_]/g, '').toLowerCase();
    } else if (item.title) {
        // Title'dan pageCode oluştur: "Analytical Dashboard" -> analytical-dashboard
        pageCode = item.title.toLowerCase()
            .replace(/[^a-zA-Z0-9\s-]/g, '')
            .replace(/\s+/g, '-')
            .replace(/-+/g, '-')
            .replace(/^-|-$/g, '');
    } else if (item.header) {
        // Header'dan pageCode oluştur
        pageCode = item.header.toLowerCase()
            .replace(/[^a-zA-Z0-9\s-]/g, '')
            .replace(/\s+/g, '-')
            .replace(/-+/g, '-')
            .replace(/^-|-$/g, '');
    }
    
    // Eğer pageCode oluşturulamadıysa veya boşsa, order'a göre oluştur
    if (!pageCode || pageCode.length === 0) {
        pageCode = `item-${order}`;
    }
    
    // Unique olmasını garantile (duplicate varsa suffix ekle)
    let uniquePageCode = pageCode;
    let suffix = 1;
    while (usedPageCodes.has(uniquePageCode)) {
        uniquePageCode = `${pageCode}-${suffix}`;
        suffix++;
    }
    
    usedPageCodes.add(uniquePageCode);
    return uniquePageCode;
}

function processMenuItem(item, level, parentId, itemOrder) {
    const mongodbItem = {
        order: itemOrder,
        itemType: item.header ? 'header' : 'item',
        level: level,
        parentId: parentId,
        pageType: 'admin', // Default: admin (kullanıcı istediği gibi)
    };
    
    // pageCode ekle (unique garantisi ile)
    const pageCode = generatePageCode(item, itemOrder);
    mongodbItem.pageCode = pageCode;
    
    if (item.header) {
        mongodbItem.header = item.header;
        return mongodbItem;
    }
    
    if (item.title) {
        mongodbItem.title = item.title;
    }
    
    if (item.icon) {
        mongodbItem.icon = typeof item.icon === 'string' ? item.icon : 'ChartPieIcon';
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
    
    return mongodbItem;
}

// Process with hierarchy
sidebarItems.forEach((item) => {
    const currentOrder = order++;
    
    if (item.header) {
        const headerItem = processMenuItem(item, 0, null, currentOrder);
        mongodbItems.push(headerItem);
        currentLevel = 0;
        currentParentId = null;
        parentStack.length = 0;
    } else {
        const menuItem = processMenuItem(item, currentLevel, currentParentId, currentOrder);
        mongodbItems.push(menuItem);
        const itemTempId = mongodbItems.length - 1; // Temporary index for parent reference
        
        if (item.children && Array.isArray(item.children) && item.children.length > 0) {
            currentLevel++;
            parentStack.push(itemTempId);
            currentParentId = itemTempId; // Use temp index as temporary parent ID
            
            item.children.forEach((child) => {
                const childOrder = order++;
                const childItem = processMenuItem(child, currentLevel, currentParentId, childOrder);
                mongodbItems.push(childItem);
                const childTempId = mongodbItems.length - 1;
                
                if (child.children && Array.isArray(child.children) && child.children.length > 0) {
                    const nestedLevel = currentLevel + 1;
                    const nestedParentTempId = childTempId;
                    
                    child.children.forEach((nestedChild) => {
                        const nestedOrder = order++;
                        const nestedItem = processMenuItem(nestedChild, nestedLevel, nestedParentTempId, nestedOrder);
                        mongodbItems.push(nestedItem);
                    });
                }
            });
            
            parentStack.pop();
            if (parentStack.length > 0) {
                currentParentId = parentStack[parentStack.length - 1];
                currentLevel = parentStack.length;
            } else {
                currentParentId = null;
                currentLevel = 0;
            }
        }
    }
});

// Second pass: Convert parentId from temp index to actual order (for hierarchy)
// Build order -> temp index map, then update parentId references
mongodbItems.forEach((item, index) => {
    if (item.parentId !== null && typeof item.parentId === 'number') {
        // parentId is a temp index, convert to actual parent's order
        const parentItem = mongodbItems[item.parentId];
        if (parentItem) {
            item.parentId = parentItem.order; // Use order as parent reference (will be converted to __dataId after insert)
        } else {
            item.parentId = null;
        }
    }
});

// Simplify parentId - set to null, load script will handle hierarchy
const finalItems = mongodbItems.map(item => ({
    ...item,
    parentId: item.itemType === 'header' ? null : item.parentId
}));

fs.writeFileSync(outputPath, JSON.stringify(finalItems, null, 2), 'utf-8');

console.log(`✅ Exported to: ${outputPath}`);
console.log(`📊 Total items: ${finalItems.length}`);
console.log('');

const headerCount = finalItems.filter(item => item.itemType === 'header').length;
const itemCount = finalItems.filter(item => item.itemType === 'item').length;

console.log('📈 Summary:');
console.log(`   Headers: ${headerCount}`);
console.log(`   Items: ${itemCount}`);
console.log('');

const levels = {};
finalItems.forEach(item => {
    levels[item.level] = (levels[item.level] || 0) + 1;
});
console.log('📊 Level distribution:');
Object.keys(levels).sort((a, b) => parseInt(a) - parseInt(b)).forEach(level => {
    console.log(`   Level ${level}: ${levels[level]} items`);
});

console.log('');
console.log('✅ Export completed!');
console.log('');
