using UnityEngine;
using System.Collections.Generic;
using Code.GameLogic;
using Code.Cards;
using Code.Player;

namespace Code.DebugTools
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
        public bool showDealerViraAndDeck = true;
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

#if UNITY_EDITOR
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
                    Gizmos.DrawCube(chair.sitTransform.position, Vector3.one * gizmoScale * 0.5f);
                    Gizmos.DrawRay(chair.sitTransform.position, chair.sitTransform.forward * gizmoScale * 2f);

                    if (showSeatLabels)
                    {
                        string label = $"Silla {i}";
                        if (chair.isOccupied && chair.occupant != null)
                            label += $"\n{chair.occupant.name}";
                        else
                            label += "\n(vacia)";
                        
                        GUIStyle style = new GUIStyle();
                        style.normal.textColor = seatColor;
                        style.fontStyle = FontStyle.Bold;
                        style.fontSize = 14;
                        UnityEditor.Handles.Label(chair.sitTransform.position + Vector3.up * 0.6f, label, style);
                    }
                }

                // ===== 2. CAMARA DE MESA =====
                if (showCameraGizmos && chair.cameraPosition != null)
                {
                    Transform camTransform = chair.cameraPosition;
                    Color camColor = new Color(seatColor.r, seatColor.g, seatColor.b, 0.7f);
                    Gizmos.color = camColor;

                    Gizmos.DrawWireSphere(camTransform.position, gizmoScale * 0.25f);
                    
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

                    if (showSeatLabels)
                    {
                        GUIStyle camStyle = new GUIStyle();
                        camStyle.normal.textColor = camColor;
                        camStyle.fontStyle = FontStyle.Bold;
                        camStyle.fontSize = 11;
                        UnityEditor.Handles.Label(camTransform.position + Vector3.up * 0.35f, $"Cam {i}", camStyle);
                    }

                    if (chair.sitTransform != null)
                    {
                        Gizmos.color = new Color(seatColor.r, seatColor.g, seatColor.b, 0.3f);
                        DrawDashedLine(chair.sitTransform.position + Vector3.up * 0.3f, camTransform.position, 0.15f);
                    }
                }

                // ===== 3. DESTINO DE CARTAS EN LA MESA =====
                if (showCardDestinations && chair.cardDestination != null)
                {
                    Transform cardDest = chair.cardDestination;
                    if (cardDest != null)
                    {
                        Color cardColor = new Color(seatColor.r, seatColor.g, seatColor.b, 0.9f);
                        Gizmos.color = cardColor;

                        float cardW = gizmoScale * 0.6f;
                        float cardH = gizmoScale * 0.9f;
                        Vector3 cp = cardDest.position;
                        Vector3 center = tableManager != null && tableManager.viraPosition != null ? tableManager.viraPosition.position : Vector3.zero;
                        
                        // Dibujar siempre plano sobre la mesa (plano XZ) independientemente de si la flecha Z (forward) mira hacia arriba
                        Vector3 right = Vector3.Cross(Vector3.up, (center - cp).normalized); // Perpendicular a la dirección hacia el centro
                        if (right == Vector3.zero) right = Vector3.right;
                        Vector3 forward = Vector3.Cross(right, Vector3.up); // Apuntando hacia el centro

                        // Si por alguna razón el centro no está bien, usamos ejes globales
                        if (forward == Vector3.zero) { right = Vector3.right; forward = Vector3.forward; }

                        Vector3 c1 = cp - right * cardW + forward * cardH;
                        Vector3 c2 = cp + right * cardW + forward * cardH;
                        Vector3 c3 = cp + right * cardW - forward * cardH;
                        Vector3 c4 = cp - right * cardW - forward * cardH;

                        Gizmos.DrawLine(c1, c2);
                        Gizmos.DrawLine(c2, c3);
                        Gizmos.DrawLine(c3, c4);
                        Gizmos.DrawLine(c4, c1);
                        Gizmos.DrawLine(c1, c3);
                        Gizmos.DrawLine(c2, c4);

                        if (showSeatLabels)
                        {
                            GUIStyle cardStyle = new GUIStyle();
                            cardStyle.normal.textColor = cardColor;
                            cardStyle.fontStyle = FontStyle.Bold;
                            cardStyle.fontSize = 11;
                            string occupantName = Application.isPlaying && chair.isOccupied && chair.occupant != null ? chair.occupant.name : "(Vacía)";
                            UnityEditor.Handles.Label(cp + Vector3.up * 0.2f, $"Destino Silla {i}\n{occupantName}", cardStyle);
                        }

                        // ===== 4. FLECHA TRAYECTORIA =====
                        if (showCardTrajectory && chair.sitTransform != null)
                        {
                            Gizmos.color = new Color(seatColor.r, seatColor.g, seatColor.b, 0.5f);
                            Vector3 from = chair.sitTransform.position + Vector3.up * 1.2f;
                            Vector3 to = cp + Vector3.up * 0.05f;
                            
                            DrawParabolicArc(from, to, 0.8f, 20);
                            DrawArrowHead(to, (to - from).normalized, gizmoScale * 0.3f);
                        }
                    }
                }

                // ===== 4.5. MAZO Y VIRA DEL REPARTIDOR =====
                if (showDealerViraAndDeck && chair.cardDestination != null)
                {
                    Transform anchor = chair.cardDestination;
                    Vector3 basePos = anchor.position;
                    Quaternion baseRot = anchor.rotation;

                    float deckHeight = tableManager != null ? tableManager.deckHeightOffset : 0.01f;
                    float viraHeight = tableManager != null ? tableManager.viraHeightOffset : 0.02f;

                    // Offset para el Mazo: A la derecha y un poco atrás de donde el jugador pone su carta
                    Vector3 deckOffset = (anchor.right * 0.25f) + (anchor.forward * -0.1f);
                    Vector3 deckPos = basePos + deckOffset + (anchor.up * deckHeight);

                    // Mazo/Deck visual outline
                    Color deckColor = new Color(seatColor.r, seatColor.g, seatColor.b, 0.7f);
                    Gizmos.color = deckColor;

                    // Draw a wire cube for the deck stack
                    Matrix4x4 oldMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.TRS(deckPos, baseRot, Vector3.one);
                    
                    float deckW = gizmoScale * 0.25f;
                    float deckH = gizmoScale * 0.4f;
                    float deckThickness = gizmoScale * 0.15f;
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(deckW * 2f, deckThickness, deckH * 2f));
                    
                    // Draw a small line to indicate the deck top/bottom
                    Gizmos.DrawLine(new Vector3(-deckW, deckThickness / 2f, -deckH), new Vector3(deckW, deckThickness / 2f, deckH));
                    
                    // Vira position: next to the deck (un poco más al centro, which is forward by 0.15f)
                    Vector3 viraWorldPos = basePos + deckOffset + (anchor.forward * 0.15f) + (anchor.up * viraHeight);
                    Gizmos.matrix = Matrix4x4.TRS(viraWorldPos, baseRot, Vector3.one);

                    Color viraColor = new Color(seatColor.r, seatColor.g, seatColor.b, 0.9f);
                    Gizmos.color = viraColor;

                    float viraW = gizmoScale * 0.25f;
                    float viraH = gizmoScale * 0.4f;
                    
                    // Draw a flat card outline for the Vira
                    Vector3 v1 = new Vector3(-viraW, 0, viraH);
                    Vector3 v2 = new Vector3(viraW, 0, viraH);
                    Vector3 v3 = new Vector3(viraW, 0, -viraH);
                    Vector3 v4 = new Vector3(-viraW, 0, -viraH);

                    Gizmos.DrawLine(v1, v2);
                    Gizmos.DrawLine(v2, v3);
                    Gizmos.DrawLine(v3, v4);
                    Gizmos.DrawLine(v4, v1);
                    // Draw diagonal cross to represent vira
                    Gizmos.color = new Color(seatColor.r, seatColor.g, seatColor.b, 0.4f);
                    Gizmos.DrawLine(v1, v3);
                    Gizmos.DrawLine(v2, v4);

                    Gizmos.matrix = oldMatrix;

                    if (showSeatLabels)
                    {
                        GUIStyle dealerLabelStyle = new GUIStyle();
                        dealerLabelStyle.normal.textColor = seatColor;
                        dealerLabelStyle.fontStyle = FontStyle.Normal;
                        dealerLabelStyle.fontSize = 9;
                        UnityEditor.Handles.Label(deckPos + Vector3.up * 0.15f, $"Mazo {i}", dealerLabelStyle);
                        UnityEditor.Handles.Label(viraWorldPos + Vector3.up * 0.15f, $"Vira {i}", dealerLabelStyle);
                    }
                }
            }

            // ===== 5. CENTRO DE LA MESA (Vira) =====
            if (tableManager != null && tableManager.viraPosition != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(tableManager.viraPosition.position, gizmoScale * 0.4f);
                
                GUIStyle viraStyle = new GUIStyle();
                viraStyle.normal.textColor = Color.white;
                viraStyle.fontStyle = FontStyle.Bold;
                viraStyle.fontSize = 12;
                UnityEditor.Handles.Label(tableManager.viraPosition.position + Vector3.up * 0.5f, "Vira / Centro", viraStyle);
            }

            // ===== 6. GIZMOS DE CARTAS EN LA MANO DE LOS JUGADORES =====
            for (int i = 0; i < seatManager.allChairs.Count; i++)
            {
                var chair = seatManager.allChairs[i];
                if (chair == null || !chair.isOccupied || chair.occupant == null) continue;

                var occupant = chair.occupant;
                var handCards = new List<PhysicalCard3D>();

                var playerLocal = occupant.GetComponent<PlayerLocal>();
                if (playerLocal != null && playerLocal.cardsHandler != null)
                {
                    foreach (var cardObj in playerLocal.cardsHandler.Cards)
                    {
                        if (cardObj != null)
                        {
                            var physicalCard = cardObj.GetComponent<PhysicalCard3D>();
                            if (physicalCard != null)
                            {
                                handCards.Add(physicalCard);
                            }
                        }
                    }
                }
                else
                {
                    var physicalCards = occupant.GetComponentsInChildren<PhysicalCard3D>(true);
                    foreach (var physicalCard in physicalCards)
                    {
                        if (physicalCard != null)
                        {
                            handCards.Add(physicalCard);
                        }
                    }
                }

                foreach (var card in handCards)
                {
                    if (card == null) continue;

                    Transform cardTransform = card.transform;
                    Matrix4x4 oldMatrix = Gizmos.matrix;

                    // Usar la matriz del transform de la carta para dbuijar en su espacio local
                    Gizmos.matrix = cardTransform.localToWorldMatrix;

                    // Determinar el tamaño de la carta. Intentamos usar el BoxCollider o un tamaño por defecto
                    Vector3 cardSize = new Vector3(0.151111543f, 0.217364341f, 0.00313752703f); // Tamaño real del prefab
                    Vector3 cardCenter = Vector3.zero;

                    var boxCol = card.GetComponent<BoxCollider>();
                    if (boxCol != null)
                    {
                        cardSize = boxCol.size;
                        cardCenter = boxCol.center;
                    }

                    // 1. Dibujar el contorno amarillo de la carta
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(cardCenter, cardSize);

                    // Dibujar una X en el plano de la carta para mayor claridad visual
                    Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.4f); // Amarillo semitransparente
                    Gizmos.DrawLine(cardCenter + new Vector3(-cardSize.x * 0.5f, -cardSize.y * 0.5f, 0), cardCenter + new Vector3(cardSize.x * 0.5f, cardSize.y * 0.5f, 0));
                    Gizmos.DrawLine(cardCenter + new Vector3(-cardSize.x * 0.5f, cardSize.y * 0.5f, 0), cardCenter + new Vector3(cardSize.x * 0.5f, -cardSize.y * 0.5f, 0));

                    // Restaurar matriz para dibujar las flechas de dirección en coordenadas de mundo
                    Gizmos.matrix = oldMatrix;

                    // 2. Dibujar flechas de orientación/ejes en el centro de la carta
                    Vector3 centerWorld = cardTransform.TransformPoint(cardCenter);
                    float arrowLength = 0.25f; // Largo de los ejes visuales

                    // Eje X (Derecha) - Rojo
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(centerWorld, cardTransform.right * arrowLength);

                    // Eje Y (Arriba) - Verde
                    Gizmos.color = Color.green;
                    Gizmos.DrawRay(centerWorld, cardTransform.up * arrowLength);

                    // Eje Z (Adelante/Dirección a la que mira) - Azul
                    Gizmos.color = Color.blue;
                    Gizmos.DrawRay(centerWorld, cardTransform.forward * arrowLength);

                    // Etiqueta de texto sobre la carta indicando qué carta es
                    string cardLabel = $"{card.cardValue} de {card.cardSuit}";
                    GUIStyle labelStyle = new GUIStyle();
                    labelStyle.normal.textColor = Color.yellow;
                    labelStyle.fontSize = 10;
                    labelStyle.alignment = TextAnchor.MiddleCenter;
                    UnityEditor.Handles.Label(centerWorld + cardTransform.up * (cardSize.y * 0.6f), cardLabel, labelStyle);
                }
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
#endif
    }
}
