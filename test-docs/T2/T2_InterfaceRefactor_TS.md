# Test Script: T2_InterfaceRefactor_TS.ts

## Overview
This TypeScript script contains redundant interfaces (`PhysicalItem` and `DigitalItem`) and a `ShoppingBasket` class with duplicated logic for handling different item types. It tests the agent's ability to utilize TypeScript's advanced type system (inheritance, discriminating unions, or type guards) to simplify and unify a codebase.

## Improvement Checklist
The model should perform a structural refactor focusing on type consolidation:
- [ ] **Unified Interface:** Create a base `Product` or `Item` interface containing the common fields (`id`, `name`/`title`, `price`/`cost`).
- [ ] **Specialized Extensions:** Refactor `PhysicalItem` and `DigitalItem` to extend the base interface or participate in a **Discriminating Union** (e.g., using a `type: 'physical' | 'digital'` field).
- [ ] **Consolidated Logic:** Refactor `ShoppingBasket` to use a single `items: Product[]` array and a single `addItem(item: Product)` method.
- [ ] **Type Safety:** Use type guards (`isPhysicalItem`) or a switch statement on a discriminant to handle type-specific logic (like shipping address vs. download URL) in `printReceipt` and `calculateTotal`.
- [ ] **Cleanup:** Remove the redundant `validate` functions in favor of a unified validation approach or polymorphic methods.

## Evaluation Prompts

### Vague Prompt
> "This TypeScript code has a lot of duplication between physical and digital items. Can you refactor the interfaces and the ShoppingBasket class to be more unified and use modern TypeScript patterns?"

```bash
logis --file test-workspaces/tier-2/T2_InterfaceRefactor_TS.ts --task "This TypeScript code has a lot of duplication between physical and digital items. Can you refactor the interfaces and the ShoppingBasket class to be more unified and use modern TypeScript patterns?"
```

### Direct Prompt
> "Refactor T2_InterfaceRefactor_TS.ts to use a unified type system. Create a base interface for products and use a discriminating union to distinguish between physical and digital items. Update the ShoppingBasket class to use a single array of items and a single 'addItem' method, using type guards where necessary to handle specific logic."

```bash
logis --file test-workspaces/tier-2/T2_InterfaceRefactor_TS.ts --task "Refactor T2_InterfaceRefactor_TS.ts to use a unified type system. Create a base interface for products and use a discriminating union to distinguish between physical and digital items. Update the ShoppingBasket class to use a single array of items and a single 'addItem' method, using type guards where necessary to handle specific logic."
```

## Expected Outcome
The resulting code should be significantly more DRY (Don't Repeat Yourself). Success is defined by:
1.  **Single Collection:** The `ShoppingBasket` no longer maintains separate arrays for different types.
2.  **Discriminated Union/Guards:** The code correctly uses TypeScript features to safely access type-specific properties (like `weight` or `downloadUrl`).
3.  **Compilability:** The refactored code remains valid TypeScript with no `any` casts or type errors.
