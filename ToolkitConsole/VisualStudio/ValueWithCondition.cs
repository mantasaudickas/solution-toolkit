using System.Xml.Serialization;

namespace SolutionToolkit.VisualStudio
{
    public class ValueWithCondition
    {
        [XmlAttribute]
        public string Condition { get; set; }

        public string Value { get; set; }
    }
}
