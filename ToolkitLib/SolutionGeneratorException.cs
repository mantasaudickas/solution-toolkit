using System;

namespace SolutionGenerator.Toolkit
{
	public class SolutionGeneratorException : ApplicationException
	{
		public SolutionGeneratorException(string message, params object [] args) : base(Format(message, args))
		{
		}

		public SolutionGeneratorException(string message, Exception innerException, params object[] args)
			: base(Format(message, args), innerException)
		{
		}

		private static string Format(string message, params object [] args)
		{
			if (string.IsNullOrWhiteSpace(message))
				return message;

			if (args == null || args.Length == 0)
				return message;

			return string.Format(message, args);
		}
	}
}
