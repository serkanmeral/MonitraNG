# Next Session - Data CRUD Controller

**Date:** 6 Kasım 2025 (Session End Notes)  
**Ready for:** Data CRUD Implementation  
**Status:** All prerequisites completed ✅

---

## 🎯 Session Goal: Data CRUD Controller

### Endpoint Structure:
```
POST   /api/datasets/{datasetName}/data          - Create data
GET    /api/datasets/{datasetName}/data          - List data (pagination)
GET    /api/datasets/{datasetName}/data/{dataId} - Get single data
PUT    /api/datasets/{datasetName}/data/{dataId} - Update data
DELETE /api/datasets/{datasetName}/data/{dataId} - Delete data
POST   /api/datasets/{datasetName}/data/{dataId}/restore - Restore data
```

---

## 📋 Prerequisites (ALL COMPLETED ✅)

1. ✅ MongoContextService - JWT → Database selection
2. ✅ UserInfoService - JWT → UserInfo extraction
3. ✅ Base Entity Pattern - Full metadata
4. ✅ Dataset Categories - @dataset_categories CRUD
5. ✅ Dataset Schema - @datasets CRUD with field definitions

---

## 🔑 Key Topics to Discuss

### 1. **Incremental Field Service** 🔢
Counter management in `@__counters` collection

**Topics:**
- Counter creation on first use
- Atomic increment (findOneAndUpdate)
- Format placeholder resolution
- Field reference handling ({projectCode})
- Prefix-based scope calculation

**Example:**
```
Field: {projectCode}-{year}{month}-{0:D4}
Data: { "projectCode": "GOREV" }
→ Counter Key: @tasks.taskNumber.GOREV-202511
→ Result: "GOREV-202511-0001"
```

### 2. **Schema Validation** ✅
forceSchema enforcement

**Topics:**
- Strict mode (only defined fields)
- Flexible mode (allow extra fields)
- Mandatory field validation
- Field type validation
- Unique constraint handling

### 3. **Index Creation** 📊
Lazy index creation on first insert

**Topics:**
- Check if collection exists
- Create collection if needed
- Create indexes from schema definition
- Once-only execution (don't recreate on every insert)

### 4. **Relation Lookup** 🔗
expand parameter strategy

**Topics:**
- Manual expansion: `?expand=project,assignedTo`
- MongoDB $lookup aggregation
- Nested expansion depth limit
- Missing relation handling
- Array relation expansion

**Decisions Needed:**
- Query parameter vs always expand?
- Max depth? (2-3 levels)
- persons/personGroups expansion (MngKeeper API call)?

### 5. **History Tracking** 📜
logging mode implementation

**Modes:**
- `self` - Each record has __history (like schema/category)
- `none` - No history
- `common` - Separate @data_logs collection

**Questions:**
- Apply same history pattern as categories/schemas?
- MaxHistoryEntries limit?
- Only changed fields?

### 6. **Default Values** 🎨
Static default population

**Topics:**
- Apply defaults on create
- Static only (Phase 1)
- Field type compatibility check

### 7. **Data Metadata** 📦
What metadata for data records?

**Options:**

**A) Full Pattern (like categories/schemas):**
```json
{
  "__dataId": "guid",
  "title": "My Task",
  "__createInfo": { ... },
  "__lastUpdateInfo": { ... },
  "__history": [ ... ]
}
```

**B) Simplified (based on logging mode):**
```json
// logging: "self"
{
  "__dataId": "guid",
  "title": "My Task",
  "__history": [ ... ]  // ← Only history
}

// logging: "none"
{
  "__dataId": "guid",
  "title": "My Task"
}
```

**Which one?**

---

## 🧪 Test Dataset Ready

**Existing Schema:** `@test_tasks_224334`

**Fields:**
- title (text, mandatory)
- description (text)
- priority (number, mandatory)
- isCompleted (bool, mandatory)
- dueDate (datetime)
- taskNumber (incremental: TASK-{0:D6})

**Ready for data insert testing!**

---

## 💡 Recommended Approach

### Phase 1: Basic CRUD
1. Create data (without incremental)
2. List data (pagination, no expansion)
3. Get by ID
4. Update data
5. Delete data

### Phase 2: Advanced Features
1. Incremental field service
2. Index creation (first insert)
3. Schema validation
4. Relation expansion
5. Default value application

### Phase 3: Complex Features
1. persons/personGroups (MngKeeper API)
2. Validation execution (HTTP calls)
3. Query execution
4. Event publishing (RabbitMQ)

---

## 📝 Questions for Next Session

**Before coding, discuss:**

1. **Data Metadata:** Full pattern vs simplified vs logging-based?
2. **Incremental:** Implement now or Phase 2?
3. **Index Creation:** On first insert or manual endpoint?
4. **Relation Expansion:** Query parameter strategy?
5. **persons/personGroups:** MngKeeper integration now or later?
6. **Validation Execution:** Postpone or implement?

---

## 🚀 Quick Start Command

```powershell
# New session opening:
"Yeni session'da 'MngDataGateway STATUS.md'yi oku ve Data CRUD Controller başla' diyebilirsiniz."

# Current status:
- MongoContextService: ✅ Ready
- Dataset Categories: ✅ Ready
- Dataset Schema: ✅ Ready
- Test Dataset: ✅ Available (@test_tasks_224334)
- Test User: ✅ serkan@seven.com (admin)
```

---

## 🎯 Success Criteria (Next Session)

- [ ] Create data record to @tasks collection
- [ ] Incremental field auto-generated
- [ ] Schema validation enforced
- [ ] Pagination working
- [ ] Update with history tracking
- [ ] Delete with backup
- [ ] Restore working

**Collection Created:** `@tasks` (from schema definition)  
**Index Created:** Based on indexList  
**Counter Created:** `@__counters` entry

---

**Current Commit:** 792b2c6  
**Status:** Ready for Data CRUD 🚀

