using System;
using System.Collections.Generic;

namespace Carambolas
{
    public class MovingAverage
    {
        // TODO: replace this with a circular buffer
        private Queue<float> samples;
        private int capacity;

        MovingAverage(float value = 0f, int capacity = 10)
        {
            this.samples = new Queue<float>(capacity);
            this.capacity = capacity;

            CurrentValue = value;
            Sum = value;
            Average = value;
        }

        /// <summary>
        /// Determines the number of samples to keep
        /// </summary>
        public int Capacity
        {
            get => capacity;

            set
            {
                if (value > 1 && value != capacity)
                {
                    capacity = value;
                    while (samples.Count >= capacity)
                        Sum -= samples.Dequeue();
                    if (samples.Count == capacity)
                        samples.TrimExcess();
                    Average = Sum / Count;
                }
            }
        }

        /// <summary>
        /// Number of samples collected yet
        /// </summary>
        public int Count => samples.Count;

        public float CurrentValue { get; private set; }

        /// <summary>
        /// Current sum of the sample set
        /// </summary>
        public float Sum { get; private set; }

        /// <summary>
        /// Current average of the sample set
        /// </summary>
        public float Average { get; private set; }

        /// <summary>
        /// Clear the sample set and add a new initial value.
        /// </summary>
        /// <param name="value"></param>
        public void Reset(float value = 0f)
        {
            samples.Clear();
            samples.TrimExcess();
            CurrentValue = value;
            Sum = value;
            Average = value;
        }

        /// <summary>
        /// Adds a value to the sequence 
        /// </summary>
        /// <param name="value"></param>
        public void Add(float value)
        {
            if (Count >= capacity)
                Sum -= samples.Dequeue();

            samples.Enqueue(CurrentValue);
            CurrentValue = value;
            Sum += value;
            Average = Sum / Count;
        }

        /// <summary>
        /// Replaces the value that was last added with this new value.
        /// </summary>
        /// <param name="value"></param>
        public void Replace(float value)
        {
            Sum -= CurrentValue;
            CurrentValue = value;
            Sum += CurrentValue;
        }

        #region Operators 

        /// <summary>
        /// The implicit operator lets us declare a specific conversion 
        /// from the parameter to our type. We'll use this to initialize
        /// the struct
        /// </summary>
        /// <param name="value">Value to initialize to</param>
        /// <returns>New instance holding the parameter value</returns>
        public static implicit operator MovingAverage(float value) => new MovingAverage(value);

        #endregion
    }
}
