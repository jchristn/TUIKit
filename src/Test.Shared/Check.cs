namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Minimal assertion helpers for Touchstone descriptors. Each method throws on failure, which
    /// Touchstone records as a failing case. The helper never writes to the console.
    /// </summary>
    public static class Check
    {
        /// <summary>
        /// Asserts that a condition is true.
        /// </summary>
        /// <param name="condition">The condition that must hold.</param>
        /// <param name="message">The failure message when the condition is false. Must not be null.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="condition"/> is false.</exception>
        public static void True(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message ?? "Assertion failed.");
        }

        /// <summary>
        /// Asserts that a condition is false.
        /// </summary>
        /// <param name="condition">The condition that must not hold.</param>
        /// <param name="message">The failure message when the condition is true. Must not be null.</param>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="condition"/> is true.</exception>
        public static void False(bool condition, string message)
        {
            if (condition)
                throw new InvalidOperationException(message ?? "Assertion failed.");
        }

        /// <summary>
        /// Asserts that two values are equal using the default equality comparer.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="expected">The expected value.</param>
        /// <param name="actual">The actual value.</param>
        /// <param name="context">A short description of what was being checked. Must not be null.</param>
        /// <exception cref="InvalidOperationException">Thrown when the values differ.</exception>
        public static void Equal<T>(T expected, T actual, string context)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    (context ?? "value") + ": expected <" + expected + "> but got <" + actual + ">.");
        }

        /// <summary>
        /// Asserts that invoking an action throws an exception of the expected type.
        /// </summary>
        /// <typeparam name="TException">The expected exception type.</typeparam>
        /// <param name="action">The action expected to throw. Must not be null.</param>
        /// <param name="context">A short description of what was being checked. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the action does not throw, or throws an unexpected type.
        /// </exception>
        public static void Throws<TException>(Action action, string context)
            where TException : Exception
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    (context ?? "action") + ": expected " + typeof(TException).Name
                    + " but got " + ex.GetType().Name + ".");
            }

            throw new InvalidOperationException(
                (context ?? "action") + ": expected " + typeof(TException).Name + " but nothing was thrown.");
        }
    }
}
