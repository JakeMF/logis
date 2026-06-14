using System.CommandLine;
using Spectre.Console;
using Logis.Models;
using Logis.Services;

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
        var fileOption = new Option<FileInfo?>(name: "--file", aliases: ["-f"]) 
        { 
            Description = "The path to the file you want to edit (required for --single-shot)." 
        };

        var taskOption = new Option<string?>(name: "--task", aliases: ["-t"]) 
        { 
            Description = "The natural language instruction for the model (required for --single-shot)." 
        };

        var modelOption = new Option<string?>(name: "--model", aliases: ["-m"]) 
        { 
            Description = "Override the model name." 
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
            Description = "The format the model should use to propose changes."
        };

        var sessionOption = new Option<string?>(name: "--session", aliases: ["-s"])
        {
            Description = "Resume an existing session by ID."
        };

        var singleShotOption = new Option<bool>(name: "--single-shot")
        {
            Description = "Run a single turn and exit (requires -f and -t)."
        };

        var rootCommand = new RootCommand("Logis — A learning-focused coding agent harness.")
        {
            fileOption,
            taskOption,
            modelOption,
            providerOption,
            verboseOption,
            debugOption,
            editFormatOption,
            sessionOption,
            singleShotOption
        };

        rootCommand.SetAction(async parseResult =>
        {
            var file = parseResult.GetValue(fileOption);
            var task = parseResult.GetValue(taskOption);
            var model = parseResult.GetValue(modelOption);
            var providerId = parseResult.GetValue(providerOption);
            var verboseOverride = parseResult.GetValue(verboseOption);
            var debugOverride = parseResult.GetValue(debugOption);
            var editFormat = parseResult.GetValue(editFormatOption);
            var sessionId = parseResult.GetValue(sessionOption);
            var singleShot = parseResult.GetValue(singleShotOption);

            // Validation: Single-shot requires file and task
            if (singleShot && (file == null || string.IsNullOrWhiteSpace(task)))
            {
                AnsiConsole.MarkupLine("[bold red]ERROR:[/] --single-shot requires both --file and --task.");
                return 1;
            }

            // Validation: Interactive mode doesn't allow -f or -t at startup
            if (!singleShot && (file != null || !string.IsNullOrWhiteSpace(task)))
            {
                AnsiConsole.MarkupLine("[bold red]ERROR:[/] --file and --task are only allowed in --single-shot mode.");
                AnsiConsole.MarkupLine("[grey]In interactive mode, simply mention the file in your first prompt.[/]");
                return 1;
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var configService = new ConfigService();
            var config = await configService.LoadConfigAsync(cts.Token);

            var options = new LogisOptions(
                Debug: debugOverride,
                Verbose: config.Verbose || verboseOverride,
                MaxToolIterations: config.MaxToolIterations,
                EditFormat: editFormat,
                SessionId: sessionId,
                SingleShot: singleShot
            );

            try
            {
                if (singleShot)
                {
                    return await ExecuteSingleShotAsync(file!, task!, model, providerId, config, options, cts.Token);
                }
                else
                {
                    return await ExecuteInteractiveLoopAsync(sessionId, model, providerId, config, options, cts.Token);
                }
            }
            catch (Exception ex)
            {
                // Fail Loudly: Ensure errors are surfaced clearly with architectural 
                // context to help the user diagnose environment or provider issues.
                AnsiConsole.MarkupLine($"[bold red]ERROR:[/] {Markup.Escape(ex.Message)}");
                if (ex.InnerException != null)
                {
                    AnsiConsole.MarkupLine($"[grey]Details: {Markup.Escape(ex.InnerException.Message)}[/]");
                }
                return 1;
            }
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task<int> ExecuteSingleShotAsync(
        FileInfo file, 
        string task, 
        string? modelOverride, 
        string? providerOverride, 
        Config config, 
        LogisOptions options,
        CancellationToken ct)
    {
        // Shimming for now until Phase 4 refactor
        AnsiConsole.MarkupLine("[yellow]Single-shot mode engaged (Phase 4 will refactor this to use sessions).[/]");
        return 0;
    }

    private static async Task<int> ExecuteInteractiveLoopAsync(
        string? sessionId,
        string? modelOverride,
        string? providerOverride,
        Config config,
        LogisOptions options,
        CancellationToken ct)
    {
        var sessionService = new SessionService(options);
        var stateService = new StateService();
        var workspaceService = new WorkspaceService();
        var contextService = new ContextService(workspaceService, sessionService);

        Session session;
        if (!string.IsNullOrEmpty(sessionId))
        {
            session = sessionService.GetSession(sessionId) ?? throw new Exception($"Session {sessionId} not found.");
            AnsiConsole.MarkupLine($"[bold cyan]RESUMED session:[/] {session.Id}");
        }
        else
        {
            session = sessionService.CreateSession(Directory.GetCurrentDirectory());
            AnsiConsole.MarkupLine($"[bold green]STARTED new session:[/] {session.Id}");
        }

        // We use AnsiConsole for UI and metadata. Standard Console.Out is reserved 
        // for final data output to ensure Logis remains pipe-friendly.
        AnsiConsole.MarkupLine("[grey]Type your instructions below. Press Enter to send, Ctrl+C to exit.[/]");
        AnsiConsole.WriteLine();

        while (!ct.IsCancellationRequested)
        {
            string? input = null;
            
            // We use a manual ReadKey loop inside AnsiConsole.Live to achieve 0ms input latency.
            // This bypasses the standard line buffer, allowing for a "native" terminal feel 
            // where the UI can potentially react to keystrokes in real-time.
            await AnsiConsole.Live(new Text("> ")).StartAsync(async ctx => 
            {
                var userInput = new StringBuilder();
                while (input == null)
                {
                    if (ct.IsCancellationRequested) break;

                    if (!Console.KeyAvailable)
                    {
                        await Task.Delay(20, ct);
                        continue;
                    }

                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Enter)
                    {
                        input = userInput.ToString();
                        Console.WriteLine();
                        break;
                    }
                    else if (key.Key == ConsoleKey.Backspace && userInput.Length > 0)
                    {
                        userInput.Remove(userInput.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                    else if (!char.IsControl(key.KeyChar))
                    {
                        userInput.Append(key.KeyChar);
                        Console.Write(key.KeyChar);
                    }
                    
                    ctx.UpdateTarget(new Text("> " + userInput.ToString()));
                }
            });

            if (string.IsNullOrWhiteSpace(input)) continue;

            // Intent detection happens immediately after input to gate the model's 
            // capability scope (tools/skills) for the upcoming turn.
            stateService.Transition(session, input);
            
            // Proactive context gathering reduces model 'exploration overhead' by loading 
            // explicitly mentioned files before the model sees the turn.
            contextService.ScanForFileMentions(session, input);
            
            if (options.Debug)
            {
                AnsiConsole.MarkupLine($"[grey]DEBUG: State transitioned to {session.State}[/]");
                foreach (var f in session.Context.FocusedFiles)
                {
                    AnsiConsole.MarkupLine($"[grey]DEBUG: Focused file: {f}[/]");
                }
            }

            // 3. Completion (Refactored in Phase 4)
            AnsiConsole.MarkupLine($"[blue]LOGIS:[/] [italic]Transitioning to {session.State}... (Phase 4 will handle the model call here)[/]");
            AnsiConsole.WriteLine();
        }

        return 0;
    }
}
