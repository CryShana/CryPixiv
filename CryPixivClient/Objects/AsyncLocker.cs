using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryPixivClient.Objects
{
    public class AsyncLocker
    {
        public SemaphoreSlim Semaphore { get; }

        /// <summary>
        /// Initializes a new AsyncLocker that uses a Semaphore to limit access to specific resources.
        /// </summary>
        /// <param name="initialCount">Initial number of requests that can be granted concurrently</param>
        /// <param name="maxCount">Maximum number of such requests</param>
        public AsyncLocker(int initialCount = 1, int maxCount = 1)
        {
            Semaphore = new SemaphoreSlim(initialCount, maxCount);
        }

        /// <summary>
        /// Blocks the thread asynchronously until Semaphore is released/available
        /// </summary>
        /// <returns>IDisposable Lock</returns>
        public async Task<Lock> LockAsync()
        {
            await Semaphore.WaitAsync();
            return new Lock(Semaphore);
        }

        /// <summary>
        /// Blocks the thread synchronously until Semaphore is released/available
        /// </summary>
        /// <returns>IDisposable Lock</returns>
        public Lock NormalLock()
        {
            Semaphore.Wait();
            return new Lock(Semaphore);
        }

        public class Lock : IDisposable
        {
            public void Dispose() => Release();
            public void Release() => sem.Release();

            SemaphoreSlim sem = null;
            public Lock(SemaphoreSlim semaphore)
            {
                sem = semaphore;
            }
        }
    }
}
