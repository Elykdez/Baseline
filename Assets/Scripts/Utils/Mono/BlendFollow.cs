using System;
using UnityEngine;

namespace Hypocycloid.Utils
{
    public class BlendFollower : MonoBehaviour
    {
        const int REFERENCE_FRAMERATE = 60;

        public Transform leader;
        public float followSharpness = 0.1f;
        public bool isSmoothUpdate;

        Vector3 _followOffset;

        void Start()
        {
            // Cache the initial offset at time of load/spawn:
            _followOffset = transform.position - leader.position;
        }

        void LateUpdate()
        {
            DoFollow();
        }

        public void DoFollow()
        {
            // Apply that offset to get a target position.
            Vector3 targetPosition = leader.position + _followOffset;

            // Keep our y position unchanged.
            targetPosition.y = transform.position.y;

            float adjustedSharpness = followSharpness;
            if (isSmoothUpdate)
            {
                adjustedSharpness = 1f - Mathf.Pow(1f - followSharpness, Time.deltaTime * REFERENCE_FRAMERATE);
            }

            // Smooth follow.
            transform.position += (targetPosition - transform.position) * adjustedSharpness;
        }
    }
}