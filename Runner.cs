using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace LinkedInAutoApply;

/// Trail = the events of the last attempt; Sent = the receipt of every answer filled in the user's name; Score = Claude's 0-100, when it gave one.
public record Entry(string Status, string Job, DateTime At, List<LogLine>? Trail = null, List<string[]>? Sent = null, int? Score = null);
/// Ultra: vet many jobs first (Claude gives each a score), then apply only to the best Max scoring at least MinScore.
public record RunOptions(string Mode, string Terms, int Max, bool DryRun, bool Ultra = false, int MinScore = 70);

/// Per-user job filters (filters.json), checked before Claude is ever asked.
public class Filters
{
    public List<string> Companies { get; set; } = [];
    public List<string> Words { get; set; } = [];
    /// Off: no Claude relevance check (Claude still answers the forms). Ultra always uses it.
    public bool UseClaude { get; set; } = true;
    /// Where and in what language the jobs should be. Mode: "off", "prefer" (the others go last) or "only" (the others are skipped).
    public string Country { get; set; } = "";
    public string CountryMode { get; set; } = "off";
    public string Language { get; set; } = "";
    public string LanguageMode { get; set; } = "off";
    public string? CountryRule => CountryMode != "off" && Country.Trim() != "" ? Country.Trim() : null;
    public string? LanguageRule => LanguageMode != "off" && Language.Trim() != "" ? Language.Trim() : null;
    /// Some rule puts jobs in two groups, so the run keeps the non-preferred ones for last.
    public bool Prefers => CountryRule is not null && CountryMode == "prefer" || LanguageRule is not null && LanguageMode == "prefer";

    /// Applies the country/language rules to Claude's answers: a skip reason, or null plus whether the job is preferred.
    /// An unanswered question (null) never blocks and never demotes.
    public (string? Skip, bool Preferred) Place(bool? inCountry, bool? inLanguage)
    {
        if (CountryRule is { } c && CountryMode == "only" && inCountry == false) return ($"fora do país ({c})", false);
        if (LanguageRule is { } l && LanguageMode == "only" && inLanguage == false) return ($"outro idioma ({l})", false);
        return (null, !(CountryRule is not null && CountryMode == "prefer" && inCountry == false)
                   && !(LanguageRule is not null && LanguageMode == "prefer" && inLanguage == false));
    }

    /// The reason the job is blocked, or null. job = "Title | Company"; text = the job page.
    /// Case and accents never matter: "estagio" blocks "Estágio".
    public string? Blocked(string job, string text)
    {
        var i = job.LastIndexOf(" | ", StringComparison.Ordinal);
        var company = Fold(Answers.Key(i < 0 ? "" : job[(i + 3)..]));
        // Whole words, so a short entry like "TI" does not swallow "Tivit".
        var c = Companies.FirstOrDefault(c => Fold(Answers.Key(c)) is var k && k != "" && $" {company} ".Contains($" {k} "));
        if (c is not null) return $"empresa ignorada ({c.Trim()})";
        var page = Fold(job + "\n" + text);
        var w = Words.FirstOrDefault(w => w.Trim() != "" &&
            Regex.IsMatch(page, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(Fold(w.Trim()))}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase));
        return w is null ? null : $"palavra ignorada ({w.Trim()})";
    }

    static string Fold(string s) => string.Concat(s.Normalize(NormalizationForm.FormD)
        .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark));

    /// Trimmed entries without duplicates. Words split on lines, commas and semicolons; companies only on
    /// lines, since names carry commas ("Acme, Inc." must not turn into an "Inc" block).
    public static List<string> Entries(IEnumerable<string> raw, bool commas = true) => raw
        .SelectMany(r => r.Split(commas ? ['\n', ',', ';'] : ['\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static void SelfCheck()
    {
        var f = new Filters { Companies = ["acme"], Words = ["Estágio", "C#"] };
        Trace.Assert(f.Blocked("Dev | Acme Corp", "") == "empresa ignorada (acme)");
        Trace.Assert(f.Blocked("Dev .NET | Other", "vaga de estágio remota")!.StartsWith("palavra"));
        Trace.Assert(f.Blocked("Dev | Other", "C# e .NET") == "palavra ignorada (C#)");
        Trace.Assert(f.Blocked("Dev | Other", "estagiário") is null); // whole words only
        Trace.Assert(f.Blocked("Acme dev", "") is null);             // company is after " | "
        Trace.Assert(new Filters { Words = ["estagio"] }.Blocked("Estágio .NET | X", "") is not null); // accents folded
        Trace.Assert(new Filters { Companies = ["Itaú"] }.Blocked("Dev | Itau Unibanco", "") is not null);
        Trace.Assert(Entries(["estágio, PHP; presencial", "PHP", " "]) is ["estágio", "PHP", "presencial"]);
        Trace.Assert(Entries(["Acme, Inc.\nOutra"], commas: false) is ["Acme, Inc.", "Outra"]);
        Trace.Assert(new Filters { Companies = ["TI"] }.Blocked("Dev | Tivit", "") is null);      // whole words only
        Trace.Assert(new Filters { Companies = ["TI"] }.Blocked("Dev | TI Solutions", "") is not null);
        Trace.Assert(new Filters { Companies = ["Acme, Inc."] }.Blocked("Dev | ACME Inc", "") is not null);
        var geo = new Filters { Country = "Brasil", CountryMode = "only", Language = "Português", LanguageMode = "prefer" };
        Trace.Assert(geo.Place(false, true) == ("fora do país (Brasil)", false));
        Trace.Assert(geo.Place(true, false) == (null, false));   // other language: kept, but goes last
        Trace.Assert(geo.Place(null, null) == (null, true));     // unknown never blocks
        Trace.Assert(new Filters { CountryMode = "only" }.Place(false, false) == (null, true)); // no country typed = off
        Trace.Assert(Runner.ProfileUrl("linkedin.com/in/fulano-de-tal?utm=x") == "https://www.linkedin.com/in/fulano-de-tal/");
        Trace.Assert(Runner.ProfileUrl("https://br.linkedin.com/in/fulano/") == "https://www.linkedin.com/in/fulano/");
        Trace.Assert(Runner.ProfileUrl("https://evil.com/linkedin.com/in/fulano") is null);
        Trace.Assert(Runner.ProfileUrl("https://www.linkedin.com/company/acme") is null);
    }
}

/// One dispatch: jobs whose avisos were answered ("ready") first, then the list pages.
/// Emits log lines and "changed" so the window can refresh.
public class Runner(Answers answers, Action<string?, string> log, Action changed)
{
    public Dictionary<string, Entry> Applied { get; } = Store.Load<Dictionary<string, Entry>>("applied.json");
    public string? CurrentJob { get; private set; }
    /// Options of the run in progress (null when idle): the UI shows whether it is a test or real.
    public RunOptions? Options { get; private set; }
    /// What the user must do in the Chrome window right now (log in, solve a check), or null.
    public string? Attention { get; private set; }
    void Attend(string? text) { Attention = text; changed(); }
    /// Real Chrome without Playwright's automation flags: navigator.webdriver stays false and the "controlled by
    /// automated software" bar is gone, which is what Cloudflare (Indeed) keys on first. Challenges still go to the user.
    static BrowserTypeLaunchPersistentContextOptions ChromeOptions() => new()
    {
        Channel = "chrome", Headless = false, ViewportSize = ViewportSize.NoViewport,
        IgnoreDefaultArgs = ["--enable-automation"], Args = ["--disable-blink-features=AutomationControlled"],
    };
    /// Ultra progress while it runs ("analisando 42 de 125").
    public string? Phase { get; private set; }
    /// What the last run did, kept per user: jobs each filter entry skipped, and the Ultra summary (Ultra runs only).
    public LastRunInfo? LastRun { get; private set; } = File.Exists("last-run.json") ? Store.Load<LastRunInfo>("last-run.json") : null;
    /// Max/Counts/End/EndedAt: the receipt for "what happened while I was away". Counts by kind (applied, dry-run,
    /// waiting, skip, failed); End says why the run stopped.
    public record LastRunInfo(DateTime At, bool DryRun, Dictionary<string, int> Blocked, string? Ultra,
        int Max = 0, Dictionary<string, int>? Counts = null, string? End = null, DateTime? EndedAt = null)
    {
        public LastRunInfo() : this(default, false, [], null) { }
    }
    /// When the next job opens (the pause between applications), for the live countdown.
    public DateTime? NextAt { get; private set; }
    string? _stopReason; // set right before a cancel that isn't the user's Parar
    public bool Running => _cts is not null;
    public bool Stopping => _cts?.IsCancellationRequested == true;
    /// Reading a LinkedIn profile for the import (uses the same Chrome profile, so no run meanwhile).
    public bool Reading => _readCts is not null;
    /// Ultra only: true while scanning, so the UI can offer "parar a análise e aplicar já".
    public bool CanStopScanning => _scanStop is not null;

    CancellationTokenSource? _cts, _readCts, _scanStop;
    int _failStreak;
    /// Claude's score per job in this run: Ultra and deferred jobs are applied later without asking again.
    readonly Dictionary<string, int> _scores = [];

    public void Stop() { _stopReason ??= "você parou"; _cts?.Cancel(); changed(); }
    /// Ultra only: stop scanning more jobs and move straight to applying to whichever already qualified.
    public void StopScanning() { _scanStop?.Cancel(); changed(); }

    /// Called after the user answers avisos: those jobs go back to the queue.
    public void MarkReady(IEnumerable<string> jobIds)
    {
        foreach (var id in jobIds)
            if (Applied.TryGetValue(id, out var e) && e.Status == "waiting")
                Applied[id] = e with { Status = "ready" };
        Store.Save("applied.json", Applied);
    }

    public async Task RunAsync(RunOptions o)
    {
        if (Running || Reading) return;
        if (answers.NoAi && o.Ultra) { log(null, "A Ultra seleção precisa da IA para dar notas: desligue o modo sem IA ou use a execução normal"); return; }
        _cts = new();
        _failStreak = 0;
        _scores.Clear();
        _stopReason = null;
        Options = o;
        SaveLastRun(new(DateTime.Now, o.DryRun, [], null, o.Max, [])); // a new run replaces the old receipt, interrupted or not
        string? end = null;
        var ct = _cts.Token;
        changed();
        try
        {
            using var pw = await Playwright.CreateAsync();
            await using var ctx = await pw.Chromium.LaunchPersistentContextAsync(Path.GetFullPath("chrome-profile"),
                ChromeOptions());
            ctx.Close += (_, _) => { log(null, "Chrome fechado"); _stopReason ??= "Chrome fechado"; _cts?.Cancel(); };
            var page = ctx.Pages.FirstOrDefault() ?? await ctx.NewPageAsync();
            log(null, (o.DryRun ? "Execução em teste iniciada (nada é enviado)" : "Execução iniciada (envio de verdade)") + (answers.NoAi ? ", modo sem IA" : ""));
            var indeed = o.Mode == "indeed";
            // Each run works one site; "Só a fila" takes both, so it logs into whichever its jobs need.
            bool Mine(string id) => o.Mode == "queue" || Indeed.Is(id) == indeed;
            List<string> queued;
            lock (Store.Gate) queued = Applied.Where(a => a.Value.Status == "ready" && Mine(a.Key)).Select(a => a.Key).ToList();
            if (!indeed && (o.Mode != "queue" || queued.Any(id => !Indeed.Is(id)))) await EnsureLoggedInAsync(ctx, page, ct);
            if (indeed || queued.Any(Indeed.Is)) await Indeed.EnsureLoggedInAsync(page, Attend, t => log(null, t), ct);
            var filters = Store.Load<Filters>("filters.json");
            if (!filters.UseClaude && !o.Ultra && !answers.NoAi) log(null, "Filtro do Claude desligado: só empresas e palavras ignoradas filtram as vagas");

            int done = 0;
            done += await ApplyAllAsync(page, queued, o, done, filters, ct);
            // "Só a fila": the jobs whose avisos were answered, nothing else.
            if (o.Mode == "queue")
            {
                end = ct.IsCancellationRequested ? _stopReason ?? "interrompida" : "fila concluída";
                log(null, ct.IsCancellationRequested ? "Execução interrompida" : $"Fila concluída: {Answers.Plural(done, "tentativa", "tentativas")}");
                return;
            }

            // A failed attempt (a stuck step, a modal that didn't open) never actually applied: worth trying again,
            // every run, before spending the budget on jobs never attempted at all.
            List<string> retry;
            lock (Store.Gate) retry = Applied.Where(a => a.Value.Status.StartsWith("failed") && Mine(a.Key)).Select(a => a.Key).ToList();
            if (retry.Count > 0) log(null, $"{Answers.Plural(retry.Count, "vaga que falhou antes", "vagas que falharam antes")}: tentando de novo");
            done += await ApplyAllAsync(page, retry, o, done, filters, ct);

            // "Recommended" = a search with no keywords: LinkedIn still picks jobs for the profile, but unlike
            // /jobs/collections/recommended/ (which ignores f_AL) it honors the Easy Apply filter.
            // "Preferências"/"Seu perfil" = sections of LinkedIn's jobs home, found by their title (no guessed URL).
            var listUrl = o.Mode switch
            {
                "indeed" => Indeed.ListUrl(o.Terms),
                "search" => $"https://www.linkedin.com/jobs/search/?keywords={Uri.EscapeDataString(o.Terms)}&f_AL=true&",
                "preferences" => await SectionUrlAsync(page, "based on your preferences|com base nas suas prefer"),
                "profile" => await SectionUrlAsync(page, "match your profile|correspondem ao seu perfil|combinam com (o )?seu perfil"),
                _ => "https://www.linkedin.com/jobs/search/?f_AL=true&",
            };

            // LinkedIn itself can narrow the list to the country.
            if (!indeed && filters.CountryRule is { } only && filters.CountryMode == "only")
                listUrl += $"location={Uri.EscapeDataString(only)}&";

            if (o.Ultra) done += await UltraAsync(ctx, page, listUrl, o, done, filters, ct);
            else
            {
                var later = new List<string>(); // outside the country/language priority: only if the list runs out first
                await foreach (var fresh in FreshIdsAsync(ctx, page, listUrl, ct))
                {
                    done += await ApplyAllAsync(page, fresh, o, done, filters, ct, later);
                    if (done >= o.Max) break;
                }
                if (later.Count > 0 && done < o.Max && !ct.IsCancellationRequested)
                {
                    log(null, $"Lista acabou: agora as {Answers.Plural(later.Count, "vaga", "vagas")} fora da prioridade de país/idioma");
                    done += await ApplyAllAsync(page, later, o, done, null, ct);
                }
            }
            end = ct.IsCancellationRequested ? _stopReason ?? "interrompida" : done >= o.Max ? $"chegou ao máximo ({o.Max})" : "a lista de vagas acabou";
            log(null, ct.IsCancellationRequested ? "Execução interrompida" : $"Execução concluída: {Answers.Plural(done, "tentativa", "tentativas")}");
        }
        catch (OperationCanceledException) { end = _stopReason ?? "interrompida"; log(null, "Execução interrompida"); }
        catch (EasyApplyLimitException e)
        {
            end = "limite diário do LinkedIn";
            log(null, $"{e.Message}. Execução parada; tente amanhã.");
        }
        catch (ClaudeUnavailableException e)
        {
            end = "Claude indisponível (limite de uso?)";
            log(null, $"{e.Message}. Execução parada; a vaga atual fica para a próxima. Tente de novo mais tarde.");
        }
        catch (Exception e) when (e is PlaywrightException or TimeoutException)
        {
            end = "erro no navegador";
            log(null, $"Erro no navegador: {e.Message.Split('\n')[0]}");
        }
        finally
        {
            if (LastRun is not null) SaveLastRun(LastRun with { End = end ?? _stopReason ?? "interrompida", EndedAt = DateTime.Now });
            _cts = null; CurrentJob = null; Attention = null; Options = null; Phase = null; NextAt = null;
            changed();
        }
    }

    /// Ultra mode: vet up to 5×Max jobs, then apply only to the best Max with a score of at least o.MinScore.
    /// Vetted-but-not-chosen jobs are not recorded, so a later run can still pick them.
    // ponytail: fixed 5× pool capped at 150 jobs; expose it if the user wants to tune.
    async Task<int> UltraAsync(IBrowserContext ctx, IPage page, string listUrl, RunOptions o, int done, Filters filters, CancellationToken ct)
    {
        var target = Math.Min(o.Max * 5, 150);
        var pool = new List<(string Id, string Job, int Score, bool Preferred)>();
        int scanned = 0;
        log(null, $"Ultra seleção: analisando até {target} vagas para enviar só as {o.Max} melhores (nota ≥ {o.MinScore})");
        int Qualified() => pool.Count(p => p.Score >= o.MinScore);
        // A separate, softer stop: unlike Parar (ct), this ends only the scanning and moves straight to applying
        // whatever already qualified, instead of discarding the pool.
        _scanStop = new CancellationTokenSource();
        var stoppedEarly = false;
        var claudeOut = false;
        try
        {
            await foreach (var fresh in FreshIdsAsync(ctx, page, listUrl, ct))
            {
                foreach (var id in fresh)
                {
                    if (scanned >= target || ct.IsCancellationRequested || _scanStop.IsCancellationRequested) break;
                    if (pool.Any(p => p.Id == id)) continue; // recommended lists repeat across pages
                    scanned++;
                    Phase = $"analisando {scanned} de {target} · {Qualified()} com nota ≥ {o.MinScore}";
                    CurrentJob = id; changed();
                    var trail = new List<LogLine>();
                    void Log(string m) { trail.Add(new(id, m, DateTime.Now)); log(id, m); }
                    var (status, job, score, preferred) = Indeed.Is(id)
                        ? await Indeed.VetAsync(page, id, answers, filters, true, Log, Attend, ct)
                        : await EasyApply.VetAsync(page, id, answers, filters, true, Log);
                    if (score is { } n) _scores[id] = n;
                    // A contender's form is answered right away (a test pass, nothing sent), while Claude still has quota:
                    // the answers land in the cache, so applying later needs no Claude at all.
                    if (status == "" && score >= o.MinScore)
                    {
                        Log("Preparando as respostas do formulário");
                        (status, _, _) = Indeed.Is(id)
                            ? await Indeed.RunAsync(page, id, answers, null, true, Log, [], Attend, ct)
                            : await EasyApply.RunAsync(page, id, answers, null, true, Log, []);
                        if (status == "dry-run") status = "";
                    }
                    if (status == "") { pool.Add((id, job, score ?? 0, preferred)); Log(score >= o.MinScore ? $"Na disputa: nota {score}, respostas prontas" : $"Fora da disputa: nota {score}"); }
                    else Record(id, status, job, trail, [], o.DryRun); // "waiting": avisos to answer, applied on a later run
                    await Task.Delay(Random.Shared.Next(3000, 6000), ct);
                }
                if (scanned >= target || _scanStop.IsCancellationRequested) break;
            }
            ct.ThrowIfCancellationRequested();
            stoppedEarly = _scanStop.IsCancellationRequested;
        }
        catch (ClaudeUnavailableException e)
        {
            // The qualified jobs already have every answer cached: apply to them instead of throwing the pool away.
            log(null, $"{e.Message}. Análise parada: aplicando às vagas com respostas prontas");
            claudeOut = true;
        }
        catch (Exception e) when (e is OperationCanceledException or EasyApplyLimitException)
        {
            // Stopped mid-analysis: say so, instead of leaving the previous run's numbers on screen.
            SaveLastRun(LastRun! with { Ultra = $"interrompida em {scanned} de {target} · {Qualified()} com nota ≥ {o.MinScore}; nada enviado" });
            throw;
        }
        finally { _scanStop = null; }
        CurrentJob = null;
        if (stoppedEarly) log(null, "Análise interrompida a pedido: aplicando às vagas já qualificadas");
        // Country/language priority first, then the score.
        var best = pool.Where(p => p.Score >= o.MinScore).OrderByDescending(p => p.Preferred).ThenByDescending(p => p.Score).Take(o.Max).ToList();
        var text = $"{Answers.Plural(scanned, "analisada", "analisadas")}, {pool.Count} no perfil, {best.Count} com nota ≥ {o.MinScore}"
            + (best.Count > 0 ? $" (notas {best.Min(p => p.Score)} a {best.Max(p => p.Score)})" : "") // not ends: priority comes before score
            + (best.Count >= o.Max ? "" : claudeOut ? $"; menos que {o.Max}: o Claude esgotou durante a análise" : stoppedEarly ? $"; menos que {o.Max}: análise interrompida antes de completar" : $"; menos que {o.Max} porque as outras ficaram abaixo da nota");
        SaveLastRun(LastRun! with { Ultra = text });
        log(null, $"Ultra seleção: {text}");
        Phase = best.Count == 1 ? "enviando a 1 escolhida" : $"enviando as {best.Count} escolhidas";
        changed();
        return await ApplyAllAsync(page, best.Select(b => b.Id).ToList(), o, done, null, ct);
    }

    /// Opens linkedin.com/jobs and takes the "Show all" link of the section whose title matches: a
    /// /jobs/search-results/ search LinkedIn builds from the preferences/profile. Returns it ready for "start=",
    /// with the Easy Apply filter. Throws (with a debug dump) when the section isn't on the page.
    async Task<string> SectionUrlAsync(IPage page, string title)
    {
        await page.GotoAsync("https://www.linkedin.com/jobs/");
        await page.WaitForTimeoutAsync(4000);
        var href = await page.EvaluateAsync<string?>("""
            title => {
              const re = new RegExp(title, 'i');
              const head = [...document.querySelectorAll('h2,h3')].find(e => re.test(e.innerText || ''));
              for (let e = head; e; e = e.parentElement) {
                const a = [...e.querySelectorAll('a[href]')].find(x => /show all|mostrar tud|exibir tud|ver tud/i.test(x.innerText || ''));
                if (a) return a.href;
              }
              return null;
            }
            """, title);
        if (href is null)
        {
            Directory.CreateDirectory("debug");
            await File.WriteAllTextAsync("debug/jobs-home.html", await page.ContentAsync());
            await page.ScreenshotAsync(new() { Path = "debug/jobs-home.png" });
            throw new PlaywrightException("seção de vagas não encontrada na página do LinkedIn (salva em debug/jobs-home)");
        }
        // currentJobId and the landing ids pin the first cards; start comes from the paging.
        var url = new UriBuilder(href);
        var q = System.Web.HttpUtility.ParseQueryString(url.Query);
        foreach (var k in new[] { "currentJobId", "originToLandingJobPostings", "start" }) q.Remove(k);
        q["f_AL"] = "true";
        url.Query = q.ToString();
        log(null, $"Lista da seção: {q["keywords"]}");
        return url.Uri.AbsoluteUri + "&";
    }

    /// Pages through the job list, yielding the ids not yet in the ledger. Ends when the list runs out.
    async IAsyncEnumerable<List<string>> FreshIdsAsync(IBrowserContext ctx, IPage page, string listUrl, [EnumeratorCancellation] CancellationToken ct)
    {
        var seen = new HashSet<string>();
        for (int start = 0; !ct.IsCancellationRequested;)
        {
            await page.GotoAsync(listUrl + "start=" + start);
            if (listUrl.Contains("indeed.com"))
            {
                await page.WaitForTimeoutAsync(3000);
                var (found, any) = await Indeed.PageIdsAsync(page, Attend, ct);
                // Past the last page Indeed serves the last one again.
                if (!any || found.Count > 0 && found.All(seen.Contains)) { log(null, "Não há mais vagas nesta lista do Indeed"); break; }
                seen.UnionWith(found);
                start += 10;
                List<string> mine;
                lock (Store.Gate) mine = found.Where(id => !Applied.ContainsKey(id)).ToList();
                yield return mine;
                continue;
            }
            if (!await HasSessionAsync(ctx)) { await EnsureLoggedInAsync(ctx, page, ct); continue; } // session expired mid-run
            await page.WaitForTimeoutAsync(3000);

            var ids = await page.EvaluateAsync<string[]>("""
                () => [...new Set([...document.querySelectorAll("[data-occludable-job-id],[data-job-id],[componentkey^='job-card-component-ref-']")]
                  .map(e => e.getAttribute('data-occludable-job-id') || e.getAttribute('data-job-id') || e.getAttribute('componentkey').slice(23)))]
                  .filter(id => /^\d+$/.test(id))
                """);
            if (ids.Length == 0)
            {
                // Dump what LinkedIn served so the selector can be fixed against the real markup.
                Directory.CreateDirectory("debug");
                await File.WriteAllTextAsync($"debug/list-{start}.html", await page.ContentAsync());
                await page.ScreenshotAsync(new() { Path = $"debug/list-{start}.png" });
                log(null, $"Não há mais vagas nesta lista ({page.Url})");
                break;
            }
            start += ids.Length;

            List<string> fresh;
            lock (Store.Gate) fresh = ids.Where(id => !Applied.ContainsKey(id)).ToList();
            yield return fresh;
        }
    }

    /// filters null = the jobs were already vetted (ultra picks, deferred jobs): no filter or relevance check again.
    /// later: where jobs outside the country/language priority go instead of being applied now (null = apply them).
    async Task<int> ApplyAllAsync(IPage page, List<string> ids, RunOptions o, int done, Filters? filters, CancellationToken ct, List<string>? later = null)
    {
        int sent = 0;
        foreach (var id in ids)
        {
            if (done + sent >= o.Max || ct.IsCancellationRequested) break;
            // The recommended list repeats jobs across pages; one already set aside must not be vetted (or later applied) twice.
            if (later?.Contains(id) == true) continue;
            if (_failStreak >= 5)
            {
                log(null, "5 falhas seguidas: o LinkedIn pode ter mudado a página. Execução parada.");
                _stopReason ??= "5 falhas seguidas";
                _cts?.Cancel();
                break;
            }
            CurrentJob = id; changed();

            var trail = new List<LogLine>();
            var receipt = new List<string[]>();
            void Log(string m) { trail.Add(new(id, m, DateTime.Now)); log(id, m); }

            var defer = later is not null && filters?.Prefers == true;
            var (status, job, score) = Indeed.Is(id)
                ? await Indeed.RunAsync(page, id, answers, filters, o.DryRun, Log, receipt, Attend, ct, defer)
                : await EasyApply.RunAsync(page, id, answers, filters, o.DryRun, Log, receipt, defer);
            if (score is { } n) _scores[id] = n;
            if (status == "defer")
            {
                later!.Add(id);
                await Task.Delay(Random.Shared.Next(3000, 6000), ct);
                continue;
            }

            // Max counts every real attempt (failures too), otherwise a broken flow never stops.
            if (!status.StartsWith("skip")) sent++;
            _failStreak = status.StartsWith("failed") ? _failStreak + 1 : 0;
            Record(id, status, job, trail, receipt, o.DryRun);

            // ponytail: fixed human-ish delays; LinkedIn can still flag the account, so keep Max low.
            var wait = status == "applied" ? Random.Shared.Next(10_000, 30_000) : Random.Shared.Next(3000, 6000);
            NextAt = DateTime.Now.AddMilliseconds(wait); changed(); // the window counts it down live
            try { await Task.Delay(wait, ct); } finally { NextAt = null; }
        }
        CurrentJob = null;
        return sent;
    }

    /// Logs the outcome (with its reason) and writes it to the ledger. Tests keep only "waiting".
    void Record(string id, string status, string job, List<LogLine> trail, List<string[]> receipt, bool dryRun)
    {
        var text = status switch
        {
            "applied" => "Enviada",
            "dry-run" => "Teste concluído, candidatura descartada",
            "waiting" => "Aguardando você: há avisos para responder",
            _ when status.StartsWith("skip: not relevant") => $"Pulada: fora do perfil ({status[19..].Trim()})",
            _ when status.StartsWith("skip: blocked") => $"Pulada: {status[14..].Trim()}",
            _ when status.StartsWith("skip") => Indeed.Is(id) ? "Pulada: sem candidatura simplificada" : "Pulada: sem Easy Apply",
            _ => $"Falhou: {status[(status.IndexOf(':') + 1)..].Trim()}",
        };
        trail.Add(new(id, text, DateTime.Now));
        log(id, text);
        if (LastRun is not null)
        {
            var k = status.StartsWith("skip") ? "skip" : status.StartsWith("failed") ? "failed" : status;
            SaveLastRun(LastRun with { Counts = new(LastRun.Counts ?? []) { [k] = (LastRun.Counts?.GetValueOrDefault(k) ?? 0) + 1 } });
        }
        if (status.StartsWith("skip: blocked") && LastRun is not null)
        {
            // "skip: blocked: palavra ignorada (PHP)" → counted under "PHP"
            var entry = Regex.Match(status, @"\((.*)\)$").Groups[1].Value;
            SaveLastRun(LastRun with { Blocked = new(LastRun.Blocked) { [entry] = LastRun.Blocked.GetValueOrDefault(entry) + 1 } });
        }
        if (status.StartsWith("skip"))
            lock (Store.Gate) answers.DropAvisos(id); // a skipped job's pending questions are moot
        if (!dryRun && status != "dry-run" || status == "waiting")
            lock (Store.Gate)
            {
                Applied[id] = new(status, job, DateTime.Now, trail, receipt,
                    _scores.TryGetValue(id, out var n) ? n : Applied.GetValueOrDefault(id)?.Score); // a retry without Claude keeps the old score
                Store.Save("applied.json", Applied);
            }
        changed();
    }

    /// "https://www.linkedin.com/in/fulano/?x" → "https://www.linkedin.com/in/fulano/", or null for anything else.
    public static string? ProfileUrl(string url)
    {
        var m = Regex.Match(url.Trim(), @"^(?:https?://)?(?:[a-z]{2,3}\.)?linkedin\.com/in/([A-Za-z0-9\-_%]{3,100})/?(?:[?#].*)?$", RegexOptions.IgnoreCase);
        return m.Success ? $"https://www.linkedin.com/in/{m.Groups[1].Value}/" : null;
    }

    /// Reads a LinkedIn profile (main page plus the full detail lists) in the app's Chrome, logged in with the
    /// user's session. Returns the visible text, for the same Claude import as pasted text.
    public async Task<string> ReadProfileAsync(string profileUrl, CancellationToken ct)
    {
        if (Running || Reading) throw new InvalidOperationException("O Chrome do app está em uso. Pare a execução e tente de novo.");
        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct); // a run must not open the same Chrome profile meanwhile
        changed();
        try
        {
            using var pw = await Playwright.CreateAsync();
            await using var ctx = await pw.Chromium.LaunchPersistentContextAsync(Path.GetFullPath("chrome-profile"),
                ChromeOptions());
            ctx.Close += (_, _) => _readCts?.Cancel();
            var page = ctx.Pages.FirstOrDefault() ?? await ctx.NewPageAsync();
            await EnsureLoggedInAsync(ctx, page, _readCts.Token);

            var text = new System.Text.StringBuilder();
            foreach (var part in new[] { "", "details/experience/", "details/education/", "details/skills/", "details/certifications/", "details/languages/" })
            {
                _readCts.Token.ThrowIfCancellationRequested();
                await page.GotoAsync(profileUrl + part);
                await page.WaitForTimeoutAsync(2500);
                // Lists load as you scroll; a few steps down bring in the rest.
                for (int i = 0; i < 4; i++) { await page.Mouse.WheelAsync(0, 2500); await page.WaitForTimeoutAsync(700); }
                if (part != "" && !page.Url.Contains("/details/")) continue; // section absent: LinkedIn redirects to the profile
                text.AppendLine($"=== {(part == "" ? "Perfil" : part.Split('/')[1])} ===");
                text.AppendLine(await page.Locator("main").First.InnerTextAsync());
            }
            // ponytail: plain cap; the full detail pages of a long career stay well under it.
            return text.Length > 30_000 ? text.ToString(0, 30_000) : text.ToString();
        }
        finally { _readCts = null; Attention = null; changed(); }
    }

    void SaveLastRun(LastRunInfo info)
    {
        LastRun = info;
        lock (Store.Gate) Store.Save("last-run.json", info);
    }

    // LinkedIn serves the signup wall on the same URL as the job list, so the session cookie is the only reliable signal.
    static async Task<bool> HasSessionAsync(IBrowserContext ctx) =>
        (await ctx.CookiesAsync(["https://www.linkedin.com"])).Any(c => c.Name == "li_at");

    async Task EnsureLoggedInAsync(IBrowserContext ctx, IPage page, CancellationToken ct)
    {
        if (await HasSessionAsync(ctx)) return;
        Attend("Entre no LinkedIn na janela do Chrome. A execução continua sozinha depois do login.");
        log(null, "Entre no LinkedIn na janela do Chrome");
        await page.GotoAsync("https://www.linkedin.com/login");
        while (!await HasSessionAsync(ctx)) await Task.Delay(2000, ct);
        Attend(null);
        log(null, "Login feito, sessão salva");
    }
}
