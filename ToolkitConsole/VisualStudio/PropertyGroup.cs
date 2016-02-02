using System.Xml.Serialization;

namespace SolutionToolkit.VisualStudio
{
    public class PropertyGroup
    {
        [XmlAttribute]
        public string Condition { get; set; }

        public ValueWithCondition Configuration { get; set; }
        public ValueWithCondition Platform { get; set; }
        public string ProjectGuid { get; set; }
        public string OutputType { get; set; }
        public string AppDesignerFolder { get; set; }
        public string RootNamespace { get; set; }
        public string AssemblyName { get; set; }
        public string TargetFrameworkVersion { get; set; }
        public string FileAlignment { get; set; }
        public string TargetFrameworkProfile { get; set; }
        public ValueWithCondition SolutionDir { get; set; }
        public string RestorePackages { get; set; }

        public string DebugSymbols { get; set; }
        public string DebugType { get; set; }
        public string Optimize { get; set; }
        public string OutputPath { get; set; }
        public string DefineConstants { get; set; }
        public string ErrorReport { get; set; }
        public string WarningLevel { get; set; }
    }
}
