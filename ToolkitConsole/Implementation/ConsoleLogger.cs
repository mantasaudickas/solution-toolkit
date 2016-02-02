using System;
using SolutionGenerator.Toolkit;

namespace SolutionToolkit.Implementation
{
    internal class ConsoleLogger : ILogger
    {
        public void Trace(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public void Debug(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public void Info(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public void Warn(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public void Error(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public void Error(Exception exception)
        {
            Console.WriteLine(exception);
        }

        public void Fatal(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }
    }
}