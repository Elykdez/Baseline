using System;
using System.Collections.Generic;

namespace Hypocycloid.Editor
{
    /// <summary>
    /// Circular buffer for zero-allocation history tracking
    /// </summary>
    public class CircularBuffer<T>
    {
        readonly T[] buffer;
        int head;
        int count;

        public int Count => count;
        public int Capacity => buffer.Length;

        public CircularBuffer(int capacity)
        {
            buffer = new T[capacity];
            head = 0;
            count = 0;
        }

        public void Add(T item)
        {
            buffer[head] = item;
            head = (head + 1) % buffer.Length;
            if (count < buffer.Length)
                count++;
        }

        public void Clear()
        {
            Array.Clear(buffer, 0, buffer.Length);
            head = 0;
            count = 0;
        }

        /// <summary>
        /// Get recent items in reverse chronological order (most recent first)
        /// Zero allocation enumeration
        /// </summary>
        public IEnumerable<T> GetRecentItems(int maxCount)
        {
            var itemsToReturn = Math.Min(maxCount, count);
            for (int i = 0; i < itemsToReturn; i++)
            {
                var index = (head - 1 - i + buffer.Length) % buffer.Length;
                yield return buffer[index];
            }
        }
    }
}
