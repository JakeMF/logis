# Test Script: T2_BugFix_GO.go

## Overview
This Go script simulates a retail tax and discount calculator. It contains two intentional logical bugs that are common in financial software. It tests the agent's ability to verify business requirements against implementation and perform diagnostic fixes in Go.

## Improvement Checklist
The model must identify and fix the following specific logic errors:
- [ ] **Bug 1 (Discount Threshold):** In `CalculateTotal`, the condition for the 10% discount should be `order.ItemCount >= 10`. The current code misses customers with exactly 10 items.
- [ ] **Bug 2 (Tax Base):** In the `else` block of the tax calculation, the tax should be calculated based on `taxableAmount` (the amount after discount), not the `subtotal`.
- [ ] (Optional) Standardize the currency output or add a helper method for formatting if desired.

## Evaluation Prompts

### Vague Prompt
> "Our customers are complaining that their loyalty discounts aren't being applied correctly and the tax amounts look slightly off for non-NY orders. Can you find the bugs in this Go script and fix them?"

```bash
logis --file test-workspaces/tier-2/T2_BugFix_GO.go --task "Our customers are complaining that their loyalty discounts aren't being applied correctly and the tax amounts look slightly off for non-NY orders. Can you find the bugs in this Go script and fix them?"
```

### Direct Prompt
> "There are two bugs in the TaxCalculator. One is an off-by-one error where premium customers with exactly 10 items aren't getting their discount. The second is that for non-NY orders, the tax is being calculated on the full subtotal instead of the discounted price. Fix both issues in T2_BugFix_GO.go."

```bash
logis --file test-workspaces/tier-2/T2_BugFix_GO.go --task "There are two bugs in the TaxCalculator. One is an off-by-one error where premium customers with exactly 10 items aren't getting their discount. The second is that for non-NY orders, the tax is being calculated on the full subtotal instead of the discounted price. Fix both issues in T2_BugFix_GO.go."
```

## Expected Outcome
A successful fix will result in:
1.  **Correct Discounting:** An order with 10 items and `IsPremium: true` should trigger the 10% discount.
2.  **Correct Tax Base:** The final tax for any order with a discount (and not in NY) should be strictly lower than the tax on the pre-discount price.
3.  **Go Idioms:** The fix should remain idiomatic to Go (e.g., proper variable naming and concise logic).
