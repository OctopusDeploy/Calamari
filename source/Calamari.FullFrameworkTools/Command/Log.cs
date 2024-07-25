using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.FullFrameworkTools.Command
{
    public class Log: ILog
    {
        private enum LogLevel
        {
            Verbose,
            Info,
            Warn,
            Error,
            Fatal, // Used for Exceptions
            Result, // Special Response
        }

        public void Verbose(string message)
        {
            var line = JsonSerializer.Serialize(new { Level = LogLevel.Verbose.ToString(), Message = message });
            Console.WriteLine(line);
        }

        public void Error(string message)
        {
            var line = JsonSerializer.Serialize(new { Level = LogLevel.Error.ToString(), Message = message });
            Console.WriteLine(line);
        }

        public void Fatal(Exception exception)
        {
            // For simplicity lets not assume Inner exceptions right now....
            var line = JsonSerializer.Serialize(new
            {
                Level = LogLevel.Fatal.ToString(),
                Message = exception.Message,
                Type = exception.GetType().Name,
                StackTrace = exception.StackTrace
            });
            Console.WriteLine(line);
        }

        string Serialize(LogLevel level, string message)
        {
            return JsonSerializer.Serialize(new { Level = level.ToString(), Message = message});
        }
        
        public void Info(string message)
        {
            var line = JsonSerializer.Serialize(new { Level = LogLevel.Info, Message = message });
            Console.WriteLine(line);
        }

        public void Result(object result)
        {
            var line = JsonSerializer.Serialize(new { Level = LogLevel.Result, Result = result});
            Console.WriteLine(line);
        }
    }
}