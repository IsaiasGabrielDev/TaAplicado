using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClaudeCodeBridge;

namespace LinkedInAutoApply;

/// Max: the text field's character limit, when the form shows one.
public record Field(string Id, string Label, string Type, string[] Options, string Value, string? Error, bool Combo, int? Max = null);
/// Reviewed: the user has checked this answer (always true for answers the user typed). Job: where it was first used.
public record Cached(string Label, string Answer, string By, bool Reviewed = false, string? Job = null);
public record Aviso(string Id, string JobId, string Job, string Label, string Type, string[] Options, string Suggestion, string? Error, DateTime At);
record ClaudeAnswer(string Id, string Answer, bool Confident);
/// One question pasted on the Perguntar screen and its answer.
public record Asked(string Question, string Answer, bool Confident);
/// Message: the reply to paste back to whoever asked, with [placeholders] where the profile had no answer.
public record AskResult(string? Message, List<Asked>? Items);
/// Claude's verdict on a job. InCountry / InLanguage are null when not asked or not answered (then they never block).
public record Relevance(bool Relevant, int Score, string Why, bool? InCountry, bool? InLanguage);

/// Claude replied without usable JSON (usage/session limit, CLI error). The run must stop instead of guessing.
public class ClaudeUnavailableException(string detail) : Exception($"Claude indisponível: {detail}");

/// Resolves form fields: answers.json cache → Claude → an aviso for the user (when Claude isn't confident).
/// claude answers forms and pasted questions (ai/answer.md); judge checks relevance (ai/judge.md, no saved answers).
public class Answers(IClaudeAgentService claude, IClaudeAgentService judge)
{
    public Dictionary<string, Cached> Cache { get; } = Store.Load<Dictionary<string, Cached>>("answers.json");
    public List<Aviso> Avisos { get; } = Store.Load<List<Aviso>>("avisos.json");
    /// Modo sem IA: nothing goes to Claude; every question not in the saved answers becomes an aviso.
    public bool NoAi { get; set; }
    /// The window is in English: what Claude writes for the user to read (reasons) comes in English too.
    public bool English { get; set; }

    /// Returns null when any field needs the user: the avisos are created and the application must wait.
    public async Task<Dictionary<string, string>?> ResolveAsync(string jobId, string job, List<Field> fields, Action<string> log)
    {
        var result = new Dictionary<string, string>();
        var pending = new List<Field>();

        foreach (var f in fields)
        {
            // A field with an error was already rejected, so the cache is wrong for this case.
            // An unlabeled field has no question to match: it would reuse whatever answer an empty label once got.
            // A choice field skips a saved answer that matches none of its options (the same question once came as free text).
            if (f.Error is null && Key(f.Label) != "" && Cache.TryGetValue(Key(f.Label), out var cached)
                && (f.Options.Length == 0 || Best(cached.Answer, f.Options) is not null))
                result[f.Id] = cached.Answer;
            else
                pending.Add(f);
        }
        if (pending.Count == 0) return result;

        List<ClaudeAnswer> parsed = [];
        if (NoAi) log($"Sem IA: {Plural(pending.Count, "pergunta nova vira aviso", "perguntas novas viram avisos")}");
        else
        {
            log($"Claude: {Plural(pending.Count, "pergunta nova", "perguntas novas")}");
            var r = await claude.RunPromptAsync(Prompt(job, pending));
            parsed = r.Success ? Parse(r.Content) : [];
            // A limit message comes back as "success" with no JSON; treat it as down, never as "unsure about everything".
            if (parsed.Count == 0) throw new ClaudeUnavailableException(Snippet(r));
        }

        var waiting = false;
        lock (Store.Gate)
        {
        foreach (var f in pending)
        {
            var a = parsed.FirstOrDefault(p => p.Id == f.Id);
            if (a is { Confident: true } && a.Answer != "")
            {
                result[f.Id] = a.Answer;
                if (Key(f.Label) != "") Cache[Key(f.Label)] = new(f.Label, a.Answer, "Claude", false, job);
                continue;
            }
            waiting = true;
            if (!Avisos.Any(v => v.JobId == jobId && Key(v.Label) == Key(f.Label)))
                Avisos.Add(new(Guid.NewGuid().ToString("N")[..8], jobId, job, f.Label, f.Type, f.Options,
                    a?.Answer ?? f.Value, f.Error, DateTime.Now));
        }
        SaveCache();
        Store.Save("avisos.json", Avisos);
        }
        return waiting ? null : result;
    }

    /// Answers every aviso with the same question. Returns the job ids that have no avisos left.
    public List<string> Answer(string avisoId, string answer, string by = "você")
    {
        var aviso = Avisos.FirstOrDefault(v => v.Id == avisoId);
        if (aviso is null) return [];
        var key = Key(aviso.Label);
        Cache[key] = new(aviso.Label, answer, by, by == "você", aviso.Job);
        var jobs = Avisos.Where(v => Key(v.Label) == key).Select(v => v.JobId).Distinct().ToList();
        Avisos.RemoveAll(v => Key(v.Label) == key);
        SaveCache();
        Store.Save("avisos.json", Avisos);
        return jobs.Where(j => !Avisos.Any(v => v.JobId == j)).ToList();
    }

    /// An answer the user wrote or checked on the Perguntar screen: theirs, reused by every form from now on.
    public void Save(string label, string answer)
    {
        Cache[Key(label)] = new(label, answer, "você", true);
        SaveCache();
    }

    /// Modo sem IA for pasted questions: one question per non-empty line, answered only from the saved answers.
    public List<Asked> Lookup(string text) => text.Split('\n').Select(l => Regex.Replace(l, @"^\s*(\d+[.)-]|[-*•])\s*", "").Trim()).Where(l => Key(l) != "")
        .Select(l => Cache.TryGetValue(Key(l), out var c) ? new Asked(l, c.Answer, true) : new Asked(l, "", false)).ToList();

    public void Edit(string key, string? answer)
    {
        if (answer is null) Cache.Remove(key);
        else if (Cache.TryGetValue(key, out var c)) Cache[key] = c with { Answer = answer, By = "você", Reviewed = true };
        SaveCache();
    }

    /// Asks Claude whether the job fits the candidate profile (rules in CLAUDE.md, "Relevance check").
    /// An unclear verdict counts as relevant (score 50); Claude being down stops the run (it once let off-profile jobs through).
    /// country / language: also ask whether the job is in that country and written in that language (null = don't ask).
    public async Task<Relevance> IsRelevantAsync(string job, string description, string? country = null, string? language = null)
    {
        var text = Regex.Replace(description, @"\s+", " ").Trim();
        if (text.Length > 6000) text = text[..6000];
        var shape = """{"relevant":true,"score":80,"reason":"<short reason>" """.TrimEnd();
        var asks = new List<string>();
        if (country is not null)
        {
            shape += ""","inCountry":true""";
            asks.Add($"- \"inCountry\": true if the job is located in {country}, or is remote and open to people living in {country}; false otherwise.");
        }
        if (language is not null)
        {
            shape += ""","inLanguage":true""";
            asks.Add($"- \"inLanguage\": true if the job description is written mainly in {language}; false otherwise.");
        }
        var r = await judge.RunPromptAsync($$"""
            Relevance check (see "Relevance check").
            Job: {{job}}

            Job page text:
            {{text}}

            Write the reason in {{(English ? "English" : "Portuguese")}}.
            Return ONLY a JSON object: {{shape}}}
            {{(asks.Count == 0 ? "" : "Also answer:" + Environment.NewLine + string.Join(Environment.NewLine, asks))}}
            """);
        if (!r.Success) throw new ClaudeUnavailableException(Snippet(r));
        int s = r.Content.IndexOf('{'), e = r.Content.LastIndexOf('}');
        if (s < 0 || e <= s) throw new ClaudeUnavailableException(Snippet(r));
        try
        {
            var v = JsonSerializer.Deserialize<JsonElement>(r.Content[s..(e + 1)]);
            bool? Flag(string k) => v.TryGetProperty(k, out var b) && b.ValueKind is JsonValueKind.True or JsonValueKind.False ? b.GetBoolean() : null;
            return new(!v.TryGetProperty("relevant", out var rel) || rel.GetBoolean(),
                    v.TryGetProperty("score", out var sc) && sc.TryGetInt32(out var n) ? Math.Clamp(n, 0, 100) : 50,
                    v.TryGetProperty("reason", out var why) ? why.GetString() ?? "" : "",
                    Flag("inCountry"), Flag("inLanguage"));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return new(true, 50, "", null, null); }
    }

    /// Re-asks Claude every pending aviso (e.g. after a usage limit). Confident answers resolve the aviso
    /// (and land in Conferência); unsure ones get a suggestion. Returns jobs with no avisos left.
    public async Task<List<string>> RetryAsync(Action<string> log)
    {
        if (NoAi) return [];
        List<Aviso> todo;
        lock (Store.Gate) todo = Avisos.ToList();
        var ready = new List<string>();
        foreach (var g in todo.GroupBy(a => a.JobId))
        {
            var fields = g.Select(a => new Field(a.Id, a.Label, a.Type, a.Options, "", a.Error, false)).ToList();
            var r = await claude.RunPromptAsync(Prompt(g.First().Job, fields));
            var parsed = r.Success ? Parse(r.Content) : [];
            if (parsed.Count == 0) throw new ClaudeUnavailableException(Snippet(r));
            int solved = 0;
            lock (Store.Gate)
            {
                foreach (var p in parsed.Where(p => p.Answer != ""))
                {
                    var i = Avisos.FindIndex(v => v.Id == p.Id);
                    if (i < 0) continue;
                    if (p.Confident) { ready.AddRange(Answer(p.Id, p.Answer, "Claude")); solved++; }
                    else Avisos[i] = Avisos[i] with { Suggestion = p.Answer };
                }
                Store.Save("avisos.json", Avisos);
            }
            log($"Claude revisou {Plural(g.Count(), "aviso", "avisos")} de {g.First().Job}: {Plural(solved, "respondido", "respondidos")}");
        }
        return ready.Distinct().ToList();
    }

    static string Snippet(AgentResult r)
    {
        var t = (r.Error ?? r.Content ?? "").Trim();
        return t.Length > 120 ? t[..120] : t;
    }

    public void DropAvisos(string jobId)
    {
        if (Avisos.RemoveAll(v => v.JobId == jobId) > 0) Store.Save("avisos.json", Avisos);
    }

    /// Saves the cache and rebuilds the system prompts, since ai/answer.md carries the saved answers.
    /// Only what the person typed or approved goes there: an unreviewed guess must not feed back into Claude.
    public void SaveCache()
    {
        Store.Save("answers.json", Cache);
        WritePrompts();
    }

    /// The two system prompts every Claude call runs with (--system-prompt-file), instead of Claude Code's own
    /// coding prompt: built from agent/CLAUDE.md (rules + profile) and the saved answers. ai/ has no CLAUDE.md.
    public void WritePrompts()
    {
        var (j, a) = Prompts(File.Exists("agent/CLAUDE.md") ? File.ReadAllText("agent/CLAUDE.md") : "", SavedAnswersMd(Cache.Values));
        Directory.CreateDirectory("ai");
        // Write then swap: a Claude call starting meanwhile reads the old file whole, never a half-written one.
        foreach (var (file, text) in new[] { ("ai/judge.md", j), ("ai/answer.md", a) })
        {
            File.WriteAllText(file + ".tmp", text);
            File.Move(file + ".tmp", file, true);
        }
    }

    /// judge: relevance rules + profile. answer: form rules + profile + saved answers, last so a new answer
    /// only changes the end of the prompt (the rest stays in Claude's prompt cache).
    internal static (string Judge, string Answer) Prompts(string md, string saved)
    {
        int sa = md.IndexOf("## Saved answers", StringComparison.Ordinal), rc = md.IndexOf("## Relevance check", StringComparison.Ordinal),
            cp = md.IndexOf("## Candidate profile", StringComparison.Ordinal);
        if (sa < 0 || rc < sa || cp < rc) return (md, md.Replace("@saved-answers.md", saved)); // unexpected layout: everything to both
        return (md[rc..], md[..sa] + md[cp..].TrimEnd() + "\n\n" + md[sa..rc].Replace("@saved-answers.md", saved));
    }

    internal static string SavedAnswersMd(IEnumerable<Cached> cache) =>
        "# Respostas salvas\n\n" + string.Concat(cache.Where(c => (c.By == "você" || c.Reviewed) && c.Label.Trim() != "").OrderBy(c => c.Label)
            .Select(c => $"- {c.Label.ReplaceLineEndings(" ")}: {c.Answer.ReplaceLineEndings(" ")}\n"));

    /// The user confirmed Claude's answer as-is.
    public void MarkReviewed(string key)
    {
        if (Cache.TryGetValue(key, out var c)) Cache[key] = c with { Reviewed = true };
        SaveCache();
    }

    static string Prompt(string job, List<Field> fields) => $$"""
        Job: {{job}}

        Answer the Easy Apply form fields below following your rules.
        Fields:
        {{JsonSerializer.Serialize(fields.Select(f => new { f.Id, f.Label, f.Type, f.Options, previous = f.Value, f.Error, maxLength = f.Max }))}}

        Return ONLY a JSON array, with no text before or after:
        [{"id":"...","answer":"...","confident":true}]
        """;

    // ── pure helpers (covered by SelfCheck) ──────────────────────────────────

    public static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

    public static string Key(string label) =>
        Regex.Replace(label.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

    internal static List<ClaudeAnswer> Parse(string content)
    {
        int s = content.IndexOf('['), e = content.LastIndexOf(']');
        if (s < 0 || e <= s) return [];
        try { return JsonSerializer.Deserialize<List<ClaudeAnswer>>(content[s..(e + 1)], Store.Json) ?? []; }
        catch (JsonException) { return []; }
    }

    /// Chooses the option that best matches Claude's answer: exact, then contains either way.
    public static string? Best(string answer, string[] options)
    {
        var a = Key(answer);
        return options.FirstOrDefault(o => Key(o) == a)
            ?? options.FirstOrDefault(o => a != "" && Key(o).Contains(a))
            ?? options.FirstOrDefault(o => Key(o) != "" && a.Contains(Key(o)));
    }

    public static void SelfCheck()
    {
        // Modo sem IA: a saved answer fills its field; a new question becomes an aviso (Claude is never called: it's null).
        var home = Directory.GetCurrentDirectory();
        var tmp = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(tmp, "agent"));
        Directory.SetCurrentDirectory(tmp);
        try
        {
            var offline = new Answers(null!, null!) { NoAi = true };
            offline.Cache[Key("Anos de C#?")] = new("Anos de C#?", "5", "você", true);
            Field F(string id, string label) => new(id, label, "text", [], "", null, false);
            Trace.Assert(offline.ResolveAsync("1", "Dev | X", [F("f0", "Anos de C#?")], _ => { }).GetAwaiter().GetResult() is { } ok && ok["f0"] == "5");
            Trace.Assert(offline.ResolveAsync("2", "Dev | Y", [new("f0", "Anos de C#?", "select", ["1-2", "3-4"], "", null, false)], _ => { }).GetAwaiter().GetResult() is null);
            Trace.Assert(offline.ResolveAsync("1", "Dev | X", [F("f0", "Anos de C#?"), F("f1", "Pretensão?")], _ => { }).GetAwaiter().GetResult() is null);
            Trace.Assert(offline.Avisos is [{ Label: "Anos de C#?", JobId: "2" }, { Label: "Pretensão?", JobId: "1" }]);
            Trace.Assert(offline.Lookup("1. Anos de C#?\r\n\n- Pretensão?") is [{ Question: "Anos de C#?", Answer: "5", Confident: true }, { Question: "Pretensão?", Answer: "", Confident: false }]);
        }
        finally { Directory.SetCurrentDirectory(home); Directory.Delete(tmp, true); }

        var fenced = "Here:\n```json\n[{\"id\":\"f0\",\"answer\":\"5\",\"confident\":true}]\n```";
        Trace.Assert(Parse(fenced) is [{ Id: "f0", Answer: "5", Confident: true }]);
        Trace.Assert(Parse("[{\"id\":\"f1\",\"answer\":\"Yes\",\"confident\":false}]").Single().Confident == false);
        Trace.Assert(Parse("no json").Count == 0);
        Trace.Assert(Key("How many years of C#? *") == Key("how many years of c#"));
        Trace.Assert(Best("yes", ["Yes", "No"]) == "Yes");
        Trace.Assert(Best("Professional", ["Select an option", "Native or bilingual", "Professional working"]) == "Professional working");
        Trace.Assert(Best("banana", ["Yes", "No"]) is null);
        Trace.Assert(Plural(1, "vaga", "vagas") == "1 vaga" && Plural(0, "vaga", "vagas") == "0 vagas");
        var md = SavedAnswersMd([new("Inglês?", "B1", "você"), new("Anos de Go?", "0", "Claude"), new("Anos\r\nde C#?", "5", "Claude", true), new(" ", "Autorizo", "você")]);
        Trace.Assert(md == "# Respostas salvas\n\n- Anos de C#?: 5\n- Inglês?: B1\n"); // unreviewed and label-less left out, one line each
        var (judge, answer) = Prompts("# A\nform\n## Saved answers\nuse:\n@saved-answers.md\n## Relevance check\nfit\n## Candidate profile\n- X\n", "- Q: R\n");
        Trace.Assert(judge == "## Relevance check\nfit\n## Candidate profile\n- X\n");
        Trace.Assert(answer == "# A\nform\n## Candidate profile\n- X\n\n## Saved answers\nuse:\n- Q: R\n\n");
    }
}

public static class Store
{
    /// ponytail: one global lock for UI thread + runner; per-file locks if it ever contends.
    public static readonly object Gate = new();
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static T Load<T>(string path) where T : new() =>
        File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) ?? new() : new();

    public static void Save(string path, object value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
}
