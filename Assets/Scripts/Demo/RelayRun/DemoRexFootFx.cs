using UnityEngine;
using UnityEngine.UI;

namespace Baseline.Demo.RelayRun
{
    /// <summary>Reuses authored canvas sprites; advances only with the runner simulation.</summary>
    public sealed class DemoRexFootFx : MonoBehaviour
    {
        public Image[] puffs;

        struct Puff
        {
            public float age, lifetime, width, drift;
        }

        Puff[] state;
        int next;

        void Awake() => Clear();

        public void Clear()
        {
            state = new Puff[puffs.Length];
            next = 0;
            foreach (Image puff in puffs) puff.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            // Settings may change while gameplay (and therefore Advance) is paused.
            if (!ConfigSettingsPersistence.BoardEffects)
                foreach (Image puff in puffs) puff.gameObject.SetActive(false);
        }

        public void Footstep(Vector3 worldPosition)
        {
            Vector2 position = transform.InverseTransformPoint(worldPosition);
            position.y = DemoRunnerModel.GroundY;
            Emit(position, 36, .38f, -30);
        }

        public void Jump() => Burst(false);
        public void Land() => Burst(true);

        void Burst(bool landing)
        {
            Vector2 origin = new Vector2(DemoRunnerModel.RunnerBodyX, DemoRunnerModel.GroundY);
            int count = landing ? 6 : 3;
            for (int i = 0; i < count; i++)
            {
                float spread = i - (count - 1) * .5f;
                Emit(origin + Vector2.right * spread * 13, landing ? 60 : 44,
                    landing ? .6f : .45f, spread * (landing ? 68 : 48));
            }
        }

        void Emit(Vector2 position, float width, float lifetime, float drift)
        {
            if (!ConfigSettingsPersistence.BoardEffects) return;
            int index = next;
            next = (next + 1) % puffs.Length;
            state[index] = new Puff { lifetime = lifetime, width = width, drift = drift };
            Image image = puffs[index];
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = new Vector2(width, width * .55f);
            image.color = new Color(1, 1, 1, .75f);
            image.gameObject.SetActive(true);
        }

        public void Advance(float seconds, float travelled)
        {
            for (int i = 0; i < puffs.Length; i++)
            {
                Image image = puffs[i];
                if (!image.gameObject.activeSelf) continue;
                ref Puff puff = ref state[i];
                puff.age += seconds;
                float t = puff.age / puff.lifetime;
                if (t >= 1 || !ConfigSettingsPersistence.BoardEffects)
                {
                    image.gameObject.SetActive(false);
                    continue;
                }
                image.rectTransform.anchoredPosition += new Vector2(puff.drift * seconds - travelled, 18 * seconds);
                float width = puff.width * (1 + .8f * t);
                image.rectTransform.sizeDelta = new Vector2(width, width * .55f);
                image.color = new Color(1, 1, 1, .75f * (1 - t) * (1 - t));
            }
        }
    }
}
