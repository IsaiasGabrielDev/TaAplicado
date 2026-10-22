using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;

namespace LinkedInAutoApply;

/// Applies through Nerdin's own form (vaga_candidatura.php), which needs no account. Map of the pages: docs/nerdin.md.
/// Ledger ids are "nerdin-<id>".
public static class Nerdin
{
    public const string Prefix = "nerdin-";
    public static bool Is(string id) => id.StartsWith(Prefix);
    static string Num(string id) => id[Prefix.Length..];
    /// The slug in front of the id is ignored by the site.
    public static string JobUrl(string id) => $"https://www.nerdin.com.br/vaga_emprego/vaga-{Num(id)}.php";
    static string FormUrl(string id) => $"https://www.nerdin.com.br/vaga_candidatura.php?id={Num(id)}";

    /// "busca" searches title, stack and city at once; "pagina" pages by 20.
    public static string ListUrl(string terms) => $"https://www.nerdin.com.br/vagas.php?busca={Uri.EscapeDataString(terms)}&pagina=";

    /// Job ids on a result page (VIP and external ones too: the card doesn't tell, the job page does).
    public static async Task<List<string>> PageIdsAsync(IPage page) => (await page.EvaluateAsync<string[]>("""
        () => [...document.querySelectorAll('#vagas-container > .vaga-card')].map(c => (c.dataset.href || '').match(/-(\d+)\.php/)?.[1]).filter(Boolean)
        """)).Select(n => Prefix + n).Distinct().ToList();

    static async Task<(string Job, string Description)> OpenAsync(IPage page, string id, Action<string> log)
    {
        await page.GotoAsync(JobUrl(id));
        await page.WaitForTimeoutAsync(Random.Shared.Next(1500, 3000));
        var t = await page.EvaluateAsync<string[]>("""
            () => {
              const h = document.querySelector('.vaga-pagina-titulo');
              // the column with the title, "Sobre a Vaga", "Requisitos" and "Benefícios", without similar jobs and courses
              let col = h; while (col && !/Requisitos/.test(col.textContent)) col = col.parentElement;
              return [h?.innerText.trim() || document.title.split(' - ')[0],
                      document.querySelector('.vaga-hero-empresa__nome')?.innerText.trim() || '',
                      (col || document.querySelector('main') || document.body).innerText];
            }
            """);
        var job = t[1] == "" ? t[0] : $"{t[0]} | {t[1]}";
        log($"Vaga aberta: {job}");
        return (job, t[2]);
    }

    /// Same contract as EasyApply.VetAsync. No link to vaga_candidatura.php = VIP (Premium only) or applied on the company's site
    /// (whose button also has .btn-candidatar-vaga).
    public static async Task<(string Status, string Job, int? Score, bool Preferred)> VetAsync(IPage page, string id, Answers answers, Filters filters,
        bool score, Action<string> log)
    {
        var (job, description) = await OpenAsync(page, id, log);
        if (filters.Blocked(job, description) is { } blocked) return ($"skip: blocked: {blocked}", job, null, false);
        if (await page.Locator("a[href*=vaga_candidatura]").CountAsync() == 0) return ("skip: no easy apply", job, null, false);
        var (status, s, preferred) = await EasyApply.JudgeAsync(job, description, answers, filters, score, log);
        return (status, job, s, preferred);
    }

    /// Same contract as EasyApply.RunAsync. filters null = already vetted.
    public static async Task<(string Status, string Job, int? Score)> RunAsync(IPage page, string id, Answers answers, Filters? filters, bool dryRun,
        Action<string> log, List<string[]> sent, bool canDefer = false)
    {
        string job, description;
        int? score = null;
        if (filters is null) (job, description) = await OpenAsync(page, id, log);
        else
        {
            string status; bool preferred;
            (status, job, score, preferred) = await VetAsync(page, id, answers, filters, false, log);
            if (status != "") return (status, job, score);
            if (canDefer && !preferred) return ("defer", job, score);
            description = await page.Locator("body").InnerTextAsync();
        }
        if (await page.Locator("a[href*=vaga_candidatura]").CountAsync() == 0) return ("skip: no easy apply", job, score);
        var resume = EasyApply.ResumeOf(EasyApply.Lang(description)) ?? EasyApply.ResumeOf("pt") ?? EasyApply.ResumeOf("en");

        await page.GotoAsync(FormUrl(id));
        await page.WaitForTimeoutAsync(Random.Shared.Next(1500, 3000));
        if (page.Url.Contains("premium_necessario")) return ("skip: no easy apply", job, score);
        // "Sua segurança é importante" covers the form until acknowledged (once per browser).
        var ack = page.Locator("[data-nerdin-seg-ack]");
        if (await ack.IsVisibleAsync()) await ack.ClickAsync();

        // Nothing comes filled (no account): every field goes through the saved answers / Claude, in one call.
        var json = await page.EvaluateAsync<string?>(ExtractJs);
        if (json is null) { await EasyApply.DumpAsync(page, id); return ("failed: formulário não encontrado", job, score); }
        var fields = JsonSerializer.Deserialize<List<Field>>(json, Store.Json)!;
        var resolved = await answers.ResolveAsync(id, job, fields, log);
        if (resolved is null) return ("waiting", job, score);

        // Three steps on the same page (#etapa-1..3); a hidden one can't be filled, so fill and advance one at a time.
        for (int step = 1; step <= 3; step++)
        {
            var etapa = page.Locator($"#etapa-{step}");
            foreach (var f in fields)
            {
                var el = etapa.Locator($"[data-lia='{f.Id}']");
                var ans = resolved.GetValueOrDefault(f.Id, "");
                if (ans == "" || await el.CountAsync() == 0) continue;
                try
                {
                    if (f.Type == "select") await el.SelectOptionAsync(new SelectOptionValue { Label = Answers.Best(ans, f.Options) ?? ans }, new() { Timeout = 5000 });
                    else
                    {
                        await el.FillAsync(ans, new() { Timeout = 5000 });
                        // The CLT salary has a money mask that reads digits as cents ("8000" → "80,00"): give it the cents.
                        if (!ans.Contains(',') && (await el.InputValueAsync()).Contains(',')) await el.FillAsync(ans + ",00", new() { Timeout = 5000 });
                    }
                    log($"{f.Label} = {ans}");
                    sent.Add([f.Label, ans]);
                }
                catch (Exception e) when (e is PlaywrightException or TimeoutException)
                {
                    log($"Falhou ao preencher {f.Label}: {e.Message.Split('\n')[0]}");
                    await EasyApply.DumpAsync(page, $"{id}-campo");
                }
            }
            if (step < 3)
            {
                await page.Locator("#btn-proximo").ClickAsync();
                try { await page.Locator($"#etapa-{step + 1}").WaitForAsync(new() { Timeout = 5000 }); }
                catch (TimeoutException) { await EasyApply.DumpAsync(page, id); return ($"failed: o formulário não passou da etapa {step}", job, score); }
                log($"Etapa {step} concluída");
                await page.WaitForTimeoutAsync(Random.Shared.Next(800, 1600));
            }
        }

        // The resume is optional on Nerdin; send it when there is one.
        if (resume is not null)
        {
            await page.Locator("#curriculo").SetInputFilesAsync(Path.GetFullPath(resume));
            log($"Currículo anexado: {Path.GetFileName(resume)}");
            sent.Add(["Currículo", Path.GetFileName(resume)]);
        }

        if (dryRun) return ("dry-run", job, score);
        await page.Locator("#btn-enviar-candidatura").ClickAsync();
        // The same URL comes back with "Candidatura enviada com sucesso!" in a green alert.
        try { await page.Locator(".alert-success").Filter(new() { HasTextRegex = new("enviada com sucesso", System.Text.RegularExpressions.RegexOptions.IgnoreCase) }).WaitForAsync(new() { Timeout = 30_000 }); }
        catch (TimeoutException) { await EasyApply.DumpAsync(page, id); return ("failed: envio não confirmado", job, score); }
        log("Candidatura enviada");
        return ("applied", job, score);
    }

    // Every field of the form, tagged with data-lia. The tech questions (palavra_chave_N) are labeled with just the
    // tech's name ("Angular"), so the label gets the question around it for Claude and the saved answers.
    const string ExtractJs = "() => {" + EasyApply.CleanJs + """
          const f = document.getElementById('form-candidatura');
          if (!f) return null;
          let n = 0; const out = [];
          f.querySelectorAll('[data-lia]').forEach(e => e.removeAttribute('data-lia'));
          for (const el of f.querySelectorAll('input:not([type=hidden]):not([type=file]):not([type=button]):not([type=submit]), select, textarea')) {
            let label = clean(f.querySelector(`label[for="${CSS.escape(el.id)}"]`)?.innerText || el.name).replace(/\s*\*\s*$/, '');
            if (/^palavra_chave_/.test(el.name)) label = `Tempo de experiência com ${label}`;
            const id = 'f' + n++; el.setAttribute('data-lia', id);
            const sel = el.tagName === 'SELECT';
            out.push({ id, label, type: sel ? 'select' : el.tagName === 'TEXTAREA' ? 'textarea' : el.type === 'number' ? 'number' : 'text',
              options: sel ? [...el.options].map(o => clean(o.text)) : [], value: '', error: null, combo: false, max: el.maxLength > 0 ? el.maxLength : null });
          }
          return JSON.stringify(out);
        }
        """;

    public static void SelfCheck()
    {
        Trace.Assert(Is("nerdin-99965") && !Is("indeed-d34e") && !Is("4031009921"));
        Trace.Assert(JobUrl("nerdin-99965") == "https://www.nerdin.com.br/vaga_emprego/vaga-99965.php");
        Trace.Assert(FormUrl("nerdin-99965") == "https://www.nerdin.com.br/vaga_candidatura.php?id=99965");
        Trace.Assert(ListUrl("dev .net") == "https://www.nerdin.com.br/vagas.php?busca=dev%20.net&pagina=");
    }
}
