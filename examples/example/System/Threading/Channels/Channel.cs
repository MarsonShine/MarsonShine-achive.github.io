using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace example.System.Threading.Channels
{
    public sealed class Channel<T>
    {
        // 用来存储
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        // 用来协作消费者和生产者
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        public void Write(T value) {
            _queue.Enqueue(value);
            _semaphore.Release();   // 通知消费者有多的数据存进来
        }
        public async ValueTask<T> ReadAsync(CancellationToken cancellationToken = default) {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            bool gotOne = _queue.TryDequeue(out T item);    // 取出数据
            Debug.Assert(gotOne);
            return item;
        }
    }
}
