# Test Script: T1_IdiomaticRefactor_CS.cs

## Overview
This script is a legacy-style C# console application that uses outdated patterns (like `ArrayList`). It tests the agent's ability to refactor C# code to use modern, type-safe features and LINQ.

## Improvement Checklist
The model should identify and apply the following modern C# patterns:
- [ ] Replace non-generic `ArrayList` with `List<TaskItem>`.
- [ ] Replace manual `for`/`foreach` loops with LINQ (e.g., `.Where()`, `.Any()`, `.Count()`).
- [ ] Use auto-properties instead of public fields for the `TaskItem` class.
- [ ] Use string interpolation (`$""`) instead of `StringBuilder` or concatenation for simple summaries.
- [ ] (Optional) Convert the namespace and class to use file-scoped namespaces and modern C# 10+ features (like records for data models).

## Evaluation Prompts

### Vague Prompt
> "Make this C# file look like modern, professional code."

```bash
logis --file test-workspaces/tier-1/T1_IdiomaticRefactor_CS.cs --task "Make this C# file look like modern, professional code."
```

### Direct Prompt
> "Refactor this legacy C# code to use modern features (C# 10+). Specifically, replace 'ArrayList' with generic 'List<T>', use LINQ for filtering and counting tasks, and modernize the class structure using auto-properties."

```bash
logis --file test-workspaces/tier-1/T1_IdiomaticRefactor_CS.cs --task "Refactor this legacy C# code to use modern features (C# 10+). Specifically, replace 'ArrayList' with generic 'List<T>', use LINQ for filtering and counting tasks, and modernize the class structure using auto-properties."
```

## Expected Outcome
The resulting code should be type-safe, significantly more concise through the use of LINQ, and follow modern .NET naming and structural conventions.
