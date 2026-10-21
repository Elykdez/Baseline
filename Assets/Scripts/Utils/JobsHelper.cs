using System;
using System.Runtime.CompilerServices;
using Unity.Jobs;

namespace Hypocycloid.Utils
{
    public static class JobsHelper
    {
        public readonly struct JobHandleAwaitable : INotifyCompletion
        {
            private readonly JobHandle handle;

            public JobHandleAwaitable(JobHandle handle)
            {
                this.handle = handle;
            }

            public JobHandleAwaitable GetAwaiter() => this;

            public bool IsCompleted => handle.IsCompleted;

            public void OnCompleted(Action continuation)
            {
                handle.Complete();
                continuation?.Invoke();
            }

            public void GetResult() => handle.Complete();
        }
    }
}