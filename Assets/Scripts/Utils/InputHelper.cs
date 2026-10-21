using System;
using System.Collections;
using UnityEngine;

namespace Hypocycloid.Utils
{
    public class InputHelper
    {

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