using System.Xml.Serialization;

namespace SolutionToolkit.VisualStudio
{
    public class ProjectReference
    {
        [XmlAttribute]
        public string Include { get; set; }
        public string Project { get; set; }
        public string Name { get; set; }
    }
}
