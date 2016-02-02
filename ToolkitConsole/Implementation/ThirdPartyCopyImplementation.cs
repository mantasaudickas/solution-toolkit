using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SolutionGenerator.Toolkit.Storage.Data;

namespace SolutionToolkit.Implementation
{
    public class ThirdPartyCopyImplementation : IImplementation
    {
        private readonly string _outputPath;

        public ThirdPartyCopyImplementation(string outputPath)
        {
            _outputPath = outputPath;
        }

        public void Execute(ProjectConfiguration configuration, string selectedProject)
        {
            CopyThirdParties(
                configuration.RootPath,
                configuration.ThirdPartiesRootPath,
                selectedProject);
        }

        private int CopyThirdParties(string projectPath, string thirdParties, string targetProject)
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

            return generator.CopyThirdParties(projectSetup, _outputPath, projectPath, thirdParties, new[] {targetProject});
        }
    }
}
