using UnityEngine;
using UnityEditor;
using Code.GameLogic;

namespace Code.Editor
{
    /// <summary>
    /// Dibuja Gizmos en la vista de Scene para visualizar:
    /// - Posición de cada silla (donde se sienta el jugador)
    /// - Posición y dirección de cada cámara de mesa
    /// - Posición donde aterriza la carta de cada jugador
    /// - Flecha desde el jugador hacia su zona de carta en la mesa
    /// </summary>
    public class SeatDebugVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        public bool showCameraGizmos = true;
        public bool showCardDestinations = true;
        public bool showSeatLabels = true;
        public bool showCardTrajectory = true;
        public float gizmoScale = 0.3f;

        private static readonly Color[] seatColors = new Color[]
        {
            new Color(0.2f, 0.8f, 0.2f),   // Verde - Silla 0
            new Color(0.8f, 0.2f, 0.2f),   // Rojo - Silla 1
            new Color(0.2f, 0.4f, 0.9f),   // Azul - Silla 2
            new Color(0.9f, 0.7f, 0.1f),   // Amarillo - Silla 3
            new Color(0.8f, 0.2f, 0.8f),   // Magenta - Silla 4
            new Color(0.2f, 0.8f, 0.8f),   // Cyan - Silla 5
        };

        private void OnDrawGizmos()
        {
            var seatManager = SeatManager.Instance;
            if (seatManager == null) seatManager = FindAnyObjectByType<SeatManager>();
            if (seatManager == null || seatManager.allChairs == null) return;

            var tableManager = TableManager.Instance;
            if (tableManager == null) tableManager = FindAnyObjectByType<TableManager>();

            for (int i = 0; i < seatManager.allChairs.Count; i++)
            {
                var chair = seatManager.allChairs[i];
                if (chair == null) continue;

                Color seatColor = seatColors[i % seatColors.Length];

                // ===== 1. SILLA (Sit Transform) =====
                if (chair.sitTransform != null)
                {
                    Gizmos.color = seatColor;
                    // Dibujar un cubo sólido donde se sienta el jugador
                    Gizmos.DrawCube(chair.sitTransform.position, Vector3.one * gizmoScale * 0.5f);
                    // Dibujar línea hacia adelante (dirección que mira el jugador)
                    Gizmos.DrawRay(chair.sitTransform.position, chair.sitTransform.forward * gizmoScale * 2f);

                    if (showSeatLabels)
                    {
                        string label = $"🪑 Silla {i}";
                        if (chair.isOccupied && chair.occupant != null)
                            label += $"\n👤 {chair.occupant.name}";
                        else
                            label += "\n(vacía)";
                        
                        Handles.color = seatColor;
                        GUIStyle style = new GUIStyle();
                        style.normal.textColor = seatColor;
                        style.fontStyle = FontStyle.Bold;
                        style.fontSize = 14;
                        Handles.Label(chair.sitTransform.position + Vector3.up * 0.6f, label, style);
                    }
                }

                // ===== 2. CÁMARA DE MESA =====
                if (showCameraGizmos && chair.tableCamera != null)
                {
                    Transform camTransform = chair.tableCamera.transform;
                    Color camColor = new Color(seatColor.r, seatColor.g, seatColor.b, 0.7f);
                    Gizmos.color = camColor;

                    // Icono de cámara (pirámide)
                    Gizmos.DrawWireSphere(camTransform.position, gizmoScale * 0.25f);
                    
                    // Frustum simplificado (4 líneas desde la cámara hacia adelante)
                    float frustumLength = gizmoScale * 3f;
                    float frustumWidth = gizmoScale * 0.8f;
                    Vector3 camPos = camTransform.position;
                    Vector3 camFwd = camTransform.forward * frustumLength;
                    Vector3 camRight = camTransform.right * frustumWidth;
                    Vector3 camUp = camTransform.up * frustumWidth * 0.6f;

                    Vector3 tl = camPos + camFwd - camRight + camUp;
                    Vector3 tr = camPos + camFwd + camRight + camUp;
                    Vector3 bl = camPos + camFwd - camRight - camUp;
                    Vector3 br = camPos + camFwd + camRight - camUp;

                    Gizmos.DrawLine(camPos, tl);
                    Gizmos.DrawLine(camPos, tr);
                    Gizmos.DrawLine(camPos, bl);
                    Gizmos.DrawLine(camPos, br);
                    Gizmos.DrawLine(tl, tr);
                    Gizmos.DrawLine(tr, br);
                    Gizmos.DrawLine(br, bl);
                    Gizmos.DrawLine(bl, tl);

                    // Etiqueta
                    if (showSeatLabels)
                    {
                        GUIStyle camStyle = new GUIStyle();
                        camStyle.normal.textColor = camColor;
                        camStyle.fontStyle = FontStyle.Bold;
                        camStyle.fontSize = 11;
                        Handles.Label(camTransform.position + Vector3.up * 0.35f, $"📷 Cam {i}", camStyle);
                    }

                    // Línea punteada desde la silla a su cámara
                    if (chair.sitTransform != null)
                    {
                        Gizmos.color = new Color(seatColor.r, seatColor.g, seatColor.b, 0.3f);
                        DrawDashedLine(chair.sitTransform.position + Vector3.up * 0.3f, camTransform.position, 0.15f);
                    }
                }

                // ===== 3. DESTINO DE CARTAS EN LA MESA =====
                if (showCardDestinations && tableManager != null && 
                    tableManager.tableCardPositions != null && 
                    tableManager.tableCardPositions.Count > i)
                {
                    Transform cardDest = tableManager.tableCardPositions[i];
                    if (cardDest != null)
                    {
                        Color cardColor = new Color(seatColor.r, seatColor.g, seatColor.b, 0.9f);
                        Gizmos.color = cardColor;

                        // Dibujar rectángulo plano representando una carta
                        float cardW = gizmoScale * 0.6f;
                        float cardH = gizmoScale * 0.9f;
                        Vector3 cp = cardDest.position;
                        Vector3 cr = cardDest.right;
                        Vector3 cf = cardDest.forward;

                        Vector3 c1 = cp - cr * cardW + cf * cardH;
                        Vector3 c2 = cp + cr * cardW + cf * cardH;
                        Vector3 c3 = cp + cr * cardW - cf * cardH;
                        Vector3 c4 = cp - cr * cardW - cf * cardH;

                        Gizmos.DrawLine(c1, c2);
                        Gizmos.DrawLine(c2, c3);
                        Gizmos.DrawLine(c3, c4);
                        Gizmos.DrawLine(c4, c1);
                        // Cruz dentro de la carta
                        Gizmos.DrawLine(c1, c3);
                        Gizmos.DrawLine(c2, c4);

                        if (showSeatLabels)
                        {
                            GUIStyle cardStyle = new GUIStyle();
                            cardStyle.normal.textColor = cardColor;
                            cardStyle.fontStyle = FontStyle.Bold;
                            cardStyle.fontSize = 11;
                            Handles.Label(cp + Vector3.up * 0.2f, $"🃏 Carta → Silla {i}", cardStyle);
                        }

                        // ===== 4. FLECHA TRAYECTORIA (Silla → Destino de Carta) =====
                        if (showCardTrajectory && chair.sitTransform != null)
                        {
                            Gizmos.color = new Color(seatColor.r, seatColor.g, seatColor.b, 0.5f);
                            Vector3 from = chair.sitTransform.position + Vector3.up * 1.2f;
                            Vector3 to = cp + Vector3.up * 0.05f;
                            
                            // Arco parabólico
                            DrawParabolicArc(from, to, 0.8f, 20);
                            
                            // Punta de flecha
                            DrawArrowHead(to, (to - from).normalized, gizmoScale * 0.3f);
                        }
                    }
                }
            }

            // ===== 5. CENTRO DE LA MESA (Vira Position) =====
            if (tableManager != null && tableManager.viraPosition != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(tableManager.viraPosition.position, gizmoScale * 0.4f);
                
                GUIStyle viraStyle = new GUIStyle();
                viraStyle.normal.textColor = Color.white;
                viraStyle.fontStyle = FontStyle.Bold;
                viraStyle.fontSize = 12;
                Handles.Label(tableManager.viraPosition.position + Vector3.up * 0.5f, "⭐ Vira / Centro", viraStyle);
            }
        }

        private void DrawDashedLine(Vector3 from, Vector3 to, float dashLength)
        {
            Vector3 dir = (to - from);
            float dist = dir.magnitude;
            dir.Normalize();
            float drawn = 0;
            bool draw = true;
            while (drawn < dist)
            {
                float segLen = Mathf.Min(dashLength, dist - drawn);
                if (draw)
                    Gizmos.DrawLine(from + dir * drawn, from + dir * (drawn + segLen));
                drawn += segLen;
                draw = !draw;
            }
        }

        private void DrawParabolicArc(Vector3 from, Vector3 to, float height, int segments)
        {
            Vector3 prev = from;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 point = Vector3.Lerp(from, to, t);
                // Parábola: máximo en t=0.5
                point.y += height * 4f * t * (1f - t);
                Gizmos.DrawLine(prev, point);
                prev = point;
            }
        }

        private void DrawArrowHead(Vector3 tip, Vector3 direction, float size)
        {
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            if (right.magnitude < 0.01f) right = Vector3.Cross(Vector3.forward, direction).normalized;
            
            Vector3 back = -direction.normalized * size;
            Gizmos.DrawLine(tip, tip + back + right * size * 0.5f);
            Gizmos.DrawLine(tip, tip + back - right * size * 0.5f);
        }
    }
}
