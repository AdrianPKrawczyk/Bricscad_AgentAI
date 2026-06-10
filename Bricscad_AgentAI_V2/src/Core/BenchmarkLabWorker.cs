using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Izolowany worker do laboratoryjnego uruchamiania benchmarkow z PromptOverridePath.
    /// Nie zapisuje wynikow do standardowej historii benchmarkow uzytkownika.
    /// </summary>
    public class BenchmarkLabWorker
    {
        private readonly AutoBenchmarkEngine _engine;

        public BenchmarkLabWorker(AutoBenchmarkEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public async Task<BenchmarkConfig> RunJobFileAsync(string jobPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobPath))
            {
                throw new ArgumentException("Job path is required.", nameof(jobPath));
            }

            if (!File.Exists(jobPath))
            {
                throw new FileNotFoundException($"Nie znaleziono pliku joba: {jobPath}", jobPath);
            }

            string json = File.ReadAllText(jobPath);
            var job = JsonConvert.DeserializeObject<BenchmarkLabJob>(json);
            if (job == null)
            {
                throw new InvalidOperationException($"Nie mozna odczytac joba: {jobPath}");
            }

            NormalizeJobPaths(job, jobPath);
            return await RunJobAsync(job, ct);
        }

        public async Task<BenchmarkConfig> RunJobAsync(BenchmarkLabJob job, CancellationToken ct = default)
        {
            ValidateJob(job);

            var benchmarkPaths = GetBenchmarkPaths(job).ToList();
            var results = new System.Collections.Generic.List<BenchmarkConfig>();
            var previousProviderId = LLMConfigManager.Current?.ActiveProviderId;
            bool providerChanged = TryActivateProvider(job.ProviderId);

            try
            {
                for (int i = 0; i < benchmarkPaths.Count; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        break;
                    }

                    var options = new BenchmarkRunOptions
                    {
                        ProfileName = job.ProfileName,
                        PromptOverridePath = job.PromptOverridePath,
                        OutputRoot = job.OutputRoot,
                        RunMode = "optimizer_lab",
                        CandidateId = job.CandidateId,
                        JobId = job.JobId,
                        ProviderId = job.ProviderId,
                        SaveToUserBenchmarkHistory = false,
                        SaveErrors = true
                    };

                    var result = await _engine.RunBenchmarkAsync(benchmarkPaths[i], options, ct);
                    results.Add(result);
                }

                WriteLabSummary(job, benchmarkPaths, results);
                return results.LastOrDefault();
            }
            finally
            {
                if (providerChanged && previousProviderId.HasValue)
                {
                    LLMConfigManager.SetActiveProvider(previousProviderId.Value);
                }
            }
        }

        public async Task<int> RunPendingJobsOnceAsync(string jobsRoot, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobsRoot))
            {
                throw new ArgumentException("Jobs root is required.", nameof(jobsRoot));
            }

            string pendingDir = Path.Combine(jobsRoot, "pending");
            string runningDir = Path.Combine(jobsRoot, "running");
            string doneDir = Path.Combine(jobsRoot, "done");
            string failedDir = Path.Combine(jobsRoot, "failed");

            Directory.CreateDirectory(pendingDir);
            Directory.CreateDirectory(runningDir);
            Directory.CreateDirectory(doneDir);
            Directory.CreateDirectory(failedDir);

            int completed = 0;
            foreach (var pendingPath in Directory.GetFiles(pendingDir, "*.json").OrderBy(p => p))
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                string fileName = Path.GetFileName(pendingPath);
                string runningPath = Path.Combine(runningDir, fileName);
                string donePath = Path.Combine(doneDir, fileName);
                string failedPath = Path.Combine(failedDir, fileName);

                File.Move(pendingPath, runningPath);

                try
                {
                    await RunJobFileAsync(runningPath, ct);
                    if (File.Exists(donePath)) File.Delete(donePath);
                    File.Move(runningPath, donePath);
                    completed++;
                }
                catch (Exception ex)
                {
                    string errorPath = Path.Combine(failedDir, Path.GetFileNameWithoutExtension(fileName) + ".error.txt");
                    File.WriteAllText(errorPath, ex.ToString());
                    if (File.Exists(failedPath)) File.Delete(failedPath);
                    if (File.Exists(runningPath)) File.Move(runningPath, failedPath);
                }
            }

            return completed;
        }

        private static void ValidateJob(BenchmarkLabJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            if (!string.Equals(job.RunMode, "optimizer_lab", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("BenchmarkLabWorker przyjmuje tylko joby z runMode='optimizer_lab'.");
            if (job.SaveToUserBenchmarkHistory)
                throw new InvalidOperationException("Job laboratoryjny nie moze zapisywac do historii benchmarkow uzytkownika.");
            var benchmarkPaths = GetBenchmarkPaths(job).ToList();
            if (benchmarkPaths.Count == 0)
                throw new InvalidOperationException("Job laboratoryjny musi wskazywac BenchmarkPath albo BenchmarkPaths.");
            foreach (var benchmarkPath in benchmarkPaths)
            {
                if (string.IsNullOrWhiteSpace(benchmarkPath) || !File.Exists(benchmarkPath))
                    throw new FileNotFoundException($"Nie znaleziono benchmarku: {benchmarkPath}", benchmarkPath);
            }
            if (string.IsNullOrWhiteSpace(job.ProfileName))
                throw new InvalidOperationException("Job laboratoryjny musi wskazywac ProfileName.");
            if (string.IsNullOrWhiteSpace(job.PromptOverridePath) || !File.Exists(job.PromptOverridePath))
                throw new FileNotFoundException($"Nie znaleziono PromptOverridePath: {job.PromptOverridePath}", job.PromptOverridePath);
            if (string.IsNullOrWhiteSpace(job.OutputRoot))
                throw new InvalidOperationException("Job laboratoryjny musi wskazywac OutputRoot.");
        }

        private static void NormalizeJobPaths(BenchmarkLabJob job, string jobPath)
        {
            string repoRoot = FindRepoRoot(jobPath);
            job.BenchmarkPath = ResolveJobPath(job.BenchmarkPath, repoRoot);
            if (job.BenchmarkPaths != null && job.BenchmarkPaths.Count > 0)
            {
                for (int i = 0; i < job.BenchmarkPaths.Count; i++)
                {
                    job.BenchmarkPaths[i] = ResolveJobPath(job.BenchmarkPaths[i], repoRoot);
                }
            }
            job.PromptOverridePath = ResolveJobPath(job.PromptOverridePath, repoRoot);
            job.OutputRoot = ResolveJobPath(job.OutputRoot, repoRoot);
        }

        private static System.Collections.Generic.IEnumerable<string> GetBenchmarkPaths(BenchmarkLabJob job)
        {
            if (job.BenchmarkPaths != null && job.BenchmarkPaths.Count > 0)
            {
                return job.BenchmarkPaths.Where(p => !string.IsNullOrWhiteSpace(p));
            }

            return string.IsNullOrWhiteSpace(job.BenchmarkPath)
                ? Enumerable.Empty<string>()
                : new[] { job.BenchmarkPath };
        }

        private static bool TryActivateProvider(string providerIdOrName)
        {
            if (string.IsNullOrWhiteSpace(providerIdOrName) || LLMConfigManager.Current?.Providers == null)
            {
                return false;
            }

            var providers = LLMConfigManager.Current.Providers;
            var requested = providerIdOrName.Trim();
            var provider = Guid.TryParse(requested, out var id)
                ? providers.FirstOrDefault(p => p.Id == id)
                : providers.FirstOrDefault(p =>
                    string.Equals(p.Name, requested, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.ModelName, requested, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(p.ModelName) && p.ModelName.IndexOf(requested, StringComparison.OrdinalIgnoreCase) >= 0));

            if (provider == null || provider.Id == LLMConfigManager.Current.ActiveProviderId)
            {
                return false;
            }

            LLMConfigManager.SetActiveProvider(provider.Id);
            return true;
        }

        private static void WriteLabSummary(BenchmarkLabJob job, System.Collections.Generic.IReadOnlyList<string> benchmarkPaths, System.Collections.Generic.IReadOnlyList<BenchmarkConfig> results)
        {
            if (results == null || results.Count == 0 || string.IsNullOrWhiteSpace(job.OutputRoot))
            {
                return;
            }

            Directory.CreateDirectory(job.OutputRoot);

            int totalTests = results.Sum(r => r.Tests.Count);
            int passedTests = results.Sum(r => r.Tests.Count(t => t.Passed));
            double weightedScore = totalTests > 0 ? Math.Round((double)passedTests / totalTests * 100, 2) : 0;
            var summary = new
            {
                jobId = job.JobId,
                runMode = "optimizer_lab",
                candidateId = job.CandidateId,
                promptOverridePath = job.PromptOverridePath,
                outputRoot = job.OutputRoot,
                runDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                totalBenchmarks = results.Count,
                totalTests,
                passedTests,
                failedTests = totalTests - passedTests,
                weightedGlobalScore = weightedScore,
                benchmarks = results.Select((result, index) => new
                {
                    benchmarkPath = benchmarkPaths[index],
                    benchmarkName = result.RunMetadata?.BenchmarkName ?? Path.GetFileNameWithoutExtension(benchmarkPaths[index]),
                    score = result.RunMetadata?.GlobalScore ?? 0,
                    testCount = result.Tests.Count,
                    passedCount = result.Tests.Count(t => t.Passed),
                    failedCount = result.Tests.Count(t => !t.Passed),
                    averageExecutionTimeMs = result.RunMetadata?.AverageExecutionTimeMs ?? 0
                }).ToList()
            };

            string summaryPath = Path.Combine(job.OutputRoot, $"{job.JobId}_SUMMARY.json");
            File.WriteAllText(summaryPath, JsonConvert.SerializeObject(summary, Formatting.Indented));
        }

        private static string ResolveJobPath(string path, string repoRoot)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            if (!string.IsNullOrWhiteSpace(repoRoot))
            {
                return Path.GetFullPath(Path.Combine(repoRoot, path));
            }

            return Path.GetFullPath(path);
        }

        private static string FindRepoRoot(string jobPath)
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(jobPath)) ?? Environment.CurrentDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Bricscad_AgentAI_V2")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "prompt-lab")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return string.Empty;
        }
    }
}
