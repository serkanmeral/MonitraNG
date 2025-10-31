// MongoDB Initialization Script for MngKeeper
// This script runs when MongoDB container starts for the first time

// Switch to admin database
db = db.getSiblingDB('admin');

// Create root user if not exists
if (!db.getUser("admin")) {
    db.createUser({
        user: "admin",
        pwd: "admin123",
        roles: [
            { role: "userAdminAnyDatabase", db: "admin" },
            { role: "readWriteAnyDatabase", db: "admin" },
            { role: "dbAdminAnyDatabase", db: "admin" }
        ]
    });
}

// Switch to MngKeeper database
db = db.getSiblingDB('mngkeeper');

// Create collections with indexes
db.createCollection('domains');
db.createCollection('users');
db.createCollection('groups');
db.createCollection('audit_logs');

// Create indexes for better performance
db.domains.createIndex({ "name": 1 }, { unique: true });
db.domains.createIndex({ "status": 1 });
db.domains.createIndex({ "createdAt": 1 });

db.users.createIndex({ "domainId": 1 });
db.users.createIndex({ "username": 1 });
db.users.createIndex({ "email": 1 });
db.users.createIndex({ "createdAt": 1 });

db.groups.createIndex({ "domainId": 1 });
db.groups.createIndex({ "name": 1 });
db.groups.createIndex({ "createdAt": 1 });

db.audit_logs.createIndex({ "timestamp": 1 });
db.audit_logs.createIndex({ "domainId": 1 });
db.audit_logs.createIndex({ "userId": 1 });
db.audit_logs.createIndex({ "action": 1 });

// Create TTL index for audit logs (90 days retention)
db.audit_logs.createIndex({ "timestamp": 1 }, { expireAfterSeconds: 7776000 });

print("MngKeeper MongoDB initialization completed successfully!");
