using System;

namespace D3dxSkinManager.Modules.Core.Utilities;

/// <summary>
/// Helper for common validation operations
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Validates that a string is not null or empty, throwing ArgumentException if invalid
    /// </summary>
    /// <param name="value">The string value to validate</param>
    /// <param name="paramName">The parameter name for the exception message</param>
    /// <exception cref="ArgumentException">Thrown when value is null or empty</exception>
    public static void RequireNotEmpty(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} cannot be null or empty", paramName);
        }
    }

    /// <summary>
    /// Returns the value if not null/empty, otherwise returns the default value
    /// </summary>
    /// <param name="value">The string value to check</param>
    /// <param name="defaultValue">The default value to return if value is null/empty</param>
    /// <returns>The value or default value</returns>
    public static string WithDefault(string? value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    /// <summary>
    /// Validates that a value is not null, throwing ArgumentNullException if invalid
    /// </summary>
    /// <typeparam name="T">The type of the value</typeparam>
    /// <param name="value">The value to validate</param>
    /// <param name="paramName">The parameter name for the exception message</param>
    /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
    public static void RequireNotNull<T>(T? value, string paramName) where T : class
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName, $"{paramName} cannot be null");
        }
    }

    /// <summary>
    /// Validates that a string matches a specific pattern or condition
    /// </summary>
    /// <param name="value">The string value to validate</param>
    /// <param name="condition">The condition that must be true</param>
    /// <param name="paramName">The parameter name for the exception message</param>
    /// <param name="errorMessage">Custom error message</param>
    /// <exception cref="ArgumentException">Thrown when condition is false</exception>
    public static void Require(string? value, Func<string?, bool> condition, string paramName, string errorMessage)
    {
        if (!condition(value))
        {
            throw new ArgumentException(errorMessage, paramName);
        }
    }
}
