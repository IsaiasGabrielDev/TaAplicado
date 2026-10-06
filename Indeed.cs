using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;

namespace LinkedInAutoApply;

/// Applies through Indeed's "Candidate-se facilmente" (smartapply.indeed.com). Map of the pages: docs/indeed.md.
/// Ledger ids are "indeed-<jk>" so they never collide with LinkedIn's numeric ids.
public static class Indeed
{
    public const string Prefix = "indeed-";
    public static bool Is(string id) => id.StartsWith(Prefix);
    public static string JobUrl(string id) => $"https://br.indeed.com/viewjob?jk={id[Prefix.Length..]}";

    /// iafilter=1 keeps only easy-apply jobs; "start" pages by 10.
    public static string ListUrl(string terms) => $"https://br.indeed.com/jobs?q={Uri.EscapeDataString(terms)}&iafilter=1&start=";

    /// Easy-apply job ids on a result page, plus whether the page had any card at all (empty = the list ended).
    public static async Task<(List<string> Ids, bool Any)> PageIdsAsync(IPage page, Action<string?> attention, CancellationToken ct)
    {
        await WaitHumanAsync(page, attention, ct);
        const string CardsJs = """
            () => [...document.querySelectorAll('a[data-jk]')].map(a => [a.dataset.jk,
              /Candidate-se facilmente|Easily apply/i.test(a.closest('.job_seen_beacon')?.innerText || '') ? '1' : ''])
            """;
        var cards = await page.EvaluateAsync<string[][]>(CardsJs);
        // No cards and no "no results" message: something stands in front of the list (a check whose wording we
        // don't know). Wait for the user instead of taking it as the end of the list.
        if (cards.Length == 0 && !await page.EvaluateAsync<bool>("() => /não encontr|nenhum resultado|did not match|no results/i.test(document.body.innerText)"))
        {
            await EasyApply.DumpAsync(page, "indeed-list");
            attention("O Indeed não mostrou a lista de vagas. Se houver uma verificação, resolva na janela do Chrome; a execução continua sozinha.");
            var until = DateTime.Now.AddMinutes(10);
            while ((cards = await page.EvaluateAsync<string[][]>(CardsJs)).Length == 0 && DateTime.Now < until)
                await Task.Delay(3000, ct);
            attention(null);
        }
        return (cards.Where(c => c[1] == "1").Select(c => Prefix + c[0]).Distinct().ToList(), cards.Length > 0);
    }

    /// Logged in when the account page opens instead of redirecting to the sign-in.
    public static async Task EnsureLoggedInAsync(IPage page, Action<string?> attention, Action<string> log, CancellationToken ct)
    {
        await page.GotoAsync("https://secure.indeed.com/settings/account");
        if (page.Url.Contains("/settings/account")) return;
        attention("Entre no Indeed na janela do Chrome. A execução continua sozinha depois do login.");
        log("Entre no Indeed na janela do Chrome");
        // ponytail: polls the account page every 5 s; a login event would avoid reloads.
        while (!page.Url.Contains("/settings/account"))
        {
            await Task.Delay(5000, ct);
            if (!page.Url.Contains("secure.indeed.com")) await page.GotoAsync("https://secure.indeed.com/settings/account");
        }
        attention(null);
        log("Login no Indeed feito, sessão salva");
    }

    // A challenge the user must solve: Cloudflare's interstitial or a reCAPTCHA/hCaptcha/Turnstile puzzle on screen.
    const string ChallengeJs = """
        () => /just a moment|um momento|verify you are human|(verifique|confirme) (se |que )?(você )?é (um )?humano/i
                .test(document.title + ' ' + (document.body?.innerText || '').slice(0, 600))
          || [...document.querySelectorAll('iframe[src*="recaptcha/api2/bframe"], iframe[src*="hcaptcha"], iframe[src*="challenges.cloudflare"]')]
               .some(f => f.getBoundingClientRect().height > 60 && getComputedStyle(f).visibility !== 'hidden')
        """;

    /// Never solves a challenge: asks the user to, and waits (up to 10 minutes).
    static async Task WaitHumanAsync(IPage page, Action<string?> attention, CancellationToken ct)
    {
        if (!await page.EvaluateAsync<bool>(ChallengeJs)) return;
        attention("O Indeed pediu uma verificação: resolva na janela do Chrome. A execução continua sozinha depois.");
        var until = DateTime.Now.AddMinutes(10);
        while (await page.EvaluateAsync<bool>(ChallengeJs))
        {
            if (DateTime.Now > until) { attention(null); throw new TimeoutException("verificação do Indeed não resolvida em 10 minutos"); }
            await Task.Delay(2000, ct);
        }
        attention(null);
    }

    static async Task<(string Job, string Description)> OpenAsync(IPage page, string id, Action<string?> attention, Action<string> log, CancellationToken ct)
    {
        await page.GotoAsync(JobUrl(id));
        await page.WaitForTimeoutAsync(Random.Shared.Next(2500, 4500));
        await WaitHumanAsync(page, attention, ct);
        var t = await page.EvaluateAsync<string[]>("""
            () => [document.querySelector('[data-testid=vj-job-title]')?.innerText.trim() || document.title.split(' - ')[0],
                   document.querySelector('[data-testid=company-info-metadata]')?.innerText.split('\n')[0].trim() || '',
                   // the whole job content: "Dados da vaga" (pay, location) sits outside the description block
                   document.querySelector('[data-testid=viewjob-job-content]')?.innerText || document.body.innerText]
            """);
        var job = t[1] == "" ? t[0] : $"{t[0]} | {t[1]}";
        log($"Vaga aberta: {job}");
        return (job, t[2]);
    }

    static ILocator ApplyButton(IPage page) => page.Locator("[data-testid=viewjob-indeed-apply]");

    /// Same contract as EasyApply.VetAsync: ignored companies/words, the easy-apply button, then Claude.
    public static async Task<(string Status, string Job, int? Score, bool Preferred)> VetAsync(IPage page, string id, Answers answers, Filters filters,
        bool score, Action<string> log, Action<string?> attention, CancellationToken ct)
    {
        var (job, description) = await OpenAsync(page, id, attention, log, ct);
        if (filters.Blocked(job, description) is { } blocked) return ($"skip: blocked: {blocked}", job, null, false);
        if (!await ApplyButton(page).First.IsVisibleAsync()) return ("skip: no easy apply", job, null, false);
        var (status, s, preferred) = await EasyApply.JudgeAsync(job, description, answers, filters, score, log);
        return (status, job, s, preferred);
    }

    /// Same contract as EasyApply.RunAsync. filters null = already vetted.
    public static async Task<(string Status, string Job, int? Score)> RunAsync(IPage page, string id, Answers answers, Filters? filters, bool dryRun,
        Action<string> log, List<string[]> sent, Action<string?> attention, CancellationToken ct, bool canDefer = false)
    {
        string job;
        int? score = null;
        if (filters is null) job = (await OpenAsync(page, id, attention, log, ct)).Job;
        else
        {
            string status; bool preferred;
            (status, job, score, preferred) = await VetAsync(page, id, answers, filters, false, log, attention, ct);
            if (status != "") return (status, job, score);
            if (canDefer && !preferred) return ("defer", job, score);
        }
        if (!await ApplyButton(page).First.IsVisibleAsync()) return ("skip: no easy apply", job, score); // e.g. already applied
        // The Indeed account's resume is kept; this one only goes in when a question asks for the file ("Anexar o currículo *").
        var resume = EasyApply.ResumeOf(EasyApply.Lang(await page.Locator("body").InnerTextAsync())) ?? EasyApply.ResumeOf("pt") ?? EasyApply.ResumeOf("en");

        // On the job page the link opens in the same tab; from a search card it opens a new one. Take whichever has the form.
        var pages = page.Context.Pages.Count;
        await ApplyButton(page).First.ClickAsync();
        await page.WaitForTimeoutAsync(4000);
        var form = page.Context.Pages.Count > pages ? page.Context.Pages[^1] : page;
        try
        {
            string last = ""; int same = 0;
            for (int step = 0; step < 15; step++)
            {
                await WaitHumanAsync(form, attention, ct);
                // A step loads behind a spinner; reading it early finds no fields and no "Continuar".
                try { await form.WaitForFunctionAsync("() => !document.querySelector('main [id^=ifl-Spinner-title]')", null, new() { Timeout = 20_000 }); }
                catch (TimeoutException) { } // still loading: the step below fails it with a dump
                if (form.Url.Contains("secure.indeed.com")) { await EnsureLoggedInAsync(form, attention, log, ct); return ("failed: login pedido no meio da candidatura", job, score); }
                var path = new Uri(form.Url).AbsolutePath;
                if (path.Contains("/post-apply")) { log("Candidatura enviada"); return ("applied", job, score); }

                if (path.Contains("review-module"))
                {
                    // The only checkbox here is "e-mails com vagas": never subscribe the user.
                    if (await form.EvaluateAsync<bool>("() => { let hit = false; for (const c of document.querySelectorAll('main input[type=checkbox]:checked')) { (c.labels?.[0] || c).click(); hit = true; } return hit; }"))
                        log("Desmarcado: e-mails com vagas");
                    if (dryRun) return ("dry-run", job, score);
                    await form.Locator("[data-testid=submit-application-button]").ClickAsync();
                    try { await form.WaitForURLAsync("**/post-apply**", new() { Timeout = 20_000 }); }
                    catch (TimeoutException) { await EasyApply.DumpAsync(form, id); return ("failed: envio não confirmado", job, score); }
                    log("Candidatura enviada");
                    return ("applied", job, score);
                }

                if (await form.EvaluateAsync<bool>(TagFileJs))
                {
                    if (resume is null) { await EasyApply.DumpAsync(form, id); return ("failed: a vaga pede o currículo como arquivo; adicione em Perfil → Currículos", job, score); }
                    await form.Locator("[data-lia-file]").SetInputFilesAsync(Path.GetFullPath(resume));
                    await form.WaitForTimeoutAsync(3000);
                    log($"Currículo anexado: {Path.GetFileName(resume)}");
                    sent.Add(["Currículo", Path.GetFileName(resume)]);
                }

                // Questions come in questions-module and also in other steps (the demographic one has a required consent).
                {
                    var fields = JsonSerializer.Deserialize<List<Field>>(await form.EvaluateAsync<string>(ExtractJs), Store.Json)!;
                    if (fields.Count > 0)
                    {
                        var resolved = await answers.ResolveAsync(id, job, fields, log);
                        if (resolved is null) return ("waiting", job, score);
                        await FillAsync(form, id, fields, resolved, log, sent);
                    }
                }

                // Several "Continuar" buttons are hidden honeypots (hp-continue-button-N): click only the visible one.
                if (!await form.EvaluateAsync<bool>(TagNextJs)) { await EasyApply.DumpAsync(form, id); return ("failed: no next button", job, score); }
                await form.Locator("[data-lia-next]").ClickAsync();
                log($"Etapa {step + 1} concluída");
                await form.WaitForTimeoutAsync(Random.Shared.Next(1500, 2500));

                var text = form.Url + await form.Locator("main").First.InnerTextAsync();
                same = text == last ? same + 1 : 0;
                last = text;
                if (same >= 3) { await EasyApply.DumpAsync(form, id); return ("failed: stuck on a step", job, score); }
            }
            await EasyApply.DumpAsync(form, id);
            return ("failed: too many steps", job, score);
        }
        finally
        {
            // An unfinished form stays as a draft on Indeed; the next job opens over it (or its tab closes).
            if (form != page) await form.CloseAsync();
        }
    }

    // A required question asking for a file with none attached yet: tags its file input. False when there is none.
    const string TagFileJs = """
        () => {
          document.querySelectorAll('[data-lia-file]').forEach(i => i.removeAttribute('data-lia-file'));
          const item = [...document.querySelectorAll('.ia-Questions-item')].find(it => it.querySelector('input[type=file]')
            && (/\*\s*$/m.test(it.innerText) || it.querySelector('[data-testid$=-label-asterisk]') || /carregue um arquivo|upload a file/i.test(it.innerText))
            && ![...it.querySelectorAll('*')].some(e => e.children.length === 0 && /\S\.(pdf|docx?|rtf|txt|odt)\s*$/i.test(e.textContent)));
          if (!item) return false;
          item.querySelector('input[type=file]').setAttribute('data-lia-file', '');
          return true;
        }
        """;

    const string TagNextJs = """
        () => {
          document.querySelectorAll('[data-lia-next]').forEach(b => b.removeAttribute('data-lia-next'));
          const ok = b => !/^hp-/.test(b.dataset.testid || '') && b.getClientRects().length > 0
            && getComputedStyle(b).visibility !== 'hidden' && +getComputedStyle(b).opacity > 0.1;
          const next = [...document.querySelectorAll('button')].filter(b => /^\s*(Continuar|Continue|Verificar sua candidatura|Review your application)\s*$/i.test(b.innerText) && ok(b));
          if (next.length !== 1) return false; // none, or ambiguous: never guess between honeypots
          next[0].setAttribute('data-lia-next', '');
          return true;
        }
        """;

    // One field per question still needing an answer (.ia-Questions-item, or a bare *-select-question fieldset as in the demographic step): required and empty, or showing an error.
    const string ExtractJs = "() => {" + EasyApply.CleanJs + """
          const star = t => /\*\s*(obrigatório|required)?\s*$/i.test(t || ''), bare = t => clean(t).replace(/\s*\*\s*(obrigatório|required)?\s*$/i, '');
          let n = 0; const out = [];
          document.querySelectorAll('[data-lia]').forEach(el => el.removeAttribute('data-lia'));
          document.querySelectorAll('.ia-Questions-item, [data-testid$=-select-question]').forEach(item => {
            if (item.parentElement.closest('.ia-Questions-item')) return;
            const err = clean(item.querySelector('[role=alert], [id*=error i], [class*=rror]')?.innerText) || null;
            const raw = item.querySelector('[data-testid$=-question-label]')?.innerText || item.querySelector('legend')?.innerText || item.querySelector('label')?.innerText || '';
            const tag = (type, options, value, max, combo = false) => { const id = 'f' + n++; item.setAttribute('data-lia', id);
              out.push({ id, label: bare(raw), type, options, value, error: err, combo, max }); };
            const radios = [...item.querySelectorAll('input[type=radio]')], checks = [...item.querySelectorAll('input[type=checkbox]')];
            const inputs = radios.length ? radios : checks;
            const req = star(raw) || !!item.querySelector('[data-testid$=-label-asterisk]') || [...item.querySelectorAll('input, select, textarea')].some(i => i.required);
            if (!req && !err) return;
            if (inputs.length) { if (!inputs.some(i => i.checked) || err) tag(radios.length ? 'radio' : 'checkbox', inputs.map(optText), '', null); return; }
            // Indeed's own select (País): a [role=combobox] button over a searchable list; multi-selects use menuitemcheckbox.
            const box = item.querySelector('[role=combobox]'), opt = '[role=option], [role=menuitemcheckbox]';
            if (box && item.querySelector(opt)) {
              if (/selecione|select an option/i.test(box.innerText) || err) tag('select', [...item.querySelectorAll(opt)].map(o => clean(o.innerText)), '', null, true);
              return;
            }
            const sel = item.querySelector('select');
            if (sel) { if (!sel.value || err) tag('select', [...sel.options].filter(o => o.value).map(o => clean(o.text)), '', null); return; }
            const t = item.querySelector('textarea, input:not([type=hidden]):not([type=file])');
            if (t && (!t.value.trim() || err))
              tag(t.tagName === 'TEXTAREA' ? 'textarea' : /^number-input/.test(t.id) || t.type === 'number' ? 'number' : 'text', [], t.value, t.maxLength > 0 ? t.maxLength : null);
          });
          return JSON.stringify(out);
        }
        """;

    static async Task FillAsync(IPage form, string id, List<Field> fields, Dictionary<string, string> answers, Action<string> log, List<string[]> sent)
    {
        foreach (var f in fields)
        {
            var ans = answers.GetValueOrDefault(f.Id, "");
            if (ans == "") continue;
            var item = form.Locator($"[data-lia='{f.Id}']");
            try
            {
                switch (f.Type)
                {
                    case "select" when f.Combo:
                        var want = Answers.Best(ans, f.Options) ?? ans;
                        await item.Locator("[role=combobox]").ClickAsync(new() { Timeout = 5000 });
                        var search = item.Locator("[role=dialog] input").First;
                        if (await search.IsVisibleAsync()) await search.FillAsync(want.Split(" (")[0]); // "Brasil (BR)" → "Brasil"
                        await form.WaitForTimeoutAsync(600);
                        await item.Locator("[role=option], [role=menuitemcheckbox]").Filter(new() { HasText = want }).First.ClickAsync(new() { Timeout = 5000 });
                        // a multi-select keeps its list open over the next questions
                        if (await item.Locator("[role=combobox][aria-expanded=true]").CountAsync() > 0) await form.Keyboard.PressAsync("Escape");
                        break;
                    case "select": await item.Locator("select").SelectOptionAsync(new SelectOptionValue { Label = Answers.Best(ans, f.Options) ?? ans }); break;
                    // ponytail: a multi-select gets one option, Claude's best; several would need a list answer.
                    case "radio" or "checkbox": await item.EvaluateAsync(EasyApply.PickJs, Answers.Best(ans, f.Options) ?? ans); break;
                    default:
                        var input = item.Locator("textarea, input:not([type=hidden]):not([type=file])").First;
                        try { await input.FillAsync(ans, new() { Timeout = 5000 }); }
                        catch (TimeoutException)
                        {
                            // A searchable list (country, city) that refuses plain typing: type it, then pick the matching option.
                            await input.ClickAsync(new() { Timeout = 5000 });
                            await input.PressSequentiallyAsync(ans, new() { Delay = 60, Timeout = 10_000 });
                            await form.WaitForTimeoutAsync(1200);
                            var match = form.Locator("[role=option]").Filter(new() { HasText = ans }).First;
                            await (await match.CountAsync() > 0 ? match : form.Locator("[role=option]").First).ClickAsync(new() { Timeout = 5000 });
                        }
                        break;
                }
                log($"{f.Label} = {ans}");
                sent.Add([f.Label, ans]);
            }
            catch (Exception e) when (e is PlaywrightException or TimeoutException)
            {
                log($"Falhou ao preencher {f.Label}: {e.Message.Split('\n')[0]}");
                await EasyApply.DumpAsync(form, $"{id}-campo"); // the field's real markup, to fix the selector
            }
        }
    }

    public static void SelfCheck()
    {
        Trace.Assert(Is("indeed-d34e9c04866a3453") && !Is("4031009921"));
        Trace.Assert(JobUrl("indeed-d34e9c04866a3453") == "https://br.indeed.com/viewjob?jk=d34e9c04866a3453");
        Trace.Assert(ListUrl("dev .net") == "https://br.indeed.com/jobs?q=dev%20.net&iafilter=1&start=");
    }
}
