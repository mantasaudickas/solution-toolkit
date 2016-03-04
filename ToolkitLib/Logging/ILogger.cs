using System;

namespace SolutionGenerator.Toolkit.Logging
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
        void Fatal(Exception exception);
    }
}