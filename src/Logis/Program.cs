using System.CommandLine;
using Spectre.Console;

namespace Logis;

/// <summary>
/// The entry point for the Logis CLI, handling argument parsing and high-level orchestration.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // Force UTF-8 encoding to ensure Unicode characters (like the Dots spinner) 
        // render correctly in all terminal environments.
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // Define CLI Options
        var fileOption = new Option<FileInfo>(name: "--file", aliases: ["-f"]) 
        { 
            Description = "The path to the file you want to edit.",
            Required = true 
        };

        var taskOption = new Option<string>(name: "--task", aliases: ["-t"]) 
        { 
            Description = "The natural language instruction for the model.",
            Required = true 
        };

        var modelOption = new Option<string?>(name: "--model", aliases: ["-m"]) 
        { 
            Description = "Override the model name (e.g., qwen3.5:4b)." 
        };

        var providerOption = new Option<string?>(name: "--provider", aliases: ["-p"]) 
        { 
            Description = "Override the provider id from config." 
        };

        var verboseOption = new Option<bool>(name: "--verbose", aliases: ["-v"]) 
        { 
            Description = "Print full prompt and raw response to stderr." 
        };

        var debugOption = new Option<bool>(name: "--debug", aliases: ["-d"]) 
        { 
            Description = "Enable debug mode with extra logging." 
        };

        var editFormatOption = new Option<EditFormat>(name: "--edit-format", aliases: ["-e"])
        {
            Description = "The format the model should use to propose changes (Whole file or Search/Replace Diff)."
        };

        var rootCommand = new RootCommand("Logis — A learning-focused coding agent harness.")
        {
            fileOption,
            taskOption,
            modelOption,
            providerOption,
            verboseOption,
            debugOption,
            editFormatOption
        };

        // SetAction replaces SetHandler
        rootCommand.SetAction(async parseResult =>
        {
            var file = parseResult.GetValue(fileOption)!;
            var task = parseResult.GetValue(taskOption)!;
            var model = parseResult.GetValue(modelOption);
            var providerId = parseResult.GetValue(providerOption);
            var verboseOverride = parseResult.GetValue(verboseOption);
            var debugOverride = parseResult.GetValue(debugOption);
            var editFormat = parseResult.GetValue(editFormatOption);

            var configService = new ConfigService();
            var config = configService.LoadConfig();

            var options = new LogisOptions(
                Debug: debugOverride,
                Verbose: config.Verbose || verboseOverride,
                MaxToolIterations: config.MaxToolIterations,
                EditFormat: editFormat
            );

            await ExecuteCompletionAsync(file, task, model, providerId, config, options);
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// The primary agent loop for a single file completion.
    /// </summary>
    private static async Task ExecuteCompletionAsync(
        FileInfo file, 
        string task, 
        string? modelOverride, 
        string? providerOverride, 
        Config config, 
        LogisOptions options)
    {
        var workspaceService = new WorkspaceService();
        var completionService = new CompletionService();
        var loggingService = new LoggingService();

        try
        {
            if (options.Debug)
            {
                AnsiConsole.MarkupLine("[grey]DEBUG: Config loaded, determining provider...[/]");
            }

            // 1. Resolve Provider
            string providerId = providerOverride ?? config.DefaultProvider;
            if (!config.Providers.TryGetValue(providerId, out var providerConfig))
            {
                throw new InvalidOperationException($"Provider '{providerId}' not found in configuration.");
            }

            if (!string.IsNullOrEmpty(modelOverride))
            {
                providerConfig = providerConfig with { Model = modelOverride };
            }

            if (options.Debug)
            {
                AnsiConsole.MarkupLine($"[grey]DEBUG: Using provider '{Markup.Escape(providerId)}' with model '{Markup.Escape(providerConfig.Model)}'[/]");
            }

            // 2. Read the Target File
            if (options.Debug)
            {
                AnsiConsole.MarkupLine($"[grey]DEBUG: Reading file '{Markup.Escape(file.FullName)}'...[/]");
            }
            string fileContent = workspaceService.ReadFile(file.FullName);

            // 3. Perform Completion with UI status
            // Status messages go to Console.Error to keep Console.Out clean for piping
            CompletionResult result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.BlueViolet))
                .StartAsync("Thinking...", async ctx =>
                {
                    return await completionService.CompleteAsync(file.FullName, fileContent, task, providerConfig, options, ctx);
                });


            // 4. Audit Logging (Always happens regardless of success)
            loggingService.LogRun(result, config);

            // 5. Output Processing
            if (options.Verbose)
            {
                AnsiConsole.MarkupLine("[grey]--- VERBOSE OUTPUT ---[/]");
                AnsiConsole.MarkupLine($"[grey]Prompt Tokens: {result.Usage.PromptTokens}[/]");
                AnsiConsole.MarkupLine($"[grey]Completion Tokens: {result.Usage.CompletionTokens}[/]");
                AnsiConsole.MarkupLine($"[grey]Finish Reason: {Markup.Escape(result.FinishReason)}[/]");
                AnsiConsole.MarkupLine("[grey]---- END VERBOSE ----[/]");
                AnsiConsole.WriteLine();
            }

            var diffService = new DiffService();
            string finalContent;

            if (options.EditFormat == EditFormat.Diff)
            {
                // In Diff mode, result.Content contains Search/Replace blocks.
                // ApplyEdit parses them, renders a visual diff, and returns the full updated content.
                finalContent = diffService.ApplyEdit(fileContent, result.Content, file.FullName);
            }
            else
            {
                // In Whole mode, result.Content is the complete new file.
                // We render a visual diff between the original and the new content.
                diffService.RenderDiff(fileContent, result.Content, file.Name);
                finalContent = result.Content;
            }

            // 6. Interactive Confirmation (v0.3 Smart Prompt)
            var optionsList = new[] { "1: Yes (Apply)", "2: No (Discard)" };
            int selectedIndex = 0;
            string? finalChoice = null;

            await AnsiConsole.Live(new Text("")).StartAsync(async ctx =>
            {
                while (finalChoice == null)
                {
                    // Render the prompt
                    var promptTable = new Table().NoBorder().HideHeaders().AddColumn("Choice");
                    promptTable.Title = new TableTitle($"Apply changes to [bold cyan]{Markup.Escape(file.Name)}[/]?");
                    
                    for (int i = 0; i < optionsList.Length; i++)
                    {
                        string prefix = (i == selectedIndex) ? "[bold cyan]> [/]" : "  ";
                        string style = (i == selectedIndex) ? "bold cyan" : "white";
                        promptTable.AddRow($"{prefix}[{style}]{optionsList[i]}[/]");
                    }
                    
                    ctx.UpdateTarget(promptTable);

                    // Block and wait for input (0ms latency)
                    var keyInfo = Console.ReadKey(intercept: true);
                    
                    // Hotkey support
                    if (keyInfo.KeyChar == '1') { finalChoice = optionsList[0]; break; }
                    if (keyInfo.KeyChar == '2') { finalChoice = optionsList[1]; break; }
                    
                    // Navigation support
                    switch (keyInfo.Key)
                    {
                        case ConsoleKey.UpArrow:
                            selectedIndex = (selectedIndex == 0) ? optionsList.Length - 1 : selectedIndex - 1;
                            break;
                        case ConsoleKey.DownArrow:
                            selectedIndex = (selectedIndex == optionsList.Length - 1) ? 0 : selectedIndex + 1;
                            break;
                        case ConsoleKey.Enter:
                            finalChoice = optionsList[selectedIndex];
                            break;
                    }
                }
                await Task.CompletedTask;
            });

            AnsiConsole.WriteLine(); // Clear the live line

            if (finalChoice!.StartsWith("1"))
            {
                // Overwrite the original file with the final calculated content
                workspaceService.WriteFile(file.FullName, finalContent);
                AnsiConsole.MarkupLine("[bold green]SUCCESS:[/] Changes applied to file.");
            }
            else
            {
                AnsiConsole.MarkupLine("[bold yellow]DISCARDED:[/] No changes were made to the disk.");

                // Still print to stdout as a fallback so the user didn't waste the tokens
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[cyan]Raw model output below:[/]");
                AnsiConsole.Write(new Text(result.Content, new Style(Color.DarkViolet)));
                AnsiConsole.WriteLine();
            }
        }
        catch (TruncationException ex)
        {
            // Fail Loudly: Log the partial result before crashing
            loggingService.LogRun(ex.PartialResult, config);
            
            AnsiConsole.MarkupLine("[bold red]ERROR:[/] Response was truncated (hit token limit).");
            AnsiConsole.MarkupLine("[grey]Try a smaller file or a more specific task.[/]");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[bold red]ERROR:[/] {Markup.Escape(ex.Message)}");
            if (ex.InnerException != null)
            {
                AnsiConsole.MarkupLine($"[grey]Details: {Markup.Escape(ex.InnerException.Message)}[/]");
            }
            Environment.Exit(1);
        }
    }
}
