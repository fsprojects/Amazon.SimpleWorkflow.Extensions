using System;
using Microsoft.FSharp.Core;

namespace Amazon.SimpleWorkflow.Extensions
{
    /// <summary>
    /// Extension methods for working with F#'s option type
    /// </summary>
    internal static class FSharpOptionExtensions
    {
        /// <summary>
        /// Converts a C# Nullable type to a F# option type
        /// </summary>
        public static FSharpOption<T> AsOption<T>(this T? value) where T : struct
        {
            return value.HasValue ? FSharpOption<T>.Some(value.Value) : FSharpOption<T>.None;
        }

        /// <summary>
        /// Converts a value of type T to a F# option type of T, if the value is equaled to the default value of
        /// its type then it's represented as 'None'.
        /// Otherwise, it's represented as Some(value).
        /// </summary>
        public static FSharpOption<T> AsOption<T>(this T value)
        {
            return value.AsOption(x => x.Equals(default(T)));
        }

        /// <summary>
        /// Converts a value of type T to a F# option type of T, if the 'isDefault' predicate returns true then 
        /// it's represented as 'None'.
        /// Otherwise, it's represented as Some(value).
        /// </summary>
        public static FSharpOption<T> AsOption<T>(this T value, Predicate<T> isDefault)
        {
            return isDefault(value) ? FSharpOption<T>.None : FSharpOption<T>.Some(value);
        }
    }
}