using System;
using SolutionGenerator.Toolkit.Core;

namespace SolutionGenerator.Toolkit.Solutions.Data
{
	[Serializable]
	public class VSIncludedFile : DomainObject
	{
		public string Include { get; set; }
		public string ResolvedInclude { get; set; }
		public bool IsLink { get; set; }
		public string LinkTarget { get; set; }
	}
}
