using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolutionToolkit.VisualStudio;

namespace SolutionToolkit.Implementation
{
    public class ProjectFileModificationImplementation : IImplementation
    {
        public void Execute(ProjectConfiguration configuration, string selectedProject)
        {
            DirectoryInfo projectDirectory = new DirectoryInfo(configuration.RootPath);
            var projectFiles = projectDirectory
                .EnumerateFileSystemInfos("*.csproj", SearchOption.AllDirectories)
                .ToList();

            var targetProjects = configuration.ResolveAssemblies(selectedProject);
            ChangeOutputPath(projectFiles, configuration.RootPath, configuration.BinariesOutputPath, targetProjects);
            ChangeReferences(projectFiles, configuration.RootPath, configuration.BinariesOutputPath, configuration.TargetFrameworkVersion, targetProjects);
        }

        private static void ChangeReferences(IEnumerable<FileSystemInfo> projectFiles, string projectRootPath, string binariesPath, string targetFrameworkVersion, string[] targetProjects)
        {
            var duplicates = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var singleProject in projectFiles)
            {
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
                //var references = projectFile.References;

                Console.WriteLine($"Replacing {singleProject.FullName}");
                foreach (var reference in projectReferences)
                {
                    var referedProject = Path.GetFullPath(Path.Combine(currentDirectory, reference.Include));
                    var referedProjectFile = new ProjectFile(referedProject);
                    var referedAssemblyName = referedProjectFile.GetAssemblyName();
                    var referedOutputPath = referedProjectFile.GetOutputPath();

                    var toPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(referedProject), referedOutputPath));
                    var outputDir = CalculateRelativePath(projectFile, singleProject.FullName, toPath);

                    var hintPath = $"{outputDir}{referedAssemblyName}.dll";
                    projectFile.AddReference(reference.Name, hintPath);
                }

                var outputPath = relativePath + binariesPath;
                if (!outputPath.EndsWith("\\"))
                {
                    outputPath += "\\";
                }

                var isTargetProject = IsTargetProject(assemblyName, targetProjects);

                projectFile.SetReferencePrivacy(isTargetProject, new[] { "\\packages\\", outputPath });
                projectFile.RemoveProjectReferences();
                projectFile.AddSystemRuntimeReference();
                if (!string.IsNullOrWhiteSpace(targetFrameworkVersion))
                {
                    projectFile.SetTargetFrameworkVersion(targetFrameworkVersion);
                }

                projectFile.RemoveNuget();

                projectFile.Save();
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

                var isTargetProject = IsTargetProject(assemblyName, targetProjects);
                if (!isTargetProject)
                {
                    projectFile.SetOutputPath(outputPath);
                }
                else
                {
                    //projectFile.SetOutputPath("bin\\" + assemblyName);
                }

                projectFile.Save();
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

        private static bool IsTargetProject(string assemblyName, string[] targetProjects)
        {
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
