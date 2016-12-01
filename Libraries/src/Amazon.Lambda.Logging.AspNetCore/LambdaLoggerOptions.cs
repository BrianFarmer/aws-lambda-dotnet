﻿using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Options that can be used to configure Lambda logging.
    /// </summary>
    public class LambdaLoggerOptions
    {
        // Default configuration section.
        // Customer configuration will be fetched from this section, unless otherwise specified.
        internal const string DEFAULT_SECTION_NAME = "Lambda.Logging";

        private const string INCLUDE_LOG_LEVEL_KEY = "IncludeLogLevel";
        private const string INCLUDE_CATEGORY_KEY = "IncludeCategory";
        private const string INCLUDE_NEWLINE_KEY = "IncludeNewline";
        private const string LOG_LEVEL_KEY = "LogLevel";

        /// <summary>
        /// Flag to indicate if LogLevel should be part of logged message.
        /// Default is true.
        /// </summary>
        public bool IncludeLogLevel { get; set; }

        /// <summary>
        /// Flag to indicate if Category should be part of logged message.
        /// Default is true.
        /// </summary>
        public bool IncludeCategory { get; set; }

        /// <summary>
        /// Flag to indicate if logged messages should have a newline appended
        /// to them, if one isn't already there.
        /// Default is true.
        /// </summary>
        public bool IncludeNewline { get; set; }

        /// <summary>
        /// Function used to filter events based on the log level.
        /// Default value is null and will instruct logger to log everything.
        /// </summary>
        [CLSCompliant(false)]  // https://github.com/aspnet/Logging/issues/500
        public Func<string, LogLevel, bool> Filter { get; set; }


        /// <summary>
        /// Constructs instance of LambdaLoggerOptions with default values.
        /// </summary>
        public LambdaLoggerOptions()
        {
            IncludeCategory = true;
            IncludeLogLevel = true;
            IncludeNewline = true;
            Filter = null;
        }

        /// <summary>
        /// Constructs instance of LambdaLoggerOptions with values from "Lambda.Logging"
        /// subsection of the specified configuration.
        /// The following configuration keys are supported:
        ///  IncludeLogLevel - boolean flag indicates if LogLevel should be part of logged message.
        ///  IncludeCategory - boolean flag indicates if Category should be part of logged message.
        ///  LogLevels - category-to-LogLevel mapping which indicates minimum LogLevel for a category.
        /// </summary>
        /// <param name="configuration"></param>
        [CLSCompliant(false)] // https://github.com/aspnet/Logging/issues/500
        public LambdaLoggerOptions(IConfiguration configuration)
            : this(configuration, DEFAULT_SECTION_NAME)
        { }

        /// <summary>
        /// Constructs instance of LambdaLoggerOptions with values from specified
        /// subsection of the configuration.
        /// The following configuration keys are supported:
        ///  IncludeLogLevel - boolean flag indicates if LogLevel should be part of logged message.
        ///  IncludeCategory - boolean flag indicates if Category should be part of logged message.
        ///  LogLevels - category-to-LogLevel mapping which indicates minimum LogLevel for a category.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="loggingSectionName"></param>
        [CLSCompliant(false)] // https://github.com/aspnet/Logging/issues/500
        public LambdaLoggerOptions(IConfiguration configuration, string loggingSectionName)
            : this()
        {
            // Input validation
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            if (string.IsNullOrEmpty(loggingSectionName))
            {
                throw new ArgumentNullException(nameof(loggingSectionName));
            }
            var loggerConfiguration = configuration.GetSection(loggingSectionName);
            if (loggerConfiguration == null)
            {
                throw new ArgumentOutOfRangeException(nameof(loggingSectionName), $"Unable to find section '{loggingSectionName}' in current configuration.");
            }

            // Parse settings

            string includeCategoryString;
            if (TryGetString(loggerConfiguration, INCLUDE_CATEGORY_KEY, out includeCategoryString))
            {
                IncludeCategory = bool.Parse(includeCategoryString);
            }

            string includeLogLevelString;
            if (TryGetString(loggerConfiguration, INCLUDE_LOG_LEVEL_KEY, out includeLogLevelString))
            {
                IncludeLogLevel = bool.Parse(includeLogLevelString);
            }

            string includeNewlineString;
            if (TryGetString(loggerConfiguration, INCLUDE_NEWLINE_KEY, out includeNewlineString))
            {
                IncludeNewline = bool.Parse(includeNewlineString);
            }

            IConfiguration logLevelsSection;
            if (TryGetSection(loggerConfiguration, LOG_LEVEL_KEY, out logLevelsSection))
            {
                Filter = CreateFilter(logLevelsSection);
            }
        }

        // Retrieves configuration value for key, if one exists
        private static bool TryGetString(IConfiguration configuration, string key, out string value)
        {
            value = configuration[key];
            return (value != null);
        }
        // Retrieves configuration section for key, if one exists
        private static bool TryGetSection(IConfiguration configuration, string key, out IConfiguration value)
        {
            value = configuration.GetSection(key);
            return (value != null);
        }
        // Creates filter for log levels section
        private static Func<string, LogLevel, bool> CreateFilter(IConfiguration logLevelsSection)
        {
            // Empty section means log everything
            var logLevels = logLevelsSection.GetChildren().ToList();
            if (logLevels.Count == 0)
            {
                return null;
            }

            // Populate mapping of category to LogLevel
            var logLevelsMapping = new Dictionary<string, LogLevel>(StringComparer.Ordinal);
            foreach(var logLevel in logLevels)
            {
                var category = logLevel.Key;
                var minLevelValue = logLevel.Value;
                LogLevel minLevel;
                if (!Enum.TryParse(minLevelValue, out minLevel))
                {
                    throw new InvalidCastException($"Unable to convert level '{minLevelValue}' for category '{category}' to LogLevel.");
                }

                logLevelsMapping[category] = minLevel;
            }

            // Filter lambda that examines mapping
            return (string category, LogLevel logLevel) =>
            {
                LogLevel minLevel;
                if (logLevelsMapping.TryGetValue(category, out minLevel))
                {
                    return (logLevel >= minLevel);
                }
                else
                {
                    return true;
                }
            };
        }
    }
}
