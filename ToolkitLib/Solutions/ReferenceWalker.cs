using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SolutionGenerator.Toolkit.Storage.Data;
using SolutionGenerator.Toolkit.FileSystem;
using SolutionGenerator.Toolkit.Logging;
using SolutionGenerator.Toolkit.Solutions.Data;

namespace SolutionGenerator.Toolkit.Solutions
{
    public class ReferenceWalker : BaseComponent
    {
        private static readonly IDictionary<Guid, ReferenceCacheItem> ReferenceCache = new ConcurrentDictionary<Guid, ReferenceCacheItem>();
        private static readonly IDictionary<Guid, ReferenceCacheItem> ProjectReferenceCache = new ConcurrentDictionary<Guid, ReferenceCacheItem>();
        private static readonly HashSet<string> SystemReferenceFileMap = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        private static readonly IDictionary<string, HashSet<string>> ThirdPartyFileCache =
            new ConcurrentDictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase);

        private readonly FileSearcher _fileSearcher;

        static ReferenceWalker()
        {
            string [] searhcFolders = {"Reference Assemblies", "Microsoft.NET", "Microsoft SDKs", @"Microsoft Visual Studio 10.0\Common7\IDE\PublicAssemblies"};

            string programFilesLocationx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (Directory.Exists(programFilesLocationx86))
            {
                AddReferenceAssemblies(programFilesLocationx86, searhcFolders);
            }

            string programFilesLocation = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (Directory.Exists(programFilesLocation))
            {
                AddReferenceAssemblies(programFilesLocation, searhcFolders);
            }

            AddReferenceAssemblies(@"c:\Windows\assembly");
        }

        public ReferenceWalker(ILogger logger) : base(logger)
        {
            _fileSearcher = new FileSearcher(logger);
        }

        internal static void ClearCache()
        {
            ReferenceCache.Clear();
            ThirdPartyFileCache.Clear();
        }

        protected FileSearcher FileSearcher { get { return _fileSearcher; } }

        private static void AddReferenceAssemblies(string folderPath, params string [] subFolders)
        {
            if (subFolders == null)
                return;

            if (!Directory.Exists(folderPath))
                return;

            if (subFolders.Length == 0)
            {
                string[] assemblies = Directory.GetFiles(folderPath, "*.dll", SearchOption.AllDirectories);
                foreach (string assemblyName in assemblies)
                {
                    string referenceName = Path.GetFileNameWithoutExtension(assemblyName);
                    if (referenceName != null)
                        SystemReferenceFileMap.Add(referenceName);
                }
            }

            foreach (string subFolder in subFolders)
            {
                if (string.IsNullOrWhiteSpace(subFolder))
                    continue;

                string assemblyCache = Path.Combine(folderPath, subFolder);

                if (!Directory.Exists(assemblyCache))
                    return;

                string[] assemblies = Directory.GetFiles(assemblyCache, "*.dll", SearchOption.AllDirectories);
                foreach (string assemblyName in assemblies)
                {
                    string referenceName = Path.GetFileNameWithoutExtension(assemblyName);
                    if (referenceName != null)
                        SystemReferenceFileMap.Add(referenceName);
                }
            }
        }

        public List<Guid> WalkReferencesRecursively(ProjectSetup projectSetup, ProjectLoader projectLoader, IEnumerable<string> projectLocations, string[] thirdPartyFolders, HashSet<string> usedThirdParties)
        {
            List<Guid> completeReferences = new List<Guid>();
            foreach (string projectLocation in projectLocations)
            {
                VSProject project = projectLoader.LoadProject(projectSetup, projectLocation);

                List<Guid> dependentReferences = WalkReferencesRecursively(projectSetup, projectLoader, project, thirdPartyFolders, usedThirdParties);
                dependentReferences.Add(project.ProjectId);

                foreach (Guid dependentReference in dependentReferences)
                {
                    if (!completeReferences.Contains(dependentReference))
                        completeReferences.Add(dependentReference);
                }
            }
            return completeReferences;
        }

        public List<Guid> WalkReferencesRecursively(ProjectSetup projectSetup, ProjectLoader projectLoader, VSProject project, string[] thirdPartyFolders, HashSet<string> usedThirdParties)
        {
            HashSet<string> thirdPartyFileMap;
            if (thirdPartyFolders != null && thirdPartyFolders.Length > 0)
            {
                string key = string.Join(":", thirdPartyFolders);

                if (!ThirdPartyFileCache.TryGetValue(key, out thirdPartyFileMap))
                {
                    thirdPartyFileMap = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                    List<string> thirdPartyFiles = FileSearcher.Scan("*.dll", thirdPartyFolders);
                    thirdPartyFileMap.UnionWith(thirdPartyFiles);
                    ThirdPartyFileCache.Add(key, thirdPartyFileMap);
                }
            }
            else
            {
                thirdPartyFileMap = new HashSet<string>();
            }

            List<Guid> checkedProjects = new List<Guid>();

            List<Guid> completeReferences = GetReferencesRecursively(projectSetup, projectLoader, project, thirdPartyFileMap, usedThirdParties, checkedProjects);
            return completeReferences;
        }

        public List<Guid> GetProjectDependencies(ProjectSetup projectSetup, ProjectLoader projectLoader, VSProject project, string[] thirdPartyFolders, HashSet<string> usedThirdParties)
        {
            HashSet<string> thirdPartyFileMap;
            if (thirdPartyFolders != null && thirdPartyFolders.Length > 0)
            {
                string key = string.Join(":", thirdPartyFolders);

                if (!ThirdPartyFileCache.TryGetValue(key, out thirdPartyFileMap))
                {
                    thirdPartyFileMap = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                    List<string> thirdPartyFiles = FileSearcher.Scan("*.dll", thirdPartyFolders);
                    thirdPartyFileMap.UnionWith(thirdPartyFiles);
                    ThirdPartyFileCache.Add(key, thirdPartyFileMap);
                }
            }
            else
            {
                thirdPartyFileMap = new HashSet<string>();
            }

            List<Guid> checkedProjects = new List<Guid>();

            var completeReferences = GetCurrentProjectReferences(projectSetup, projectLoader, project, thirdPartyFileMap, usedThirdParties, checkedProjects);
            return completeReferences;
        }

        private List<Guid> GetReferencesRecursively(ProjectSetup projectSetup, ProjectLoader projectLoader, VSProject project, HashSet<string> thirdPartyFileMap, HashSet<string> usedThirdParties, List<Guid> checkedProjects)
        {
            ReferenceCacheItem cacheItem;
            if (!ReferenceCache.TryGetValue(project.ProjectId, out cacheItem))
            {
                if (checkedProjects.Contains(project.ProjectId))
                {
                    StringBuilder builder = new StringBuilder();

                    for (int i = checkedProjects.Count - 1; i >= 0; i--)
                    {
                        Guid checkedProject = checkedProjects[i];
                        builder.Insert(0, projectLoader.GetProjectById(checkedProject).ProjectFileLocation + Environment.NewLine);
                        if (checkedProject == project.ProjectId)
                            break;
                    }

                    builder.AppendLine(project.ProjectFileLocation);

                    throw new SolutionGeneratorException("Circular reference detected! {1}Projects: {1}{0}", builder, Environment.NewLine);
                }
                checkedProjects.Add(project.ProjectId);

                cacheItem = new ReferenceCacheItem
                {
                    References = new List<Guid>(),
                    UsedThirdParties = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                };

                if (project.References != null)
                {
                    List<VSProject> referencedProjects = new List<VSProject>();
                    foreach (VSProjectReference reference in project.References)
                    {
                        if (thirdPartyFileMap.Contains(reference.ResolvedHintPath))
                        {
                            cacheItem.UsedThirdParties.Add(reference.ResolvedHintPath);
                            continue;
                        }

                        if (SystemReferenceFileMap.Contains(reference.ResolvedInclude))
                        {
                            continue;
                        }

                        if (reference.ResolvedInclude.StartsWith("Microsoft.", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        ResolveProject(projectSetup, projectLoader, project, reference, referencedProjects);
                    }

                    foreach (VSProject referencedProject in referencedProjects)
                    {
                        List<Guid> dependentReferences = GetReferencesRecursively(projectSetup, projectLoader, referencedProject, thirdPartyFileMap, cacheItem.UsedThirdParties, checkedProjects);
                        dependentReferences.Add(referencedProject.ProjectId);

                        foreach (Guid dependentReference in dependentReferences)
                        {
                            if (!cacheItem.References.Contains(dependentReference))
                                cacheItem.References.Add(dependentReference);
                        }
                    }
                }

                //cacheItem.UsedThirdParties.UnionWith(usedThirdParties);
                ReferenceCache.Add(project.ProjectId, cacheItem);
            }

            usedThirdParties.UnionWith(cacheItem.UsedThirdParties);
            return cacheItem.References;
        }

        private List<Guid> GetCurrentProjectReferences(ProjectSetup projectSetup, ProjectLoader projectLoader, VSProject project, HashSet<string> thirdPartyFileMap, HashSet<string> usedThirdParties, List<Guid> checkedProjects)
        {
            ReferenceCacheItem cacheItem;
            if (!ProjectReferenceCache.TryGetValue(project.ProjectId, out cacheItem))
            {
                if (checkedProjects.Contains(project.ProjectId))
                {
                    StringBuilder builder = new StringBuilder();

                    for (int i = checkedProjects.Count - 1; i >= 0; i--)
                    {
                        Guid checkedProject = checkedProjects[i];
                        builder.Insert(0, projectLoader.GetProjectById(checkedProject).ProjectFileLocation + Environment.NewLine);
                        if (checkedProject == project.ProjectId)
                            break;
                    }

                    builder.AppendLine(project.ProjectFileLocation);

                    throw new SolutionGeneratorException("Circular reference detected! {1}Projects: {1}{0}", builder, Environment.NewLine);
                }
                checkedProjects.Add(project.ProjectId);

                cacheItem = new ReferenceCacheItem
                {
                    References = new List<Guid>(),
                    UsedThirdParties = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                };

                if (project.References != null)
                {
                    List<VSProject> referencedProjects = new List<VSProject>();
                    foreach (VSProjectReference reference in project.References)
                    {
                        if (thirdPartyFileMap.Contains(reference.ResolvedHintPath))
                        {
                            cacheItem.UsedThirdParties.Add(reference.ResolvedHintPath);
                            continue;
                        }

                        if (SystemReferenceFileMap.Contains(reference.ResolvedInclude))
                        {
                            continue;
                        }

                        if (reference.ResolvedInclude.StartsWith("Microsoft.", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        ResolveProject(projectSetup, projectLoader, project, reference, referencedProjects);
                    }

                    foreach (VSProject referencedProject in referencedProjects)
                    {
                        if (!cacheItem.References.Contains(referencedProject.ProjectId))
                            cacheItem.References.Add(referencedProject.ProjectId);
                    }
                }

                //cacheItem.UsedThirdParties.UnionWith(usedThirdParties);
                ProjectReferenceCache.Add(project.ProjectId, cacheItem);
            }

            usedThirdParties.UnionWith(cacheItem.UsedThirdParties);
            return cacheItem.References;
        }

        private void ResolveProject(ProjectSetup projectSetup, ProjectLoader projectLoader, VSProject project, VSProjectReference reference, List<VSProject> referencedProjects)
        {
            VSProject vsProject;
            if (reference.IsProjectReference)
            {
                // resolve by ID
                vsProject = projectLoader.GetProjectById(reference.ProjectReferenceId);
            }
            else if (reference.IsSilverlightReference)
            {
                vsProject = projectLoader.GetProjectById(reference.ProjectReferenceId);
            }
            else
            {
                // resolve by output path
                vsProject = projectLoader.GetProjectByOutput(reference.ResolvedHintPath);
            }

            if (vsProject != null)
            {
                referencedProjects.Add(vsProject);
            }
            else
            {
                // fall back scenario, check assembly name in current project output path
                string outputDirectory = Path.GetDirectoryName(project.ResolvedOutput) ?? ".";
                string currentFolderAssembly = Path.Combine(outputDirectory, reference.ResolvedInclude) + ".dll";
                vsProject = projectLoader.GetProjectByOutput(currentFolderAssembly);

                if (vsProject== null)
                {
                    currentFolderAssembly = Path.Combine(outputDirectory, reference.ResolvedInclude) + ".exe";
                    vsProject = projectLoader.GetProjectByOutput(currentFolderAssembly);
                }

                if (vsProject == null)
                {
                    if (projectSetup.WhenReferenceNotResolved == ProjectSetupBehavior.Fail)
                    {
                        throw new SolutionGeneratorException(
                            "Reference {0} was not resolved. {3}Project {1}. {3}Expected location = {2}{3}",
                            reference.ResolvedInclude, project.ProjectFileLocation, reference.ResolvedHintPath, Environment.NewLine);
                    }
                    if (projectSetup.WhenReferenceNotResolved == ProjectSetupBehavior.Warn)
                    {
                        Logger.Warn(
                            "Reference {0} was not resolved. {3}Project {1}. {3}Expected location = {2}{3}",
                            reference.ResolvedInclude, project.ProjectFileLocation, reference.ResolvedHintPath, Environment.NewLine);
                    }
                }
                else
                {
                    if (projectSetup.WhenReferenceResolvedInDifferentLocation == ProjectSetupBehavior.Fail)
                    {
                        throw new SolutionGeneratorException(
                            "Reference {0} was not resolved. {4}Project {1}. {4}Expected location = {2}{4}However it was found in project output folder {3}.{4}",
                            reference.ResolvedInclude, project.ProjectFileLocation, reference.ResolvedHintPath, outputDirectory, Environment.NewLine);
                    }
                    if (projectSetup.WhenReferenceResolvedInDifferentLocation == ProjectSetupBehavior.Warn)
                    {
                        Logger.Warn(
                            "Reference {0} was not resolved. {4}Project {1}. {4}Expected location = {2}{4}However it was found in project output folder {3}.{4}",
                            reference.ResolvedInclude, project.ProjectFileLocation, reference.ResolvedHintPath, outputDirectory, Environment.NewLine);
                    }
                }
            }
        }

        private class ReferenceCacheItem
        {
            public List<Guid> References;
            public HashSet<string> UsedThirdParties;
        }
    }
}
