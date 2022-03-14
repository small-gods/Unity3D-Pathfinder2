using System.Collections.Generic;
using System.Linq;

namespace AI
{
    public class SlowPriorityQueue<T>
    {
        private readonly SortedDictionary<float, List<T>> _dict = new SortedDictionary<float, List<T>>();

        public int Count { get; private set; } = 0;

        public SlowPriorityQueue()
        {
        }

        public void Enqueue(float priority, T elem)
        {
            if (!_dict.ContainsKey(priority))
                _dict[priority] = new List<T>();
            _dict[priority].Add(elem);
            Count += 1;
        }

        public (float priority, T value) Dequeue()
        {
            Count -= 1;
            var min = _dict.Keys.First();
            var list = _dict[min];
            if (list.Count == 1)
            {
                _dict.Remove(min);
                return (min, list[0]);
            }

            var val = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return (min, val);
        }
    }
}