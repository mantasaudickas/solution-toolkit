using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolutionGenerator.Toolkit.Logging;
using SolutionGenerator.Toolkit.Solutions;
using SolutionGenerator.Toolkit.Storage.Data;
using SolutionToolkit.VisualStudio;

namespace SolutionToolkit.Implementation
{
    public class ProjectFileModificationImplementation : IImplementation
    {
        private readonly bool _checkOnlyDependencies;

        public ProjectFileModificationImplementation(bool checkOnlyDependencies)
        {
            _checkOnlyDependencies = checkOnlyDependencies;
        }

        public void Execute(ProjectConfiguration configuration, string selectedProject)
        {
            DirectoryInfo projectDirectory = new DirectoryInfo(configuration.RootPath);
            List<FileSystemInfo> projectFiles = projectDirectory
                .EnumerateFileSystemInfos("*.csproj", SearchOption.AllDirectories)
                .ToList();

            var targetProjects = configuration.ResolveAssemblies(selectedProject);

            if (_checkOnlyDependencies)
            {
                var projectSetup = new ProjectSetup
                {
                    WhenAssemblyKeyFileNotFound = ProjectSetupBehavior.Valid,
                    WhenContainsFileReferences = ProjectSetupBehavior.Valid,
                    WhenContainsProjectReferences = ProjectSetupBehavior.Warn,
                    WhenReferenceNotResolved = ProjectSetupBehavior.Warn,
                    WhenReferenceResolvedInDifferentLocation = ProjectSetupBehavior.Warn,
                    WhenRequiredProjectLinkNotFound = ProjectSetupBehavior.Valid
                };

                var consoleLogger = ConsoleLogger.Default;

                var generator = new SolutionGenerator.Toolkit.SolutionGenerator(consoleLogger);
                var projectLoader = generator.GetProjectLoader(projectSetup, configuration.RootPath);
                var targetProjectFiles = generator.GetTargetProjectFiles(projectLoader, targetProjects);

                ReferenceWalker walker = new ReferenceWalker(consoleLogger);
                var dependencies = walker.WalkReferencesRecursively(projectSetup, projectLoader, targetProjectFiles,
                    new[] {configuration.ThirdPartiesRootPath}, new HashSet<string>());

                projectFiles = new List<FileSystemInfo>();
                foreach (var dependency in dependencies)
                {
                    var project = projectLoader.GetProjectById(dependency);
                    projectFiles.Add(new FileInfo(project.ProjectFileLocation));
                }
            }

            ChangeOutputPath(projectFiles, configuration.RootPath, configuration.BinariesOutputPath, targetProjects);

            ChangeReferences(projectFiles,
                configuration.RootPath,
                configuration.BinariesOutputPath,
                configuration.TargetFrameworkVersion,
                configuration.GetSystemRuntimeReferenceMode,
                configuration.GetSpecificVersionReferenceMode,
                targetProjects);
        }

        private static void ChangeReferences(IEnumerable<FileSystemInfo> projectFiles, string projectRootPath, string binariesPath, string targetFrameworkVersion, ReferenceChangeMode sysRuntimeMode, ReferenceChangeMode specificVersionMode, string[] targetProjects)
        {
            var duplicates = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var singleProject in projectFiles)
            {
                var changed = false;
                var projectFile = new ProjectFile(singleProject.FullName);
                var relativePath = projectFile.CalculateRelativePath(projectRootPath);
                var assemblyName = projectFile.GetAssemblyName();
                var projectGuid = projectFile.GetProjectId();
                var currentDirectory = Path.GetDirectoryName(singleProject.FullName);
                if (currentDirectory == null)
                    throw new Exception("Unable to resolve project directory");

                if (duplicates.ContainsKey(projectGuid))
                {
                    throw new Exception(
                        $"Projects has duplicate ids.\n{duplicates[projectGuid]}\n{singleProject.FullName}");
                }

                duplicates.Add(projectGuid, singleProject.FullName);

                var projectReferences = projectFile.ProjectReferences;

                foreach (var reference in projectReferences)
                {
                    var referedProject = Path.GetFullPath(Path.Combine(currentDirectory, reference.Include));
                    var referedProjectFile = new ProjectFile(referedProject);
                    var referedAssemblyName = referedProjectFile.GetAssemblyName();
                    var referedOutputPath = referedProjectFile.GetOutputPath();

                    var referredDirectoryName = Path.GetDirectoryName(referedProject);
                    if (referredDirectoryName == null)
                        throw new Exception("Unable to resolve reffered directory name.");

                    var toPath = Path.GetFullPath(Path.Combine(referredDirectoryName, referedOutputPath));
                    var outputDir = CalculateRelativePath(projectFile, singleProject.FullName, toPath);

                    var hintPath = $"{outputDir}{referedAssemblyName}.dll";
                    projectFile.AddReference(reference.Name, hintPath);
                }

                var outputPath = relativePath + binariesPath;
                if (!outputPath.EndsWith("\\"))
                {
                    outputPath += "\\";
                }

                var isTargetProject = IsTargetProject(assemblyName, targetProjects, projectFile);

                changed |= projectFile.SetReferencePrivacy(isTargetProject, new[] { "\\packages\\", outputPath });
                changed |= projectFile.SetReferenceSpecificVersion(specificVersionMode);
                changed |= projectFile.RemoveProjectReferences();

                if (sysRuntimeMode == ReferenceChangeMode.Add)
                    changed |= projectFile.AddSystemRuntimeReference();
                else if (sysRuntimeMode == ReferenceChangeMode.Remove)
                    changed |= projectFile.RemoveSystemRuntimeReference();

                if (!string.IsNullOrWhiteSpace(targetFrameworkVersion))
                {
                    changed |= projectFile.SetTargetFrameworkVersion(targetFrameworkVersion);
                }

                changed |= projectFile.RemoveNuget();

                if (changed)
                {
                    ConsoleLogger.Default.Info($"Replacing {singleProject.FullName}");
                    projectFile.Save();
                }
            }
        }

        private static void ChangeOutputPath(IEnumerable<FileSystemInfo> projectFiles, string projectPath, string binariesPath, string[] targetProjects)
        {
            foreach (var singleProject in projectFiles)
            {
                var projectFile = new ProjectFile(singleProject.FullName);
                var relativePath = projectFile.CalculateRelativePath(projectPath);
                var assemblyName = projectFile.GetAssemblyName();

                var outputPath = relativePath + binariesPath;
                if (!outputPath.EndsWith("\\"))
                {
                    outputPath += "\\";
                }

                var changed = false;
                var isTargetProject = IsTargetProject(assemblyName, targetProjects, projectFile);
                if (!isTargetProject)
                {
                    changed = projectFile.SetOutputPath(outputPath);
                }

                if (changed)
                {
                    projectFile.Save();
                }
            }
        }

        private static string CalculateRelativePath(ProjectFile projectFile, string fromPath, string toPath)
        {
            var fromNodes = fromPath.ToLowerInvariant().Split("\\".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var toNodes = toPath.ToLowerInvariant().Split("\\".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            int index = 0;
            var master = string.Empty;
            while (fromNodes[index] == toNodes[index])
            {
                master += fromNodes[index] + "\\";
                ++index;
            }

            var relativePath = projectFile.CalculateRelativePath(master);
            for (int i = index; i < toNodes.Length; ++i)
            {
                relativePath += toNodes[i] + "\\";
            }
            return relativePath;
        }

        private static bool IsTargetProject(string assemblyName, string[] targetProjects, ProjectFile projectFile)
        {
            if (projectFile.IsExecutable)
                return true;

            var directory = Path.GetDirectoryName(projectFile.PathToFile);
            if (directory == null)
                throw new Exception("Unable to resolve directory");

            var webConfig = Path.Combine(directory, "Web.config");
            if (File.Exists(webConfig))
                return true;

            if (assemblyName.IndexOf("Test", 0, StringComparison.InvariantCultureIgnoreCase) >= 0)
                return true;

            var isTargetProject = false;
            if (targetProjects != null)
            {
                foreach (var targetProject in targetProjects)
                {
                    if (targetProject.StartsWith("*"))
                    {
                        var pattern = targetProject.Remove(0, 1);
                        if (assemblyName.Contains(pattern))
                        {
                            isTargetProject = true;
                        }
                    }
                    else if (string.Equals(assemblyName, targetProject, StringComparison.InvariantCultureIgnoreCase))
                    {
                        isTargetProject = true;
                    }
                }
            }
            return isTargetProject;
        }
    }
}
