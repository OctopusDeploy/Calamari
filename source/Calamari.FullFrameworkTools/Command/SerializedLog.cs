using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using JsonConverter = Newtonsoft.Json.JsonConverter;

namespace Calamari.FullFrameworkTools.Command
{
    public class SerializedLog: ILog
    {
        static readonly JsonSerializerSettings Settings = new() { Converters = new List<JsonConverter>() { new Newtonsoft.Json.Converters.StringEnumConverter() } };

        enum LogLevel
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
            var line =  JsonConvert.SerializeObject(new { Level = LogLevel.Verbose.ToString(), Message = message }, Settings);
            Console.WriteLine(line);
        }

        public void Error(string message)
        {
            var line = JsonConvert.SerializeObject(new { Level = LogLevel.Error.ToString(), Message = message }, Settings);
            Console.WriteLine(line);
        }

        public void Fatal(Exception exception)
        {
            // For simplicity lets not assume Inner exceptions right now....
            var line =  JsonConvert.SerializeObject(new
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
            return  JsonConvert.SerializeObject(new { Level = level.ToString(), Message = message}, Settings);
        }
        
        public void Info(string message)
        {
            var line =  JsonConvert.SerializeObject(new { Level = LogLevel.Info, Message = message }, Settings);
            Console.WriteLine(line);
        }

        public void Result(object result)
        {
            var line =  JsonConvert.SerializeObject(new { Level = LogLevel.Result, Result = result}, Settings);
            Console.WriteLine(line);
        }
    }
}