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
        // Define CLI Options
        var fileOption = new Option<FileInfo>(name: "--file") 
        { 
            Description = "The path to the file you want to edit.",
            Required = true 
        };

        var taskOption = new Option<string>(name: "--task") 
        { 
            Description = "The natural language instruction for the model.",
            Required = true 
        };

        var modelOption = new Option<string?>(name: "--model") 
        { 
            Description = "Override the model name (e.g., qwen3.5:4b)." 
        };

        var providerOption = new Option<string?>(name: "--provider") 
        { 
            Description = "Override the provider id from config." 
        };

        var verboseOption = new Option<bool>(name: "--verbose") 
        { 
            Description = "Print full prompt and raw response to stderr." 
        };

        var debugOption = new Option<bool>(name: "--debug") 
        { 
            Description = "Enable debug mode with extra logging." 
        };

        var rootCommand = new RootCommand("Logis — A learning-focused coding agent harness.")
        {
            fileOption,
            taskOption,
            modelOption,
            providerOption,
            verboseOption,
            debugOption
        };

        // Add options individually
        rootCommand.Options.Add(fileOption);
        rootCommand.Options.Add(taskOption);
        rootCommand.Options.Add(modelOption);
        rootCommand.Options.Add(providerOption);
        rootCommand.Options.Add(verboseOption);
        rootCommand.Options.Add(debugOption);

        // SetAction replaces SetHandler
        rootCommand.SetAction(async parseResult =>
        {
            var file = parseResult.GetValue(fileOption)!;
            var task = parseResult.GetValue(taskOption)!;
            var model = parseResult.GetValue(modelOption);
            var providerId = parseResult.GetValue(providerOption);
            var verboseOverride = parseResult.GetValue(verboseOption);
            var debugOverride = parseResult.GetValue(debugOption);

            var configService = new ConfigService();
            var config = configService.LoadConfig();

            var options = new LogisOptions(
                Debug: debugOverride,
                Verbose: config.Verbose || verboseOverride
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
                AnsiConsole.MarkupLine($"[grey]DEBUG: Using provider '{providerId}' with model '{providerConfig.Model}'[/]");
            }

            // 2. Read the Target File
            if (options.Debug)
            {
                AnsiConsole.MarkupLine($"[grey]DEBUG: Reading file '{file.FullName}'...[/]");
            }
            string fileContent = workspaceService.ReadFile(file.FullName);

            // 3. Perform Completion with UI status
            // Status messages go to Console.Error to keep Console.Out clean for piping
            CompletionResult result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Thinking...", async ctx =>
                {
                    return await completionService.CompleteAsync(file.FullName, fileContent, task, providerConfig);
                });

            // 4. Audit Logging (Always happens regardless of success)
            loggingService.LogRun(result, config);

            // 5. Output Processing
            if (options.Verbose)
            {
                AnsiConsole.MarkupLine("[grey]--- VERBOSE OUTPUT ---[/]");
                AnsiConsole.MarkupLine($"[grey]Prompt Tokens: {result.Usage.PromptTokens}[/]");
                AnsiConsole.MarkupLine($"[grey]Completion Tokens: {result.Usage.CompletionTokens}[/]");
                AnsiConsole.MarkupLine($"[grey]Finish Reason: {result.FinishReason}[/]");
                AnsiConsole.MarkupLine("[grey]---- END VERBOSE ----[/]");
                AnsiConsole.WriteLine();
            }

            // Show the proposed changes in green for visual review
            AnsiConsole.MarkupLine("[bold cyan]--- PROPOSED CHANGES ---[/]");
            AnsiConsole.Write(new Text(result.Content, new Style(Color.Green)));
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold cyan]--- END OF CHANGES ---[/]");
            AnsiConsole.WriteLine();

            // 6. Interactive Confirmation (v0.2 Destructive Writing)
            AnsiConsole.Markup($"Apply changes to [bold cyan]{Markup.Escape(file.Name)}[/]? (1: Yes / 2: No): ");
            
            char key;
            while (true)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.KeyChar;
                if (key == '1' || key == '2') break;
            }
            Console.WriteLine(key); // Echo the key pressed
            AnsiConsole.WriteLine();

            if (key == '1')
            {
                // Overwrite the original file with the model's response
                workspaceService.WriteFile(file.FullName, result.Content);
                AnsiConsole.MarkupLine("[bold green]SUCCESS:[/] Changes applied to file.");
            }
            else
            {
                AnsiConsole.MarkupLine("[bold yellow]DISCARDED:[/] No changes were made to the disk.");

                // Still print to stdout as a fallback so the user didn't waste the tokens
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Raw output below:[/]");
                Console.WriteLine(result.Content);
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
            AnsiConsole.MarkupLine($"[bold red]ERROR:[/] {ex.Message}");
            if (ex.InnerException != null)
            {
                AnsiConsole.MarkupLine($"[grey]Details: {ex.InnerException.Message}[/]");
            }
            Environment.Exit(1);
        }
    }
}
