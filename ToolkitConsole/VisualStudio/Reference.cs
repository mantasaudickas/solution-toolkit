using System;
using System.Xml.Serialization;

namespace SolutionToolkit.VisualStudio
{
    public class Reference
    {
        [XmlAttribute]
        public string Include { get; set; }
        public string HintPath { get; set; }
        public string Private { get; set; }
        public string SpecificVersion { get; set; }

        [XmlIgnore]
        public bool IsCustom { get; set; }

        public string GetIncludeName()
        {
            if (string.IsNullOrWhiteSpace(Include))
            {
                return Include;
            }

            int index = Include.IndexOf(",", StringComparison.Ordinal);
            if (index < 0)
            {
                return Include;
            }

            return Include.Substring(0, index);
        }
    }
}
