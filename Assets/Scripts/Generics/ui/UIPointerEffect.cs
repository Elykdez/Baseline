using Hypocycloid.Utils;
using UnityEngine;

namespace Baseline
{
    public class UIPointerEffect : AbstractInputSystem
    {
        public GameObject particlePrefab;

        ParticleSystem particle;

        void Start()
        {
            if (particlePrefab != null)
            {
                particle = Instantiate(particlePrefab, transform)
                    .GetComponentInChildren<ParticleSystem>();
            }
        }

        void Update()
        {
            if (particle == null)
            {
                return;
            }

            if (GetMouseButtonDown(0))
            {
                EnableParticleEffect(GetMousePosition());
            }
            else if (GetMouseButton(0))
            {
                MoveParticleEffect(GetMousePosition());
            }
            else if (GetMouseButtonUp(0))
            {
                particle.Stop();
            }
        }

        void EnableParticleEffect(Vector2 screenPosition)
        {
            particle.Play();
            MoveParticleEffect(screenPosition);
        }

        void MoveParticleEffect(Vector2 screenPosition)
        {
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            particle.transform.position = Physics.Raycast(ray, out RaycastHit hit)
                ? hit.point
                : ray.origin + ray.direction;
        }
    }
}
