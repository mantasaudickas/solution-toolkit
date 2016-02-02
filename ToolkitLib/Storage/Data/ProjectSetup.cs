using System;
using System.Xml.Serialization;
using SolutionGenerator.Toolkit.Core;
using SolutionGenerator.Toolkit.Solutions.Project;

namespace SolutionGenerator.Toolkit.Storage.Data
{
	[Serializable]
	public class ProjectSetup : DomainObject
	{
		public ProjectSetup()
		{
			this.WhenContainsFileReferences = ProjectSetupBehavior.Valid;
			this.WhenContainsProjectReferences = ProjectSetupBehavior.Warn;
			this.WhenReferenceNotResolved = ProjectSetupBehavior.Fail;
			this.WhenReferenceResolvedInDifferentLocation = ProjectSetupBehavior.Warn;
			this.WhenRequiredProjectLinkNotFound = ProjectSetupBehavior.Warn;
			this.WhenAssemblyKeyFileNotFound = ProjectSetupBehavior.Valid;
			this.ProjectReaderType = ProjectReaderType.Xml;
		}

		[XmlElement("whenContainsProjectReferences")]
		public ProjectSetupBehavior WhenContainsProjectReferences { get; set; }
		[XmlElement("whenContainsFileReferences")]
		public ProjectSetupBehavior WhenContainsFileReferences { get; set; }
		[XmlElement("whenReferenceNotResolved")]
		public ProjectSetupBehavior WhenReferenceNotResolved { get; set; }
		[XmlElement("whenReferenceResolvedInDifferentLocation")]
		public ProjectSetupBehavior WhenReferenceResolvedInDifferentLocation { get; set; }
		[XmlElement("whenRequiredProjectLinkNotFound")]
		public ProjectSetupBehavior WhenRequiredProjectLinkNotFound { get; set; }
		[XmlElement("whenAssemblyKeyFileNotFound")]
		public ProjectSetupBehavior WhenAssemblyKeyFileNotFound { get; set; }

		[XmlArray("requiredProjectFiles")]
		[XmlArrayItem("file")]
		public string[] RequiredProjectFiles { get; set; }

		[XmlElement("assemblyKeyFile")]
		public string AssemblyKeyFile { get; set; }

		[XmlElement("ProjectReader")]
		public ProjectReaderType ProjectReaderType { get; set; }
	}
}
