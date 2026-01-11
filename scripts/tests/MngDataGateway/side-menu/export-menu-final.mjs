// Final Export Script - Better TypeScript Parsing
// Converts sidebarItem.ts to MongoDB format JSON

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

// Read file
let content = fs.readFileSync(sidebarItemPath, 'utf-8');

// Extract icon imports and create replacement map
const iconMap = new Map();
const iconImportMatch = content.match(/import\s*\{([^}]+)\}\s*from\s*["']vue-tabler-icons["']/);
if (iconImportMatch) {
    iconImportMatch[1].split(',').forEach(name => {
        const iconName = name.trim();
        iconMap.set(iconName, iconName);
    });
}
console.log(`✅ Found ${iconMap.size} icon imports`);

// Replace icon component references with strings BEFORE parsing
iconMap.forEach((iconName) => {
    // Match: icon: IconName (not inside quotes, not already a string)
    // Pattern: icon: IconName followed by comma or closing brace
    const regex = new RegExp(`(icon\\s*:\\s*)${iconName}(?=[,\\s}])`, 'g');
    content = content.replace(regex, `$1"${iconName}"`);
});

// Now extract the array content
const arrayMatch = content.match(/const\s+sidebarItem[^=]*=\s*(\[[\s\S]*?\]);/);
if (!arrayMatch) {
    console.error('❌ sidebarItem array bulunamadı!');
    process.exit(1);
}

let arrayContent = arrayMatch[1];

// Remove type annotation if present
arrayContent = arrayContent.replace(/:\s*menu\[\]/g, '');

// Now we'll use a safer eval approach
// First, wrap in a function that returns the array
const evalCode = `(function() { return ${arrayContent}; })()`;

let sidebarItems;
try {
    sidebarItems = eval(evalCode);
    console.log(`✅ Parsed ${sidebarItems.length} menu items from array`);
} catch (error) {
    console.error('❌ Error evaluating array:', error.message);
    console.error('Trying alternative parsing...');
    
    // Alternative: Manual parsing
    sidebarItems = [];
    // This would require complex parsing, so for now we'll exit
    process.exit(1);
}

// Convert to MongoDB format
const mongodbItems = [];
let order = 0;
let currentLevel = 0;
let currentParentId = null;
const parentStack = []; // Stack of parent orders

function processMenuItem(item, level, parentId) {
    const mongodbItem = {
        order: order++,
        itemType: item.header ? 'header' : 'item',
        level: level,
        parentId: parentId,
        pageType: 'user',
    };
    
    if (item.header) {
        mongodbItem.header = item.header;
        // Header resets hierarchy
        return mongodbItem;
    }
    
    if (item.title) {
        mongodbItem.title = item.title;
    }
    
    if (item.icon) {
        // Icon is now a string (we replaced component references)
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

// Process all items with hierarchy tracking
sidebarItems.forEach((item) => {
    if (item.header) {
        // Header: reset hierarchy
        const headerItem = processMenuItem(item, 0, null);
        mongodbItems.push(headerItem);
        currentLevel = 0;
        currentParentId = null;
        parentStack.length = 0;
    } else {
        // Regular item
        const menuItem = processMenuItem(item, currentLevel, currentParentId);
        const itemOrder = menuItem.order;
        mongodbItems.push(menuItem);
        
        // Check if item has children
        if (item.children && Array.isArray(item.children) && item.children.length > 0) {
            // This item is a parent - next items will be children
            currentLevel++;
            parentStack.push(itemOrder);
            currentParentId = itemOrder; // Use order as temporary parent ID
            
            // Process children recursively
            item.children.forEach((child) => {
                const childItem = processMenuItem(child, currentLevel, currentParentId);
                mongodbItems.push(childItem);
                
                // Check if child also has children
                if (child.children && Array.isArray(child.children) && child.children.length > 0) {
                    // Nested children - increase level again
                    const nestedLevel = currentLevel + 1;
                    const nestedParentId = childItem.order;
                    
                    child.children.forEach((nestedChild) => {
                        const nestedItem = processMenuItem(nestedChild, nestedLevel, nestedParentId);
                        mongodbItems.push(nestedItem);
                    });
                }
            });
            
            // After processing children, restore previous level
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

// Second pass: Convert parentId from order-based to proper references
// For now, we'll keep order-based IDs and the load script will handle conversion
// But we need to mark items that should have parentId set

// Actually, let's simplify: use null for all parentIds initially
// The load script will handle hierarchy based on level and order
const finalItems = mongodbItems.map((item) => {
    const final = { ...item };
    
    // For headers, parentId is always null
    if (item.itemType === 'header') {
        final.parentId = null;
        final.level = 0;
    } else {
        // For items, we'll keep the parentId as order reference
        // Load script will need to convert this properly
        // Actually, let's set to null and let load script handle it
        final.parentId = null;
    }
    
    return final;
});

// Actually, let's keep the parentId structure but mark it clearly
// We'll use a two-pass approach in the load script

// Save to JSON
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

// Show hierarchy levels
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
console.log('⚠️  Note: parentId is currently null for all items.');
console.log('   The load script will handle hierarchy based on level and order.');
console.log('');
