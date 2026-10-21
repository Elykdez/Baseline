using System;
using System.Collections;
using UnityEngine;

// using Object = UnityEngine.Object;

namespace Hypocycloid.Utils
{
    public static class CoroutineHelper
    {
        public static WaitForSeconds wait1s = new(1f);
        public static WaitForSeconds wait2s = new(2f);
        public static WaitForSeconds wait3s = new(3f);
        public static WaitForSeconds wait4s = new(4f);
        public static WaitForSeconds wait5s = new(5f);

        public static IEnumerator WaitForEndFrame(Action cb)
        {
            yield return new WaitForEndOfFrame();
            cb?.Invoke();
        }

        public class Debounce
        {
            Action callback = null;

            Coroutine corountine = null;

            public void Run(Action callback, float interval, MonoBehaviour mono)
            {
                this.callback = callback;

                ResetTime(mono);

                corountine = mono.StartCoroutine(DebounceCorountine(interval));
            }

            public void ResetTime(MonoBehaviour mono)
            {
                if (corountine != null)
                {
                    mono.StopCoroutine(corountine);
                }
            }

            private IEnumerator DebounceCorountine(float time)
            {
                yield return new WaitForSeconds(time);
                callback?.Invoke();
            }
        }

        public class Throttle
        {
            float targetTime = 0;

            public bool IsThrottling() => targetTime > 0;

            public void Run(Action callback, float interval)
            {
                if (targetTime <= Time.time)
                {
                    targetTime = Time.time + interval;
                    callback?.Invoke();
                }
            }

            public void ResetTimer()
            {
                targetTime = 0;
            }
        }
    }
}