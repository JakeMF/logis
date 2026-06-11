# Logis Agent Benchmarking Suite

This directory contains the documentation, evaluation criteria, and prompts for testing the capabilities of the Logis agent. The actual source files used for testing are located in the `test-workspaces/` directory.

## Purpose
The goal of this suite is to provide a consistent, multi-language benchmark for evaluating how well the agent handles different categories of coding tasks, ranging from simple refactors to complex multi-file architectural changes.

---

## How to Run a Test
1.  **Select a Script**: Browse the `T1`, `T2`, or `T3` folders and choose a test case.
2.  **Choose a Prompt**: Use either the **Vague Prompt** (to test general intuition) or the **Direct Prompt** (to test specific instruction following).
3.  **Execute in Logis**: Run Logis targeting the corresponding file in `test-workspaces/`.
    *   *Example:* `logis --file test-workspaces/tier-1/T1_IdiomaticRefactor_JS.js --task "[Prompt Here]"`
4.  **Verify Result**: Use the **Improvement Checklist** in the documentation to audit the model's output.

---

## Tier Definitions

### **Tier 1: Baseline (Idiomatic Refactor)**
Tests the model's ability to write clean, modern, and idiomatic code. 
- **Focus**: Syntax modernization (ES6+, PEP 8, Modern C#), style, and local logic cleanup.
- **Expectation**: Functionally identical code that is significantly more readable and maintainable.

### **Tier 2: Structural (Logic & Complexity)**
Tests the model's reasoning, diagnostic, and context management capabilities.
- **Bug Fixes**: Identifying and fixing subtle logical flaws (off-by-one errors, incorrect accumulation).
- **Interdependence**: Understanding how changes in one part of a file require updates in another.
- **Context Pressure**: Handling large files (150-200+ lines) where surgical precision is required.

### **Tier 3: Architectural (Multi-file)**
Tests the ability to maintain a consistent state across a distributed system.
- **Focus**: Modifying interfaces and their implementations simultaneously, updating consumers, and cross-file discovery.
- **Expectation**: A project that remains compilable and structurally sound across all affected files.

---

## Full Suite Index

| Tier | Script Name (Source) | Language | Evaluation & Prompts |
| :--- | :--- | :--- | :--- |
| **T1** | [T1_IdiomaticRefactor_JS.js](../test-workspaces/tier-1/T1_IdiomaticRefactor_JS.js) | JavaScript | [Evaluation Guide](./T1/T1_IdiomaticRefactor_JS.md) |
| **T1** | [T1_IdiomaticRefactor_PY.py](../test-workspaces/tier-1/T1_IdiomaticRefactor_PY.py) | Python | [Evaluation Guide](./T1/T1_IdiomaticRefactor_PY.md) |
| **T1** | [T1_IdiomaticRefactor_CS.cs](../test-workspaces/tier-1/T1_IdiomaticRefactor_CS.cs) | C# | [Evaluation Guide](./T1/T1_IdiomaticRefactor_CS.md) |
| **T2** | [T2_BugFix_CS.cs](../test-workspaces/tier-2/T2_BugFix_CS.cs) | C# | [Evaluation Guide](./T2/T2_BugFix_CS.md) |
| **T2** | [T2_BugFix_GO.go](../test-workspaces/tier-2/T2_BugFix_GO.go) | Go | [Evaluation Guide](./T2/T2_BugFix_GO.md) |
| **T2** | [T2_InterdependentFunctions_JS.js](../test-workspaces/tier-2/T2_InterdependentFunctions_JS.js) | JavaScript | [Evaluation Guide](./T2/T2_InterdependentFunctions_JS.md) |
| **T2** | [T2_InterfaceRefactor_TS.ts](../test-workspaces/tier-2/T2_InterfaceRefactor_TS.ts) | TypeScript | [Evaluation Guide](./T2/T2_InterfaceRefactor_TS.md) |
| **T2** | [T2_LargeFileRefactor_PY.py](../test-workspaces/tier-2/T2_LargeFileRefactor_PY.py) | Python | [Evaluation Guide](./T2/T2_LargeFileRefactor_PY.md) |
| **T3** | [T3_MultiFileFeature_CS](../test-workspaces/tier-3/T3_MultiFileFeature_CS) | C# | [Evaluation Guide](./T3/T3_MultiFileFeature_CS.md) |
