# Seven Domain - Credentials & Information

**Created:** 6 Kasım 2025  
**Purpose:** Test & Development

---

## 🏢 Domain Information

| Property | Value |
|----------|-------|
| **Domain ID** | `690cda3aae502df7d3330bba` |
| **Domain Name** | `seven` |
| **Display Name** | `Seven Domain` |
| **Database** | `mng_seven` |
| **Realm** | `seven` |
| **Status** | Active ✅ |

---

## 👥 Users

### Admin User (Default)
- **Username:** `seven_admin`
- **Password:** `Admin123!`
- **Email:** `admin@seven.com`
- **Groups:** `admins`
- **Is Admin:** `true`

### Serkan MERAL (Main Test User)
- **Username:** `serkan`
- **Password:** `Serkan123!`
- **Email:** `serkan@seven.com`
- **Full Name:** Serkan MERAL
- **Groups:** `admins`
- **Is Admin:** `true` ✅
- **User ID:** `690cdb7fae502df7d3330bbb`

---

## 🔑 Getting Token

### PowerShell Script

```powershell
# Quick token retrieval
.\get-serkan-token.ps1

# OR Manual
$tokenBody = @{
    username = "serkan"
    password = "Serkan123!"
    domain = "seven"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/token" `
  -Method POST `
  -Body $tokenBody `
  -ContentType "application/json" `
  -SkipCertificateCheck

$token = $response.accessToken
```

### curl

```bash
curl -k -X POST "https://localhost:5001/api/auth/token" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "serkan",
    "password": "Serkan123!",
    "domain": "seven"
  }' | jq -r '.accessToken' > token.txt
```

---

## 📋 Token Claims

```json
{
  "sub": "2308999b-cdeb-4916-849b-a7980a0c96f6",
  "preferred_username": "serkan",
  "email": "serkan@seven.com",
  "email_verified": true,
  "given_name": "Serkan",
  "family_name": "MERAL",
  "name": "Serkan MERAL",
  "domain_name": "seven",
  "isAdmin": true,
  "user_groups": ["admins"]
}
```

**Important Claims:**
- `domain_name`: `seven` → Database: `mng_seven`
- `isAdmin`: `true` → Full access
- `user_groups`: `["admins"]` → Admin permissions

---

## 🧪 Testing with Token

### MngKeeper API

```powershell
# Get token
$token = .\get-serkan-token.ps1

# List users
curl -k -X GET "https://localhost:5001/api/user" `
  -H "Authorization: Bearer $token"

# List groups
curl -k -X GET "https://localhost:5001/api/group" `
  -H "Authorization: Bearer $token"
```

### MngDataGateway API

```powershell
# Get token
$token = Get-Content "$env:TEMP\serkan_token.txt" -Raw

# Test authentication
curl -k -X GET "https://localhost:5010/api/authtest/decode" `
  -H "Authorization: Bearer $token"

# Get domain info
curl -k -X GET "https://localhost:5010/api/authtest/domain" `
  -H "Authorization: Bearer $token"
```

**Expected Database:** `mng_seven`

---

## 📚 Token Refresh

```powershell
$refreshBody = @{
    refreshToken = $response.refreshToken
    domain = "seven"
} | ConvertTo-Json

$newToken = Invoke-RestMethod -Uri "https://localhost:5001/api/auth/refresh" `
  -Method POST `
  -Body $refreshBody `
  -ContentType "application/json" `
  -SkipCertificateCheck
```

---

## 🗄️ MongoDB Collections

**Database:** `mng_seven`

**Collections:**
- `domains` - Domain configuration
- `users` - User data (synced with Keycloak)
- `groups` - Group data (synced with Keycloak)
- `@datasets` - Dataset schemas (MngDataGateway)
- `@dataset_categories` - Dataset categories

---

## 🔐 Keycloak Access

**Realm:** `seven`  
**Admin Console:** http://localhost:8080/admin/master/console/#/seven

**Master Admin:**
- Username: `admin`
- Password: `admin123`

---

## ⚡ Quick Commands

```powershell
# Get fresh token
.\get-serkan-token.ps1

# Read token from file
$token = Get-Content "$env:TEMP\serkan_token.txt" -Raw

# Use global variable (after running script)
$global:serkanToken
```

---

## 📝 Notes

- Token expires in **5 minutes** (300 seconds)
- Refresh token expires in **30 minutes** (1800 seconds)
- Always use HTTPS with `-k` flag for self-signed certificates
- Database name format: `mng_{domain_name}`

---

**Last Updated:** 6 Kasım 2025  
**Status:** ✅ Active & Ready for Testing

