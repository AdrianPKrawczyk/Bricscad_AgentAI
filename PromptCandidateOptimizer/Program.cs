using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var app = new PromptOptimizerCli();
return await app.RunAsync(args);

internal sealed class PromptOptimizerCli
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1).ToArray());

        try
        {
            return command switch
            {
                "suggest" => await SuggestAsync(options),
                "create-job" => await CreateJobAsync(options),
                "record-result" => await RecordResultAsync(options),
                "inspect" => await InspectAsync(options),
                _ => Fail($"Unknown command: {command}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 2;
        }
    }

    private static async Task<int> SuggestAsync(Dictionary<string, string> options)
    {
        string profile = Require(options, "profile");
        string sourcePath = Require(options, "source");
        string fullReportPath = Require(options, "full-report");
        string errorsReportPath = options.GetValueOrDefault("errors-report", fullReportPath);
        string outRoot = Require(options, "out");
        int maxChanges = int.TryParse(options.GetValueOrDefault("max-changes"), out var parsed) ? parsed : 5;

        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Source prompt not found.", sourcePath);
        if (!File.Exists(fullReportPath)) throw new FileNotFoundException("FULL report not found.", fullReportPath);
        if (!File.Exists(errorsReportPath)) throw new FileNotFoundException("ERRORS report not found.", errorsReportPath);

        Directory.CreateDirectory(outRoot);
        string candidateId = NextCandidateId(outRoot);
        string candidateDir = Path.Combine(outRoot, candidateId);
        Directory.CreateDirectory(candidateDir);

        string sourcePrompt = await File.ReadAllTextAsync(sourcePath, Encoding.UTF8);
        var report = BenchmarkReport.Load(fullReportPath, errorsReportPath);
        var failures = FailureClassifier.Classify(report).ToList();
        var proposedRules = BuildProposedRules(failures, maxChanges).ToList();
        string candidatePrompt = BuildCandidatePrompt(sourcePrompt, report.BenchmarkName, proposedRules);

        string candidateFileName = Path.GetFileNameWithoutExtension(sourcePath) + ".candidate" + Path.GetExtension(sourcePath);
        string candidatePath = Path.Combine(candidateDir, candidateFileName);
        await File.WriteAllTextAsync(candidatePath, candidatePrompt, Encoding.UTF8);

        var manifest = new CandidateManifest
        {
            CandidateId = candidateId,
            CreatedAt = DateTimeOffset.Now,
            ProfileName = profile,
            SourcePromptPath = sourcePath,
            CandidatePromptPath = candidatePath,
            FullReportPath = fullReportPath,
            ErrorsReportPath = errorsReportPath,
            BenchmarkName = report.BenchmarkName,
            ModelName = report.ModelName,
            BaselineScore = report.GlobalScore,
            FailedTestsCount = failures.Count,
            ProposedChangesCount = proposedRules.Count,
            Status = "candidate_created",
            ProductionPromptModified = false,
            LabRun = new LabRunInfo()
        };

        await WriteJsonAsync(Path.Combine(candidateDir, "manifest.json"), manifest);
        await WriteJsonAsync(Path.Combine(candidateDir, "classified_failures.json"), new { failures });
        await File.WriteAllTextAsync(Path.Combine(candidateDir, "diff.patch"), DiffGenerator.Create(Path.GetFileName(sourcePath), candidateFileName, sourcePrompt, candidatePrompt), Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(candidateDir, "rationale.md"), RationaleWriter.Create(report, failures, proposedRules, manifest), Encoding.UTF8);
        await File.WriteAllTextAsync(Path.Combine(candidateDir, "review_checklist.md"), ReviewChecklist(), Encoding.UTF8);

        Console.WriteLine("=== PROMPT CANDIDATE CREATED ===");
        Console.WriteLine($"Candidate: {candidateDir}");
        Console.WriteLine($"Source prompt: {sourcePath}");
        Console.WriteLine($"Benchmark: {report.BenchmarkName}");
        Console.WriteLine($"Baseline score: {report.GlobalScore:F2}%");
        Console.WriteLine($"Failed tests analyzed: {failures.Count}");
        Console.WriteLine($"Changes proposed: {proposedRules.Count}");
        return 0;
    }

    private static async Task<int> CreateJobAsync(Dictionary<string, string> options)
    {
        string candidateDir = Require(options, "candidate");
        string benchmarkPath = Require(options, "benchmark");
        string profile = Require(options, "profile");
        string jobsRoot = Require(options, "jobs-root");
        string provider = options.GetValueOrDefault("provider", "");
        double? targetScore = double.TryParse(options.GetValueOrDefault("target-score"), out var target) ? target : null;

        var manifestPath = Path.Combine(candidateDir, "manifest.json");
        var manifest = await ReadJsonAsync<CandidateManifest>(manifestPath);
        string jobId = "job_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string outputRoot = Path.Combine(candidateDir, "bricscad-results");

        var job = new BenchmarkLabJob
        {
            JobId = jobId,
            RunMode = "optimizer_lab",
            BenchmarkPath = Path.GetFullPath(benchmarkPath),
            ProfileName = profile,
            ProviderId = provider,
            PromptOverridePath = Path.GetFullPath(manifest.CandidatePromptPath),
            CandidateId = manifest.CandidateId,
            OutputRoot = Path.GetFullPath(outputRoot),
            TargetScore = targetScore,
            SaveToUserBenchmarkHistory = false
        };

        Directory.CreateDirectory(Path.Combine(jobsRoot, "pending"));
        Directory.CreateDirectory(outputRoot);
        string jobPath = Path.Combine(jobsRoot, "pending", jobId + ".json");
        await WriteJsonAsync(jobPath, job);
        await WriteJsonAsync(Path.Combine(candidateDir, "bricscad-job.json"), job);

        manifest.LabRun.Enabled = true;
        manifest.LabRun.TargetScore = targetScore;
        manifest.LabRun.LastJobId = jobId;
        manifest.LabRun.LastResultPath = outputRoot;
        await WriteJsonAsync(manifestPath, manifest);

        Console.WriteLine("=== BRICSCAD LAB JOB CREATED ===");
        Console.WriteLine($"Job: {jobPath}");
        Console.WriteLine($"Candidate: {candidateDir}");
        Console.WriteLine($"OutputRoot: {outputRoot}");
        return 0;
    }

    private static async Task<int> RecordResultAsync(Dictionary<string, string> options)
    {
        string candidateDir = Require(options, "candidate");
        string fullReportPath = Require(options, "full-report");
        string errorsReportPath = options.GetValueOrDefault("errors-report", "");
        var manifestPath = Path.Combine(candidateDir, "manifest.json");
        var manifest = await ReadJsonAsync<CandidateManifest>(manifestPath);
        var report = BenchmarkReport.Load(fullReportPath, string.IsNullOrWhiteSpace(errorsReportPath) ? fullReportPath : errorsReportPath);

        manifest.Status = manifest.LabRun.TargetScore.HasValue && report.GlobalScore >= manifest.LabRun.TargetScore.Value
            ? "target_reached"
            : "benchmarked";
        manifest.LabRun.LastScore = report.GlobalScore;
        manifest.LabRun.LastResultPath = Path.GetDirectoryName(Path.GetFullPath(fullReportPath));
        await WriteJsonAsync(manifestPath, manifest);

        Console.WriteLine("=== RESULT RECORDED ===");
        Console.WriteLine($"Candidate: {candidateDir}");
        Console.WriteLine($"Score: {report.GlobalScore:F2}%");
        Console.WriteLine($"Status: {manifest.Status}");
        return 0;
    }

    private static async Task<int> InspectAsync(Dictionary<string, string> options)
    {
        string candidateDir = Require(options, "candidate");
        var manifest = await ReadJsonAsync<CandidateManifest>(Path.Combine(candidateDir, "manifest.json"));
        Console.WriteLine($"Candidate: {manifest.CandidateId}");
        Console.WriteLine($"Status: {manifest.Status}");
        Console.WriteLine($"Benchmark: {manifest.BenchmarkName}");
        Console.WriteLine($"Baseline: {manifest.BaselineScore:F2}%");
        Console.WriteLine($"Last score: {(manifest.LabRun.LastScore.HasValue ? manifest.LabRun.LastScore.Value.ToString("F2") + "%" : "(none)")}");
        Console.WriteLine($"Prompt: {manifest.CandidatePromptPath}");
        return 0;
    }

    private static IEnumerable<string> BuildProposedRules(IEnumerable<ClassifiedFailure> failures, int maxChanges)
    {
        return failures
            .Where(f => f.Classification == "prompt_gap" || f.Classification == "model_bug")
            .GroupBy(f => f.Category)
            .Take(maxChanges)
            .Select(g => RuleForCategory(g.Key, g.ToList()))
            .Where(r => !string.IsNullOrWhiteSpace(r));
    }

    private static string RuleForCategory(string category, IReadOnlyList<ClassifiedFailure> failures)
    {
        string joinedErrors = string.Join(" | ", failures.SelectMany(f => f.FailedRules).Take(3));
        string lower = (category + " " + joinedErrors).ToLowerInvariant();

        if (lower.Contains("foreach"))
            return "- Gdy zadanie dotyczy wielu elementow tego samego typu, preferuj Foreach zamiast serii osobnych wywolan narzedzia.";
        if (lower.Contains("insertblock"))
            return "- Dla InsertBlock z wieloma blokami, punktami lub atrybutami wybierz stabilny wzorzec: Foreach + Action z parametrami zaleznymi od {item}.";
        if (lower.Contains("editattributes"))
            return "- Dla przeplywow z atrybutami zachowaj kolejnosc: odczyt lub wybor celu, operacja blokowa, a na koncu EditAttributes.";
        if (lower.Contains("sequence") || lower.Contains("kolejn"))
            return "- W zadaniach wielokrokowych zachowuj kolejnosc narzedzi wynikajaca z polecenia uzytkownika; nie pomijaj krokow posrednich.";

        return $"- Dla kategorii {category} doprecyzuj wybor narzedzia i argumentow zgodnie z niezaliczonymi testami benchmarku.";
    }

    private static string BuildCandidatePrompt(string sourcePrompt, string benchmarkName, IReadOnlyList<string> proposedRules)
    {
        if (proposedRules.Count == 0) return sourcePrompt;
        var sb = new StringBuilder(sourcePrompt.TrimEnd());
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine($"Dodatkowe zasady po analizie {benchmarkName}:");
        foreach (var rule in proposedRules.Distinct())
        {
            if (!sourcePrompt.Contains(rule, StringComparison.OrdinalIgnoreCase))
                sb.AppendLine(rule);
        }
        return sb.ToString();
    }

    private static string ReviewChecklist() => """
        # Review checklist

        - [ ] Diff jest maly i zrozumialy.
        - [ ] Zmiany dotycza failed tests z raportu.
        - [ ] Brak zmian w promptach produkcyjnych.
        - [ ] Brak duplikatow istniejacych regul.
        - [ ] Brak regul sprzecznych z aktualnym kontraktem narzedzi.
        - [ ] Kandydat nie przekracza limitu dlugosci promptu.
        - [ ] Uruchomiono benchmark w BricsCAD z PromptOverridePath albo przez optimizer_lab.
        - [ ] Porownano wynik z baseline.
        """;

    private static string NextCandidateId(string outRoot)
    {
        int next = Directory.GetDirectories(outRoot, "candidate_*")
            .Select(Path.GetFileName)
            .Select(n => int.TryParse(n?.Replace("candidate_", ""), out var id) ? id : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return "candidate_" + next.ToString("000");
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            string key = args[i][2..];
            string value = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
            result[key] = value;
        }
        return result;
    }

    private static string Require(Dictionary<string, string> options, string key)
    {
        if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing --{key}");
        return value;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string json = JsonSerializer.Serialize(value, JsonOptions);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8);
    }

    private static async Task<T> ReadJsonAsync<T>(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("JSON file not found.", path);
        string json = await File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException($"Cannot parse {path}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("PromptCandidateOptimizer");
        Console.WriteLine("Commands:");
        Console.WriteLine("  suggest --profile P --source prompt.txt --full-report FULL.json --errors-report ERRORS.json --out prompt-lab/blocks");
        Console.WriteLine("  create-job --candidate candidate_dir --benchmark Benchmark.json --profile CadBlocksProfile --jobs-root prompt-lab/jobs");
        Console.WriteLine("  record-result --candidate candidate_dir --full-report FULL.json [--errors-report ERRORS.json]");
        Console.WriteLine("  inspect --candidate candidate_dir");
    }
}

internal sealed class BenchmarkReport
{
    public string BenchmarkName { get; init; } = "";
    public string ModelName { get; init; } = "";
    public double GlobalScore { get; init; }
    public List<FailedTest> FailedTests { get; init; } = [];

    public static BenchmarkReport Load(string fullReportPath, string errorsReportPath)
    {
        using var fullDoc = JsonDocument.Parse(File.ReadAllText(fullReportPath));
        var meta = fullDoc.RootElement.GetProperty("RunMetadata");
        string benchmarkName = GetString(meta, "BenchmarkName", Path.GetFileNameWithoutExtension(fullReportPath));
        string modelName = GetString(meta, "ModelName", "");
        double score = GetDouble(meta, "GlobalScore", 0);

        string failedSource = File.Exists(errorsReportPath) ? errorsReportPath : fullReportPath;
        using var errorsDoc = JsonDocument.Parse(File.ReadAllText(failedSource));
        var failures = new List<FailedTest>();
        if (errorsDoc.RootElement.TryGetProperty("Tests", out var tests))
        {
            foreach (var test in tests.EnumerateArray())
            {
                bool passed = GetBool(test, "Passed", false);
                if (passed) continue;
                failures.Add(new FailedTest
                {
                    Id = GetInt(test, "Id", 0),
                    TestName = GetString(test, "TestName", ""),
                    Category = GetString(test, "Category", ""),
                    Difficulty = GetInt(test, "Difficulty", 0),
                    UserPrompt = GetString(test, "UserPrompt", ""),
                    FailedRules = GetStringArray(test, "FailedRulesErrors")
                });
            }
        }

        return new BenchmarkReport
        {
            BenchmarkName = benchmarkName,
            ModelName = modelName,
            GlobalScore = score,
            FailedTests = failures
        };
    }

    private static string GetString(JsonElement element, string property, string fallback)
        => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : fallback;
    private static double GetDouble(JsonElement element, string property, double fallback)
        => element.TryGetProperty(property, out var value) && value.TryGetDouble(out var number) ? number : fallback;
    private static int GetInt(JsonElement element, string property, int fallback)
        => element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : fallback;
    private static bool GetBool(JsonElement element, string property, bool fallback)
        => element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
    private static List<string> GetStringArray(JsonElement element, string property)
        => element.TryGetProperty(property, out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(v => v.ToString()).ToList()
            : [];
}

internal sealed record FailedTest
{
    public int Id { get; init; }
    public string TestName { get; init; } = "";
    public string Category { get; init; } = "";
    public int Difficulty { get; init; }
    public string UserPrompt { get; init; } = "";
    public List<string> FailedRules { get; init; } = [];
}

internal sealed record ClassifiedFailure
{
    public int TestId { get; init; }
    public string TestName { get; init; } = "";
    public string Category { get; init; } = "";
    public int Difficulty { get; init; }
    public string Classification { get; init; } = "unclear";
    public double Confidence { get; init; }
    public List<string> FailedRules { get; init; } = [];
    public string RecommendedAction { get; init; } = "";
}

internal static class FailureClassifier
{
    public static IEnumerable<ClassifiedFailure> Classify(BenchmarkReport report)
    {
        foreach (var failure in report.FailedTests)
        {
            string text = (failure.Category + " " + failure.TestName + " " + string.Join(" ", failure.FailedRules)).ToLowerInvariant();
            string classification;
            double confidence;
            string action;

            if (text.Contains("anyargumentmatch") || text.Contains("format") || text.Contains("wariant"))
            {
                classification = "benchmark_bug";
                confidence = 0.65;
                action = "Review validator tolerance before changing prompt.";
            }
            else if (text.Contains("nieistniej") || text.Contains("halucyn"))
            {
                classification = "model_bug";
                confidence = 0.7;
                action = "Add a compact negative rule only if the pattern repeats.";
            }
            else if (text.Contains("foreach") || text.Contains("sequence") || text.Contains("kolejn") || text.Contains("brak"))
            {
                classification = "prompt_gap";
                confidence = 0.75;
                action = "Add a small, concrete rule tied to the failed workflow.";
            }
            else if (text.Contains("argument") || text.Contains("schema") || text.Contains("parametr"))
            {
                classification = "schema_gap";
                confidence = 0.55;
                action = "Review tool contract; do not mask schema ambiguity with broad prompt rules.";
            }
            else
            {
                classification = "unclear";
                confidence = 0.35;
                action = "Manual review required.";
            }

            yield return new ClassifiedFailure
            {
                TestId = failure.Id,
                TestName = failure.TestName,
                Category = failure.Category,
                Difficulty = failure.Difficulty,
                Classification = classification,
                Confidence = confidence,
                FailedRules = failure.FailedRules,
                RecommendedAction = action
            };
        }
    }
}

internal static class DiffGenerator
{
    public static string Create(string beforeName, string afterName, string before, string after)
    {
        var beforeLines = before.Replace("\r\n", "\n").Split('\n');
        var afterLines = after.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        sb.AppendLine("--- " + beforeName);
        sb.AppendLine("+++ " + afterName);
        sb.AppendLine("@@");
        foreach (var line in beforeLines.Except(afterLines))
            if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine("-" + line);
        foreach (var line in afterLines.Except(beforeLines))
            if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine("+" + line);
        return sb.ToString();
    }
}

internal static class RationaleWriter
{
    public static string Create(BenchmarkReport report, IReadOnlyList<ClassifiedFailure> failures, IReadOnlyList<string> rules, CandidateManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Candidate rationale");
        sb.AppendLine();
        sb.AppendLine("## Source");
        sb.AppendLine($"- Profile: {manifest.ProfileName}");
        sb.AppendLine($"- Source prompt: {manifest.SourcePromptPath}");
        sb.AppendLine($"- Benchmark report: {manifest.FullReportPath}");
        sb.AppendLine($"- Baseline score: {report.GlobalScore:F2}%");
        sb.AppendLine();
        sb.AppendLine("## Failed tests analyzed");
        sb.AppendLine("| Test | Category | Difficulty | Classification | Decision |");
        sb.AppendLine("|------|----------|------------|----------------|----------|");
        foreach (var failure in failures)
            sb.AppendLine($"| {failure.TestId} | {failure.Category} | D{failure.Difficulty} | {failure.Classification} | {failure.RecommendedAction} |");
        sb.AppendLine();
        sb.AppendLine("## Proposed changes");
        if (rules.Count == 0) sb.AppendLine("- No prompt changes proposed; manual review required.");
        foreach (var rule in rules) sb.AppendLine("- " + rule.TrimStart('-', ' '));
        sb.AppendLine();
        sb.AppendLine("## Not changed");
        foreach (var failure in failures.Where(f => f.Classification is "benchmark_bug" or "schema_gap" or "unclear"))
            sb.AppendLine($"- Test {failure.TestId}: {failure.Classification}, {failure.RecommendedAction}");
        sb.AppendLine();
        sb.AppendLine("## Regression risks");
        sb.AppendLine("- Added rules may over-trigger on simpler tasks; verify category-level regressions after benchmark.");
        sb.AppendLine("- Do not promote this candidate unless BricsCAD benchmark confirms improvement without unacceptable regressions.");
        return sb.ToString();
    }
}

internal sealed class CandidateManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string CandidateId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string ProfileName { get; set; } = "";
    public string SourcePromptPath { get; set; } = "";
    public string CandidatePromptPath { get; set; } = "";
    public string FullReportPath { get; set; } = "";
    public string ErrorsReportPath { get; set; } = "";
    public string BenchmarkName { get; set; } = "";
    public string ModelName { get; set; } = "";
    public double BaselineScore { get; set; }
    public int FailedTestsCount { get; set; }
    public int ProposedChangesCount { get; set; }
    public string Status { get; set; } = "candidate_created";
    public bool ProductionPromptModified { get; set; }
    public LabRunInfo LabRun { get; set; } = new();
}

internal sealed class LabRunInfo
{
    public bool Enabled { get; set; }
    public double? TargetScore { get; set; }
    public double? LastScore { get; set; }
    public string? LastJobId { get; set; }
    public string? LastResultPath { get; set; }
}

internal sealed class BenchmarkLabJob
{
    public int SchemaVersion { get; set; } = 1;
    public string JobId { get; set; } = "";
    public string RunMode { get; set; } = "optimizer_lab";
    public string BenchmarkPath { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public string ProviderId { get; set; } = "";
    public string PromptOverridePath { get; set; } = "";
    public string CandidateId { get; set; } = "";
    public string OutputRoot { get; set; } = "";
    public double? TargetScore { get; set; }
    public bool SaveToUserBenchmarkHistory { get; set; }
}
