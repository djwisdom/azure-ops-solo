using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MyCrownJewelApp.Pfpad
{
    /// <summary>
    /// Analyzes a repository to provide insights about its structure, purpose, and conventions
    /// </summary>
    public class RepositoryAnalyticsEngine
    {
        private readonly string _workspaceRoot;

        public RepositoryAnalyticsEngine(string workspaceRoot)
        {
            _workspaceRoot = workspaceRoot;
        }

        public async Task<RepositoryAnalysis> AnalyzeAsync()
        {
            var analysis = new RepositoryAnalysis
            {
                WorkspaceRoot = _workspaceRoot,
                RepositoryName = Path.GetFileName(_workspaceRoot.TrimEnd(Path.DirectorySeparatorChar))
            };

            // Analyze file structure
            await AnalyzeFileStructureAsync(analysis);

            // Detect project type and language
            await DetectProjectTypeAsync(analysis);

            // Parse documentation
            await ParseDocumentationAsync(analysis);

            // Analyze naming conventions
            await AnalyzeNamingConventionsAsync(analysis);

            // Analyze dependencies
            await AnalyzeDependenciesAsync(analysis);

            // Analyze code quality metrics
            await AnalyzeCodeQualityAsync(analysis);

            // Analyze architecture patterns
            await AnalyzeArchitectureAsync(analysis);

            // Analyze project maturity
            await AnalyzeProjectMaturityAsync(analysis);

            // Analyze development activity (Git history simulation)
            await AnalyzeDevelopmentActivityAsync(analysis);

            // Analyze code complexity
            await AnalyzeCodeComplexityAsync(analysis);

            // Analyze security considerations
            await AnalyzeSecurityAsync(analysis);

            // Analyze performance characteristics
            await AnalyzePerformanceAsync(analysis);

            // Generate DeepWiki-style comprehensive summary
            GenerateDeepWikiSummary(analysis);

            return analysis;
        }

        private async Task AnalyzeFileStructureAsync(RepositoryAnalysis analysis)
        {
            var allFiles = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => !IsIgnoredPath(f))
                .ToList();

            analysis.TotalFiles = allFiles.Count;
            analysis.FileExtensions = allFiles
                .GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            // Categorize files
            analysis.CodeFiles = allFiles.Count(f => IsCodeFile(f));
            analysis.ConfigFiles = allFiles.Count(f => IsConfigFile(f));
            analysis.DocumentationFiles = allFiles.Count(f => IsDocumentationFile(f));

            // Directory structure analysis
            var directories = Directory.EnumerateDirectories(_workspaceRoot, "*", SearchOption.AllDirectories)
                .Where(d => !IsIgnoredPath(d))
                .Select(d => d.Substring(_workspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar))
                .ToList();

            analysis.DirectoryStructure = directories
                .GroupBy(d => d.Split(Path.DirectorySeparatorChar).FirstOrDefault() ?? "")
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());
        }

        private async Task DetectProjectTypeAsync(RepositoryAnalysis analysis)
        {
            var indicators = new Dictionary<string, (string Type, string Language, int Weight)>();

            // Check for specific files and patterns
            if (File.Exists(Path.Combine(_workspaceRoot, "main.tf")) ||
                File.Exists(Path.Combine(_workspaceRoot, "variables.tf")))
            {
                indicators["terraform"] = ("Infrastructure as Code", "HCL/Terraform", 100);
            }

            if (File.Exists(Path.Combine(_workspaceRoot, "main.bicep")) ||
                Directory.EnumerateFiles(_workspaceRoot, "*.bicep", SearchOption.AllDirectories).Any())
            {
                indicators["bicep"] = ("Infrastructure as Code", "Bicep", 100);
            }

            if (File.Exists(Path.Combine(_workspaceRoot, "ansible.cfg")) ||
                File.Exists(Path.Combine(_workspaceRoot, "playbook.yml")) ||
                Directory.EnumerateFiles(_workspaceRoot, "*.yml", SearchOption.AllDirectories)
                    .Any(f => Path.GetFileName(f).Contains("playbook")))
            {
                indicators["ansible"] = ("Configuration Management", "YAML/Ansible", 100);
            }

            if (File.Exists(Path.Combine(_workspaceRoot, "CMakeLists.txt")) ||
                File.Exists(Path.Combine(_workspaceRoot, "Makefile")))
            {
                indicators["c-cpp"] = ("System Software", "C/C++", 90);
            }

            if (File.Exists(Path.Combine(_workspaceRoot, "setup.py")) ||
                File.Exists(Path.Combine(_workspaceRoot, "requirements.txt")) ||
                File.Exists(Path.Combine(_workspaceRoot, "Pipfile")))
            {
                indicators["python"] = ("Application/Library", "Python", 85);
            }

            if (File.Exists(Path.Combine(_workspaceRoot, "package.json")))
            {
                indicators["node"] = ("Web Application", "JavaScript/Node.js", 80);
            }

            if (Directory.EnumerateFiles(_workspaceRoot, "*.csproj", SearchOption.AllDirectories).Any())
            {
                indicators["dotnet"] = ("Application/Library", ".NET/C#", 95);
            }

            // Determine primary type based on file extensions
            var topExtension = analysis.FileExtensions.FirstOrDefault();
            if (topExtension.Key == ".tf" || topExtension.Key == ".tfvars")
                indicators["terraform"] = ("Infrastructure as Code", "HCL/Terraform", 100);
            else if (topExtension.Key == ".bicep")
                indicators["bicep"] = ("Infrastructure as Code", "Bicep", 100);
            else if (topExtension.Key == ".yml" || topExtension.Key == ".yaml")
                indicators["ansible"] = ("Configuration Management", "YAML/Ansible", 100);
            else if (topExtension.Key == ".c" || topExtension.Key == ".cpp" || topExtension.Key == ".h")
                indicators["c-cpp"] = ("System Software", "C/C++", 90);
            else if (topExtension.Key == ".py")
                indicators["python"] = ("Application/Library", "Python", 85);
            else if (topExtension.Key == ".js" || topExtension.Key == ".ts")
                indicators["node"] = ("Web Application", "JavaScript/TypeScript", 80);
            else if (topExtension.Key == ".cs")
                indicators["dotnet"] = ("Application/Library", ".NET/C#", 95);

            var bestMatch = indicators.OrderByDescending(x => x.Value.Weight).FirstOrDefault();
            if (bestMatch.Value.Type != null)
            {
                analysis.ProjectType = bestMatch.Value.Type;
                analysis.PrimaryLanguage = bestMatch.Value.Language;
            }
            else
            {
                analysis.ProjectType = "Unknown";
                analysis.PrimaryLanguage = "Mixed/Other";
            }
        }

        private async Task ParseDocumentationAsync(RepositoryAnalysis analysis)
        {
            // Look for common documentation files
            var docFiles = new[] { "README.md", "README.txt", "README", "readme.md", "readme.txt" };
            string? readmePath = null;

            foreach (var docFile in docFiles)
            {
                var path = Path.Combine(_workspaceRoot, docFile);
                if (File.Exists(path))
                {
                    readmePath = path;
                    break;
                }
            }

            if (readmePath != null)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(readmePath);
                    analysis.ReadmeSummary = ExtractReadmeSummary(content);
                }
                catch
                {
                    analysis.ReadmeSummary = "Could not read README file";
                }
            }

            // Check for other documentation
            analysis.HasContributingGuide = File.Exists(Path.Combine(_workspaceRoot, "CONTRIBUTING.md")) ||
                                          File.Exists(Path.Combine(_workspaceRoot, "CONTRIBUTING.txt"));
            analysis.HasChangelog = File.Exists(Path.Combine(_workspaceRoot, "CHANGELOG.md")) ||
                                   File.Exists(Path.Combine(_workspaceRoot, "CHANGELOG.txt")) ||
                                   File.Exists(Path.Combine(_workspaceRoot, "HISTORY.md"));
        }

        private async Task AnalyzeNamingConventionsAsync(RepositoryAnalysis analysis)
        {
            var namingConventions = new Dictionary<string, int>();

            // Analyze file naming patterns
            var codeFiles = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => IsCodeFile(f) && !IsIgnoredPath(f))
                .Take(100) // Sample first 100 files
                .ToList();

            foreach (var file in codeFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);

                // Check naming patterns
                if (Regex.IsMatch(fileName, @"^[a-z]+(_[a-z]+)*$"))
                    namingConventions["snake_case"] = namingConventions.GetValueOrDefault("snake_case") + 1;
                else if (Regex.IsMatch(fileName, @"^[a-z][a-zA-Z0-9]*$"))
                    namingConventions["camelCase"] = namingConventions.GetValueOrDefault("camelCase") + 1;
                else if (Regex.IsMatch(fileName, @"^[A-Z][a-zA-Z0-9]*$"))
                    namingConventions["PascalCase"] = namingConventions.GetValueOrDefault("PascalCase") + 1;
                else if (Regex.IsMatch(fileName, @"^[a-z-]+(-[a-z-]+)*$"))
                    namingConventions["kebab-case"] = namingConventions.GetValueOrDefault("kebab-case") + 1;
            }

            analysis.NamingConventions = namingConventions
                .OrderByDescending(x => x.Value)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        private async Task AnalyzeDependenciesAsync(RepositoryAnalysis analysis)
        {
            var dependencies = new List<string>();
            var dependencyDetails = new Dictionary<string, List<string>>();

            // Check for various dependency files
            if (File.Exists(Path.Combine(_workspaceRoot, "package.json")))
            {
                dependencies.Add("Node.js/npm");
                try
                {
                    var packageJson = await File.ReadAllTextAsync(Path.Combine(_workspaceRoot, "package.json"));
                    // Could parse for specific dependencies, but for now just mark presence
                    dependencyDetails["Node.js/npm"] = new List<string> { "package.json found" };
                }
                catch { }
            }

            if (File.Exists(Path.Combine(_workspaceRoot, "requirements.txt")) ||
                File.Exists(Path.Combine(_workspaceRoot, "Pipfile")) ||
                File.Exists(Path.Combine(_workspaceRoot, "setup.py")))
            {
                dependencies.Add("Python/pip");
                var pythonFiles = new List<string>();
                if (File.Exists(Path.Combine(_workspaceRoot, "requirements.txt"))) pythonFiles.Add("requirements.txt");
                if (File.Exists(Path.Combine(_workspaceRoot, "Pipfile"))) pythonFiles.Add("Pipfile");
                if (File.Exists(Path.Combine(_workspaceRoot, "setup.py"))) pythonFiles.Add("setup.py");
                dependencyDetails["Python/pip"] = pythonFiles;
            }

            if (File.Exists(Path.Combine(_workspaceRoot, "Cargo.toml")))
            {
                dependencies.Add("Rust/Cargo");
                dependencyDetails["Rust/Cargo"] = new List<string> { "Cargo.toml found" };
            }

            if (Directory.EnumerateFiles(_workspaceRoot, "*.csproj", SearchOption.AllDirectories).Any())
            {
                dependencies.Add(".NET/NuGet");
                var csprojFiles = Directory.EnumerateFiles(_workspaceRoot, "*.csproj", SearchOption.AllDirectories)
                    .Select(f => Path.GetFileName(f))
                    .ToList();
                dependencyDetails[".NET/NuGet"] = csprojFiles;
            }

            if (File.Exists(Path.Combine(_workspaceRoot, "go.mod")))
            {
                dependencies.Add("Go modules");
                dependencyDetails["Go modules"] = new List<string> { "go.mod found" };
            }

            if (File.Exists(Path.Combine(_workspaceRoot, "Gemfile")))
            {
                dependencies.Add("Ruby/Bundler");
                dependencyDetails["Ruby/Bundler"] = new List<string> { "Gemfile found" };
            }

            // Infrastructure as Code dependencies
            if (Directory.EnumerateFiles(_workspaceRoot, "*.tf", SearchOption.AllDirectories).Any() ||
                Directory.EnumerateFiles(_workspaceRoot, "*.bicep", SearchOption.AllDirectories).Any())
            {
                dependencies.Add("Infrastructure as Code");
                var iacFiles = new List<string>();
                if (Directory.EnumerateFiles(_workspaceRoot, "*.tf", SearchOption.AllDirectories).Any())
                    iacFiles.Add("Terraform");
                if (Directory.EnumerateFiles(_workspaceRoot, "*.bicep", SearchOption.AllDirectories).Any())
                    iacFiles.Add("Azure Bicep");
                dependencyDetails["Infrastructure as Code"] = iacFiles;
            }

            analysis.Dependencies = dependencies;
            analysis.DependencyDetails = dependencyDetails;
        }

        private async Task AnalyzeCodeQualityAsync(RepositoryAnalysis analysis)
        {
            var qualityMetrics = new Dictionary<string, object>();

            // Count lines of code (rough estimate)
            var codeFiles = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => IsCodeFile(f) && !IsIgnoredPath(f))
                .ToList();

            long totalLines = 0;
            long codeLines = 0;
            long commentLines = 0;
            long blankLines = 0;

            foreach (var file in codeFiles.Take(50)) // Sample first 50 files for performance
            {
                try
                {
                    var lines = await File.ReadAllLinesAsync(file);
                    totalLines += lines.Length;

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed))
                            blankLines++;
                        else if (trimmed.StartsWith("//") || trimmed.StartsWith("#") || trimmed.StartsWith("/*"))
                            commentLines++;
                        else
                            codeLines++;
                    }
                }
                catch { }
            }

            qualityMetrics["TotalLines"] = totalLines;
            qualityMetrics["CodeLines"] = codeLines;
            qualityMetrics["CommentLines"] = commentLines;
            qualityMetrics["BlankLines"] = blankLines;
            qualityMetrics["CommentRatio"] = totalLines > 0 ? (double)commentLines / totalLines : 0;

            // Test file detection
            var testFiles = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Count(f => Path.GetFileName(f).ToLowerInvariant().Contains("test") ||
                           Path.GetFileName(f).ToLowerInvariant().Contains("spec"));

            qualityMetrics["TestFiles"] = testFiles;
            qualityMetrics["TestCoverage"] = codeFiles.Count > 0 ? (double)testFiles / codeFiles.Count : 0;

            analysis.CodeQualityMetrics = qualityMetrics;
        }

        private async Task AnalyzeArchitectureAsync(RepositoryAnalysis analysis)
        {
            var architecture = new Dictionary<string, object>();

            // Detect architectural patterns
            var patterns = new List<string>();

            // MVC pattern detection
            if (Directory.Exists(Path.Combine(_workspaceRoot, "Controllers")) ||
                Directory.Exists(Path.Combine(_workspaceRoot, "Models")) ||
                Directory.Exists(Path.Combine(_workspaceRoot, "Views")))
            {
                patterns.Add("MVC (Model-View-Controller)");
            }

            // Microservices indicators
            var dockerFiles = Directory.EnumerateFiles(_workspaceRoot, "Dockerfile*", SearchOption.AllDirectories).Count();
            var dockerCompose = Directory.EnumerateFiles(_workspaceRoot, "docker-compose*.yml", SearchOption.AllDirectories).Count();
            if (dockerFiles > 0 || dockerCompose > 0)
            {
                patterns.Add("Containerized (Docker)");
                architecture["Containerization"] = $"{dockerFiles} Dockerfiles, {dockerCompose} docker-compose files";
            }

            // API detection
            var apiIndicators = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Count(f => Path.GetFileName(f).ToLowerInvariant().Contains("api") ||
                           Path.GetFileName(f).ToLowerInvariant().Contains("controller") ||
                           Path.GetFileName(f).ToLowerInvariant().Contains("route"));

            if (apiIndicators > 0)
            {
                patterns.Add("API/Web Service");
                architecture["APIEndpoints"] = apiIndicators;
            }

            // Database indicators
            var dbIndicators = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Count(f => Path.GetFileName(f).ToLowerInvariant().Contains("migration") ||
                           Path.GetFileName(f).ToLowerInvariant().Contains("schema") ||
                           Path.GetFileName(f).ToLowerInvariant().Contains("model"));

            if (dbIndicators > 0)
            {
                patterns.Add("Database Integration");
                architecture["DatabaseModels"] = dbIndicators;
            }

            // Monorepo detection
            var subProjects = Directory.EnumerateDirectories(_workspaceRoot, "*", SearchOption.TopDirectoryOnly)
                .Count(d => File.Exists(Path.Combine(d, "package.json")) ||
                           Directory.EnumerateFiles(d, "*.csproj", SearchOption.TopDirectoryOnly).Any() ||
                           File.Exists(Path.Combine(d, "Cargo.toml")));

            if (subProjects > 2)
            {
                patterns.Add("Monorepo Structure");
                architecture["SubProjects"] = subProjects;
            }

            architecture["ArchitecturalPatterns"] = patterns;
            analysis.Architecture = architecture;
        }

        private async Task AnalyzeDevelopmentActivityAsync(RepositoryAnalysis analysis)
        {
            var activity = new Dictionary<string, object>();

            // Simulate development activity analysis (in real implementation, would analyze git history)
            var totalFiles = analysis.TotalFiles;
            var codeFiles = analysis.CodeFiles;

            // Estimate commit frequency based on file count and structure
            var estimatedCommits = Math.Max(10, Math.Min(1000, totalFiles / 2));
            activity["EstimatedCommits"] = estimatedCommits;

            // Estimate contributors based on code volume
            var estimatedContributors = Math.Max(1, Math.Min(50, (int)Math.Sqrt(codeFiles / 10)));
            activity["EstimatedContributors"] = estimatedContributors;

            // Development velocity (lines per commit estimate)
            var avgLinesPerCommit = codeFiles > 0 ? Convert.ToDouble(analysis.CodeQualityMetrics.GetValueOrDefault("TotalLines", 0L)) / estimatedCommits : 0;
            activity["AvgLinesPerCommit"] = avgLinesPerCommit;

            // Activity level
            string activityLevel;
            if (estimatedCommits > 500) activityLevel = "Very High";
            else if (estimatedCommits > 200) activityLevel = "High";
            else if (estimatedCommits > 50) activityLevel = "Moderate";
            else activityLevel = "Low";
            activity["ActivityLevel"] = activityLevel;

            analysis.DevelopmentActivity = activity;
        }

        private async Task AnalyzeCodeComplexityAsync(RepositoryAnalysis analysis)
        {
            var complexity = new Dictionary<string, object>();

            // Analyze code complexity patterns
            var codeFiles = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => IsCodeFile(f) && !IsIgnoredPath(f))
                .ToList();

            var totalFunctions = 0;
            var totalClasses = 0;
            var totalInterfaces = 0;
            var nestingDepth = 0;
            var longestFile = 0;

            foreach (var file in codeFiles.Take(20)) // Sample for performance
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var lines = content.Split('\n');
                    longestFile = Math.Max(longestFile, lines.Length);

                    // Simple pattern matching for different languages
                    var ext = Path.GetExtension(file).ToLower();
                    switch (ext)
                    {
                        case ".cs":
                            totalClasses += Regex.Matches(content, @"(?:public|private|internal|protected)?\s*(?:static|abstract|sealed)?\s*class\s+\w+").Count;
                            totalInterfaces += Regex.Matches(content, @"(?:public|private|internal|protected)?\s*interface\s+\w+").Count;
                            totalFunctions += Regex.Matches(content, @"(?:public|private|internal|protected)?\s*(?:static|virtual|abstract|override)?\s*\w+\s+\w+\s*\([^)]*\)\s*{").Count;
                            nestingDepth += Regex.Matches(content, @"{").Count;
                            break;
                        case ".py":
                            totalFunctions += Regex.Matches(content, @"^\s*def\s+\w+\s*\(").Count;
                            totalClasses += Regex.Matches(content, @"^\s*class\s+\w+").Count;
                            break;
                        case ".js":
                        case ".ts":
                            totalFunctions += Regex.Matches(content, @"(?:function|const|let|var)\s+\w+\s*\(").Count;
                            totalClasses += Regex.Matches(content, @"class\s+\w+").Count;
                            break;
                        case ".cpp":
                        case ".c":
                        case ".h":
                            totalFunctions += Regex.Matches(content, @"(?:\w+\s+)+\w+\s*\([^)]*\)\s*{").Count;
                            totalClasses += Regex.Matches(content, @"class\s+\w+").Count;
                            nestingDepth += Regex.Matches(content, @"{").Count;
                            break;
                    }
                }
                catch { }
            }

            complexity["TotalFunctions"] = totalFunctions;
            complexity["TotalClasses"] = totalClasses;
            complexity["TotalInterfaces"] = totalInterfaces;
            complexity["AvgNestingDepth"] = nestingDepth > 0 ? nestingDepth / (double)codeFiles.Count : 0;
            complexity["LongestFile"] = longestFile;

            // Complexity assessment
            string complexityLevel;
            var functionsPerFile = totalFunctions / (double)codeFiles.Count;
            if (functionsPerFile > 20) complexityLevel = "Very High";
            else if (functionsPerFile > 10) complexityLevel = "High";
            else if (functionsPerFile > 5) complexityLevel = "Moderate";
            else complexityLevel = "Low";
            complexity["ComplexityLevel"] = complexityLevel;

            analysis.CodeComplexity = complexity;
        }

        private async Task AnalyzeSecurityAsync(RepositoryAnalysis analysis)
        {
            var security = new Dictionary<string, object>();

            var securityIssues = new List<string>();
            var securityStrengths = new List<string>();

            // Check for security-related files
            var hasSecurityAudit = File.Exists(Path.Combine(_workspaceRoot, "SECURITY.md")) ||
                                  File.Exists(Path.Combine(_workspaceRoot, "security.txt"));
            var hasDependabot = File.Exists(Path.Combine(_workspaceRoot, ".github", "dependabot.yml"));
            var hasSecurityScans = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Any(f => Path.GetFileName(f).ToLowerInvariant().Contains("security") ||
                         Path.GetFileName(f).ToLowerInvariant().Contains("audit"));

            // Check for vulnerable patterns
            var allFiles = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => IsCodeFile(f) && !IsIgnoredPath(f))
                .Take(50); // Sample for performance

            var potentialIssues = 0;
            foreach (var file in allFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    // Simple security pattern checks
                    if (content.Contains("password") && content.Contains("string")) potentialIssues++;
                    if (content.Contains("api_key") || content.Contains("API_KEY")) potentialIssues++;
                    if (content.Contains("eval(") || content.Contains("innerHTML")) potentialIssues++;
                }
                catch { }
            }

            // Security assessment
            if (hasSecurityAudit) securityStrengths.Add("Security policy documented");
            if (hasDependabot) securityStrengths.Add("Automated dependency updates");
            if (hasSecurityScans) securityStrengths.Add("Security scanning configured");

            if (potentialIssues > 0) securityIssues.Add($"{potentialIssues} potential security patterns detected");
            if (!hasSecurityAudit) securityIssues.Add("No security policy found");

            security["SecurityIssues"] = securityIssues;
            security["SecurityStrengths"] = securityStrengths;
            security["SecurityScore"] = Math.Max(0, 10 - securityIssues.Count + securityStrengths.Count);

            analysis.SecurityAnalysis = security;
        }

        private async Task AnalyzePerformanceAsync(RepositoryAnalysis analysis)
        {
            var performance = new Dictionary<string, object>();

            var codeFiles = Directory.EnumerateFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => IsCodeFile(f) && !IsIgnoredPath(f))
                .ToList();

            // Performance indicators
            var largeFiles = codeFiles.Count(f => new FileInfo(f).Length > 1024 * 1024); // > 1MB
            var totalSize = codeFiles.Sum(f => new FileInfo(f).Length);
            var avgFileSize = codeFiles.Count > 0 ? totalSize / codeFiles.Count : 0;

            performance["LargeFiles"] = largeFiles;
            performance["TotalCodeSize"] = totalSize;
            performance["AvgFileSize"] = avgFileSize;

            // Performance bottlenecks
            var bottlenecks = new List<string>();
            if (largeFiles > 0) bottlenecks.Add($"{largeFiles} large files may impact editor performance");
            if (avgFileSize > 500 * 1024) bottlenecks.Add("Large average file size may slow loading");
            if (analysis.TotalFiles > 1000) bottlenecks.Add("Large number of files may impact navigation");

            performance["PerformanceBottlenecks"] = bottlenecks;

            // Optimization suggestions
            var suggestions = new List<string>();
            if (largeFiles > 0) suggestions.Add("Consider splitting large files into smaller modules");
            if (analysis.DirectoryStructure.Count > 10) suggestions.Add("Deep directory structure may benefit from flattening");
            if (Convert.ToDouble(analysis.CodeQualityMetrics.GetValueOrDefault("CommentRatio", 0.0)) < 0.05)
                suggestions.Add("Add more comments for better code maintainability");

            performance["OptimizationSuggestions"] = suggestions;

            analysis.PerformanceAnalysis = performance;
        }

        private async Task AnalyzeProjectMaturityAsync(RepositoryAnalysis analysis)
        {
            var maturity = new Dictionary<string, object>();

            // Documentation score (0-5)
            int docScore = 0;
            if (analysis.HasReadme) docScore += 2;
            if (analysis.HasContributingGuide) docScore += 2;
            if (analysis.HasChangelog) docScore += 1;
            if (analysis.DocumentationFiles > 5) docScore += 1;

            maturity["DocumentationScore"] = docScore;

            // Testing score (0-10)
            var testScore = (int)(Convert.ToDouble(analysis.CodeQualityMetrics.GetValueOrDefault("TestCoverage", 0.0)) * 10);
            maturity["TestingScore"] = testScore;

            // Structure score (0-5)
            int structureScore = 0;
            if (analysis.DirectoryStructure.Count > 3) structureScore += 2; // Well organized
            if (analysis.NamingConventions.Count > 0) structureScore += 2; // Consistent naming
            if (analysis.CodeFiles > analysis.ConfigFiles) structureScore += 1; // Code-focused
            maturity["StructureScore"] = structureScore;

            // Security score (0-5)
            var securityScore = Math.Min(5, Math.Max(0, Convert.ToInt32(analysis.SecurityAnalysis.GetValueOrDefault("SecurityScore", 0))));
            maturity["SecurityScore"] = securityScore;

            // Activity score (0-5)
            var activityScore = Convert.ToInt32(analysis.DevelopmentActivity.GetValueOrDefault("EstimatedCommits", 0)) switch
            {
                > 500 => 5,
                > 200 => 4,
                > 50 => 3,
                > 10 => 2,
                _ => 1
            };
            maturity["ActivityScore"] = activityScore;

            // Overall maturity score
            var overallScore = (docScore + testScore + structureScore + securityScore + activityScore) / 5.0;
            maturity["OverallMaturityScore"] = overallScore;

            string maturityLevel;
            if (overallScore >= 8) maturityLevel = "🏆 Enterprise Grade";
            else if (overallScore >= 6) maturityLevel = "🎯 Production Ready";
            else if (overallScore >= 4) maturityLevel = "📈 Developing";
            else if (overallScore >= 2) maturityLevel = "🌱 Early Stage";
            else maturityLevel = "🍼 Seed Stage";

            maturity["MaturityLevel"] = maturityLevel;

            analysis.ProjectMaturity = maturity;
        }

        private void GenerateDeepWikiSummary(RepositoryAnalysis analysis)
        {
            var summary = new StringBuilder();

            // DeepWiki-style header
            summary.AppendLine("# 🔬 Deep Repository Intelligence Report");
            summary.AppendLine();
            summary.AppendLine("## 🎯 " + analysis.RepositoryName);
            summary.AppendLine();
            summary.AppendLine("**Type:** " + analysis.ProjectType + "  |  **Language:** " + analysis.PrimaryLanguage);
            summary.AppendLine("**Location:** `" + analysis.WorkspaceRoot + "`");
            summary.AppendLine();

            // Executive Summary (like DeepWiki's overview)
            summary.AppendLine("## 📋 Executive Summary");
            if (!string.IsNullOrEmpty(analysis.ReadmeSummary))
            {
                summary.AppendLine(analysis.ReadmeSummary);
            }
            else
            {
                summary.AppendLine("This repository contains a software project with multiple components and dependencies.");
            }
            summary.AppendLine();

            // Key Statistics (DeepWiki style metrics)
            summary.AppendLine("## 📊 Key Statistics");
            summary.AppendLine("| Metric | Value |");
            summary.AppendLine("|--------|-------|");
            summary.AppendLine("| **Total Files** | " + analysis.TotalFiles.ToString("N0") + " |");
            summary.AppendLine("| **Code Files** | " + analysis.CodeFiles.ToString("N0") + " |");

            var totalLines = (long)analysis.CodeQualityMetrics.GetValueOrDefault("TotalLines", 0L);
            summary.AppendLine("| **Lines of Code** | " + totalLines.ToString("N0") + " |");

            var testCoverage = Convert.ToDouble(analysis.CodeQualityMetrics.GetValueOrDefault("TestCoverage", 0.0));
            summary.AppendLine("| **Test Coverage** | " + testCoverage.ToString("P1") + " |");

            var maturityLevel = analysis.ProjectMaturity.GetValueOrDefault("MaturityLevel", "Unknown");
            summary.AppendLine("| **Maturity Level** | " + maturityLevel.ToString() + " |");
            summary.AppendLine();

            // Technology Stack (DeepWiki style)
            summary.AppendLine("## 🛠️ Technology Stack");
            if (analysis.Dependencies.Any())
            {
                foreach (var dep in analysis.Dependencies)
                {
                    summary.AppendLine("### " + dep);
                    if (analysis.DependencyDetails.TryGetValue(dep, out var details))
                    {
                        foreach (var detail in details)
                        {
                            summary.AppendLine("- " + detail);
                        }
                    }
                    summary.AppendLine();
                }
            }
            else
            {
                summary.AppendLine("Primary technologies detected based on file analysis.");
                summary.AppendLine();
            }

            // Architecture & Design (DeepWiki style)
            summary.AppendLine("## 🏗️ Architecture & Design");
            if (analysis.Architecture.TryGetValue("ArchitecturalPatterns", out var patternsObj) &&
                patternsObj is List<string> patterns && patterns.Any())
            {
                summary.AppendLine("### Architectural Patterns");
                foreach (var pattern in patterns)
                {
                    summary.AppendLine("- **" + pattern + "**");
                }
                summary.AppendLine();
            }

            summary.AppendLine("### Directory Structure");
            if (analysis.DirectoryStructure.Any())
            {
                foreach (var dir in analysis.DirectoryStructure.Take(8))
                {
                    summary.AppendLine("- `" + dir.Key + "/` (" + dir.Value + " subdirectories)");
                }
                summary.AppendLine();
            }

            summary.AppendLine("### Code Organization");
            if (analysis.NamingConventions.Any())
            {
                summary.AppendLine("**Naming Conventions Used:**");
                var sortedConventions = analysis.NamingConventions.OrderByDescending(x => x.Value);
                foreach (var convention in sortedConventions.Take(3))
                {
                    var percentage = analysis.CodeFiles > 0 ? (double)convention.Value / analysis.CodeFiles * 100 : 0;
                    summary.AppendLine("- `" + convention.Key + "`: " + percentage.ToString("F1") + "% of files");
                }
                summary.AppendLine();
            }

            // Code Quality & Metrics (DeepWiki style)
            summary.AppendLine("## 📈 Code Quality & Metrics");
            summary.AppendLine("### Code Metrics");

            var codeLines = (long)analysis.CodeQualityMetrics.GetValueOrDefault("CodeLines", 0L);
            var commentLines = (long)analysis.CodeQualityMetrics.GetValueOrDefault("CommentLines", 0L);
            var commentRatio = Convert.ToDouble(analysis.CodeQualityMetrics.GetValueOrDefault("CommentRatio", 0.0));

            summary.AppendLine("| Metric | Value |");
            summary.AppendLine("|--------|-------|");
            summary.AppendLine("| **Total Lines** | " + totalLines.ToString("N0") + " |");
            summary.AppendLine("| **Code Lines** | " + codeLines.ToString("N0") + " |");
            summary.AppendLine("| **Comment Lines** | " + commentLines.ToString("N0") + " |");
            summary.AppendLine("| **Comment Ratio** | " + commentRatio.ToString("P1") + " |");

            var totalFunctions = Convert.ToInt32(analysis.CodeComplexity.GetValueOrDefault("TotalFunctions", 0));
            summary.AppendLine("| **Functions** | " + totalFunctions.ToString("N0") + " |");

            var totalClasses = Convert.ToInt32(analysis.CodeComplexity.GetValueOrDefault("TotalClasses", 0));
            summary.AppendLine("| **Classes** | " + totalClasses.ToString("N0") + " |");
            summary.AppendLine();

            summary.AppendLine("### Complexity Assessment");
            var complexityLevel = analysis.CodeComplexity.GetValueOrDefault("ComplexityLevel", "Unknown");
            summary.AppendLine("**Complexity Level:** " + complexityLevel.ToString());

            var avgNesting = Convert.ToDouble(analysis.CodeComplexity.GetValueOrDefault("AvgNestingDepth", 0.0));
            summary.AppendLine("**Average Nesting Depth:** " + avgNesting.ToString("F1") + " levels");

            var longestFile = Convert.ToInt32(analysis.CodeComplexity.GetValueOrDefault("LongestFile", 0));
            summary.AppendLine("**Longest File:** " + longestFile.ToString("N0") + " lines");

            var estimatedCommits = Convert.ToInt32(analysis.DevelopmentActivity.GetValueOrDefault("EstimatedCommits", 0));
            summary.AppendLine("**Estimated Commits:** " + estimatedCommits.ToString("N0"));

            var estimatedContributors = Convert.ToInt32(analysis.DevelopmentActivity.GetValueOrDefault("EstimatedContributors", 0));
            summary.AppendLine("**Estimated Contributors:** " + estimatedContributors.ToString());

            var avgLinesPerCommit = Convert.ToDouble(analysis.DevelopmentActivity.GetValueOrDefault("AvgLinesPerCommit", 0.0));
            summary.AppendLine("**Avg Lines/Commit:** " + avgLinesPerCommit.ToString("F0"));
            summary.AppendLine();

            // Security & Performance (DeepWiki style)
            summary.AppendLine("## 🔒 Security & Performance");
            summary.AppendLine("### Security Assessment");

            if (analysis.SecurityAnalysis.TryGetValue("SecurityStrengths", out var strengths) &&
                strengths is List<string> strengthList)
            {
                summary.AppendLine("**Strengths:**");
                foreach (var strength in strengthList)
                {
                    summary.AppendLine("- ✅ " + strength);
                }
            }

            if (analysis.SecurityAnalysis.TryGetValue("SecurityIssues", out var issues) &&
                issues is List<string> issueList && issueList.Any())
            {
                summary.AppendLine("**Areas for Improvement:**");
                foreach (var issue in issueList)
                {
                    summary.AppendLine("- ⚠️ " + issue);
                }
            }
            summary.AppendLine();

            summary.AppendLine("### Performance Characteristics");
            var totalCodeSize = (long)analysis.PerformanceAnalysis.GetValueOrDefault("TotalCodeSize", 0L);
            var totalCodeSizeMB = totalCodeSize / (1024.0 * 1024.0);
            summary.AppendLine("**Total Code Size:** " + totalCodeSizeMB.ToString("F1") + " MB");

            var largeFiles = Convert.ToInt32(analysis.PerformanceAnalysis.GetValueOrDefault("LargeFiles", 0));
            summary.AppendLine("**Large Files (>1MB):** " + largeFiles.ToString());

            if (analysis.PerformanceAnalysis.TryGetValue("PerformanceBottlenecks", out var bottlenecks) &&
                bottlenecks is List<string> bottleneckList && bottleneckList.Any())
            {
                summary.AppendLine("**Performance Considerations:**");
                foreach (var bottleneck in bottleneckList)
                {
                    summary.AppendLine("- " + bottleneck);
                }
            }
            summary.AppendLine();

            // Documentation & Community (DeepWiki style)
            summary.AppendLine("## 📚 Documentation & Community");
            summary.AppendLine("| Document | Status |");
            summary.AppendLine("|----------|--------|");
            summary.AppendLine("| **README** | " + (analysis.HasReadme ? "✅ Available" : "❌ Missing") + " |");
            summary.AppendLine("| **Contributing Guide** | " + (analysis.HasContributingGuide ? "✅ Available" : "❌ Missing") + " |");
            summary.AppendLine("| **Changelog** | " + (analysis.HasChangelog ? "✅ Available" : "❌ Missing") + " |");
            summary.AppendLine();

            // File Type Analysis (DeepWiki style)
            summary.AppendLine("## 📁 File Type Analysis");
            if (analysis.FileExtensions.Any())
            {
                summary.AppendLine("| Extension | Count | Percentage |");
                summary.AppendLine("|-----------|-------|------------|");
                foreach (var ext in analysis.FileExtensions.Take(10))
                {
                    var percentage = analysis.TotalFiles > 0 ? (double)ext.Value / analysis.TotalFiles * 100 : 0;
                    summary.AppendLine("| `" + ext.Key + "` | " + ext.Value.ToString("N0") + " | " + percentage.ToString("F1") + "% |");
                }
                summary.AppendLine();
            }

            // Recommendations (DeepWiki style insights)
            summary.AppendLine("## 💡 Deep Insights & Recommendations");

            var recommendations = new List<string>();
            var insights = new List<string>();

            // Maturity-based insights
            var maturityLevelObj = analysis.ProjectMaturity.GetValueOrDefault("MaturityLevel", "");
            var maturityLevelStr = maturityLevelObj?.ToString() ?? "";
            if (maturityLevelStr.Contains("Enterprise"))
                insights.Add("This appears to be an enterprise-grade project with comprehensive practices");
            else if (maturityLevelStr.Contains("Production"))
                insights.Add("This project demonstrates production-ready quality and practices");
            else if (maturityLevelStr.Contains("Developing"))
                insights.Add("This project is actively developing with good foundations");

            // Architecture insights
            if (analysis.Architecture.TryGetValue("ArchitecturalPatterns", out var archPatterns) &&
                archPatterns is List<string> archList)
            {
                if (archList.Contains("Containerized (Docker)"))
                    insights.Add("Containerization suggests modern deployment practices");
                if (archList.Contains("Microservices"))
                    insights.Add("Microservices architecture indicates scalable design");
            }

            // Code quality insights
            if (commentRatio > 0.2)
                insights.Add("Well-documented codebase with comprehensive comments");
            else if (commentRatio < 0.05)
                recommendations.Add("Consider adding more inline documentation for better maintainability");

            // Security insights
            var securityScore = Convert.ToInt32(analysis.SecurityAnalysis.GetValueOrDefault("SecurityScore", 0));
            if (securityScore >= 8)
                insights.Add("Strong security practices and awareness demonstrated");
            else if (securityScore <= 3)
                recommendations.Add("Consider implementing security best practices and regular audits");

            // Performance insights
            if (largeFiles == 0)
                insights.Add("Good file size management - no performance-impacting large files");
            else
                recommendations.Add("Consider breaking down large files for better performance");

            // Testing insights
            if (testCoverage > 0.8)
                insights.Add("Excellent test coverage indicates robust quality assurance");
            else if (testCoverage < 0.1)
                recommendations.Add("Consider expanding test coverage for better reliability");

            // Output insights and recommendations
            if (insights.Any())
            {
                summary.AppendLine("### 🔍 Key Insights");
                foreach (var insight in insights)
                {
                    summary.AppendLine("- **" + insight + "**");
                }
                summary.AppendLine();
            }

            if (recommendations.Any())
            {
                summary.AppendLine("### 📋 Recommendations for Enhancement");
                foreach (var rec in recommendations)
                {
                    summary.AppendLine("- " + rec);
                }
                summary.AppendLine();
            }

            // Project Health Score (DeepWiki style)
            summary.AppendLine("## 🏆 Project Health Score");
            var overallScore = Convert.ToDouble(analysis.ProjectMaturity.GetValueOrDefault("OverallMaturityScore", 0.0));
            var healthLevel = overallScore >= 8 ? "Excellent" :
                              overallScore >= 6 ? "Good" :
                              overallScore >= 4 ? "Fair" : "Needs Improvement";

            summary.AppendLine("**Overall Health:** " + healthLevel + " (" + overallScore.ToString("F1") + "/10)");
            summary.AppendLine();
            summary.AppendLine("| Category | Score | Max |");
            summary.AppendLine("|----------|-------|-----|");

            var docScore = analysis.ProjectMaturity.GetValueOrDefault("DocumentationScore", 0);
            var testScore = analysis.ProjectMaturity.GetValueOrDefault("TestingScore", 0);
            var structureScore = analysis.ProjectMaturity.GetValueOrDefault("StructureScore", 0);
            var securityScore2 = analysis.ProjectMaturity.GetValueOrDefault("SecurityScore", 0);
            var activityScore = analysis.ProjectMaturity.GetValueOrDefault("ActivityScore", 0);

            summary.AppendLine("| Documentation | " + docScore.ToString() + " | 5 |");
            summary.AppendLine("| Testing | " + testScore.ToString() + " | 10 |");
            summary.AppendLine("| Structure | " + structureScore.ToString() + " | 5 |");
            summary.AppendLine("| Security | " + securityScore2.ToString() + " | 5 |");
            summary.AppendLine("| Activity | " + activityScore.ToString() + " | 5 |");
            summary.AppendLine();

            // Footer with metadata
            summary.AppendLine("---");
            summary.AppendLine("*Analysis generated by Pfpad Repository Analytics Engine on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "*");
            summary.AppendLine("*Repository: " + analysis.RepositoryName + " | Files analyzed: " + analysis.TotalFiles.ToString("N0") + " | Analysis depth: Deep Intelligence*");

            analysis.Summary = summary.ToString();
        }

        private bool IsIgnoredPath(string path)
        {
            var fileName = Path.GetFileName(path);
            var relativePath = path.Substring(_workspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar);

            // Common ignore patterns
            if (fileName.StartsWith('.') ||
                fileName.EndsWith(".tmp") ||
                fileName.EndsWith(".bak") ||
                relativePath.Contains(".git") ||
                relativePath.Contains(".svn") ||
                relativePath.Contains("node_modules") ||
                relativePath.Contains("bin") ||
                relativePath.Contains("obj") ||
                relativePath.Contains(".vs") ||
                relativePath.Contains("packages") ||
                relativePath.Contains("__pycache__"))
            {
                return true;
            }

            return false;
        }

        private bool IsCodeFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return new[] { ".cs", ".vb", ".fs", ".py", ".js", ".ts", ".java", ".cpp", ".c", ".h", ".hpp", ".php", ".rb", ".go", ".rs", ".swift", ".kt", ".scala", ".clj", ".fsx", ".ml", ".fs", ".elm", ".hs", ".lua", ".pl", ".pm", ".tcl", ".r", ".m", ".mat", ".jl" }.Contains(ext);
        }

        private bool IsConfigFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var fileName = Path.GetFileName(path).ToLowerInvariant();
            return new[] { ".json", ".xml", ".yml", ".yaml", ".toml", ".ini", ".cfg", ".conf", ".config", ".properties", ".env" }.Contains(ext) ||
                   fileName == "dockerfile" || fileName.StartsWith("dockerfile.") ||
                   fileName == "makefile" || fileName == "cmakelists.txt";
        }

        private bool IsDocumentationFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var fileName = Path.GetFileName(path).ToLowerInvariant();
            return new[] { ".md", ".txt", ".rst", ".adoc", ".asciidoc" }.Contains(ext) ||
                   fileName.ToLowerInvariant().Contains("readme") ||
                   fileName.ToLowerInvariant().Contains("changelog") ||
                   fileName.ToLowerInvariant().Contains("license") ||
                   fileName.ToLowerInvariant().Contains("contributing");
        }

        private string ExtractReadmeSummary(string content)
        {
            var lines = content.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            var meaningfulLines = new List<string>();
            foreach (var line in lines.Take(10)) // First 10 meaningful lines
            {
                if (!line.StartsWith("#") && !line.StartsWith("```") && !line.StartsWith("---") && !line.StartsWith("==="))
                {
                    meaningfulLines.Add(line);
                }
            }

            var summary = string.Join(" ", meaningfulLines);
            return summary.Length > 200 ? summary.Substring(0, 200) + "..." : summary;
        }
    }

    public class RepositoryAnalysis
    {
        public string WorkspaceRoot { get; set; } = "";
        public string RepositoryName { get; set; } = "";
        public string ProjectType { get; set; } = "";
        public string PrimaryLanguage { get; set; } = "";
        public string ReadmeSummary { get; set; } = "";
        public int TotalFiles { get; set; }
        public int CodeFiles { get; set; }
        public int ConfigFiles { get; set; }
        public int DocumentationFiles { get; set; }
        public Dictionary<string, int> FileExtensions { get; set; } = new();
        public Dictionary<string, int> DirectoryStructure { get; set; } = new();
        public Dictionary<string, int> NamingConventions { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public Dictionary<string, List<string>> DependencyDetails { get; set; } = new();
        public Dictionary<string, object> CodeQualityMetrics { get; set; } = new();
        public Dictionary<string, object> Architecture { get; set; } = new();
        public Dictionary<string, object> ProjectMaturity { get; set; } = new();
        public Dictionary<string, object> DevelopmentActivity { get; set; } = new();
        public Dictionary<string, object> CodeComplexity { get; set; } = new();
        public Dictionary<string, object> SecurityAnalysis { get; set; } = new();
        public Dictionary<string, object> PerformanceAnalysis { get; set; } = new();
        public bool HasReadme { get; set; }
        public bool HasContributingGuide { get; set; }
        public bool HasChangelog { get; set; }
        public string Summary { get; set; } = "";
    }
}