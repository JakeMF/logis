# Logis

A high-performance AI coding agent harness written in C# and engineered for **Native AOT** compilation.

> **Current Status:** **v0.7.0 (Stateful Interactive Agent)**
> Logis has transitioned into a persistent resident process that maintains conversation memory and enforces professional engineering workflows through a state-aware orchestration engine.

---

## Core Features

* **Persistent Sessions:** Conversation history and researched context persist across turns. Resume any previous work using the unique session ID.
* **State Machine Orchestration:** Enforces a rigorous `Research -> Edit -> Review` workflow. The agent must successfully gather file context via tools before it is authorized to propose code changes.
* **Autonomous Tool Use:** The model dynamically lists directories and reads files to understand your codebase's architecture and dependencies.
* **Surgical Diff Engine:** Propose changes using precise Search/Replace blocks (Diff Mode) or full-file replacements (Whole Mode) with integrated visual verification.
* **Crash-Resilient Persistence:** Conversations are stored as append-only JSONL files (Source of Truth) with a SQLite speed-layer for fast session indexing.
* **Native Performance:** Optimized for 0ms input latency and constant-speed history retrieval, regardless of conversation length.

---

## CLI Options

Logis exposes the following configuration parameters:

| Option | Alias | Description |
| :--- | :--- | :--- |
| `--session <id>` | `-s` | Resume an existing persistent session by its ID. |
| `--single-shot` | | Run in legacy mode for one-off tasks (requires `-f` and `-t`). |
| `--file <path>` | `-f` | [Single-Shot Only] The path to the source file you want to edit. |
| `--task <string>` | `-t` | [Single-Shot Only] The natural language instruction for the changes. |
| `--edit-format` | `-e` | `Diff` (Search/Replace blocks) or `Whole` (Full file replacement). |
| `--model <string>` | `-m` | Override the default model (e.g., `qwen2.5-coder`, `claude-3.5-sonnet`). |
| `--provider <id>` | `-p` | Override the configuration provider block. |
| `--verbose` | `-v` | Prints token metrics and finish reasons to the console. |
| `--debug` | `-d` | Enables internal diagnostic telemetry for troubleshooting. |

---

## Usage Workflow

### 1. Interactive Mode (Standard)
Simply run `logis` and interact with the agent conversationally.
- **Instruction:** "Find where the stats are calculated and refactor them."
- **Research:** Logis enters **Research** mode, explores your files, and reads relevant code.
- **Transition:** Once the agent has sufficient context, say "Proceed with the fix."
- **Apply:** Logis enters **Edit** mode, shows a visual diff, and prompts for confirmation before writing to disk.

### 2. Single-Shot Mode (Legacy)
Execute a one-off task and exit immediately:
`logis --single-shot -f App.cs -t "Add type hints to all methods"`

---

## Engineering Mandates

- **Native AOT:** Zero reflection. All serialization uses Source Generated JSON contexts.
- **UI/Data Separation:** `AnsiConsole` for UI/Metadata; `Console.Out` reserved for pipe-friendly data output.
- **Source of Truth:** The filesystem (JSONL) is authoritative; databases are disposable indices.
