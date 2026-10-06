using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace LinkedInAutoApply;

public class EasyApplyLimitException() : Exception("O LinkedIn bloqueou o Easy Apply por hoje (limite diário)");

/// Applies to one job through the Easy Apply modal. Returns the status for applied.json and the job title.
public static class EasyApply
{
    static readonly Regex SubmitBtn = new("Submit application|Enviar candidatura", RegexOptions.IgnoreCase);
    static readonly Regex ReviewBtn = new("Review|Revisar", RegexOptions.IgnoreCase);
    static readonly Regex NextBtn = new("Next|Avançar|Próximo|Continue|Continuar", RegexOptions.IgnoreCase);
    static readonly Regex DiscardBtn = new(@"^\s*(Discard|Descartar)\s*$", RegexOptions.IgnoreCase);

    static async Task<string> OpenAsync(IPage page, string jobId, Action<string> log)
    {
        await page.GotoAsync($"https://www.linkedin.com/jobs/view/{jobId}/");
        await page.WaitForTimeoutAsync(Random.Shared.Next(2500, 4500));
        // "(3) .NET Developer | Acme | LinkedIn" → ".NET Developer | Acme"
        var job = Regex.Replace(await page.TitleAsync(), @"^\(\d+\)\s*|\s*\|\s*LinkedIn$", "");
        log($"Vaga aberta: {job}");
        return job;
    }

    // The real button is a link to /apply/?openSDUIApplyFlow=true; matching on text also hits "Easy Apply" filter chips.
    // PT: the button can carry only the visible text, so match it exactly (filter chips say more / sit outside main).
    static ILocator ApplyButton(IPage page) =>
        page.Locator("a[href*='openSDUIApplyFlow'], [aria-label^='Easy Apply to' i], [aria-label*='candidatura simplificada' i]")
            .Or(page.Locator("main button:not([aria-pressed]), main a").Filter(new() { HasTextRegex = new(@"^\s*Candidatura simplificada\s*$", RegexOptions.IgnoreCase) }));

    /// Opens the job and decides if it is worth applying: ignored companies/words, Easy Apply, then Claude
    /// (skipped when the user turned it off, unless a score or the country/language rules need it).
    /// Status "" = go ahead; Score is 0-100, null when Claude was not asked; Preferred = matches the country/language the user prioritizes.
    public static async Task<(string Status, string Job, int? Score, bool Preferred)> VetAsync(IPage page, string jobId, Answers answers, Filters filters, bool score, Action<string> log)
    {
        var job = await OpenAsync(page, jobId, log);
        // Top card (location, pay, remote/hybrid) + "About the job" + "About the company": the rest of main is
        // Premium ads, similar jobs, people and the footer. Whole main when LinkedIn's layout changes.
        var description = await page.EvaluateAsync<string>("""
            () => {
              const about = document.querySelector('main [componentkey^=JobDetails_AboutTheJob]');
              const col = about?.closest('[data-testid=lazy-column]');
              if (!col) return document.querySelector('main')?.innerText || '';
              const top = [...col.children].find(c => c.innerText.trim());
              const company = document.querySelector('main [componentkey^=JobDetails_AboutTheCompany]');
              return [top, about, company].map(e => e?.innerText || '').join('\n\n');
            }
            """);
        if (filters.Blocked(job, description) is { } blocked) return ($"skip: blocked: {blocked}", job, null, false);
        if (!await ApplyButton(page).First.IsVisibleAsync()) { await DumpAsync(page, jobId); return ("skip: no easy apply", job, null, false); }
        var (status, s, preferred) = await JudgeAsync(job, description, answers, filters, score, log);
        return (status, job, s, preferred);
    }

    /// The Claude part of vetting, shared by every site: relevance, score, country/language rules.
    public static async Task<(string Status, int? Score, bool Preferred)> JudgeAsync(string job, string description, Answers answers, Filters filters, bool score, Action<string> log)
    {
        var judge = filters.UseClaude || score;
        // Modo sem IA: only the lists filter (a country "only" still narrows LinkedIn's own search).
        if (answers.NoAi || !judge && filters.CountryRule is null && filters.LanguageRule is null) return ("", null, true);

        var v = await answers.IsRelevantAsync(job, description, filters.CountryRule, filters.LanguageRule);
        if (judge && !v.Relevant) return ($"skip: not relevant: {v.Why}", v.Score, false);
        var (skip, preferred) = filters.Place(v.InCountry, v.InLanguage);
        if (skip is not null) return ($"skip: blocked: {skip}", v.Score, false);
        if (judge) log($"Claude: nota {v.Score}{(v.Why == "" ? "" : $", {v.Why}")}");
        if (filters.Prefers) log(preferred ? "Na prioridade (país/idioma)" : "Fora da prioridade (país/idioma): fica para o fim");
        return ("", v.Score, preferred);
    }

    /// filters null = already vetted by VetAsync in this run: just open the job and apply.
    /// canDefer: a job outside the country/language priority returns "defer" instead of being applied now.
    public static async Task<(string Status, string Job, int? Score)> RunAsync(IPage page, string jobId, Answers answers, Filters? filters, bool dryRun, Action<string> log, List<string[]> sent, bool canDefer = false)
    {
        string job;
        int? score = null;
        if (filters is null) job = await OpenAsync(page, jobId, log);
        else
        {
            string status;
            bool preferred;
            (status, job, score, preferred) = await VetAsync(page, jobId, answers, filters, false, log);
            if (status != "") return (status, job, score);
            if (canDefer && !preferred) return ("defer", job, score);
        }
        var apply = ApplyButton(page);
        if (!await apply.First.IsVisibleAsync()) return ("skip: no easy apply", job, score);
        // The job's language picks the resume (read now: the modal covers the description).
        var lang = Lang(await page.Locator("#job-details, .jobs-description__content, main").First.InnerTextAsync());
        var resume = ResumeOf(lang);

        // Only Easy Apply jobs count toward the mapping quota; skipped ones must not use it up.
        var map = Mapping(jobId);
        if (map) await MapAsync(page, jobId, "0-vaga");
        await apply.First.ClickAsync();
        await page.WaitForTimeoutAsync(2000);

        // LinkedIn caps Easy Apply per day; past the cap every job would "fail", so the run must stop.
        if (await page.GetByText(new Regex("reached today.s Easy Apply limit|limite (diário|de hoje).{0,20}Candidatura simplificada", RegexOptions.IgnoreCase)).First.IsVisibleAsync())
            throw new EasyApplyLimitException();
        if (map) await MapAsync(page, jobId, "1-apos-clicar-easy-apply");

        // Occasional "job search safety" warning in front of the modal.
        var cont = page.Locator("button").Filter(new() { HasTextRegex = new("Continue applying|Continuar (a )?candidat", RegexOptions.IgnoreCase) });
        if (await cont.First.IsVisibleAsync()) await cont.First.ClickAsync();

        // The Easy Apply form used to live only in the #interop-outlet shadow root (Playwright's CSS pierces it);
        // LinkedIn now renders it as a native light-DOM <dialog data-testid="dialog" open>.
        // A bare [role=dialog] would hit LinkedIn's empty, inert "popover-floating" popover first.
        var modal = page.Locator("#interop-outlet [role='dialog'], .jobs-easy-apply-modal, dialog[data-testid='dialog'][open]").First;
        try { await modal.WaitForAsync(new() { Timeout = 10_000 }); }
        catch (TimeoutException) { await DumpAsync(page, jobId); return ("failed: modal did not open", job, score); }

        string last = ""; int same = 0;
        for (int step = 0; step < 20; step++)
        {
            if (map) await MapAsync(page, jobId, $"{step + 2}-etapa-{step + 1}");
            // Every step: besides the resume step, a later question can ask for the CV again (a bare Upload button).
            if (resume is not null) await PickResumeAsync(page, modal, resume, lang, log, sent);
            var fields = JsonSerializer.Deserialize<List<Field>>(
                await modal.EvaluateAsync<string>(ExtractJs), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            if (fields.Count > 0)
            {
                Dictionary<string, string>? resolved;
                // Close the modal first: Ultra keeps using this page to apply to the jobs already answered.
                try { resolved = await answers.ResolveAsync(jobId, job, fields, log); }
                catch (ClaudeUnavailableException) { await DiscardAsync(page); throw; }
                if (resolved is null) { await DiscardAsync(page); return ("waiting", job, score); }
                await FillAsync(page, modal, fields, resolved, log, sent);
            }
            if (await modal.EvaluateAsync<bool>(UnfollowJs)) log("Desmarcado: seguir empresa");

            var submit = modal.Locator("button").Filter(new() { HasTextRegex = SubmitBtn });
            if (await submit.First.IsVisibleAsync())
            {
                if (dryRun || map) { await DiscardAsync(page); return ("dry-run", job, score); }
                await submit.First.ClickAsync();
                log("Candidatura enviada");
                await page.WaitForTimeoutAsync(3000);
                await page.Keyboard.PressAsync("Escape"); // close "application sent"
                return ("applied", job, score);
            }

            var next = modal.Locator("button").Filter(new() { HasTextRegex = ReviewBtn });
            if (!await next.First.IsVisibleAsync())
                next = modal.Locator("button").Filter(new() { HasTextRegex = NextBtn });
            if (!await next.First.IsVisibleAsync()) { await DumpAsync(page, jobId); await DiscardAsync(page); return ("failed: no next button", job, score); }

            await next.First.ClickAsync();
            log($"Etapa {step + 1} concluída");
            await page.WaitForTimeoutAsync(Random.Shared.Next(1200, 2200));

            var text = await modal.InnerTextAsync();
            same = text == last ? same + 1 : 0;
            last = text;
            if (same >= 3) { await DumpAsync(page, jobId); await DiscardAsync(page); return ("failed: stuck on a step", job, score); }
        }
        await DumpAsync(page, jobId);
        await DiscardAsync(page);
        return ("failed: too many steps", job, score);
    }

    static async Task FillAsync(IPage page, ILocator modal, List<Field> fields, Dictionary<string, string> answers, Action<string> log, List<string[]> sent)
    {
        foreach (var f in fields)
        {
            var ans = answers.GetValueOrDefault(f.Id, "");
            if (ans == "") continue;
            var loc = modal.Locator($"[data-lia='{f.Id}']");
            try
            {
                switch (f.Type)
                {
                    case "select":
                        await loc.SelectOptionAsync(new SelectOptionValue { Label = Answers.Best(ans, f.Options) ?? ans });
                        break;
                    case "radio" or "checkbox":
                        await loc.EvaluateAsync(PickJs, Answers.Best(ans, f.Options) ?? ans);
                        break;
                    default:
                        await loc.FillAsync(ans);
                        if (f.Combo) // location typeahead: choose the first suggestion
                        {
                            await page.WaitForTimeoutAsync(1500);
                            var opt = page.Locator("[role='listbox'] [role='option']").First;
                            if (await opt.IsVisibleAsync()) await opt.ClickAsync();
                        }
                        break;
                }
                log($"{f.Label} = {ans}");
                sent.Add([f.Label, ans]);
            }
            catch (Exception e) when (e is PlaywrightException or TimeoutException)
            {
                log($"Falhou ao preencher {f.Label}: {e.Message.Split('\n')[0]}");
            }
        }
    }

    // Mapping mode: the first MapJobs applications record every step (screenshot, dialog HTML, visible
    // buttons) under debug/map/<job>/ and never submit. Delete debug/map to map again.
    const int MapJobs = 0; // off; set to 10 (and delete debug/map) to map the page again
    public static bool Mapping(string jobId) => MapJobs > 0 && (
        Directory.Exists($"debug/map/{jobId}") ||
        (Directory.Exists("debug/map") ? Directory.GetDirectories("debug/map").Length : 0) < MapJobs);

    static async Task MapAsync(IPage page, string jobId, string name)
    {
        var dir = $"debug/map/{jobId}";
        Directory.CreateDirectory(dir);
        await page.ScreenshotAsync(new() { Path = $"{dir}/{name}.png" });
        await File.WriteAllTextAsync($"{dir}/{name}.json", await page.EvaluateAsync<string>(MapJs));
        await File.WriteAllTextAsync($"{dir}/{name}.html", await page.EvaluateAsync<string>(
            "() => { const o = document.querySelector('#interop-outlet')?.shadowRoot; return (o ? o.innerHTML : '') || document.body.outerHTML; }"));
    }

    // Every visible button/link-button and dialog, with the attributes selectors could key on.
    const string MapJs = """
        () => {
          const roots = [document, ...[...document.querySelectorAll('*')].map(e => e.shadowRoot).filter(Boolean)];
          const all = sel => roots.flatMap(r => [...r.querySelectorAll(sel)]);
          const vis = el => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden'; };
          const txt = el => (el.innerText || '').replace(/\s+/g, ' ').trim().slice(0, 80);
          return JSON.stringify({
            url: location.href,
            dialogs: all('[role=dialog], .artdeco-modal').filter(vis).map(d => ({
              cls: d.className, aria: d.getAttribute('aria-labelledby') || d.getAttribute('aria-label'), text: txt(d).slice(0, 160) })),
            buttons: all('button, a[role=button], a[href*="apply"]').filter(vis).map(b => ({
              tag: b.tagName, text: txt(b), aria: b.getAttribute('aria-label'), cls: b.className,
              dataTest: [...b.attributes].filter(a => a.name.startsWith('data-')).map(a => `${a.name}=${a.value}`).join(' '),
              inDialog: !!b.closest('[role=dialog], .artdeco-modal') })),
            fields: all('[role=dialog] input, [role=dialog] select, [role=dialog] textarea').filter(vis).map(f => ({
              tag: f.tagName, type: f.type, id: f.id, name: f.name, value: f.value, required: f.required || f.getAttribute('aria-required') })),
          }, null, 2);
        }
        """;

    // Saves what LinkedIn showed at the moment of a failure, so selectors get fixed against real markup.
    internal static async Task DumpAsync(IPage page, string jobId)
    {
        Directory.CreateDirectory("debug");
        await File.WriteAllTextAsync($"debug/job-{jobId}.html", await page.ContentAsync());
        await page.ScreenshotAsync(new() { Path = $"debug/job-{jobId}.png" });
    }

    static async Task DiscardAsync(IPage page)
    {
        await page.Keyboard.PressAsync("Escape");
        var discard = page.Locator("button").Filter(new() { HasTextRegex = DiscardBtn });
        try { await discard.First.ClickAsync(new() { Timeout = 5000 }); } catch (TimeoutException) { }
    }

    internal const string CleanJs = """
        const clean = t => { t = (t || '').replace(/\s+/g, ' ').trim();
          const h = t.length / 2; return t.length % 2 === 0 && t.slice(0, h) === t.slice(h) ? t.slice(0, h) : t; };
        // Radio/checkbox option text: the old markup puts it in label[for]; the new one leaves that label empty
        // and puts the text somewhere in the option's wrapper: the nearest ancestor that holds only this input and has text.
        const optText = i => {
          const own = clean(i.getRootNode().querySelector(`label[for="${CSS.escape(i.id)}"]`)?.innerText);
          if (own) return own;
          for (let e = i.parentElement; e && e.tagName !== 'FIELDSET' && e.querySelectorAll('input').length === 1; e = e.parentElement) {
            const t = clean(e.innerText || e.textContent); if (t) return t;
          }
          return clean(i.getAttribute('aria-label')) || (i.value !== 'on' ? clean(i.value) : ''); };
        """;

    // Tags each field that needs an answer with data-lia and returns it as JSON.
    // Included: empty required text fields, empty selects/radios, or any field with a validation error.
    const string ExtractJs = "modal => {" + CleanJs + """
          const labelOf = el => clean((el.id && modal.querySelector(`label[for="${CSS.escape(el.id)}"]`)?.innerText)
            || el.getAttribute('aria-label') || el.placeholder);
          const errOf = el => { const g = el.closest('.fb-dash-form-element, [data-test-form-element], .jobs-easy-apply-form-element')
            || el.parentElement?.parentElement?.parentElement;
            const e = g?.querySelector('.artdeco-inline-feedback--error'); if (e) return clean(e.innerText) || 'invalid';
            // New markup: no error class, and aria-describedby="error-message-…" is there even without an error.
            // The message is a <p> appended after the field, as the last child of its wrapper.
            const last = el.closest('[componentkey^="easyApplyFieldFocus"]')?.lastElementChild;
            if (last?.tagName === 'P' && !last.contains(el) && clean(last.innerText)) return clean(last.innerText);
            // Text inputs: the message is a direct <p> of the helper aria-describedby points at (the counter sits in a nested div).
            return clean(helpOf(el)?.querySelector(':scope > p')?.innerText) || null; };
          const helpOf = el => el.getAttribute('aria-describedby') && el.getRootNode().getElementById?.(el.getAttribute('aria-describedby'));
          // Character limit: maxlength, or the helper's "6/20" counter (its aria-hidden span; the spoken one is glued to it).
          const maxOf = el => el.maxLength > 0 ? el.maxLength
            : +(helpOf(el)?.querySelector('[aria-hidden=true]')?.textContent.match(/^\s*\d+\s*\/\s*(\d+)\s*$/)?.[1] || 0) || null;
          const visible = el => el.getClientRects().length > 0;
          // Tags from earlier steps can linger in the DOM and would make f0 match two elements.
          modal.querySelectorAll('[data-lia]').forEach(el => el.removeAttribute('data-lia'));
          let n = 0; const tag = el => { const id = 'f' + n++; el.setAttribute('data-lia', id); return id; };
          const out = [];

          modal.querySelectorAll('input[type=text], input[type=number], input[type=tel], input[type=email], input:not([type]), textarea').forEach(el => {
            if (!visible(el)) return;
            const err = errOf(el), req = el.required || el.getAttribute('aria-required') === 'true';
            if ((req && !el.value.trim()) || err) out.push({ id: tag(el), label: labelOf(el),
              type: el.type === 'number' || /numeric/.test(el.id) ? 'number' : el.tagName === 'TEXTAREA' ? 'textarea' : 'text',
              options: [], value: el.value, error: err, combo: el.getAttribute('role') === 'combobox', max: maxOf(el) });
          });

          modal.querySelectorAll('select').forEach(el => {
            if (!visible(el)) return;
            const placeholder = /^(select|selecione|selecionar)/i;
            // Optional selects (e.g. "if so, which type") stay empty; a required one we miss comes back via its error.
            const err = errOf(el), cur = clean(el.options[el.selectedIndex]?.text);
            const req = el.required || el.getAttribute('aria-required') === 'true';
            if ((req && (!cur || placeholder.test(cur))) || err) out.push({ id: tag(el), label: labelOf(el), type: 'select',
              options: [...el.options].map(o => clean(o.text)).filter(t => t && !placeholder.test(t)),
              value: cur, error: err, combo: false });
          });

          modal.querySelectorAll('fieldset').forEach(fs => {
            const radios = [...fs.querySelectorAll('input[type=radio]')], checks = [...fs.querySelectorAll('input[type=checkbox]')];
            const inputs = radios.length ? radios : checks;
            if (!inputs.length) return;
            const err = errOf(fs) || (fs.querySelector('.artdeco-inline-feedback--error') ? 'invalid' : null);
            if (inputs.some(i => i.checked) && !err) return;
            // Unchecked optional checkboxes (marketing opt-ins) stay unchecked; a required one we miss comes back via its error.
            // New markup has no legend: the question is the <p> right before the fieldset.
            const legend = fs.querySelector('legend span[aria-hidden=true]') || fs.querySelector('legend');
            const label = clean(legend?.innerText) || clean(fs.previousElementSibling?.innerText)
              || clean(fs.querySelector('[role=radio], [role=checkbox]')?.getAttribute('aria-label')) || labelOf(inputs[0]);
            const req = inputs.some(i => i.required) || fs.getAttribute('aria-required') === 'true' || /\*|required|obrigat/i.test(fs.innerText + label);
            if (!radios.length && !req && !err) return;
            out.push({ id: tag(fs), label, type: radios.length ? 'radio' : 'checkbox',
              options: inputs.map(optText), value: '', error: err, combo: false });
          });
          return JSON.stringify(out);
        }
        """;

    /// The user's resume for a language ("pt" / "en"), kept in resumes/<lang>/ under its original name
    /// (LinkedIn shows the file name, and matching it avoids uploading the same file twice).
    public static string? ResumeOf(string lang) =>
        Directory.Exists($"resumes/{lang}") ? Directory.GetFiles($"resumes/{lang}").FirstOrDefault() : null;

    static readonly HashSet<string> EnWords = ["the", "and", "to", "of", "you", "with", "for", "our", "will", "are", "is", "we", "your", "experience", "team", "skills"];
    static readonly HashSet<string> PtWords = ["de", "e", "que", "com", "para", "você", "nossa", "nosso", "será", "são", "é", "uma", "os", "das", "dos", "experiência", "equipe"];

    /// "en" or "pt" by common words; ties go to Portuguese (the default market).
    public static string Lang(string text)
    {
        int en = 0, pt = 0;
        foreach (Match m in Regex.Matches(text.ToLowerInvariant(), @"\p{L}+"))
            if (EnWords.Contains(m.Value)) en++;
            else if (PtWords.Contains(m.Value)) pt++;
        return en > pt ? "en" : "pt";
    }

    /// On a step asking for the resume: selects the user's file if LinkedIn already has it, else uploads it
    /// (through the file input, or the file picker the new markup's bare Upload button opens).
    static async Task PickResumeAsync(IPage page, ILocator modal, string resume, string lang, Action<string> log, List<string[]> sent)
    {
        var name = Path.GetFileName(resume);
        var kind = await modal.EvaluateAsync<string>(TagResumeInputJs);
        if (kind == "") return;
        var label = lang == "en" ? "inglês" : "português";
        var more = modal.Locator("button").Filter(new() { HasTextRegex = new(@"Show \d+ more|Mostrar mais \d+|Exibir mais", RegexOptions.IgnoreCase) });
        if (await more.First.IsVisibleAsync()) { await more.First.ClickAsync(); await page.WaitForTimeoutAsync(800); }
        if (await modal.EvaluateAsync<bool>(SelectResumeJs, name)) log($"Currículo em {label}: {name} (já estava no LinkedIn)");
        // New markup's resume step: LinkedIn already lists a resume (just not under this name); keep it, don't upload.
        else if (kind == "button" && await modal.EvaluateAsync<bool>(HasResumeJs)) { log("Currículo: mantido o que já estava no LinkedIn"); return; }
        else
        {
            try
            {
                var target = modal.Locator("[data-lia-resume]");
                if (kind == "button")
                    await (await page.RunAndWaitForFileChooserAsync(() => target.ClickAsync(), new() { Timeout = 10_000 })).SetFilesAsync(Path.GetFullPath(resume));
                else await target.SetInputFilesAsync(Path.GetFullPath(resume));
            }
            catch (Exception e) when (e is PlaywrightException or TimeoutException) { log($"Falhou ao enviar o currículo: {e.Message.Split('\n')[0]}"); return; }
            await page.WaitForTimeoutAsync(3000);
            await modal.EvaluateAsync<bool>(SelectResumeJs, name); // usually selected on upload; make sure
            log($"Currículo em {label}: {name} (enviado ao LinkedIn)");
        }
        sent.Add(["Currículo", name]);
    }

    // Tags the resume file input (not a cover letter one) with data-lia-resume: "input", or "button" for the new
    // markup's bare Upload button (no file input until clicked) next to a CV question; "" when this step has neither.
    const string TagResumeInputJs = """
        modal => {
          const inputs = [...modal.querySelectorAll('input[type=file]')];
          const about = i => `${i.id} ${i.name} ${i.getAttribute('aria-label') || ''} ${i.closest('div')?.parentElement?.innerText || ''}`;
          const input = inputs.find(i => /resume|curr[ií]culo|\bcv\b/i.test(about(i)) && !/cover|carta/i.test(i.id))
            || (inputs.length === 1 && !/cover|carta/i.test(about(inputs[0])) ? inputs[0] : null);
          const button = !input && [...modal.querySelectorAll('button')].find(b => { const q = b.parentElement?.innerText || '';
            return /^\s*(upload|carregar|fazer upload)/i.test(b.innerText) && /resume|curr[ií]culo|\bcv\b/i.test(q) && !/cover|carta/i.test(q); });
          modal.querySelectorAll('[data-lia-resume]').forEach(e => e.removeAttribute('data-lia-resume'));
          const hit = input || button;
          if (!hit) return '';
          hit.setAttribute('data-lia-resume', '');
          return input ? 'input' : 'button';
        }
        """;

    // True when the step shows some resume file (a leaf text like "cv.pdf"); the "PDF, DOC · 2 MB" hint doesn't match.
    const string HasResumeJs = """
        modal => [...modal.querySelectorAll('*')].some(e => e.children.length === 0 && /\S\.(pdf|docx?|odt|rtf)\s*$/i.test(e.textContent))
        """;

    // Clicks the radio of the resume card showing this file name. False when no card has it.
    const string SelectResumeJs = """
        (modal, name) => {
          const title = [...modal.querySelectorAll('h3, p, span, div')].find(e => e.children.length === 0 && e.textContent.trim() === name);
          let card = title;
          while (card && card !== modal && !card.querySelector('input[type=radio], [role=radio]')) card = card.parentElement;
          if (!card || card === modal) return false;
          const radio = card.querySelector('input[type=radio], [role=radio]');
          const label = radio.id && card.querySelector(`label[for="${CSS.escape(radio.id)}"]`);
          (label || radio).click();
          return true;
        }
        """;

    // Unchecks LinkedIn's pre-checked "Follow <company>" box. True when it clicked something.
    const string UnfollowJs = """
        modal => {
          let hit = false;
          for (const i of modal.querySelectorAll('input[type=checkbox]:checked, [role=checkbox][aria-checked=true]')) {
            const label = i.id && modal.querySelector(`label[for="${CSS.escape(i.id)}"]`);
            const text = `${i.id} ${label?.innerText || ''} ${i.getAttribute('aria-label') || ''} ${i.closest('div')?.innerText || ''}`;
            if (!/follow|seguir/i.test(text)) continue;
            (label || i).click();
            hit = true;
          }
          return hit;
        }
        """;

    internal const string PickJs = "(fs, ans) => {" + CleanJs + """
          for (const i of fs.querySelectorAll('input')) {
            if (optText(i).toLowerCase() === ans.toLowerCase()) {
              (fs.querySelector(`label[for="${CSS.escape(i.id)}"]`) || i).click(); return true; }
          }
          return false;
        }
        """;
}
