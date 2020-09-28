using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SIBR.Storage.Data.Utils
{
    public static class EnumerableExtensions
    {
        public static async IAsyncEnumerable<ConsecutiveGroupSync<TKey, TElement>> GroupByConsecutive<TKey, TElement>(this IAsyncEnumerable<TElement> input, Func<TElement, TKey> keyExtractor)
            where TKey: IEquatable<TKey>
        {
            var buffer = new List<TElement>();

            TKey lastKey = default;
            await foreach (var element in input)
            {
                var elementKey = keyExtractor(element);
                if (buffer.Count > 0 && !elementKey.Equals(lastKey))
                {
                    yield return new ConsecutiveGroupSync<TKey, TElement>(lastKey, buffer);
                    buffer = new List<TElement>();
                }
                
                buffer.Add(element);
                lastKey = elementKey;
            }
            
            if (buffer.Count > 0)
                yield return new ConsecutiveGroupSync<TKey, TElement>(lastKey, buffer);
        }

        private class PushEnumerable<T> : IAsyncEnumerable<T>
        {
            private Queue<T> _values = new Queue<T>();
            private TaskCompletionSource<bool> _nextItem = new TaskCompletionSource<bool>();
            
            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
            {
                async IAsyncEnumerable<T> Inner()
                {
                    while (_values.Count > 0)
                        yield return _values.Dequeue();

                    while (await _nextItem.Task)
                        while (_values.Count > 0)
                            yield return _values.Dequeue();
                }

                return Inner().GetAsyncEnumerator(cancellationToken);
            }

            public void Push(T val)
            {
                var old = _nextItem;
                _nextItem = new TaskCompletionSource<bool>();
                _values.Enqueue(val);
                old.SetResult(true);
            }

            public void Close()
            {
                _nextItem.SetResult(false);
            }
        }

        private class ConsecutiveGroup<TKey, TElement> : IAsyncGrouping<TKey, TElement>
        {
            private readonly TKey _key;
            private readonly IAsyncEnumerable<TElement> _inner;

            public ConsecutiveGroup(TKey key, IAsyncEnumerable<TElement> inner)
            {
                _key = key;
                _inner = inner;
            }

            public IAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
            {
                return _inner.GetAsyncEnumerator(cancellationToken);
            }

            public TKey Key => _key;
        }

        public class ConsecutiveGroupSync<TKey, TElement> : IGrouping<TKey, TElement>
        {
            private readonly TKey _key;
            public List<TElement> Values { get; }

            public ConsecutiveGroupSync(TKey key, List<TElement> values)
            {
                _key = key;
                Values = values;
            }
            
            public TKey Key => _key;
            
            public IEnumerator<TElement> GetEnumerator()
            {
                return Values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}