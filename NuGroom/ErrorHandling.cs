using System.Diagnostics;

namespace NuGroom
{
#pragma warning disable ConstructorDocumentationHeader // The constructor must have a documentation header.
#pragma warning disable ClassDocumentationHeader // The class must have a documentation header.
	/// <summary>
	/// Custom exceptions for the application
	/// </summary>
	public class AzureDevOpsException : Exception
	{
		public AzureDevOpsException(string message) : base(message) { }
		public AzureDevOpsException(string message, Exception innerException) : base(message, innerException) { }
	}

	public class ConfigurationException : Exception
	{
		public ConfigurationException(string message) : base(message) { }
		public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
	}

	public class PackageExtractionException : Exception
	{
		public PackageExtractionException(string message) : base(message) { }
		public PackageExtractionException(string message, Exception innerException) : base(message, innerException) { }
	}
#pragma warning restore ClassDocumentationHeader // The class must have a documentation header.
#pragma warning restore ConstructorDocumentationHeader // The constructor must have a documentation header.

	/// <summary>
	/// Simple logger for the application
	/// </summary>
	public static class Logger
	{
		/// <summary>
		/// The log level.
		/// </summary>
		public enum LogLevel
		{
			Info,
			Warning,
			Error,
			Debug
		}

		/// <summary>
		/// Gets or sets a value indicating whether debug logging is enabled.
		/// </summary>
		/// <value>
		/// <c>true</c> if debug logging is enabled; otherwise, <c>false</c>. 
		/// Defaults to <c>true</c> when a debugger is attached.
		/// </value>
		public static bool EnableDebugLogging { get; set; } = Debugger.IsAttached;

		/// <summary>
		/// Logs a message with the specified log level to the console.
		/// </summary>
		/// <param name="level">The severity level of the log message.</param>
		/// <param name="message">The message to log.</param>
		/// <remarks>
		/// Debug messages are only logged when <see cref="EnableDebugLogging"/> is <c>true</c>.
		/// The message is formatted with a timestamp and colored based on the log level.
		/// </remarks>
		public static void Log(LogLevel level, string message)
		{
			if (level == LogLevel.Debug && !EnableDebugLogging)
				return;

			var color = level switch
			{
				LogLevel.Warning => ConsoleColor.Yellow,
				LogLevel.Error => ConsoleColor.Red,
				LogLevel.Debug => ConsoleColor.Gray,
				_ => ConsoleColor.White
			};

			var levelStr = level switch
			{
				LogLevel.Info => "INFO",
				LogLevel.Warning => "WARN",
				LogLevel.Error => "ERROR",
				LogLevel.Debug => "DEBUG",
				_ => "INFO"
			};

			var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
			ConsoleWriter.Out.Color(color).WriteLine($"[{timestamp}] [{levelStr}] {message}").ResetColor();
		}

		/// <summary>
		/// Logs an informational message to the console.
		/// </summary>
		/// <param name="message">The informational message to log.</param>
		public static void Info(string message) => Log(LogLevel.Info, message);

		/// <summary>
		/// Logs a warning message to the console.
		/// </summary>
		/// <param name="message">The warning message to log.</param>
		public static void Warning(string message) => Log(LogLevel.Warning, message);

		/// <summary>
		/// Logs an error message to the console.
		/// </summary>
		/// <param name="message">The error message to log.</param>
		public static void Error(string message) => Log(LogLevel.Error, message);

		/// <summary>
		/// Logs a debug message to the console.
		/// </summary>
		/// <param name="message">The debug message to log.</param>
		/// <remarks>
		/// Debug messages are only logged when <see cref="EnableDebugLogging"/> is <c>true</c>.
		/// </remarks>
		public static void Debug(string message) => Log(LogLevel.Debug, message);
	}
}