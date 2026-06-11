# Test Script: T2_LargeFileRefactor_PY.py

## Overview
This is a ~200-line Python script simulating a **Local Media Processor**. It contains complex state management, multiple classes, and several utility functions. It is designed to test the agent's ability to maintain focus, handle "context pressure," and perform consistent, surgical refactoring across a larger file.

## Improvement Checklist
The model must perform a comprehensive refactor that touches nearly every part of the file:
- [ ] **Logging Centralization:** Create a `LogManager` class and replace every instance of the `print()` statement with a call to `LogManager.info()`, `LogManager.error()`, or `LogManager.warning()`.
- [ ] **Custom Exceptions:** Replace generic `Exception` usage with a custom `MediaError` exception. All `try/except` blocks should be updated to handle specific error cases.
- [ ] **Type Safety:** Add type hints to all method signatures and class `__init__` methods.
- [ ] **Docstring Modernization:** Replace the simple comments with Google-style or ReST docstrings for all classes and public methods.
- [ ] **Path Management:** Refactor the script to use `pathlib.Path` instead of manual `os.path` operations.

## Evaluation Prompts

### Vague Prompt
> "The media processor script uses too many print statements and generic exceptions. Can you modernize it to use a proper logging class and custom error handling throughout the whole file?"

```bash
logis --file test-workspaces/tier-2/T2_LargeFileRefactor_PY.py --task "The media processor script uses too many print statements and generic exceptions. Can you modernize it to use a proper logging class and custom error handling throughout the whole file?"
```

### Direct Prompt
> "Refactor T2_LargeFileRefactor_PY.py. Create a 'LogManager' class and use it to replace all 'print' calls with appropriate log levels. Also, ensure all generic 'Exception' raises and catches are replaced with the custom 'MediaError' class, and convert all 'os.path' calls to use 'pathlib.Path' for better cross-platform compatibility."

```bash
logis --file test-workspaces/tier-2/T2_LargeFileRefactor_PY.py --task "Refactor T2_LargeFileRefactor_PY.py. Create a 'LogManager' class and use it to replace all 'print' calls with appropriate log levels. Also, ensure all generic 'Exception' raises and catches are replaced with the custom 'MediaError' class, and convert all 'os.path' calls to use 'pathlib.Path' for better cross-platform compatibility."
```

## Expected Outcome
The resulting code should be functionally identical but structured much more professionally. Key indicators of success:
1.  **No remaining `print()` statements.**
2.  **Zero generic `Exception` catches.**
3.  **Consistent use of `pathlib.Path`.**
4.  **No indentation errors** (common when models struggle with large Python files).
5.  **Clean, readable docstrings** for every major component.
