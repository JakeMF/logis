using System;
using System.Collections.Generic;

namespace InventoryManager
{
    public class StockItem
    {
        public string Sku { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public int ReorderThreshold { get; set; }
        public decimal UnitCost { get; set; }

        public StockItem(string sku, string name, string category, int quantity, int reorderThreshold, decimal unitCost)
        {
            Sku = sku;
            Name = name;
            Category = category;
            Quantity = quantity;
            ReorderThreshold = reorderThreshold;
            UnitCost = unitCost;
        }
    }

    public class InventoryCalculator
    {
        private readonly List<StockItem> _inventory;

        public InventoryCalculator(List<StockItem> inventory)
        {
            _inventory = inventory;
        }

        /// <summary>
        /// Returns all items that need to be reordered.
        /// An item needs reordering when its quantity falls below the reorder threshold.
        /// </summary>
        public List<StockItem> GetItemsNeedingReorder()
        {
            var result = new List<StockItem>();
            foreach (var item in _inventory)
            {
                // BUG 1: Should be <= not <
                // Items AT the reorder threshold also need reordering,
                // but this condition misses them.
                if (item.Quantity < item.ReorderThreshold)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the total stock value grouped by category.
        /// Stock value = Quantity * UnitCost for all items in a category.
        /// </summary>
        public Dictionary<string, decimal> GetStockValueByCategory()
        {
            var result = new Dictionary<string, decimal>();

            foreach (var item in _inventory)
            {
                if (!result.ContainsKey(item.Category))
                {
                    result[item.Category] = 0;
                }

                // BUG 2: Should be += not =
                // This resets the category total on every item instead of
                // accumulating it, so only the last item's value is counted.
                result[item.Category] = item.Quantity * item.UnitCost;
            }

            return result;
        }

        /// <summary>
        /// Returns the total value of all inventory.
        /// </summary>
        public decimal GetTotalInventoryValue()
        {
            decimal total = 0;
            foreach (var item in _inventory)
            {
                total += item.Quantity * item.UnitCost;
            }
            return total;
        }

        /// <summary>
        /// Returns a summary report of the current inventory state.
        /// </summary>
        public string GenerateReport()
        {
            var needsReorder = GetItemsNeedingReorder();
            var valueByCategory = GetStockValueByCategory();
            var totalValue = GetTotalInventoryValue();

            var lines = new List<string>();
            lines.Add("=== INVENTORY REPORT ===");
            lines.Add($"Total Items: {_inventory.Count}");
            lines.Add($"Total Value: {totalValue:C}");
            lines.Add("");
            lines.Add("Value by Category:");
            foreach (var kvp in valueByCategory)
            {
                lines.Add($"  {kvp.Key}: {kvp.Value:C}");
            }
            lines.Add("");
            lines.Add($"Items Needing Reorder: {needsReorder.Count}");
            foreach (var item in needsReorder)
            {
                lines.Add($"  [{item.Sku}] {item.Name} — Qty: {item.Quantity}, Threshold: {item.ReorderThreshold}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var inventory = new List<StockItem>
            {
                // Electronics category — 3 items
                // Total value should be: (50 * 299.99) + (12 * 499.99) + (8 * 149.99)
                //                      = 14999.50 + 5999.88 + 1199.92 = 22199.30
                new StockItem("ELEC-001", "Wireless Headphones",  "Electronics", 50,  20,  299.99m),
                new StockItem("ELEC-002", "Bluetooth Speaker",    "Electronics", 12,  15,  499.99m),
                new StockItem("ELEC-003", "USB-C Hub",            "Electronics", 8,   8,   149.99m),

                // Office category — 3 items
                // Total value should be: (200 * 4.99) + (75 * 12.99) + (30 * 24.99)
                //                      = 998.00 + 974.25 + 749.70 = 2721.95
                new StockItem("OFFC-001", "Ballpoint Pens (12pk)","Office",      200, 50,  4.99m),
                new StockItem("OFFC-002", "Notebook A4",          "Office",      75,  25,  12.99m),
                new StockItem("OFFC-003", "Desk Organizer",       "Office",      30,  10,  24.99m),

                // Tools category — 2 items
                // Total value should be: (15 * 89.99) + (6 * 199.99)
                //                      = 1349.85 + 1199.94 = 2549.79
                new StockItem("TOOL-001", "Cordless Drill",       "Tools",       15,  5,   89.99m),
                new StockItem("TOOL-002", "Laser Level",          "Tools",       6,   6,   199.99m),
            };

            // Note for evaluation:
            // ELEC-003 (USB-C Hub): Qty=8, Threshold=8 — AT threshold, should be flagged for reorder
            // TOOL-002 (Laser Level): Qty=6, Threshold=6 — AT threshold, should be flagged for reorder
            // Bug 1 means both of these are missed by GetItemsNeedingReorder()
            //
            // Bug 2 means category totals are wrong:
            // Electronics will show only USB-C Hub value ($1199.92) instead of $22199.30
            // Office will show only Desk Organizer value ($749.70) instead of $2721.95
            // Tools will show only Laser Level value ($1199.94) instead of $2549.79

            var calculator = new InventoryCalculator(inventory);
            Console.WriteLine(calculator.GenerateReport());
        }
    }
}