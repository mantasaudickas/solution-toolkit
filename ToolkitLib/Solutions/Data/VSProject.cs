using System;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.Solutions.Data
{
    [Serializable]
    public class VSProject : DomainObject
    {
        public string ProjectFileLocation { get; set; }
        public Guid ProjectId { get; set; }
        public string AssemblyName { get; set; }
        public string RootNamespace { get; set; }
        public string OutputPath { get; set; }
        public string AssemblyOriginatorKeyFile { get; set; }
        public VSProjectOutputType OutputType { get; set; }

        public string ResolvedOutput { get; set; }
        public string ResolvedAssemblyOriginatorKeyFile { get; set; }

        public VSProjectReference[] References { get; set; }
        public bool IsLauncher { get; set; }
        //public VSIncludedFile[] IncludedFiles { get; set; }
    }
}
