using System;
using System.Linq;

using Carambolas.Collections.Generic;

namespace Carambolas
{
    // Arithmentic mean (aka average) = (i0 + i1 +...+ in-1) / n
    // Geometric mean = Math.Pow(i0 * i1 *... in-1, 1.0/n)  
    // Harmonic mean = n / (1/i0 + 1/i1 + ... + 1/in-1)

    public abstract class SingleMovingMean
    {
        private Deque<float> samples;
        private float accumulator;

        public float Value { get; private set; }
        public int Capacity => samples.Capacity;
        public int Count => samples.Count;
        
        public SingleMovingMean(int capacity) => samples = new Deque<float>(capacity);
        
        protected abstract float Decrease(float current, float value);
        protected abstract float Increase(float current, float value);
        protected abstract float Mean(float current, int count);

        public void Add(float sample)
        {
            if (samples.Count >= samples.Capacity)
                accumulator = Decrease(accumulator, samples.PopFront());

            samples.PushBack(sample);
            accumulator = Increase(accumulator, sample);
            Value = Mean(accumulator, samples.Count());
        }

        public void Reset()
        {
            samples.Clear();
            accumulator = 0f;
            Value = 0f;
        }
    }

    public sealed class SingleMovingArithmeticMean: SingleMovingMean
    {
        public SingleMovingArithmeticMean(int capacity = 10) : base(capacity) { }

        protected override float Decrease(float current, float value) => current - value;

        protected override float Increase(float current, float value) => current + value;

        protected override float Mean(float current, int count) => current / count;
    }

    public sealed class SingleMovingGeometricMean: SingleMovingMean
    {
        public SingleMovingGeometricMean(int capacity = 10) : base(capacity) { }

        protected override float Decrease(float current, float value) => current / value;

        protected override float Increase(float current, float value) => current * value;

        protected override float Mean(float current, int count) => (float)Math.Pow(current, 1.0 / count);
    }

    public sealed class SingleMovingHarmonicMean: SingleMovingMean
    {
        public SingleMovingHarmonicMean(int capacity = 10) : base(capacity) { }

        protected override float Decrease(float current, float value) => current - (1.0f / value);

        protected override float Increase(float current, float value) => current + (1.0f / value);

        protected override float Mean(float current, int count) => count / current;
    }

    public abstract class DoubleMovingMean
    {
        private Deque<double> samples;
        private double accumulator;

        public double Value { get; private set; }
        public int Capacity => samples.Capacity;
        public int Count => samples.Count;

        public DoubleMovingMean(int capacity) => samples = new Deque<double>(capacity);

        protected abstract double Decrease(double current, double value);
        protected abstract double Increase(double current, double value);
        protected abstract double Mean(double current, int count);

        public void Add(double sample)
        {
            if (samples.Count >= samples.Capacity)
                accumulator = Decrease(accumulator, samples.PopFront());

            samples.PushBack(sample);
            accumulator = Increase(accumulator, sample);
            Value = Mean(accumulator, samples.Count());
        }

        public void Reset()
        {
            samples.Clear();
            accumulator = 0f;
            Value = 0f;
        }
    }

    public sealed class DoubleMovingArithmeticMean: DoubleMovingMean
    {
        public DoubleMovingArithmeticMean(int capacity = 10) : base(capacity) { }

        protected override double Decrease(double current, double value) => current - value;

        protected override double Increase(double current, double value) => current + value;

        protected override double Mean(double current, int count) => current / count;
    }

    public sealed class DoubleMovingGeometricMean: DoubleMovingMean
    {
        public DoubleMovingGeometricMean(int capacity = 10) : base(capacity) { }

        protected override double Decrease(double current, double value) => current / value;

        protected override double Increase(double current, double value) => current * value;

        protected override double Mean(double current, int count) => Math.Pow(current, 1.0 / count);
    }

    public sealed class DoubleMovingHarmonicMean: DoubleMovingMean
    {
        public DoubleMovingHarmonicMean(int capacity = 10) : base(capacity) { }

        protected override double Decrease(double current, double value) => current - (1.0 / value);

        protected override double Increase(double current, double value) => current + (1.0 / value);

        protected override double Mean(double current, int count) => count / current;
    }
}
