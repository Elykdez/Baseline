using UnityEngine;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>Mirrors portrait artwork without moving its layout, pivot or children.</summary>
    public sealed class UIPortrait : Image
    {
        bool mirrored;

        public bool Mirrored
        {
            get => mirrored;
            set
            {
                if (mirrored == value) return;
                mirrored = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            base.OnPopulateMesh(mesh);
            if (!mirrored || mesh.currentVertCount == 0) return;
            // Use the generated bounds so preserve-aspect padding and custom pivots stay in place.
            UIVertex vertex = default;
            float left = float.MaxValue, right = float.MinValue;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                left = Mathf.Min(left, vertex.position.x);
                right = Mathf.Max(right, vertex.position.x);
            }
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.position.x = left + right - vertex.position.x;
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
