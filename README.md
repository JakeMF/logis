# Logis

A lightweight AI coding utility written in C# and engineered for AOT native compilation.

> **Current Status:** **v0.6.0 (Tool-Enabled Agent)**
> Logis now supports basic tool calling, allowing the model to list directories and read other files to gather context before proposing changes.

---

## Features

* **Basic Tool Use:** The model can now gather context by listing directory contents and reading files across the workspace to better understand dependencies.
* **Diff Mode Support:** Propose changes using Search/Replace blocks instead of rewriting the entire file—improving reliability on larger source files.
* **Interactive Review:** Review model-generated code in color-coded blocks and overwrite the target file with a single keystroke.
* **Smart Prompt UI:** High-speed interaction using numeric hotkeys (1 for Yes, 2 for No) or standard arrow-key navigation.
* **Detailed Audit Logging:** Every run generates a log file tracking the specific model used, edit format, tool execution count, and token metrics.

---

## CLI Options

Logis exposes the following configuration parameters:

| Option | Alias | Description |
| :--- | :--- | :--- |
| `--file <path>` | `-f` | The path to the source file you want to edit. |
| `--task <string>` | `-t` | The natural language instruction explaining the changes. |
| `--edit-format <Whole\|Diff>`| `-e` | Use `Diff` for Search/Replace blocks or `Whole` for full file replacement. |
| `--model <string>` | `-m` | Override the default model (e.g., `qwen2.5-coder`, `llama3`). |
| `--provider <id>` | `-p` | Override the configuration provider block. |
| `--verbose` | `-v` | Prints token metrics and finish reasons to the console. |
| `--debug` | `-d` | Enables internal diagnostic logging for troubleshooting. |

---

## Usage Workflow

1. **Run a Task:** Execute a command like `logis -f App.cs -t "Refactor this method"`.
2. **Research (Automatic):** If the model needs more context, it will call tools to list files or read related code.
3. **Review:** Inspect the visual diff or the new code block displayed in the terminal.
4. **Confirm:** Press `1` to overwrite the file or `2` to discard.
