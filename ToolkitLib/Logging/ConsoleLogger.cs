using System;

namespace SolutionGenerator.Toolkit.Logging
{
    public class ConsoleLogger : ILogger
    {
        public static int VerbosityLevel = 3; // 6 - Debug, 5 - Trace, 4 - Info, 3 - Warn, 2 - Error, 1 - Fatal

        public static ILogger Default => new ConsoleLogger();

        public void Debug(string message, params object[] args)
        {
            if (VerbosityLevel >= 6)
                Console.WriteLine(message, args);
        }

        public void Trace(string message, params object[] args)
        {
            if (VerbosityLevel >= 5)
                Console.WriteLine(message, args);
        }

        public void Info(string message, params object[] args)
        {
            if (VerbosityLevel >= 4)
                Console.WriteLine(message, args);
        }

        public void Warn(string message, params object[] args)
        {
            if (VerbosityLevel >= 3)
                Console.WriteLine(message, args);
        }

        public void Error(string message, params object[] args)
        {
            if (VerbosityLevel >= 2)
                Console.WriteLine(message, args);
        }

        public void Error(Exception exception)
        {
            if (VerbosityLevel >= 2)
                Console.WriteLine(exception);
        }

        public void Fatal(string message, params object[] args)
        {
            if (VerbosityLevel >= 1)
                Console.WriteLine(message, args);
        }

        public void Fatal(Exception exception)
        {
            if (VerbosityLevel >= 1)
                Console.WriteLine(exception.ToString());
        }
    }
}