# Test Script: T2_BugFix_CS.cs

## Overview
This C# script simulates a simple Inventory Management system. It contains two intentional logical bugs that affect reporting and reordering logic. It tests the agent's ability to analyze business logic comments against implementation and perform precise diagnostic fixes in C#.

## Improvement Checklist
The model must identify and fix the following specific logic errors:
- [ ] **Bug 1 (Reorder Threshold):** In `GetItemsNeedingReorder`, the condition `item.Quantity < item.ReorderThreshold` should be updated to `item.Quantity <= item.ReorderThreshold`. Items exactly at the threshold should be flagged for reorder.
- [ ] **Bug 2 (Category Value Accumulation):** In `GetStockValueByCategory`, the value assignment `result[item.Category] = item.Quantity * item.UnitCost` should be updated to `+=`. The current code overwrites the total for each item in the category rather than accumulating it.
- [ ] **LINQ Modernization (Optional):** Refactor the loop-based logic in `GetItemsNeedingReorder` and `GetTotalInventoryValue` to use LINQ (e.g., `.Where().ToList()` or `.Sum()`).

## Evaluation Prompts

### Vague Prompt
> "The inventory report is showing incorrect totals for our categories, and some items that are at their reorder limit aren't being flagged. Can you find the bugs in this C# script and fix them?"

```bash
logis --file test-workspaces/tier-2/T2_BugFix_CS.cs --task "The inventory report is showing incorrect totals for our categories, and some items that are at their reorder limit aren't being flagged. Can you find the bugs in this C# script and fix them?"
```

### Direct Prompt
> "Fix two bugs in T2_BugFix_CS.cs. First, update 'GetItemsNeedingReorder' so it includes items where the quantity is exactly equal to the reorder threshold. Second, fix 'GetStockValueByCategory' so it correctly accumulates the total value of all items in a category instead of overwriting it."

```bash
logis --file test-workspaces/tier-2/T2_BugFix_CS.cs --task "Fix two bugs in T2_BugFix_CS.cs. First, update 'GetItemsNeedingReorder' so it includes items where the quantity is exactly equal to the reorder threshold. Second, fix 'GetStockValueByCategory' so it correctly accumulates the total value of all items in a category instead of overwriting it."
```

## Expected Outcome
A successful fix will result in:
1.  **Correct Reordering:** Items like "USB-C Hub" (Qty 8, Threshold 8) appear in the reorder list.
2.  **Correct Category Totals:** The "Electronics" category shows a total value reflecting all three items (~$22,199.30) instead of just the last item.
3.  **Modern C#:** The implementation remains idiomatic, potentially utilizing LINQ to simplify the logic.
