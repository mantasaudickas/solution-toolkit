using System;

namespace SolutionGenerator.Toolkit
{
	public interface ILogger
	{
		void Trace(string message, params object[] args);
		void Debug(string message, params object[] args);
		void Info(string message, params object[] args);
		void Warn(string message, params object[] args);
		void Error(string message, params object[] args);
		void Error(Exception exception);
		void Fatal(string message, params object[] args);
	}

	public abstract class LoggerBase : ILogger
	{
		public void Trace(string message, params object[] args)
		{
			Write(EventType.TRACE, message, args);
		}

		public void Debug(string message, params object[] args)
		{
			Write(EventType.DEBUG, message, args);
		}

		public void Info(string message, params object[] args)
		{
			Write(EventType.INFO, message, args);
		}

		public void Warn(string message, params object[] args)
		{
			Write(EventType.WARN, message, args);
		}

		public void Error(string message, params object[] args)
		{
			Write(EventType.ERROR, message, args);
		}

		public void Error(Exception exception)
		{
			Write(EventType.ERROR, exception.ToString());
		}

		public void Fatal(string message, params object[] args)
		{
			Write(EventType.FATAL, message, args);
		}

		protected abstract void Write(EventType eventType, string message, params object[] args);

		protected enum EventType
		{
			TRACE,
			DEBUG,
			INFO,
			WARN,
			ERROR,
			FATAL
		}
	}
}
