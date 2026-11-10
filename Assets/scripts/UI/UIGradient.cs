using UnityEngine;
using UnityEngine.UI;

namespace LinuxFuriaPinguim.UI
{
    [RequireComponent(typeof(Graphic))]
    public class UIGradient : BaseMeshEffect
    {
        [SerializeField] private Color topColor = Color.white;
        [SerializeField] private Color bottomColor = Color.black;

        public Color TopColor { set => topColor = value; }
        public Color BottomColor { set => bottomColor = value; }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) {
                return;
            }

            int count = vh.currentVertCount;
            UIVertex vertex = default;

            float topY = float.MinValue;
            float bottomY = float.MaxValue;
            for (int i = 0; i < count; i++) {
                vh.PopulateUIVertex(ref vertex, i);
                topY = Mathf.Max(topY, vertex.position.y);
                bottomY = Mathf.Min(bottomY, vertex.position.y);
            }

            float range = Mathf.Max(topY - bottomY, 0.0001f);
            for (int i = 0; i < count; i++) {
                vh.PopulateUIVertex(ref vertex, i);
                float t = (vertex.position.y - bottomY) / range;
                vertex.color = Color32.Lerp(bottomColor, topColor, t);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
