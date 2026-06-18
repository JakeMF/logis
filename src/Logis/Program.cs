using System.CommandLine;
using Spectre.Console;
using Logis.Models;
using Logis.Services;
using Logis.UI;
using Logis.UI.Commands;
using System.Text;

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

        PrintLogo();

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
                SingleShot: singleShot,
                ModelOverride: model,
                ProviderOverride: providerId
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
        var sessionService = new SessionService(options);
        var workspaceService = new WorkspaceService();
        var contextService = new ContextService(workspaceService, sessionService);
        var completionService = new CompletionService();
        var skillService = new SkillService();

        // 1. Create a transient session for this run
        var session = sessionService.CreateSession(Directory.GetCurrentDirectory());
        if (!string.IsNullOrEmpty(options.ModelOverride)) session.Model = options.ModelOverride;
        if (!string.IsNullOrEmpty(options.ProviderOverride)) session.Provider = options.ProviderOverride;
        
        // Single-shot is always an EDIT intent initially
        session.State = SessionState.Edit;
        session.Context.FocusedFiles.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), file.FullName));

        // 2. Persist the User Task
        var userTurn = new SessionTurn(
            Id: Guid.NewGuid().ToString("N"),
            SessionId: session.Id,
            Role: "user",
            Content: task,
            ToolName: null,
            ToolCallId: null,
            StateAtTurn: session.State.ToString(),
            FinishReason: null,
            TokenCount: null,
            Iteration: null,
            IsPinned: false,
            IsSummary: false,
            ToolResultPath: null,
            WorkspaceRoot: session.Context.WorkspaceRoot,
            Timestamp: DateTime.UtcNow
        );
        sessionService.AppendTurn(session, userTurn);

        // 3. Perform Completion
        CompletionResult result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(Color.BlueViolet))
            .StartAsync("Thinking...", async ctx =>
            {
                return await completionService.CompleteAsync(session, sessionService, contextService, config, options, skillService, ctx, ct);
            });

        // 4. Handle Edit (Reusing the existing Diff logic from old Program.cs would go here)
        // For v0.7 infrastructure, we just print the result content.
        AnsiConsole.Write(new Text(result.Content, new Style(Color.Grey84)));
        AnsiConsole.WriteLine();

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
        var completionService = new CompletionService();
        var skillService = new SkillService();

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

        // CLI Overrides take absolute precedence for the current run
        if (!string.IsNullOrEmpty(options.ModelOverride)) session.Model = options.ModelOverride;
        if (!string.IsNullOrEmpty(options.ProviderOverride)) session.Provider = options.ProviderOverride;

        // Load durable history into memory once at session start
        await sessionService.LoadHistoryAsync(session, ct);

        // --- Phase 3: UI Integration ---
        var registry = new CommandRegistry();
        registry.Register(new UI.Commands.HelpCommand(registry));
        registry.Register(new UI.Commands.ExitCommand());

        var statusLine = new StatusLine();
        statusLine.AddSegment(new StatusSegment("directory", s => {
            string cwd = Directory.GetCurrentDirectory();
            return cwd.Length > 30 ? "..." + cwd[^27..] : cwd;
        }, s => ConsoleColor.Yellow, 1));
        statusLine.AddSegment(new StatusSegment("model", s => {
            var provider = s.Provider ?? config.DefaultProvider;
            var model = s.Model ?? (config.Providers.TryGetValue(provider, out var p) ? p.Model : "unknown");
            return $"{provider}:{model}";
        }, s => ConsoleColor.Cyan, 2));
        statusLine.AddSegment(new StatusSegment("state", s => s.State.ToString(), s => s.State switch {
            SessionState.Research => ConsoleColor.Yellow,
            SessionState.Edit => ConsoleColor.Blue,
            SessionState.Review => ConsoleColor.Cyan,
            SessionState.Error => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        }, 3));
        statusLine.AddSegment(new StatusSegment("session", s => s.Id[..8], s => ConsoleColor.DarkGray, 4));

        var inputBar = new UI.InputBar(session, statusLine, registry);

        AnsiConsole.MarkupLine("[grey]Interactive mode active. Type /help for commands, /exit to exit.[/]");
        AnsiConsole.WriteLine();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                string input = await inputBar.ReadLineAsync(ct);

                // 1. Slash Command Routing
                if (registry.TryExecute(input, session)) continue;

                // 2. Intent detection: gates the model's capability scope for the upcoming turn.
                stateService.Transition(session, input, msg => 
                {
                    if (options.Debug) AnsiConsole.MarkupLine($"[grey]DEBUG: {Markup.Escape(msg)}[/]");
                });

                // 3. Persist User Turn (Sync both memory and JSONL)
                var userMessage = new ChatMessage(ChatRole.User, input);
                var userTurn = new SessionTurn(
                    Id: Guid.NewGuid().ToString("N"),
                    SessionId: session.Id,
                    Role: "user",
                    Content: input,
                    ToolName: null,
                    ToolCallId: null,
                    StateAtTurn: session.State.ToString(),
                    FinishReason: null,
                    TokenCount: null,
                    Iteration: null,
                    IsPinned: false,
                    IsSummary: false,
                    ToolResultPath: null,
                    WorkspaceRoot: session.Context.WorkspaceRoot,
                    Timestamp: DateTime.UtcNow
                );

                session.History.Add(userMessage);
                sessionService.AppendTurn(session, userTurn);
                
                if (options.Debug)
                {
                    AnsiConsole.MarkupLine($"[grey]DEBUG: State transitioned to {session.State}[/]");
                }

                // 4. Perform Completion
                CompletionResult result = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(new Style(Color.BlueViolet))
                    .StartAsync("Thinking...", async ctx =>
                    {
                        return await completionService.CompleteAsync(session, sessionService, contextService, config, options, skillService, ctx, ct);
                    });

                // 5. Output Response & Apply Edits
                bool handledAsEdit = false;
                if (session.State == SessionState.Edit)
                {
                    var diffService = new DiffService();
                    foreach (var relativePath in session.Context.FocusedFiles)
                    {
                        string fullPath = Path.Combine(session.Context.WorkspaceRoot, relativePath);
                        if (!File.Exists(fullPath)) continue;

                        try 
                        {
                            string original = await File.ReadAllTextAsync(fullPath, ct);
                            string updated;

                            if (options.EditFormat == EditFormat.Whole)
                            {
                                updated = result.Content.Trim();
                                diffService.RenderDiff(original, updated, relativePath);
                            }
                            else
                            {
                                updated = diffService.ApplyEdit(original, result.Content, relativePath);
                            }

                            if (updated != original)
                            {
                                handledAsEdit = true;
                                var choice = AnsiConsole.Prompt(
                                    new SelectionPrompt<string>()
                                        .Title($"[bold yellow]Apply changes to {relativePath}?[/]")
                                        .AddChoices(new[] { "1: Yes", "2: No" }));

                                if (choice.StartsWith("1"))
                                {
                                    await File.WriteAllTextAsync(fullPath, updated, ct);
                                    AnsiConsole.MarkupLine($"[green]✔ Applied to {relativePath}[/]");
                                    session.State = SessionState.Review;
                                    sessionService.UpdateSessionMetadata(session);
                                }
                                else
                                {
                                    AnsiConsole.MarkupLine("[grey]Changes discarded. You can provide feedback to adjust the proposal.[/]");
                                }
                            }
                        }
                        catch 
                        { 
                            // No blocks for this specific file or match failed, continue to next or fallback to print
                        }
                    }
                }

                if (!handledAsEdit)
                {
                    AnsiConsole.Write(new Text(result.Content, new Style(Color.Grey84)));
                    AnsiConsole.WriteLine();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful exit requested by user
        }
        finally
        {
            // 1. Restore terminal attributes first
            inputBar.Cleanup();

            // 2. Draw a final separator and termination message
            int width = Console.WindowWidth;
            if (width <= 0) width = 80;
            
            Console.Write("\x1b[90m"); // DarkGray ANSI
            Console.WriteLine(new string('─', width));
            Console.Write("\x1b[0m"); // Reset
            
            AnsiConsole.MarkupLine("[yellow]Session terminated.[/]");
            Console.WriteLine();
        }

        return 0;
    }

    private static void PrintLogo()
    {
        var logoLines = new[]
        {
            " ██╗      ██████╗  ██████╗ ██╗███████╗",
            " ██║     ██╔═══██╗██╔════╝ ██║██╔════╝",
            " ██║     ██║   ██║██║  ███╗██║███████╗",
            " ██║     ██║   ██║██║   ██║██║╚════██║",
            " ███████╗╚██████╔╝╚██████╔╝██║███████║",
            " ╚══════╝ ╚═════╝  ╚═════╝ ╚═╝╚══════╝"
        };

        var startColor = Color.BlueViolet;
        var endColor = Color.Cyan;

        var rows = new List<Markup>();
        int maxLength = logoLines.Max(l => l.Length);

        foreach (var line in logoLines)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                float t = maxLength > 1 ? (float)i / (maxLength - 1) : 0;

                byte r = (byte)(startColor.R + (endColor.R - startColor.R) * t);
                byte g = (byte)(startColor.G + (endColor.G - startColor.G) * t);
                byte b = (byte)(startColor.B + (endColor.B - startColor.B) * t);

                var color = new Color(r, g, b);
                sb.Append($"[{color.ToMarkup()}]{Markup.Escape(line[i].ToString())}[/]");
            }
            rows.Add(new Markup(sb.ToString()));
        }

        var panel = new Panel(new Rows(rows))
            .Border(BoxBorder.Rounded)
            .BorderColor(startColor)
            .Padding(1, 0, 1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }
}