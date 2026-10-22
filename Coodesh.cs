using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;

namespace LinkedInAutoApply;

/// Applies through Coodesh's "Tenho Interesse": the preferences modal (only the salary is usually missing), then the
/// ScoreCard self-assessment in a new tab. Map of the pages: docs/coodesh.md.
/// Ledger ids are "coodesh-<slug>": the site 404s without the slug, so the id keeps it ("coodesh-desenvolvedor-rpa-191038516").
public static class Coodesh
{
    public const string Prefix = "coodesh-";
    public static bool Is(string id) => id.StartsWith(Prefix);
    public static string JobUrl(string id) => $"https://coodesh.com/pt/jobs/{id[Prefix.Length..]}";
    /// No terms = the whole list (~100 jobs picked for the profile); the search narrows it a lot.
    public static string ListUrl(string terms) => terms.Trim() == "" ? "https://coodesh.com/jobs" : $"https://coodesh.com/jobs?search={Uri.EscapeDataString(terms.Trim())}";

    static readonly string[] Levels = ["Nenhum", "Básico", "Praticante", "Avançado"];
    /// The same label on the job page (tags) and on the ScoreCard, so a level answered once is reused by every job.
    static string LevelLabel(string skill) => $"Nível de domínio em {skill} (autoavaliação)";

    /// Every job of the list: it pages with a button, not the URL, and opening a job loses the page, so all pages are read up front.
    /// Each job with its card text (title, company, category, location), for the title triage.
    public static async Task<List<(string Id, string Card)>> AllIdsAsync(IPage page, string listUrl)
    {
        await page.GotoAsync(listUrl);
        try { await page.Locator("[data-testid=table-total]").WaitForAsync(new() { Timeout = 20_000 }); }
        catch (TimeoutException) { await EasyApply.DumpAsync(page, "coodesh-list"); return []; }
        await page.WaitForTimeoutAsync(1500);
        var ids = new List<(string Id, string Card)>();
        for (int p = 0; p < 30; p++) // ponytail: 30 pages cap; the list tops at ~100 jobs
        {
            var found = await page.EvaluateAsync<string[][]>("""
                () => [...document.querySelectorAll('a[aria-label="Ver vaga"][href*="/jobs/"]')].map(a =>
                  [a.getAttribute('href').split('?')[0].split('/').pop(), (a.closest('tr')?.cells[0] || a).innerText.trim()])
                """);
            // logo and title both link the job
            var fresh = found.Select(f => (Id: Prefix + f[0], Card: f[1])).DistinctBy(f => f.Id).Where(f => !ids.Any(i => i.Id == f.Id)).ToList();
            if (fresh.Count == 0) break;
            ids.AddRange(fresh);
            var next = page.Locator("button[aria-label='Próxima página']");
            if (await next.CountAsync() == 0 || await next.IsDisabledAsync()) break;
            await next.ClickAsync();
            await page.WaitForTimeoutAsync(2000);
        }
        return ids;
    }

    /// Logged in when the dashboard shows the candidate menu ("Candidaturas").
    public static async Task EnsureLoggedInAsync(IPage page, Action<string?> attention, Action<string> log, CancellationToken ct)
    {
        await page.GotoAsync("https://coodesh.com/dashboard");
        var menu = page.Locator("a[href$='/dashboard/applicants']");
        try { await menu.First.WaitForAsync(new() { Timeout = 15_000 }); return; } catch (TimeoutException) { }
        attention("Entre no Coodesh na janela do Chrome. A execução continua sozinha depois do login.");
        log("Entre no Coodesh na janela do Chrome");
        // ponytail: polls every 5 s, like the Indeed login.
        while (await menu.CountAsync() == 0)
        {
            await Task.Delay(5000, ct);
            if (page.Url.TrimEnd('/') == "https://coodesh.com") await page.GotoAsync("https://coodesh.com/dashboard");
        }
        attention(null);
        log("Login no Coodesh feito, sessão salva");
    }

    static ILocator Interest(IPage page) => page.Locator("button[type=submit]", new() { HasTextString = "Tenho Interesse" }).First;
    /// "Visualizar" in place of "Tenho Interesse": the user already applied (by hand or in an earlier run).
    static ILocator Applied(IPage page) => page.Locator("button", new() { HasTextRegex = new("^\\s*Visualizar\\s*$") });

    /// Title, description and the skill tags. The page renders logged-out first; the button shows once the session loads.
    static async Task<(string Job, string Description, string[] Skills)> OpenAsync(IPage page, string id, Action<string> log)
    {
        await page.GotoAsync(JobUrl(id));
        // Some jobs make "Tenho Interesse" a link to an outside form (forms.monday.com), and an applied job shows "Visualizar":
        // wait for any of them, the vet sorts them out.
        try { await page.Locator("a, button", new() { HasTextRegex = new("^\\s*(Tenho Interesse|Visualizar)\\s*$") }).First.WaitForAsync(new() { Timeout = 15_000 }); }
        catch (TimeoutException) { } // the vet reports it
        await page.WaitForTimeoutAsync(Random.Shared.Next(1000, 2000));
        var t = await page.EvaluateAsync<string[]>("""
            () => {
              const h = [...document.querySelectorAll('h1,h2')].find(e => /^Habilidades$/.test(e.innerText.trim()));
              const tags = h ? [...h.parentElement.querySelectorAll('*')].filter(e => e.children.length === 0 && e !== h && e.innerText.trim()).map(e => e.innerText.trim()) : [];
              return [document.title.replace(/\s*\|\s*Coodesh$/, ''), (document.querySelector('main') || document.body).innerText, JSON.stringify(tags)];
            }
            """);
        log($"Vaga aberta: {t[0]}"); // the title is already "Cargo | Empresa"
        return (t[0], t[1], JsonSerializer.Deserialize<string[]>(t[2])!.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// Same contract as EasyApply.VetAsync.
    public static async Task<(string Status, string Job, int? Score, bool Preferred)> VetAsync(IPage page, string id, Answers answers, Filters filters,
        bool score, Action<string> log)
    {
        var (job, description, _) = await OpenAsync(page, id, log);
        if (await Applied(page).CountAsync() > 0) return ("skip: applied", job, null, false);
        if (filters.Blocked(job, description) is { } blocked) return ($"skip: blocked: {blocked}", job, null, false);
        if (await Interest(page).CountAsync() == 0) return ("skip: no easy apply", job, null, false);
        var (status, s, preferred) = await EasyApply.JudgeAsync(job, description, answers, filters, score, log);
        return (status, job, s, preferred);
    }

    /// Same contract as EasyApply.RunAsync. filters null = already vetted.
    /// "Tenho Interesse" already creates the application, so a test stops before it, with the ScoreCard levels answered from the job's tags.
    public static async Task<(string Status, string Job, int? Score)> RunAsync(IPage page, string id, Answers answers, Filters? filters, bool dryRun,
        Action<string> log, List<string[]> sent, bool canDefer = false)
    {
        string job; string[] skills;
        int? score = null;
        if (filters is null) (job, _, skills) = await OpenAsync(page, id, log);
        else
        {
            string status; bool preferred;
            (status, job, score, preferred) = await VetAsync(page, id, answers, filters, false, log);
            if (status != "") return (status, job, score);
            if (canDefer && !preferred) return ("defer", job, score);
            skills = (await OpenAsync(page, id, _ => { })).Skills;
        }
        if (await Interest(page).CountAsync() == 0) return ("skip: no easy apply", job, score);

        // The levels first: an aviso here costs nothing, the application isn't created yet.
        var levels = await LevelsAsync(id, job, skills, answers, log);
        if (levels is null) return ("waiting", job, score);
        if (dryRun)
        {
            foreach (var (k, v) in levels) log($"{k} = {v}");
            return ("dry-run", job, score);
        }

        await Interest(page).ClickAsync();
        try { await page.WaitForURLAsync("**/dashboard/applicants/**", new() { Timeout = 20_000 }); }
        catch (TimeoutException) { await EasyApply.DumpAsync(page, id); return ("failed: a candidatura não abriu", job, score); }
        log("Interesse registrado");
        await page.WaitForTimeoutAsync(3000);

        if (await PreferencesAsync(page, id, job, answers, log, sent) is { } fail) return (fail, job, score);

        var start = page.Locator("button", new() { HasTextString = "Iniciar" });
        if (await start.CountAsync() > 0)
        {
            var tab = await page.Context.RunAndWaitForPageAsync(() => start.First.ClickAsync());
            try { if (await ScoreCardAsync(tab, id, job, answers, log, sent) is { } f) return (f, job, score); }
            finally { await tab.CloseAsync(); }
        }

        await page.ReloadAsync();
        try { await page.GetByText("entrou na triagem").WaitForAsync(new() { Timeout = 15_000 }); }
        catch (TimeoutException) { await EasyApply.DumpAsync(page, id); return ("failed: a pré-candidatura não terminou (etapa não mapeada?)", job, score); }
        log("Candidatura enviada: entrou na triagem");
        return ("applied", job, score);
    }

    /// One answer per skill tag ("Praticante"...), or null when some became an aviso.
    static async Task<Dictionary<string, string>?> LevelsAsync(string id, string job, string[] skills, Answers answers, Action<string> log)
    {
        if (skills.Length == 0) return [];
        var fields = skills.Select((s, i) => new Field($"f{i}", LevelLabel(s), "radio", Levels, "", null, false)).ToList();
        var r = await answers.ResolveAsync(id, job, fields, log);
        return r?.ToDictionary(kv => fields.First(f => f.Id == kv.Key).Label, kv => Answers.Best(kv.Value, Levels) ?? kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    /// The "Dados da candidatura" modal: fills required fields left empty (the salary comes as 0) and saves. Error text, or null.
    static async Task<string?> PreferencesAsync(IPage page, string id, string job, Answers answers, Action<string> log, List<string[]> sent)
    {
        var dialog = page.Locator("[role=dialog]");
        try { await dialog.Locator("button[type=submit]").WaitForAsync(new() { Timeout = 10_000 }); }
        catch (TimeoutException) { return null; } // already saved once (a repeated "Tenho Interesse")
        var fields = JsonSerializer.Deserialize<List<Field>>(await page.EvaluateAsync<string>(PrefsJs), Store.Json)!;
        if (fields.Count > 0)
        {
            var resolved = await answers.ResolveAsync(id, job, fields, log);
            if (resolved is null) return "waiting";
            foreach (var f in fields)
            {
                var ans = resolved.GetValueOrDefault(f.Id, "");
                if (ans == "") continue;
                var el = page.Locator($"[data-lia='{f.Id}']");
                await el.FillAsync(f.Type == "number" ? new string(ans.Where(char.IsDigit).ToArray()) : ans);
                await el.BlurAsync();
                log($"{f.Label} = {ans}");
                sent.Add([f.Label, ans]);
            }
        }
        await dialog.Locator("button[type=submit]").ClickAsync();
        try { await page.GetByText("Salvo com sucesso").First.WaitForAsync(new() { Timeout = 10_000 }); }
        catch (TimeoutException) { await EasyApply.DumpAsync(page, $"{id}-preferencias"); return "failed: as preferências não salvaram"; }
        log("Preferências salvas");
        await page.WaitForTimeoutAsync(1500);
        return null;
    }

    // Required visible fields of the modal still empty ("0" in a number input). The salary label is just "PJ"/"CLT":
    // it gets "Pretensão salarial mensal" in front so Claude and the saved answers know what it is.
    const string PrefsJs = "() => {" + EasyApply.CleanJs + """
          const d = document.querySelector('[role=dialog]'); let n = 0; const out = [];
          d.querySelectorAll('[data-lia]').forEach(e => e.removeAttribute('data-lia'));
          for (const el of d.querySelectorAll('input:not([type=hidden]):not([type=file]):not([type=checkbox]):not([role=combobox]), textarea')) {
            if (!el.getClientRects().length) continue;
            let lab = null; for (let e = el.parentElement, i = 0; e && i < 5 && !lab; e = e.parentElement, i++) lab = e.querySelector('label');
            const raw = clean(lab?.innerText);
            const num = el.getAttribute('role') === 'spinbutton';
            if (!/\*\s*$/.test(raw) && !el.required) continue;
            if (num ? +el.value > 0 : el.value.trim()) continue;
            const id = 'f' + n++; el.setAttribute('data-lia', id);
            const label = raw.replace(/\s*\*\s*$/, '');
            out.push({ id, label: /^salary_range/.test(el.name) ? `Pretensão salarial mensal ${label}` : label, type: num ? 'number' : 'text',
              options: [], value: '', error: null, combo: false, max: null });
          }
          return JSON.stringify(out);
        }
        """;

    /// The "Mapa de Habilidades" tab: declaration, instructions, one level per skill, Finalizar. Error text, or null.
    static async Task<string?> ScoreCardAsync(IPage tab, string id, string job, Answers answers, Action<string> log, List<string[]> sent)
    {
        await tab.GetByText("Responder agora").WaitForAsync(new() { Timeout = 20_000 });
        await tab.GetByText("Declaro que as informações são verídicas").ClickAsync();
        await tab.GetByText("Responder agora").ClickAsync();
        await tab.WaitForURLAsync("**/instructions", new() { Timeout = 20_000 });
        await tab.Locator("button", new() { HasTextString = "Próximo" }).ClickAsync();
        await tab.WaitForURLAsync("**/skills", new() { Timeout = 20_000 });
        await tab.Locator("button[id$='-level']").First.WaitForAsync(new() { Timeout = 20_000 });

        var skills = await tab.EvaluateAsync<string[]>("() => [...document.querySelectorAll('button[id$=\"-level\"]')].map(b => document.getElementById(b.id.replace(/-level$/, ''))?.innerText.trim() || '')");
        var levels = await LevelsAsync(id, job, skills, answers, log);
        if (levels is null) return "waiting"; // the ScoreCard stays open on Coodesh; the queue picks it up after the avisos
        for (int i = 0; i < skills.Length; i++)
        {
            var level = levels.GetValueOrDefault(LevelLabel(skills[i]), "");
            if (level == "") continue;
            await tab.Locator("button[id$='-level']").Nth(i).ClickAsync();
            var pop = tab.Locator("[role=dialog]");
            await pop.Locator("[role=radiogroup]").WaitForAsync(new() { Timeout = 5000 });
            await pop.Locator("label", new() { HasTextRegex = new($"^\\s*{level}\\b") }).First.ClickAsync();
            await pop.Locator("button", new() { HasTextString = "Concluir" }).ClickAsync();
            await tab.WaitForTimeoutAsync(Random.Shared.Next(500, 900));
            log($"{skills[i]} = {level}");
            sent.Add([skills[i], level]);
        }
        if (await tab.Locator("button[id$='-level']", new() { HasTextString = "Selecione o nível" }).CountAsync() > 0)
        { await EasyApply.DumpAsync(tab, $"{id}-scorecard"); return "failed: ScoreCard com habilidade sem nível"; }
        await tab.Locator("button", new() { HasTextString = "Finalizar" }).ClickAsync();
        try { await tab.WaitForURLAsync("**/thank-you", new() { Timeout = 20_000 }); }
        catch (TimeoutException) { await EasyApply.DumpAsync(tab, $"{id}-scorecard"); return "failed: ScoreCard não finalizou"; }
        log("ScoreCard finalizado");
        return null;
    }

    public static void SelfCheck()
    {
        Trace.Assert(Is("coodesh-desenvolvedor-rpa-191038516") && !Is("nerdin-1") && !Is("4031009921"));
        Trace.Assert(JobUrl("coodesh-desenvolvedor-rpa-191038516") == "https://coodesh.com/pt/jobs/desenvolvedor-rpa-191038516");
        Trace.Assert(ListUrl("dev .net") == "https://coodesh.com/jobs?search=dev%20.net");
        Trace.Assert(ListUrl(" ") == "https://coodesh.com/jobs");
        Trace.Assert(Answers.Key(LevelLabel("AUTOMATION (RPA)")) == Answers.Key(LevelLabel("Automation (RPA)")));
    }
}
