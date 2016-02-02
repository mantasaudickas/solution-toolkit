using System.Linq;
using SolutionGenerator.Toolkit.Storage.Data;

namespace SolutionToolkit.Implementation
{
    public class SolutionGenerationImplementation : IImplementation
    {
        private readonly string _solutionOutputPath;

        public SolutionGenerationImplementation(string solutionOutputPath)
        {
            _solutionOutputPath = solutionOutputPath;
        }

        public void Execute(ProjectConfiguration configuration, string selectedProject)
        {
            GenerateSolution(
                configuration.RootPath,
                configuration.ThirdPartiesRootPath,
                configuration.ResolveAssemblies(selectedProject));
        }

        private int GenerateSolution(string projectPath, string thirdParties, string[] targetProjects)
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

            SolutionGenerator.Toolkit.SolutionGenerator generator =
                new SolutionGenerator.Toolkit.SolutionGenerator(new ConsoleLogger());

            return generator.CreateSolution(projectSetup, _solutionOutputPath, projectPath, thirdParties, targetProjects);
        }

    }
}
