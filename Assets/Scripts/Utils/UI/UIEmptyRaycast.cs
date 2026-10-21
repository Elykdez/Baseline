using UnityEngine;
using UnityEngine.UI;

namespace Hypocycloid.Utils
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIEmptyRaycast : MaskableGraphic
    {
        protected UIEmptyRaycast()
        {
            useLegacyMeshGeneration = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}