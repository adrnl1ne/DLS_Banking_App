using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Security;

namespace TransactionService.Infrastructure.Logging
{
    public class SensitiveDataLogFilter : ILogger
    {
        private readonly ILogger _innerLogger;
        
        public SensitiveDataLogFilter(ILogger innerLogger)
        {
            _innerLogger = innerLogger;
        }
        
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _innerLogger.BeginScope(state);
        }
        
        public bool IsEnabled(LogLevel logLevel)
        {
            return _innerLogger.IsEnabled(logLevel);
        }
        
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            
            // Sanitize the message
            string sanitizedMessage = LogSanitizer.SanitizeLogMessage(message);
            
            // Pass the sanitized message to the inner logger
            _innerLogger.Log(logLevel, eventId, state, exception, (s, ex) => sanitizedMessage);
        }
    }
    
    public class SensitiveDataLoggerProvider : ILoggerProvider
    {
        private readonly ILoggerProvider _innerProvider;
        private readonly Dictionary<string, SensitiveDataLogFilter> _loggers = new();
        
        public SensitiveDataLoggerProvider(ILoggerProvider innerProvider)
        {
            _innerProvider = innerProvider;
        }
        
        public ILogger CreateLogger(string categoryName)
        {
            if (!_loggers.TryGetValue(categoryName, out var logger))
            {
                var innerLogger = _innerProvider.CreateLogger(categoryName);
                logger = new SensitiveDataLogFilter(innerLogger);
                _loggers[categoryName] = logger;
            }
            
            return logger;
        }
        
        public void Dispose()
        {
            _innerProvider.Dispose();
            _loggers.Clear();
        }
    }

    // Extension method for registering the filter
    public static class SensitiveDataLoggerExtensions
    {
        public static ILoggingBuilder AddSensitiveDataFilter(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<ILoggerProvider, SensitiveDataLoggerProvider>(services => 
            {
                var existingProviders = services.GetServices<ILoggerProvider>();
                foreach (var provider in existingProviders)
                {
                    return new SensitiveDataLoggerProvider(provider);
                }
                throw new InvalidOperationException("No logger provider found to wrap");
            });
            
            return builder;
        }
    }
}