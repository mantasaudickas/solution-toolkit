using System;
using SolutionGenerator.Toolkit.Logging;

namespace SolutionGenerator.Toolkit
{
	public class BaseComponent
	{
		private readonly ILogger logger;

		public BaseComponent(ILogger logger)
		{
			this.logger = logger;
		}

		protected ILogger Logger { get { return this.logger; } }
	}
}
