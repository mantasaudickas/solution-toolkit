using System;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.Solutions.Data
{
	[Serializable]
	public class VSProjectReference : DomainObject
	{
		public string Include { get; set; }
		public string Name { get; set; }
		public string HintPath { get; set; }
		public bool IsProjectReference { get; set; }
		public bool IsSilverlightReference { get; set; }
		public Guid ProjectReferenceId { get; set; }

		public string ResolvedHintPath { get; set; }
		public string ResolvedInclude { get; set; }
	}
}
