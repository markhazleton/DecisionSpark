using System.CommandLine;
using System.Text.Json;

namespace DecisionSpecSeeder;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var inputOption = new Option<string>(
            name: "--input",
            description: "Path to sample JSON spec files (supports wildcards)",
            getDefaultValue: () => "./samples/*.json");

        var outputOption = new Option<string>(
            name: "--output",
            description: "Output directory for seeded specs",
            getDefaultValue: () => "./Config/DecisionSpecs");

        var rootCommand = new RootCommand("DecisionSpec Seeder - Seeds sample DecisionSpec JSON files for local development")
        {
            inputOption,
            outputOption
        };

        rootCommand.SetHandler(async (string input, string output) =>
        {
            await SeedSpecsAsync(input, output);
        }, inputOption, outputOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task SeedSpecsAsync(string inputPattern, string outputPath)
    {
        Console.WriteLine($"Seeding DecisionSpecs from '{inputPattern}' to '{outputPath}'");

        // Create output directory if it doesn't exist
        var outputDir = new DirectoryInfo(outputPath);
        if (!outputDir.Exists)
        {
            outputDir.Create();
            Console.WriteLine($"Created output directory: {outputPath}");
        }

        // Create status subdirectories
        var draftDir = Directory.CreateDirectory(Path.Combine(outputPath, "draft"));
        var publishedDir = Directory.CreateDirectory(Path.Combine(outputPath, "published"));
        Console.WriteLine($"Created subdirectories: draft, published");

        // Find all matching input files
        var inputDir = Path.GetDirectoryName(inputPattern) ?? ".";
        var searchPattern = Path.GetFileName(inputPattern);
        
        if (!Directory.Exists(inputDir))
        {
            Console.WriteLine($"Creating samples directory structure...");
            Directory.CreateDirectory(Path.Combine(inputDir, "samples"));
            await CreateSampleSpecsAsync(Path.Combine(inputDir, "samples"));
            inputDir = Path.Combine(inputDir, "samples");
        }

        var files = Directory.GetFiles(inputDir, searchPattern, SearchOption.TopDirectoryOnly);
        
        if (files.Length == 0)
        {
            Console.WriteLine($"No files found matching pattern: {inputPattern}");
            Console.WriteLine("Creating sample specs...");
            await CreateSampleSpecsAsync(inputDir);
            files = Directory.GetFiles(inputDir, searchPattern, SearchOption.TopDirectoryOnly);
        }

        Console.WriteLine($"Found {files.Length} spec file(s) to seed");

        int seeded = 0;
        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Extract specId and version
                var specId = root.GetProperty("specId").GetString() ?? Path.GetFileNameWithoutExtension(file);
                var version = root.TryGetProperty("version", out var versionProp) ? versionProp.GetString() : "1.0.0";
                var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "Draft";

                // Determine target directory
                var targetDir = status?.ToLowerInvariant() == "published" ? publishedDir.FullName : draftDir.FullName;
                var targetFile = Path.Combine(targetDir, $"{specId}.{version}.{status}.json");

                // Copy file with metadata updates
                await File.WriteAllTextAsync(targetFile, json);
                Console.WriteLine($"  ✓ Seeded: {Path.GetFileName(targetFile)}");
                seeded++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Failed to seed {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        Console.WriteLine($"\nSeeding complete: {seeded}/{files.Length} specs seeded successfully");
    }

    static async Task CreateSampleSpecsAsync(string samplesDir)
    {
        Directory.CreateDirectory(samplesDir);

        // Sample 1: Single Select
        var singleSelectSpec = new
        {
            specId = "sample-single-select",
            version = "1.0.0",
            status = "Draft",
            metadata = new
            {
                name = "Sample Single Select Decision",
                description = "A simple single-select decision flow",
                owner = "System",
                tags = new[] { "sample", "single-select" }
            },
            questions = new[]
            {
                new
                {
                    questionId = "q1",
                    type = "SingleSelect",
                    prompt = "What is your primary goal?",
                    required = true,
                    options = new[]
                    {
                        new { optionId = "o1", label = "Cost savings", value = "cost" },
                        new { optionId = "o2", label = "Performance", value = "performance" },
                        new { optionId = "o3", label = "Scalability", value = "scalability" }
                    }
                }
            },
            outcomes = new[]
            {
                new
                {
                    outcomeId = "outcome1",
                    selectionRules = new[] { "q1:cost" },
                    displayCards = new[] { new { title = "Cost-Optimized Solution", description = "Focus on reducing expenses" } }
                },
                new
                {
                    outcomeId = "outcome2",
                    selectionRules = new[] { "q1:performance" },
                    displayCards = new[] { new { title = "High-Performance Solution", description = "Maximize speed and efficiency" } }
                }
            }
        };

        await File.WriteAllTextAsync(
            Path.Combine(samplesDir, "sample-single-select.json"),
            JsonSerializer.Serialize(singleSelectSpec, new JsonSerializerOptions { WriteIndented = true }));

        // Sample 2: Multi Select
        var multiSelectSpec = new
        {
            specId = "sample-multi-select",
            version = "1.0.0",
            status = "Draft",
            metadata = new
            {
                name = "Sample Multi Select Decision",
                description = "A multi-select decision flow",
                owner = "System",
                tags = new[] { "sample", "multi-select" }
            },
            questions = new[]
            {
                new
                {
                    questionId = "q1",
                    type = "MultiSelect",
                    prompt = "Select all technologies you want to use:",
                    required = true,
                    options = new[]
                    {
                        new { optionId = "o1", label = "React", value = "react" },
                        new { optionId = "o2", label = "Vue", value = "vue" },
                        new { optionId = "o3", label = "Angular", value = "angular" }
                    }
                }
            },
            outcomes = new[]
            {
                new
                {
                    outcomeId = "outcome1",
                    selectionRules = new[] { "q1:react" },
                    displayCards = new[] { new { title = "React Stack", description = "Modern React-based solution" } }
                }
            }
        };

        await File.WriteAllTextAsync(
            Path.Combine(samplesDir, "sample-multi-select.json"),
            JsonSerializer.Serialize(multiSelectSpec, new JsonSerializerOptions { WriteIndented = true }));

        // Sample 3: Branching
        var branchingSpec = new
        {
            specId = "sample-branching",
            version = "1.0.0",
            status = "Published",
            metadata = new
            {
                name = "Sample Branching Decision",
                description = "A decision flow with conditional branching",
                owner = "System",
                tags = new[] { "sample", "branching" }
            },
            questions = new[]
            {
                new
                {
                    questionId = "q1",
                    type = "SingleSelect",
                    prompt = "Are you building a new project or migrating?",
                    required = true,
                    options = new[]
                    {
                        new { optionId = "o1", label = "New Project", value = "new", nextQuestionId = "q2" },
                        new { optionId = "o2", label = "Migration", value = "migrate", nextQuestionId = "q3" }
                    }
                },
                new
                {
                    questionId = "q2",
                    type = "SingleSelect",
                    prompt = "What is your team size?",
                    required = true,
                    options = new[]
                    {
                        new { optionId = "o3", label = "Small (1-5)", value = "small" },
                        new { optionId = "o4", label = "Large (5+)", value = "large" }
                    }
                },
                new
                {
                    questionId = "q3",
                    type = "SingleSelect",
                    prompt = "What framework are you migrating from?",
                    required = true,
                    options = new[]
                    {
                        new { optionId = "o5", label = "Legacy ASP.NET", value = "legacy" },
                        new { optionId = "o6", label = "Node.js", value = "node" }
                    }
                }
            },
            outcomes = new[]
            {
                new
                {
                    outcomeId = "outcome1",
                    selectionRules = new[] { "q1:new", "q2:small" },
                    displayCards = new[] { new { title = "Lean Startup Stack", description = "Optimized for small teams" } }
                },
                new
                {
                    outcomeId = "outcome2",
                    selectionRules = new[] { "q1:migrate", "q3:legacy" },
                    displayCards = new[] { new { title = "ASP.NET Core Migration", description = "Migrate from legacy ASP.NET" } }
                }
            }
        };

        await File.WriteAllTextAsync(
            Path.Combine(samplesDir, "sample-branching.json"),
            JsonSerializer.Serialize(branchingSpec, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"Created 3 sample specs in {samplesDir}");
    }
}
