using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ClaudeCodeBridge;
using Microsoft.Extensions.DependencyInjection;
using Photino.NET;

namespace LinkedInAutoApply;

public record LogLine(string? JobId, string Text, DateTime At);

static class Program
{
    const string ProfileMarker = "## Candidate profile";

    // Every Claude call sees only its system prompt file (ai/judge.md or ai/answer.md) and the prompt: no Claude Code
    // coding prompt, user-level settings, hooks, skills or MCP servers, no saved transcript, and no tools at all
    // (so nothing outside the prompt can be read or written, and no tool definitions are sent).
    const string Isolation = "--setting-sources project --disable-slash-commands --no-session-persistence --strict-mcp-config --tools \"\"";
    static readonly List<LogLine> Log = [];

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--selfcheck")) { Answers.SelfCheck(); Filters.SelfCheck(); Indeed.SelfCheck(); SelfCheck(); Console.WriteLine("selfcheck ok"); return; }

        // Each person on this computer is a "remetente" with its own folder under %APPDATA%\LinkedInAutoApply\users;
        // no login. Everything personal (profile, answers, ledger, Chrome login) lives there.
        // Claude Code sends only the prompt: no telemetry, error reports or other non-essential traffic.
        // Child processes (the bridge's claude calls) inherit this.
        Environment.SetEnvironmentVariable("CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC", "1");

        Users.Migrate();
        if (Users.List().Count == 0) Users.Current = Users.Create("Principal");
        if (!Users.Exists(Users.Current)) Users.Current = Users.List()[0].Id;

        IClaudeAgentService claude = null!, judge = null!;
        Answers answers = null!;
        Runner runner = null!;
        var settings = new Dictionary<string, bool>();
        var filters = new Filters();

        var window = new PhotinoWindow()
            .SetLogVerbosity(0)
            .SetTitle("Tá Aplicado")
            .SetIconFile(Path.Combine(AppContext.BaseDirectory, "icon.ico"))
            .SetSize(1360, 880)
            .SetMinSize(900, 620)
            .Center();

        void Push()
        {
            string json;
            lock (Store.Gate) json = JsonSerializer.Serialize(State(), Store.Json);
            window.SendWebMessage(json);
        }

        void AddLog(string? jobId, string text)
        {
            lock (Store.Gate)
            {
                Log.Add(new(jobId, text, DateTime.Now));
                if (Log.Count > 400) Log.RemoveAt(0);
                File.AppendAllText("run.log", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {jobId,-10} {text}{Environment.NewLine}");
            }
            Push();
        }

        var retrying = false;
        var suggesting = false;
        var asking = false;

        // Onboarding checks: Claude must really answer through the bridge, Chrome must really open under Playwright,
        // and the user accepts the LinkedIn ToS risk once (settings.json).
        bool? claudeOk = null, chromeOk = null;
        void CheckChrome()
        {
            chromeOk = null;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var pw = await Microsoft.Playwright.Playwright.CreateAsync();
                    await using var b = await pw.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true });
                    chromeOk = true;
                }
                catch (Exception) { chromeOk = false; }
                Push();
            });
        }
        CheckChrome();
        void CheckClaude()
        {
            claudeOk = null;
            RefreshPath(); // an install from the setup steps changed PATH after this process started
            _ = Task.Run(async () =>
            {
                var r = await judge.RunPromptAsync("Return ONLY this JSON, nothing else: {\"ok\":true}");
                claudeOk = r.Success && r.Content.Contains("\"ok\"");
                Push();
            });
        }

        // Points the whole app at one remetente: its folder becomes the working directory (every relative path
        // resolves there) and every per-user object is rebuilt from it.
        void Activate(string id)
        {
            Directory.SetCurrentDirectory(Users.PathOf(id));
            Directory.CreateDirectory("agent");
            // Rules ship with the program; only the profile part of CLAUDE.md is the user's. Rebuilt on every
            // activation so rule updates reach existing users.
            var rules = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "agent-rules.md"));
            var saved = File.Exists("agent/CLAUDE.md") ? SplitProfile(File.ReadAllText("agent/CLAUDE.md")).Profile : "";
            File.WriteAllText("agent/CLAUDE.md", rules.TrimEnd() + "\n\n" + ProfileMarker + "\n" + saved + "\n");

            IClaudeAgentService Bridge(string prompt) => new ServiceCollection()
                .AddClaudeCodeBridge(o =>
                {
                    o.ProjectDirectory = Path.GetFullPath("ai");
                    o.MaxTurns = 3;
                    o.Timeout = TimeSpan.FromMinutes(2);
                    o.ExtraArguments = $"{Isolation} --system-prompt-file \"{Path.GetFullPath(prompt)}\"";
                })
                .BuildServiceProvider()
                .GetRequiredService<IClaudeAgentService>();
            claude = Bridge("ai/answer.md");
            judge = Bridge("ai/judge.md");
            answers = new Answers(claude, judge);
            answers.WritePrompts();
            File.Delete("agent/saved-answers.md"); // older versions imported it from CLAUDE.md; ai/answer.md carries it now
            runner = new Runner(answers, AddLog, Push);
            settings = Store.Load<Dictionary<string, bool>>("settings.json");
            answers.NoAi = settings.GetValueOrDefault("noAi");
            // No choice yet: the Windows language decides (Portuguese, or English for everything else).
            answers.English = settings.TryGetValue("english", out var en) ? en : !System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("pt");
            filters = Store.Load<Filters>("filters.json");
            lock (Store.Gate) Log.Clear();
            Users.Current = id;
        }
        Activate(Users.Current);

        object State() => new
        {
            type = "state",
            running = runner.Running,
            stopping = runner.Stopping,
            attention = runner.Attention,
            reading = runner.Reading,
            retrying,
            suggesting,
            asking,
            searches = Store.Load<List<string>>("searches.json"),
            runDryRun = runner.Options?.DryRun,
            claudeOk,
            chromeOk,
            riskAccepted = settings.GetValueOrDefault("riskAccepted"),
            dataNoticeAccepted = settings.GetValueOrDefault("dataNoticeAccepted"),
            noAi = answers.NoAi,
            lang = answers.English ? "en" : "pt",
            users = Users.List(),
            currentUser = Users.Current,
            currentJob = runner.CurrentJob,
            runUltra = runner.Options?.Ultra,
            runPhase = runner.Phase,
            nextAt = runner.NextAt,
            canStopScanning = runner.CanStopScanning,
            lastRun = runner.LastRun,
            filters,
            log = Log,
            avisos = answers.Avisos,
            ledger = runner.Applied.Select(a => new { id = a.Key, a.Value.Status, a.Value.Job, a.Value.At, a.Value.Trail, a.Value.Sent, a.Value.Score }).OrderByDescending(a => a.At),
            answers = answers.Cache.Select(c => new { key = c.Key, c.Value.Label, c.Value.Answer, c.Value.By, c.Value.Reviewed, c.Value.Job }).OrderBy(c => c.Label),
            profile = File.Exists("agent/profile.json") ? JsonNode.Parse(File.ReadAllText("agent/profile.json")) : null,
            resumes = new { pt = Path.GetFileName(EasyApply.ResumeOf("pt")), en = Path.GetFileName(EasyApply.ResumeOf("en")) },
            profileMd = SplitProfile(File.ReadAllText("agent/CLAUDE.md")).Profile,
        };

        window.RegisterWebMessageReceivedHandler((_, raw) =>
        {
            var m = JsonDocument.Parse(raw).RootElement;
            string S(string k) => m.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";

            switch (S("type"))
            {
                case "init":
                    if (claudeOk is null && !answers.NoAi) CheckClaude();
                    break;
                case "switchUser":
                    if (runner.Running || runner.Reading || retrying || !Users.Exists(S("id"))) break;
                    Activate(S("id"));
                    break;
                case "createUser":
                    if (runner.Running || runner.Reading || retrying || S("name").Trim() == "") break;
                    Activate(Users.Create(S("name")));
                    break;
                case "setLang":
                    answers.English = settings["english"] = m.GetProperty("en").GetBoolean();
                    Store.Save("settings.json", settings);
                    break;
                case "setNoAi":
                    if (runner.Running) break; // read by every call of the run: never flip it midway
                    answers.NoAi = settings["noAi"] = m.GetProperty("on").GetBoolean();
                    Store.Save("settings.json", settings);
                    if (!answers.NoAi && claudeOk is null) CheckClaude();
                    break;
                case "installChrome":
                    RunScript(ChromeScript, CheckChrome);
                    break;
                case "installClaude":
                    RunScript(ClaudeInstallScript, CheckClaude);
                    break;
                case "loginClaude":
                    RunScript(ClaudeLoginScript, CheckClaude);
                    break;
                case "recheckChrome":
                    CheckChrome();
                    break;
                case "recheckClaude":
                    CheckClaude();
                    break;
                case "acceptRisk":
                case "acceptDataNotice":
                    settings[S("type") == "acceptRisk" ? "riskAccepted" : "dataNoticeAccepted"] = true;
                    Store.Save("settings.json", settings);
                    break;
                case "deleteUser":
                    // Right to erasure: the whole folder goes (profile, answers, ledger, log, LinkedIn session).
                    var gone = Users.Current;
                    if (runner.Running || runner.Reading || retrying || S("id") != gone) break;
                    var next = Users.List().FirstOrDefault(u => u.Id != gone)?.Id ?? Users.Create("Principal");
                    Activate(next); // leave the folder first: Windows won't delete the working directory
                    Users.Delete(gone);
                    break;
                case "pickResume":
                    var rlang = S("lang") == "en" ? "en" : "pt";
                    var file = window.ShowOpenFile(rlang == "en" ? "Currículo em inglês" : "Currículo em português", null, false,
                        [("Currículo", ["*.pdf", "*.doc", "*.docx"])]).FirstOrDefault();
                    if (string.IsNullOrEmpty(file)) break;
                    // LinkedIn's own limits: anything else would fail only mid-application.
                    if (!Regex.IsMatch(file, @"\.(pdf|docx?)$", RegexOptions.IgnoreCase) || new FileInfo(file).Length > 2 * 1024 * 1024)
                    { AddLog(null, "Currículo não salvo: o LinkedIn aceita só PDF, DOC ou DOCX de até 2 MB"); break; }
                    if (Directory.Exists($"resumes/{rlang}")) Directory.Delete($"resumes/{rlang}", true);
                    Directory.CreateDirectory($"resumes/{rlang}");
                    File.Copy(file, Path.Combine("resumes", rlang, Path.GetFileName(file)));
                    break;
                case "removeResume":
                    var dropLang = S("lang") == "en" ? "en" : "pt";
                    if (Directory.Exists($"resumes/{dropLang}")) Directory.Delete($"resumes/{dropLang}", true);
                    break;
                case "exportUser":
                    // Chrome's profile (the LinkedIn login) and debug dumps stay behind: big, and the session is tied to this PC.
                    if (runner.Running || runner.Reading || retrying) break;
                    var to = window.ShowSaveFile("Exportar perfil", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        $"TaAplicado-{Users.Current}.zip"), [("Zip", ["*.zip"])]);
                    if (string.IsNullOrEmpty(to)) break;
                    try { Users.Export(Users.Current, to); AddLog(null, $"Perfil exportado para {to}"); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { AddLog(null, $"Não deu para exportar: {e.Message}"); }
                    break;
                case "importUser":
                    if (runner.Running || runner.Reading || retrying) break;
                    var from = window.ShowOpenFile("Importar perfil", null, false, [("Zip", ["*.zip"])]).FirstOrDefault();
                    if (string.IsNullOrEmpty(from)) break;
                    try { Activate(Users.Import(from)); AddLog(null, "Perfil importado. Entre no LinkedIn de novo na primeira execução."); }
                    catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { AddLog(null, $"Não deu para importar: {e.Message}"); }
                    break;
                case "importProfile":
                    if (answers.NoAi) return;
                    var forUser = Users.Current;
                    var text = S("text");
                    var fields = m.GetProperty("fields").GetRawText();
                    _ = Task.Run(async () =>
                    {
                        var reply = await ImportProfileAsync(judge, text, fields, answers.English);
                        // Tagged with its remetente: the page drops it if someone else is active by now.
                        window.SendWebMessage(JsonSerializer.Serialize(new { reply, user = forUser }, Store.Json));
                    });
                    return;
                case "importLinkedIn":
                    if (answers.NoAi) break;
                    var who = Users.Current;
                    var url = Runner.ProfileUrl(S("url"));
                    var askFields = m.GetProperty("fields").GetRawText();
                    var reader = runner;
                    _ = Task.Run(async () =>
                    {
                        object reply;
                        if (url is null) reply = new { type = "importResult", error = "Esse link não é de um perfil do LinkedIn. Use o endereço que começa com linkedin.com/in/." };
                        else
                            try
                            {
                                // Generous: the user may have to log in to LinkedIn in the Chrome window first.
                                using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                                var page = await reader.ReadProfileAsync(url, limit.Token);
                                reply = await ImportProfileAsync(judge, $"LinkedIn: {url}" + Environment.NewLine + page, askFields, answers.English);
                            }
                            catch (InvalidOperationException e) { reply = new { type = "importResult", error = e.Message }; }
                            catch (OperationCanceledException) { reply = new { type = "importResult", error = "A leitura do perfil foi interrompida (Chrome fechado ou demorou demais). Tente de novo." }; }
                            catch (Exception e) when (e is Microsoft.Playwright.PlaywrightException or TimeoutException)
                            { reply = new { type = "importResult", error = $"Não consegui abrir o perfil no Chrome: {e.Message.Split('\n')[0]}" }; }
                            // Anything else must still answer, or the page keeps both import buttons disabled forever.
                            catch (Exception e) { reply = new { type = "importResult", error = $"A leitura do perfil falhou: {e.Message.Split('\n')[0]}" }; }
                        window.SendWebMessage(JsonSerializer.Serialize(new { reply, user = who }, Store.Json));
                    });
                    break; // push state so the page sees "reading"
                case "start":
                    if (!settings.GetValueOrDefault("riskAccepted") || !settings.GetValueOrDefault("dataNoticeAccepted")
                        || claudeOk != true && !answers.NoAi || chromeOk != true) break;
                    var opts = new RunOptions(S("mode"), S("terms"),
                        Math.Clamp(m.GetProperty("max").GetInt32(), 1, 100), m.GetProperty("dryRun").GetBoolean(),
                        m.TryGetProperty("ultra", out var ultra) && ultra.GetBoolean(),
                        m.TryGetProperty("minScore", out var min) ? Math.Clamp(min.GetInt32(), 0, 100) : 70);
                    _ = Task.Run(() => runner.RunAsync(opts));
                    break;
                case "saveFilters":
                    // Read at the start of each run, so a change applies from the next run on.
                    List<string> Lines(string k, bool commas) => Filters.Entries(m.GetProperty(k).EnumerateArray().Select(e => e.GetString() ?? ""), commas);
                    string Mode(string k) => S(k) is "prefer" or "only" ? S(k) : "off";
                    filters = new Filters
                    {
                        Companies = Lines("companies", false), Words = Lines("words", true), UseClaude = m.GetProperty("useClaude").GetBoolean(),
                        Country = S("country").Trim(), CountryMode = Mode("countryMode"), Language = S("language").Trim(), LanguageMode = Mode("languageMode"),
                    };
                    Store.Save("filters.json", filters);
                    break;
                case "stop":
                    runner.Stop();
                    break;
                case "stopScanning":
                    runner.StopScanning();
                    break;
                case "answer":
                    lock (Store.Gate) runner.MarkReady(answers.Answer(S("id"), S("answer")));
                    break;
                case "editAnswer":
                    lock (Store.Gate) answers.Edit(S("key"), S("answer"));
                    break;
                case "retryAvisos":
                    if (retrying || answers.NoAi) return;
                    retrying = true;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var ready = await answers.RetryAsync(t => AddLog(null, t));
                            lock (Store.Gate) runner.MarkReady(ready);
                        }
                        catch (ClaudeUnavailableException e) { AddLog(null, $"{e.Message}. Tente mais tarde."); }
                        finally { retrying = false; Push(); }
                    });
                    break;
                case "suggestSearches":
                    if (suggesting || answers.NoAi) return;
                    suggesting = true;
                    var askedFor = Users.Current;
                    var asker = judge;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var found = await SuggestSearchesAsync(asker);
                            // Saved in the remetente's own folder: skip if someone else became active meanwhile.
                            if (found is null) AddLog(null, "Claude indisponível: não deu para sugerir pesquisas. Tente mais tarde.");
                            else if (Users.Current == askedFor) lock (Store.Gate) Store.Save("searches.json", found);
                        }
                        finally { suggesting = false; Push(); }
                    });
                    break;
                case "ask":
                    if (asking || S("text").Trim() == "") return;
                    var askText = S("text");
                    var askedBy = Users.Current;
                    if (answers.NoAi)
                    {
                        List<Asked> found;
                        lock (Store.Gate) found = answers.Lookup(askText);
                        window.SendWebMessage(JsonSerializer.Serialize(new { reply = new { type = "askResult", items = found }, user = askedBy }, Store.Json));
                        return;
                    }
                    asking = true;
                    var asked = claude;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var got = await AskAsync(asked, askText);
                            object reply = got is null
                                ? new { type = "askResult", error = "O Claude não respondeu. Confira se o Claude Code está logado (ou se o limite do plano acabou) e tente de novo." }
                                : new { type = "askResult", got.Items, got.Message };
                            window.SendWebMessage(JsonSerializer.Serialize(new { reply, user = askedBy }, Store.Json));
                        }
                        finally { asking = false; Push(); }
                    });
                    break;
                case "saveAnswer":
                    if (S("label").Trim() == "" || S("answer").Trim() == "") break;
                    lock (Store.Gate) answers.Save(S("label").Trim(), S("answer").Trim());
                    break;
                case "reviewAll":
                    lock (Store.Gate)
                        foreach (var key in answers.Cache.Where(c => c.Value.By != "você" && !c.Value.Reviewed).Select(c => c.Key).ToList())
                            answers.MarkReviewed(key);
                    break;
                case "reviewAnswer":
                    lock (Store.Gate) answers.MarkReviewed(S("key"));
                    break;
                case "deleteAnswer":
                    lock (Store.Gate) answers.Edit(S("key"), null);
                    break;
                case "saveProfile":
                    // The page renders the markdown (wwwroot/profile.js) so the questionnaire lives in one place.
                    File.WriteAllText("agent/profile.json", m.GetProperty("values").GetRawText());
                    WriteProfile(S("markdown"));
                    answers.WritePrompts();
                    break;
                case "openJob":
                    if (Regex.IsMatch(S("id"), @"^(\d+|indeed-[0-9a-f]+)$"))
                        Process.Start(new ProcessStartInfo(Indeed.Is(S("id")) ? Indeed.JobUrl(S("id")) : $"https://www.linkedin.com/jobs/view/{S("id")}/") { UseShellExecute = true });
                    return;
            }
            Push();
        });

        window.Load(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));
        window.WaitForClose();
        runner.Stop();
    }

    // Setup scripts, run in a visible PowerShell window so the person sees every step (and any UAC prompt).
    // The app tests again when the window closes.
    const string ChromeScript = """
        Write-Host 'Instalando o Google Chrome...' -ForegroundColor Cyan
        if (Get-Command winget -ErrorAction SilentlyContinue) {
          winget install -e --id Google.Chrome --accept-source-agreements --accept-package-agreements
        } else {
          Write-Host 'O winget nao existe neste Windows: abrindo a pagina de download do Chrome.'
          Start-Process 'https://www.google.com/chrome/'
        }
        Read-Host 'Pronto. Aperte Enter para fechar; o app testa de novo'
        """;

    const string ClaudeInstallScript = """
        Write-Host 'Instalando o Claude Code (instalador oficial da Anthropic)...' -ForegroundColor Cyan
        if (-not (Get-Command git -ErrorAction SilentlyContinue) -and (Get-Command winget -ErrorAction SilentlyContinue)) {
          Write-Host 'Instalando o Git, que o Claude Code usa no Windows...'
          winget install -e --id Git.Git --accept-source-agreements --accept-package-agreements
        }
        irm https://claude.ai/install.ps1 | iex
        Read-Host 'Pronto. Aperte Enter para fechar; depois clique em Entrar na conta'
        """;

    const string ClaudeLoginScript = """
        $env:Path = [Environment]::GetEnvironmentVariable('Path','Machine') + ';' + [Environment]::GetEnvironmentVariable('Path','User')
        Write-Host 'Entre com a sua conta Claude Pro ou Max no navegador que vai abrir.' -ForegroundColor Cyan
        Write-Host 'Quando o Claude mostrar a caixa de conversa, digite /exit e aperte Enter.' -ForegroundColor Cyan
        claude
        """;

    static void RunScript(string script, Action after)
    {
        // -EncodedCommand (UTF-16 base64): no quoting of the script, accents intact.
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var p = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}") { UseShellExecute = true });
        if (p is not null) _ = p.WaitForExitAsync().ContinueWith(_ => after());
    }

    /// Picks up programs installed after the app started: children (the bridge's cmd.exe) inherit this PATH.
    static void RefreshPath() => Environment.SetEnvironmentVariable("PATH", string.Join(';',
        (Environment.GetEnvironmentVariable("PATH") + ";" + Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)
         + ";" + Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User))
        .Split(';', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase)));

    /// Asks Claude to answer the Remetente questionnaire from free text (resume, LinkedIn export, notes).
    /// Returns { type: "importResult", values } or { type: "importResult", error }.
    static async Task<object> ImportProfileAsync(IClaudeAgentService claude, string text, string fieldsJson, bool english)
    {
        var r = await claude.RunPromptAsync($$"""
            Profile import. Ignore the relevance rules for this task.
            Read the candidate's text below and answer the questionnaire fields.

            Fields (id, label, type, options):
            {{fieldsJson}}

            Candidate text:
            {{text}}

            Rules:
            - Use only facts stated in the text. Leave a field out when the text does not cover it.
            - The text may be copied from LinkedIn pages: use only the candidate's own profile; ignore menus, ads,
              "people also viewed", recommendations of other people and other people's names.
            - "select": the value must be exactly one of the options.
            - "table" (years per skill or tool): one line per skill or tool, formatted "Skill = years".
            - "number": digits only. Answer in {{(english ? "English" : "Portuguese")}}, first person where it is prose.
            Return ONLY a JSON object mapping field id to value, e.g. {"nome":"...","anosDev":"5"}.
            """);
        int s = r.Content?.IndexOf('{') ?? -1, e = r.Content?.LastIndexOf('}') ?? -1;
        if (!r.Success || s < 0 || e <= s)
            return new { type = "importResult", error = "O Claude não respondeu. Confira se o Claude Code está logado e tente de novo." };
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(r.Content![s..(e + 1)])!
                .ToDictionary(kv => kv.Key, kv => kv.Value.ValueKind == JsonValueKind.String ? kv.Value.GetString() ?? "" : kv.Value.ToString());
            return new { type = "importResult", values };
        }
        catch (JsonException)
        {
            return new { type = "importResult", error = "Não consegui ler a resposta do Claude. Tente de novo." };
        }
    }

    /// LinkedIn search terms that fit the candidate profile. Null when Claude gives no usable list.
    static async Task<List<string>?> SuggestSearchesAsync(IClaudeAgentService claude)
    {
        var r = await claude.RunPromptAsync("""
            Search suggestions. Ignore the relevance rules for this task.
            From the candidate profile, suggest 8 LinkedIn job search queries this candidate should run.
            - Each one short (2 to 5 words), written like real job titles recruiters post, e.g. "desenvolvedor .net sênior".
            - Cover the candidate's main stack and seniority first, then close variations (other titles, related roles
              the profile really supports). Portuguese titles, plus English ones only if the profile's English level fits.
            - No boolean operators, quotes or locations.
            Return ONLY a JSON array of strings.
            """);
        int s = r.Content?.IndexOf('[') ?? -1, e = r.Content?.LastIndexOf(']') ?? -1;
        if (!r.Success || s < 0 || e <= s) return null;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(r.Content![s..(e + 1)])!
                .Select(t => t.Trim()).Where(t => t != "").Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
            return list.Count == 0 ? null : list;
        }
        catch (JsonException) { return null; }
    }

    /// Answers questions the user pasted (a recruiter's form, an application outside Easy Apply) from the profile and
    /// saved answers, under the same rules as the forms, plus a reply message ready to paste back (e.g. on LinkedIn).
    /// Null when Claude gives no usable list.
    static async Task<AskResult?> AskAsync(IClaudeAgentService claude, string text)
    {
        var r = await claude.RunPromptAsync($$"""
            Pasted questions. The candidate copied the text below from a job form or a recruiter's message and wants
            each question answered in their name, following your answering rules (facts only, first person,
            in the language of the question, confident:false when the profile and saved answers do not settle it).
            - Find every question in the text, in order; drop numbering and form noise. A sub-item (a, b, c…) is its own
              question, written whole with its parent's context (e.g. "Quanto tempo de experiência em C#?").
            - When the text lists options for a question, the answer must be exactly one of them.
            - Text that is not a question (instructions, requests for documents, headers) is not an item.

            Also write "message": the candidate's reply to send back in the same chat (LinkedIn, e-mail), in the
            language of the text. Greet the sender by name if the text gives it; a short polite opening; then every
            answer under the sender's own numbering and lettering (1a, 1b…), concise; a short closing signed with the
            candidate's first name from the profile. Where the profile and saved answers do not give a fact, put a
            placeholder in square brackets for the candidate to fill (e.g. "[nome e contato do gestor]"); never invent it.
            Never say a document was sent, signed or filled.

            Text:
            {{text}}

            Return ONLY a JSON object, with no text before or after:
            {"message":"...","items":[{"question":"...","answer":"...","confident":true}]}
            """);
        int s = r.Content?.IndexOf('{') ?? -1, e = r.Content?.LastIndexOf('}') ?? -1;
        if (!r.Success || s < 0 || e <= s) return null;
        try
        {
            var got = JsonSerializer.Deserialize<AskResult>(r.Content![s..(e + 1)], Store.Json);
            var items = (got?.Items ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Question)).Select(a => a with { Answer = a.Answer ?? "" }).ToList();
            return items.Count == 0 ? null : new(got!.Message ?? "", items);
        }
        catch (JsonException) { return null; }
    }

    // The profile is the part of agent/CLAUDE.md after "## Candidate profile"; the rules above it stay untouched.
    static void WriteProfile(string profile)
    {
        var rules = SplitProfile(File.ReadAllText("agent/CLAUDE.md")).Rules;
        File.WriteAllText("agent/CLAUDE.md", rules + ProfileMarker + "\n" + profile.Trim() + "\n");
    }

    static (string Rules, string Profile) SplitProfile(string md)
    {
        var i = md.IndexOf(ProfileMarker, StringComparison.Ordinal);
        return i < 0 ? (md.TrimEnd() + "\n\n", "") : (md[..i], md[(i + ProfileMarker.Length)..].Trim());
    }

    static void SelfCheck()
    {
        var (rules, profile) = SplitProfile("# R\nrule\n\n## Candidate profile\n- Name: X\n");
        Trace.Assert(rules == "# R\nrule\n\n" && profile == "- Name: X");
        Trace.Assert(SplitProfile("# only rules").Profile == "");
        Trace.Assert(EasyApply.Lang("About the job. We are looking for a developer with experience in C# and you will join our team") == "en");
        Trace.Assert(EasyApply.Lang("Sobre a vaga. Buscamos uma pessoa desenvolvedora com experiência em C# para a nossa equipe") == "pt");
        Trace.Assert(EasyApply.Lang("") == "pt");
        // Export/import round trip: the data comes back, the Chrome login does not.
        var src = Users.Create("selfcheck");
        var zip = Path.Combine(Path.GetTempPath(), $"{src}.zip");
        File.WriteAllText(Path.Combine(Users.PathOf(src), "agent", "profile.json"), "{}");
        Directory.CreateDirectory(Path.Combine(Users.PathOf(src), "chrome-profile"));
        File.WriteAllText(Path.Combine(Users.PathOf(src), "chrome-profile", "Cookies"), "x");
        Users.Export(src, zip);
        var copy = Users.Import(zip);
        Trace.Assert(copy != src && File.Exists(Path.Combine(Users.PathOf(copy), "agent", "profile.json")));
        Trace.Assert(!Directory.Exists(Path.Combine(Users.PathOf(copy), "chrome-profile")));
        Users.Delete(src); Users.Delete(copy); File.Delete(zip);
    }
}

/// Local people sharing this computer ("remetentes"). No login: a folder per person, picked from a menu.
static class Users
{
    static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LinkedInAutoApply"); // the old name: kept so nobody loses their data
    static string Dir => Path.Combine(Root, "users");
    static string CurrentFile => Path.Combine(Root, "current.txt");

    public record Info(string Id, string Name, int Avisos);

    public static string PathOf(string id) => Path.Combine(Dir, id);
    public static bool Exists(string id) => id != "" && Directory.Exists(PathOf(id));

    public static string Current
    {
        get => File.Exists(CurrentFile) ? File.ReadAllText(CurrentFile).Trim() : "";
        set => File.WriteAllText(CurrentFile, value);
    }

    public static List<Info> List() => Directory.Exists(Dir)
        ? Directory.GetDirectories(Dir).Select(d => new Info(Path.GetFileName(d), NameOf(d), CountAvisos(d))).OrderBy(u => u.Name).ToList()
        : [];

    public static string Create(string name)
    {
        var slug = Answers.Key(name).Replace(' ', '-');
        if (slug == "") slug = "remetente";
        var id = slug;
        for (int i = 2; Directory.Exists(PathOf(id)); i++) id = $"{slug}-{i}";
        Directory.CreateDirectory(Path.Combine(PathOf(id), "agent"));
        File.WriteAllText(Path.Combine(PathOf(id), "user.json"), JsonSerializer.Serialize(new { name = name.Trim() }));
        return id;
    }

    /// Deletes everything of one remetente, plus any Claude Code transcripts kept for its agent folder
    /// (none are written since --no-session-persistence, but older versions of the app did).
    public static void Delete(string id)
    {
        var agent = Path.Combine(PathOf(id), "agent");
        var transcripts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects",
            Regex.Replace(agent, "[^A-Za-z0-9]", "-"));
        if (Directory.Exists(transcripts)) Directory.Delete(transcripts, true);
        foreach (var f in Directory.GetFiles(PathOf(id), "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(PathOf(id), true);
    }

    static bool Portable(string rel) => !rel.StartsWith("chrome-profile") && !rel.StartsWith("debug");

    public static void Export(string id, string zipPath)
    {
        File.Delete(zipPath); // the save dialog already asked before overwriting
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var f in Directory.GetFiles(PathOf(id), "*", SearchOption.AllDirectories))
            if (Path.GetRelativePath(PathOf(id), f) is var rel && Portable(rel))
                zip.CreateEntryFromFile(f, rel.Replace('\\', '/'));
    }

    /// A new remetente from an exported .zip (never overwrites an existing one). Returns its id.
    public static string Import(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.GetEntry("user.json") is null && zip.GetEntry("agent/CLAUDE.md") is null)
            throw new InvalidDataException("esse .zip não é um perfil exportado pelo app");
        string? name = null;
        if (zip.GetEntry("user.json") is { } u)
            using (var s = u.Open())
                try { name = JsonDocument.Parse(s).RootElement.GetProperty("name").GetString(); } catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException) { }
        var id = Create(string.IsNullOrWhiteSpace(name) ? "Importado" : name);
        try
        {
            // Entries that would land outside the folder ("../x") are refused; the Chrome login never comes in.
            foreach (var entry in zip.Entries.Where(e => e.Name != "" && Portable(e.FullName)))
            {
                var target = Path.GetFullPath(Path.Combine(PathOf(id), entry.FullName));
                if (!target.StartsWith(Path.GetFullPath(PathOf(id)) + Path.DirectorySeparatorChar)) throw new InvalidDataException($"caminho inválido no .zip: {entry.FullName}");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, true);
            }
        }
        catch { Delete(id); throw; }
        return id;
    }

    /// One-time move of the old single-user layout (data straight in Root) into users/<id>.
    public static void Migrate()
    {
        if (Directory.Exists(Dir) || !Directory.Exists(Path.Combine(Root, "agent"))) return;
        var id = Create(FirstName(ReadName(Path.Combine(Root, "agent", "profile.json"))) ?? "Principal");
        foreach (var entry in Directory.GetFileSystemEntries(Root))
        {
            var target = Path.Combine(PathOf(id), Path.GetFileName(entry));
            if (entry == Dir || entry == CurrentFile) continue;
            if (Directory.Exists(entry))
            {
                if (Directory.Exists(target)) Directory.Delete(target, true); // Create() made an empty agent/
                Directory.Move(entry, target);
            }
            else File.Move(entry, target, true);
        }
        Current = id;
    }

    // The profile's name wins over the name typed when the remetente was created: no rename screen needed.
    static string NameOf(string dir) =>
        FirstName(ReadName(Path.Combine(dir, "agent", "profile.json")))
        ?? ReadJson(Path.Combine(dir, "user.json"), "name")
        ?? Path.GetFileName(dir);

    static string? ReadName(string profilePath) => ReadJson(profilePath, "nome");

    static string? FirstName(string? full)
    {
        var parts = (full ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : string.Join(' ', parts.Take(2));
    }

    static string? ReadJson(string path, string prop)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var v = doc.RootElement.TryGetProperty(prop, out var p) ? p.GetString() : null;
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }
        catch (JsonException) { return null; }
    }

    static int CountAvisos(string dir)
    {
        try
        {
            var f = Path.Combine(dir, "avisos.json");
            if (!File.Exists(f)) return 0;
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            return doc.RootElement.GetArrayLength();
        }
        catch (JsonException) { return 0; }
    }
}
