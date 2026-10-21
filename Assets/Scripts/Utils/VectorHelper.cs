using System;
using UnityEngine;

using Random = UnityEngine.Random;

namespace Hypocycloid.Utils
{
    public class VectorHelper
    {
        public static Vector2 GetRandomPointOn2DBound(Bounds bounds, float variance = 0, int edgeIndex = -1)
        {
            // Choose a random face of the bounding box
            edgeIndex = edgeIndex < 0 ? Random.Range(0, 4) : edgeIndex;
            float randomDistance = Random.Range(0, variance);

            // Generate a random point on the chosen face
            return edgeIndex switch
            {
                // Top edge
                0 => new(Random.Range(bounds.min.x, bounds.max.x), bounds.max.y - randomDistance),
                // Right edge
                1 => new(bounds.max.x - randomDistance, Random.Range(bounds.min.y, bounds.max.y)),
                // Bottom edge
                2 => new(Random.Range(bounds.min.x, bounds.max.x), bounds.min.y + randomDistance),
                // Left edge
                3 => new(bounds.min.x + randomDistance, Random.Range(bounds.min.y, bounds.max.y)),
                _ => Vector2.zero,// Should not happen
            };

        }

        public static Vector3 GetRandomPointOn3DBound(Bounds bounds, float variance = 0, int edgeIndex = -1)
        {
            // Choose a random face of the bounding box
            edgeIndex = edgeIndex < 0 ? Random.Range(0, 4) : edgeIndex;
            float randomDistance = Random.Range(0, variance);

            // Generate a random point on the chosen face
            return edgeIndex switch
            {
                // Front face
                0 => new(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y), bounds.min.z - randomDistance),
                // Back face
                1 => new(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y), bounds.max.z + randomDistance),
                // Left face
                2 => new(bounds.min.x + randomDistance, Random.Range(bounds.min.y, bounds.max.y), Random.Range(bounds.min.z, bounds.max.z)),
                // Right face
                3 => new(bounds.max.x - randomDistance, Random.Range(bounds.min.y, bounds.max.y), Random.Range(bounds.min.z, bounds.max.z)),
                // Top face
                4 => new(Random.Range(bounds.min.x, bounds.max.x), bounds.max.y - randomDistance, Random.Range(bounds.min.z, bounds.max.z)),
                // Bottom face
                5 => new(Random.Range(bounds.min.x, bounds.max.x), bounds.min.y + randomDistance, Random.Range(bounds.min.z, bounds.max.z)),
                _ => Vector3.zero,// Should not happen
            };

        }

        public static int CheckDirection(Vector3 fromPoint, Vector3 toPoint)
        {
            Vector3 direction = (toPoint - fromPoint).normalized;

            // Check if the direction is primarily horizontal or vertical
            float horizontalDot = Vector3.Dot(direction, Vector3.right);
            float verticalDot = Vector3.Dot(direction, Vector3.up);

            if (Mathf.Abs(horizontalDot) > Mathf.Abs(verticalDot))
            {
                // Horizontal direction
                if (horizontalDot > 0)
                    return 1;
                else
                    return 3;
            }
            else
            {
                // Vertical direction
                if (verticalDot > 0)
                    return 0;
                else
                    return 2;
            }
        }

    }
}
