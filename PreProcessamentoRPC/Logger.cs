using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace PreProcessamentoRPC
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public class Logger
    {
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly string _logFilePath;
        private readonly object _consoleLock = new object();
        private bool _isProcessingQueue;

        public static Logger Instance => _instance.Value;

        private Logger()
        {
            _logQueue = new ConcurrentQueue<LogEntry>();
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "preprocessamento.log");
            _isProcessingQueue = false;

            // Criar diretório de logs se não existir
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath));

            // Iniciar processamento de logs
            StartProcessingQueue();
        }

        public void Log(LogLevel level, string message, Exception ex = null)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Exception = ex
            };

            _logQueue.Enqueue(entry);

            // Log imediato no console para níveis críticos
            if (level >= LogLevel.Error)
            {
                WriteToConsole(entry);
            }

            if (!_isProcessingQueue)
            {
                StartProcessingQueue();
            }
        }

        private void StartProcessingQueue()
        {
            if (_isProcessingQueue) return;

            _isProcessingQueue = true;
            Task.Run(async () =>
            {
                while (_logQueue.TryDequeue(out LogEntry entry))
                {
                    await WriteToFileAsync(entry);
                    
                    if (entry.Level < LogLevel.Error)
                    {
                        WriteToConsole(entry);
                    }
                }
                _isProcessingQueue = false;
            });
        }

        private async Task WriteToFileAsync(LogEntry entry)
        {
            var logLine = FormatLogEntry(entry);
            try
            {
                await File.AppendAllTextAsync(_logFilePath, logLine + Environment.NewLine);
            }
            catch (Exception ex)
            {
                WriteToConsole(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = LogLevel.Critical,
                    Message = $"Falha ao escrever no arquivo de log: {ex.Message}"
                });
            }
        }

        private void WriteToConsole(LogEntry entry)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = GetColorForLogLevel(entry.Level);
                Console.WriteLine(FormatLogEntry(entry));
                Console.ResetColor();
            }
        }

        private string FormatLogEntry(LogEntry entry)
        {
            var baseLog = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level}] {entry.Message}";
            if (entry.Exception != null)
            {
                baseLog += $"\nException: {entry.Exception.Message}\nStackTrace: {entry.Exception.StackTrace}";
            }
            return baseLog;
        }

        private ConsoleColor GetColorForLogLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };
        }

        public void Flush()
        {
            while (_logQueue.TryDequeue(out LogEntry entry))
            {
                WriteToFileAsync(entry).Wait();
            }
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }

    // Extensões para facilitar o uso do logger
    public static class LoggerExtensions
    {
        public static void Debug(this Logger logger, string message) =>
            logger.Log(LogLevel.Debug, message);

        public static void Info(this Logger logger, string message) =>
            logger.Log(LogLevel.Info, message);

        public static void Warning(this Logger logger, string message) =>
            logger.Log(LogLevel.Warning, message);

        public static void Error(this Logger logger, string message, Exception ex = null) =>
            logger.Log(LogLevel.Error, message, ex);

        public static void Critical(this Logger logger, string message, Exception ex = null) =>
            logger.Log(LogLevel.Critical, message, ex);
    }
} 