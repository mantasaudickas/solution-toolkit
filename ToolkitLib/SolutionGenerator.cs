using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SolutionGenerator.Toolkit.FileSystem;
using SolutionGenerator.Toolkit.Logging;
using SolutionGenerator.Toolkit.Solutions;
using SolutionGenerator.Toolkit.Solutions.Data;
using SolutionGenerator.Toolkit.Storage;
using SolutionGenerator.Toolkit.Storage.Data;

namespace SolutionGenerator.Toolkit
{
    public class SolutionGenerator : BaseComponent
    {
        public SolutionGenerator(ILogger logger) : base(logger)
        {
            ProjectConfigurationReader = new ProjectConfigurationReader(logger);
            FileSearcher = new FileSearcher(logger);
            SolutionCreator = new SolutionCreator(logger);
        }

        private ProjectConfigurationReader ProjectConfigurationReader { get; }
        private FileSearcher FileSearcher { get; }
        private SolutionCreator SolutionCreator { get; }

        public static void ClearCaches()
        {
            ProjectConfigurationReader.ClearCache();
            FileSearcher.ClearCache();
            ReferenceWalker.ClearCache();
        }

        public int CreateSolution(ProjectSetup projectSetup, string solutionToGenerate, string sourceRootFolder, string thirdPartiesFolder, string[] targetProjects)
        {
            var projectLoader = GetProjectLoader(projectSetup, sourceRootFolder);

            var targetProjectFiles = GetTargetProjectFiles(projectLoader, targetProjects);

            var thirdPartyFolders = string.IsNullOrWhiteSpace(thirdPartiesFolder) ? new string[] { } : new[] { thirdPartiesFolder };

            ReferenceWalker walker = new ReferenceWalker(Logger);
            var dependencies = walker.WalkReferencesRecursively(projectSetup, projectLoader, targetProjectFiles, thirdPartyFolders, new HashSet<string>());
            var projectList = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            projectList.UnionWith(targetProjectFiles);
            foreach (var dependency in dependencies)
            {
                var p = projectLoader.GetProjectById(dependency);
                if (p != null)
                {
                    projectList.Add(p.ProjectFileLocation);
                }
            }

            Logger.Info("Files configured for solution generation:");
            foreach (string item in projectList)
            {
                Logger.Info("   {0}", item);
            }

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                if (!Path.IsPathRooted(solutionToGenerate))
                {
                    solutionToGenerate = Path.GetFullPath(Path.Combine(sourceRootFolder, solutionToGenerate));
                }

                HashSet<string> usedThirdParties = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                GeneratedSolution solution = SolutionCreator.CreateSolution(
                    projectSetup, projectLoader, projectList, solutionToGenerate, thirdPartyFolders, usedThirdParties, null);

                string solutionDirectory = Path.GetDirectoryName(solutionToGenerate) ?? ".";
                if (!Directory.Exists(solutionDirectory))
                    Directory.CreateDirectory(solutionDirectory);

                Logger.Info("Writing solution with {0} project to file {1}", solution.IncludedProjects, solutionToGenerate);
                File.WriteAllText(solutionToGenerate, solution.Content, Encoding.UTF8);

                HashSet<string> completeThirdPartyList = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string assemblyLocation in usedThirdParties)
                {
                    string folder = Path.GetDirectoryName(assemblyLocation) ?? ".";
                    CollectDependentAssemblies(folder, assemblyLocation, completeThirdPartyList, null);
                }

                List<string> usedThirdPartyList = new List<string>(completeThirdPartyList);
                usedThirdPartyList.Sort();
                if (usedThirdPartyList.Count > 0)
                {
                    Logger.Info("Third parties used in solution:");

                    ThirdPartyFileContainer container = new ThirdPartyFileContainer { Items = new ThirdPartyFile[usedThirdPartyList.Count] };

                    for (int i = 0; i < usedThirdPartyList.Count; ++i)
                    {
                        string assemblyLocation = usedThirdPartyList[i];
                        container.Items[i] = new ThirdPartyFile(sourceRootFolder, assemblyLocation);
                    }
                    string thirdPartyFileContent = container.XmlSerialize(false, "http://schemas.microsoft.com/developer/msbuild/2003");
                    File.WriteAllText(solutionToGenerate + ".thirdparties", thirdPartyFileContent, Encoding.UTF8);
                }

                return solution.IncludedProjects;
            }
            finally
            {
                timer.Stop();
                Logger.Info("Solution generated in {0}", timer.Elapsed);
            }
        }

        public List<string> GetTargetProjectFiles(ProjectLoader projectLoader, string[] targetProjects)
        {
            var targetProjectFiles = new List<string>();
            foreach (var assemblyName in targetProjects)
            {
                var projectFile = projectLoader.GetProjectByAssemblyName(assemblyName);
                if (projectFile == null)
                    throw new Exception($"Project by assembly name {assemblyName} not found");
                targetProjectFiles.Add(projectFile.ProjectFileLocation);
            }
            return targetProjectFiles;
        }

        public ProjectLoader GetProjectLoader(ProjectSetup projectSetup, string sourceRootFolder)
        {
            List<string> projectFiles = FileSearcher.Scan("*.csproj", new[] { sourceRootFolder });
            ProjectLoader projectLoader = ProjectLoader.Create(Logger, sourceRootFolder);

            int loadedCount = 0;
            Stopwatch loadTimer = Stopwatch.StartNew();
            try
            {
                Logger.Info("");
                Logger.Info("Preloading project files...");
                loadedCount = projectLoader.PreloadProjects(projectSetup, projectFiles);
            }
            finally
            {
                loadTimer.Stop();
                Logger.Info("Source load completed in {0}. Loaded project files = {1}{2}", loadTimer.Elapsed, loadedCount,
                    Environment.NewLine);
            }
            return projectLoader;
        }

        public void CreateSolution(string configurationFilePath, string solutionToGenerate, string sourceRootFolder, params string[] groupNames)
        {
            ProjectConfiguration configuration = ProjectConfigurationReader.Read(configurationFilePath, sourceRootFolder);
            sourceRootFolder = configuration.ResolvedSourceRootPath;

            string[] sourceFolders = configuration.SourceFolders;
            if (sourceFolders == null || sourceFolders.Length == 0)
            {
                sourceFolders = new[] { sourceRootFolder };
            }

            List<string> projectFiles = FileSearcher.Scan("*.csproj", sourceFolders);
            ProjectLoader projectLoader = ProjectLoader.Create(this.Logger, sourceRootFolder);

            int loadedCount = 0;
            Stopwatch loadTimer = Stopwatch.StartNew();
            try
            {
                Logger.Info("");
                Logger.Info("Preloading project files...");
                loadedCount = projectLoader.PreloadProjects(configuration.ProjectSetup, projectFiles);
            }
            finally
            {
                loadTimer.Stop();
                Logger.Info("Source load completed in {0}. Loaded project files = {1}{2}", loadTimer.Elapsed, loadedCount, Environment.NewLine);
            }

            string groupNameList = string.Join(";", groupNames);

            Logger.Info("");
            Logger.Info("Processing groups: {0}{1}", groupNameList, Environment.NewLine);

            string[] projectGroups = configuration.Select(groupNames);

            if (projectGroups == null || projectGroups.Length == 0)
            {
                throw new SolutionGeneratorException("Chosen groups does not have configured projects. Groups: {0}", groupNameList);
            }

            Logger.Info("Files configured for solution generation:");
            foreach (string item in projectGroups)
            {
                Logger.Info("   {0}", item);
            }

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                if (!Path.IsPathRooted(solutionToGenerate))
                {
                    solutionToGenerate = Path.GetFullPath(Path.Combine(sourceRootFolder, solutionToGenerate));
                }

                HashSet<string> usedThirdParties = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                GeneratedSolution solution = SolutionCreator.CreateSolution(
                    configuration.ProjectSetup, projectLoader, projectGroups, solutionToGenerate, configuration.ThirdPartyFolders, usedThirdParties, configuration.CustomAppend);

                string solutionDirectory = Path.GetDirectoryName(solutionToGenerate) ?? ".";
                if (!Directory.Exists(solutionDirectory))
                    Directory.CreateDirectory(solutionDirectory);

                Logger.Info("Writing solution with {0} project to file {1}", solution.IncludedProjects, solutionToGenerate);
                File.WriteAllText(solutionToGenerate, solution.Content, Encoding.UTF8);

                HashSet<string> completeThirdPartyList = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string assemblyLocation in usedThirdParties)
                {
                    string folder = Path.GetDirectoryName(assemblyLocation) ?? ".";
                    CollectDependentAssemblies(folder, assemblyLocation, completeThirdPartyList, null);
                }

                List<string> usedThirdPartyList = new List<string>(completeThirdPartyList);
                usedThirdPartyList.Sort();
                if (usedThirdPartyList.Count > 0)
                {
                    Logger.Info("Third parties used in solution:");

                    ThirdPartyFileContainer container = new ThirdPartyFileContainer { Items = new ThirdPartyFile[usedThirdPartyList.Count] };

                    for (int i = 0; i < usedThirdPartyList.Count; ++i)
                    {
                        string assemblyLocation = usedThirdPartyList[i];
                        container.Items[i] = new ThirdPartyFile(sourceRootFolder, assemblyLocation);
                    }
                    string thirdPartyFileContent = container.XmlSerialize(false, "http://schemas.microsoft.com/developer/msbuild/2003");
                    File.WriteAllText(solutionToGenerate + ".thirdparties", thirdPartyFileContent, Encoding.UTF8);
                }
            }
            finally
            {
                timer.Stop();
                Logger.Info("Solution generated in {0}", timer.Elapsed);
            }
        }

        public void FindUnusedProjects(string configurationFilePath, string sourceRootFolder, params string[] groupNames)
        {
            List<string> unusedProjects = new List<string>();

            ProjectConfiguration configuration = ProjectConfigurationReader.Read(configurationFilePath, sourceRootFolder);
            sourceRootFolder = configuration.ResolvedSourceRootPath;

            string[] sourceFolders = configuration.SourceFolders;
            if (sourceFolders == null || sourceFolders.Length == 0)
            {
                sourceFolders = new[] { sourceRootFolder };
            }

            List<string> projectFiles = FileSearcher.Scan("*.csproj", sourceFolders);
            ProjectLoader projectLoader = ProjectLoader.Create(this.Logger, sourceRootFolder);

            int loadedCount = 0;
            Stopwatch loadTimer = Stopwatch.StartNew();
            try
            {
                Logger.Info("");
                Logger.Info("Preloading project files...");
                loadedCount = projectLoader.PreloadProjects(configuration.ProjectSetup, projectFiles);
            }
            finally
            {
                loadTimer.Stop();
                Logger.Info("Source load completed in {0}. Loaded project files = {1}{2}", loadTimer.Elapsed, loadedCount, Environment.NewLine);
            }

            string groupNameList = string.Join(";", groupNames);

            Logger.Info("");
            Logger.Info("Processing groups: {0}{1}", groupNameList, Environment.NewLine);

            string[] projectGroups = configuration.Select(groupNames);

            if (projectGroups == null || projectGroups.Length == 0)
            {
                throw new SolutionGeneratorException("Chosen groups does not have configured projects. Groups: {0}", groupNameList);
            }

            Logger.Info("Files configured for source check:");
            foreach (string item in projectGroups)
            {
                Logger.Info("   {0}", item);
            }

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                HashSet<string> usedThirdParties = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                ReferenceWalker referenceWalker = new ReferenceWalker(Logger);
                List<Guid> solutionProjectList = referenceWalker.WalkReferencesRecursively(configuration.ProjectSetup, projectLoader, projectGroups, configuration.ThirdPartyFolders, usedThirdParties);

                HashSet<Guid> includedProjects = new HashSet<Guid>(solutionProjectList);

                Logger.Info("Not used projects:");
                foreach (string projectLocation in projectFiles)
                {
                    VSProject loadedProject = projectLoader.LoadProject(configuration.ProjectSetup, projectLocation);
                    if (loadedProject != null && !includedProjects.Contains(loadedProject.ProjectId))
                    {
                        Logger.Warn("\t {0}", loadedProject.ProjectFileLocation);
                        unusedProjects.Add(loadedProject.ProjectFileLocation);
                    }
                }
                Logger.Info("Found {0} unused projects...", unusedProjects.Count);
            }
            finally
            {
                timer.Stop();
                Logger.Info("Solution generated in {0}", timer.Elapsed);
            }
        }

        private void CollectDependentAssemblies(string thirdPartyFolder, string assemblyLocation, HashSet<string> completeThirdPartyList, HashSet<string> checkedAssemblies)
        {
            CollectDependentAssembliesInternal(thirdPartyFolder, assemblyLocation, completeThirdPartyList, checkedAssemblies);
        }

        private void CollectDependentAssembliesInternal(string thirdPartyFolder, string assemblyLocation, HashSet<string> completeThirdPartyList, HashSet<string> checkedAssemblies)
        {
            try
            {
                if (checkedAssemblies == null)
                {
                    checkedAssemblies = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                }
                checkedAssemblies.Add(assemblyLocation);

                var depLocation = assemblyLocation.Replace(".dll", ".dep");
                if (File.Exists(depLocation))
                {
                    completeThirdPartyList.Add(depLocation);
                }

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Assembly assembly = assemblies.FirstOrDefault(a => a.FullName == Path.GetFileNameWithoutExtension(assemblyLocation));
                if (assembly == null)
                {
                    assembly = Assembly.LoadFrom(assemblyLocation);
                }
                completeThirdPartyList.Add(assemblyLocation);

                AssemblyName[] referencedAssemblies = assembly.GetReferencedAssemblies();
                foreach (AssemblyName referencedAssembly in referencedAssemblies)
                {
                    string location = referencedAssembly.Name;
                    string referencedAssemblyLocation = Path.Combine(thirdPartyFolder, location) + ".dll";
                    if (File.Exists(referencedAssemblyLocation) &&
                        !checkedAssemblies.Contains(referencedAssemblyLocation))
                    {
                        completeThirdPartyList.Add(referencedAssemblyLocation);
                        CollectDependentAssembliesInternal(thirdPartyFolder, referencedAssemblyLocation, completeThirdPartyList, checkedAssemblies);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warn(e.Message);
            }
        }

        public int CopyThirdParties(ProjectSetup projectSetup, string thirdPartyOutput, string sourceRootFolder, string thirdPartiesFolder, string[] targetProjects)
        {
            Logger.Info("Root folder: {0}", sourceRootFolder);
            Logger.Info("Thirdparties folder: {0}", thirdPartiesFolder);
            Logger.Info("Output folder: {0}", thirdPartyOutput);
            Logger.Info("Projects: {0}", string.Join(";", targetProjects));

            List<string> projectFiles = FileSearcher.Scan("*.csproj", new[] { sourceRootFolder });
            ProjectLoader projectLoader = ProjectLoader.Create(Logger, sourceRootFolder);

            int loadedCount = 0;
            Stopwatch loadTimer = Stopwatch.StartNew();
            try
            {
                Logger.Info("");
                Logger.Info("Preloading project files...");
                loadedCount = projectLoader.PreloadProjects(projectSetup, projectFiles);
            }
            finally
            {
                loadTimer.Stop();
                Logger.Info("Source load completed in {0}. Loaded project files = {1}{2}", loadTimer.Elapsed, loadedCount, Environment.NewLine);
            }

            var targetProjectFiles = new List<string>();
            foreach (var assemblyName in targetProjects)
            {
                var projectFile = projectLoader.GetProjectByAssemblyName(assemblyName);
                if (projectFile == null)
                    throw new Exception($"Project by assembly name {assemblyName} not found");
                targetProjectFiles.Add(projectFile.ProjectFileLocation);
            }

            var thirdPartyFolders = string.IsNullOrWhiteSpace(thirdPartiesFolder) ? new string[] { } : new[] { thirdPartiesFolder };

            var walker = new ReferenceWalker(Logger);
            var dependencies = walker.WalkReferencesRecursively(projectSetup, projectLoader, targetProjectFiles, thirdPartyFolders, new HashSet<string>());
            var projectList = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            projectList.UnionWith(targetProjectFiles);

            HashSet<string> projectDependenciesToCopy = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var dependency in dependencies)
            {
                var p = projectLoader.GetProjectById(dependency);
                if (p != null)
                {
                    projectList.Add(p.ProjectFileLocation);
                    projectDependenciesToCopy.Add(p.ResolvedOutput);
                }
            }

            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                if (!Directory.Exists(thirdPartyOutput))
                    Directory.CreateDirectory(thirdPartyOutput);

                HashSet<string> dependenciesToCopy = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                GeneratedSolution solution = SolutionCreator.CreateSolution(
                    projectSetup, projectLoader, projectList, "GeneratedSolution", thirdPartyFolders, dependenciesToCopy, null);

                HashSet<string> completeThirdPartyList = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string assemblyLocation in dependenciesToCopy)
                {
                    string folder = Path.GetDirectoryName(assemblyLocation) ?? ".";
                    CollectDependentAssemblies(folder, assemblyLocation, completeThirdPartyList, null);
                }

                projectDependenciesToCopy.UnionWith(completeThirdPartyList);

                if (completeThirdPartyList.Count > 0)
                {
                    ThirdPartyFileContainer container = new ThirdPartyFileContainer { Items = new ThirdPartyFile[completeThirdPartyList.Count] };

                    var coppied = 0;
                    var index = 0;
                    foreach (var assemblyLocation in completeThirdPartyList)
                    {
                        container.Items[index++] = new ThirdPartyFile(sourceRootFolder, assemblyLocation);

                        var fileName = Path.GetFileName(assemblyLocation) ?? "";
                        var targetFileLocation = Path.Combine(thirdPartyOutput, fileName);

                        FileInfo sourceFileInfo = new FileInfo(assemblyLocation);
                        FileInfo targetFileInfo = new FileInfo(targetFileLocation);

                        if (!targetFileInfo.Exists || targetFileInfo.Length != sourceFileInfo.Length || targetFileInfo.LastWriteTimeUtc != sourceFileInfo.LastWriteTimeUtc)
                        {
                            try
                            {
                                sourceFileInfo.CopyTo(targetFileLocation, true);
                                ++coppied;
                                Logger.Info("Copied to target: {0}", targetFileLocation);
                            }
                            catch (Exception e)
                            {
                                Logger.Error("Unable to copy to target: {0}. Error: {1}", targetFileLocation, e.Message);
                            }
                        }
                    }

                    Logger.Info("Coppied {0} files.", coppied);
                }

                return solution.IncludedProjects;
            }
            finally
            {
                timer.Stop();
                Logger.Warn("Third parties copied in {0}", timer.Elapsed);
            }
        }

    }
}
