# Test Script: T1_IdiomaticRefactor_PY.py

## Overview
This script is a non-idiomatic Python script that uses outdated or "un-pythonic" patterns (like manual indexing, manual list copying, and old-style string formatting). It tests the agent's ability to refactor Python code to be clean, readable, and idiomatic (PEP 8).

## Improvement Checklist
The model should identify and apply the following idiomatic Python patterns:
- [ ] Replace manual `for` loops with list comprehensions for filtering or mapping.
- [ ] Use `f-strings` for string interpolation instead of `.format()` or `%` formatting.
- [ ] Use `list.copy()` or slicing (`[:]`) instead of manual element-by-element list copying.
- [ ] (Optional) Add type hints to functions to improve maintainability.
- [ ] Ensure proper PEP 8 naming (snake_case for functions and variables).

## Evaluation Prompts

### Vague Prompt
> "Make this Python script look more professional and idiomatic."

```bash
logis --file test-workspaces/tier-1/T1_IdiomaticRefactor_PY.py --task "Make this Python script look more professional and idiomatic."
```

### Direct Prompt
> "Refactor this Python code to be more idiomatic (PEP 8). Use a list comprehension for filtering even numbers, replace the manual list copy with a list comprehension or '.copy()', and use f-strings for the final output formatting."

```bash
logis --file test-workspaces/tier-1/T1_IdiomaticRefactor_PY.py --task "Refactor this Python code to be more idiomatic (PEP 8). Use a list comprehension for filtering even numbers, replace the manual list copy with a list comprehension or '.copy()', and use f-strings for the final output formatting."
```

## Expected Outcome
The resulting code should be significantly more concise and follow Pythonic best practices (PEP 8). Success is defined by the removal of manual loops in favor of comprehensions and the use of modern string formatting.
