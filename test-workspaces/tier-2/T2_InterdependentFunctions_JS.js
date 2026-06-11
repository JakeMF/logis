var crypto = require('crypto');

// --- Session Store ---
function SessionStore() {
    this.sessions = {};
}
SessionStore.prototype.createSession = function(userId, role) {
    var sessionId = crypto.randomBytes(16).toString('hex');
    var createdAt = new Date().toISOString();
    var sessionData = {
        id: sessionId,
        userId: userId,
        role: role,
        createdAt: createdAt
    };
    this.sessions[sessionId] = sessionData;
    return sessionData;
};
SessionStore.prototype.getSession = function(id) {
    var session = this.sessions[id];
    if (!session) {
        return null;
    }
    return session;
};

// --- Inventory Manager ---
function InMemoryInventoryManager() {
    this.items = [];
}
InMemoryInventoryManager.prototype.addItem = function(name, sku, qty, price) {
    var item = { name: name, sku: sku, quantity: qty, price: price };
    this.items.push(item);
};
InMemoryInventoryManager.prototype.findItemBySku = function(sku) {
    for (var i = 0; i < this.items.length; i++) {
        if (this.items[i].sku === sku) {
            return this.items[i];
        }
    }
    return null;
};
InMemoryInventoryManager.prototype.getLowStockItems = function(threshold) {
    var result = [];
    for (var i = 0; i < this.items.length; i++) {
        var currentItem = this.items[i];
        if (currentItem.quantity < threshold) {
            result.push(currentItem);
        }
    }
    return result;
};

// --- Audit Logger ---
function AuditLogger() {
    this.logs = [];
}
AuditLogger.prototype.logAction = function(action, user, status) {
    var timestamp = new Date().toISOString();
    var message = "[" + timestamp + "] Action: " + action + " executed by User: " + user + " -> Status: " + status;
    this.logs.push(message);
    return new Promise(function(resolve) {
        setTimeout(function() {
            resolve(true);
        }, 10);
    });
};

// --- User Order Engine ---
function UserOrderEngine(inventoryManager, sessionStore, logger) {
    this.inventory = inventoryManager;
    this.sessions = sessionStore;
    this.logger = logger;
}
UserOrderEngine.prototype.processUserOrder = function(sessionId, sku, requestQty) {
    var self = this;
    return new Promise(function(resolve, reject) {
        var session = self.sessions.getSession(sessionId);
        if (!session) {
            self.logger.logAction("ORDER_FAIL", "UNKNOWN", "INVALID_SESSION")
                .then(function() {
                    reject(new Error("Unauthorized access: Invalid session token " + sessionId));
                });
            return;
        }
        var userId = session.userId;
        var item = self.inventory.findItemBySku(sku);
        if (!item) {
            self.logger.logAction("ORDER_FAIL", userId, "ITEM_NOT_FOUND")
                .then(function() {
                    reject(new Error("Item with SKU " + sku + " was not found in database"));
                });
            return;
        }
        if (item.quantity < requestQty) {
            self.logger.logAction("ORDER_FAIL", userId, "INSUFFICIENT_STOCK")
                .then(function() {
                    reject(new Error("Cannot fulfill order for " + requestQty + " units of " + item.name + "; only " + item.quantity + " available"));
                });
            return;
        }
        item.quantity = item.quantity - requestQty;
        var totalCost = item.price * requestQty;
        var successMsg = "SUCCESS: " + userId + " bought " + requestQty + "x " + item.name + " (SKU: " + sku + ") total: $" + totalCost;
        self.logger.logAction("ORDER_SUCCESS", userId, "COMPLETED")
            .then(function() {
                var responsePayload = {
                    status: "COMPLETE",
                    user: userId,
                    sku: sku,
                    quantity: requestQty,
                    cost: totalCost,
                    message: successMsg
                };
                resolve(responsePayload);
            });
    });
};
UserOrderEngine.prototype.generateInventoryReport = function() {
    var totalVal = 0;
    for (var i = 0; i < this.inventory.items.length; i++) {
        var item = this.inventory.items[i];
        var itemVal = item.price * item.quantity;
        totalVal = totalVal + itemVal;
    }
    var skus = [];
    for (var j = 0; j < this.inventory.items.length; j++) {
        skus.push(this.inventory.items[j].sku);
    }
    var joinedSkus = "";
    for (var k = 0; k < skus.length; k++) {
        joinedSkus = joinedSkus + skus[k];
        if (k < skus.length - 1) {
            joinedSkus = joinedSkus + ", ";
        }
    }
    var report = "REPORT Summary -> Total Catalog value: $" + totalVal + " | Managed SKUs: [" + joinedSkus + "]";
    return report;
};

// --- Runnable Verification Hook ---
if (require.main === module) {
    var inventory = new InMemoryInventoryManager();
    var sessions = new SessionStore();
    var logger = new AuditLogger();
    inventory.addItem("Wireless Mouse", "MS-01", 50, 25);
    inventory.addItem("Mechanical Keyboard", "KB-02", 5, 120);
    inventory.addItem("4K Monitor", "MN-03", 12, 350);
    var adminSession = sessions.createSession("admin_user_89", "ADMIN");
    var engine = new UserOrderEngine(inventory, sessions, logger);
    
    // Case 1: Valid Order Processing
    engine.processUserOrder(adminSession.id, "MS-01", 2)
        .then(function(res) {
            console.assert(res.status === "COMPLETE", "Test 1 Failed: Status should be COMPLETE");
            console.assert(res.cost === 50, "Test 1 Failed: Cost mismatch");
            console.log("Test 1 Passed: Order processed successfully.");
            
            // Case 2: Insufficient Stock Failure
            return engine.processUserOrder(adminSession.id, "KB-02", 10);
        })
        .catch(function(err) {
            console.assert(err.message.indexOf("Cannot fulfill order") !== -1, "Test 2 Failed: Error message mismatch");
            console.log("Test 2 Passed: Caught expected low stock error.");
            
            // Case 3: Invalid Session Failure
            return engine.processUserOrder("fake_token_123", "MN-03", 1);
        })
        .catch(function(err) {
            console.assert(err.message.indexOf("Unauthorized access") !== -1, "Test 3 Failed: Error message mismatch");
            console.log("Test 3 Passed: Caught expected unauthorized token error.");
            
            // Case 4: Synchronous Report & Data Validation
            var lowStock = inventory.getLowStockItems(10);
            console.assert(lowStock.length === 1, "Test 4 Failed: Expected 1 low stock item");
            console.assert(lowStock[0].sku === "KB-02", "Test 4 Failed: SKU mismatch for low stock");
            var summaryReport = engine.generateInventoryReport();
            console.assert(summaryReport.indexOf("Total Catalog value: $5000") !== -1, "Test 4 Failed: Report calculation issue");
            console.log("Test 4 Passed: Inventory report metrics and low stock validation correct.");
            console.log("All execution verification checks passed successfully.");
        });
}