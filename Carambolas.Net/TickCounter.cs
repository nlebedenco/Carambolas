using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Carambolas.Net
{
    internal readonly struct TickCounter
    {
        private static readonly double TicksToSecondsFactor = Stopwatch.IsHighResolution ? 1.0 / Stopwatch.Frequency : 0.0000001;
        private static readonly double TicksToMillisecondsFactor = Stopwatch.IsHighResolution ? 1000.0 / Stopwatch.Frequency : 0.0001;

        /// <summary>
        /// Gets the current number of ticks from the underlying timer mechanism.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetTicks() => Stopwatch.GetTimestamp();

        /// <summary>
        /// Converts ticks to seconds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double TicksToSeconds(long ticks) => ticks * TicksToSecondsFactor;

        /// <summary>
        /// Converts ticks to millisseconds.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double TicksToMilliseconds(long ticks) => ticks * TicksToMillisecondsFactor;

        public TickCounter(long start = 0) => this.start = start;

        private readonly long start;

        /// <summary>
        /// Number of ticks elapsed since that creation of the counter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ElapsedTicks() => Stopwatch.GetTimestamp() - start;

        /// <summary>
        /// Number of milliseconds elapsed since that creation of the counter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ElapsedMilliseconds() => (long)TicksToMilliseconds(ElapsedTicks());
    }
}
