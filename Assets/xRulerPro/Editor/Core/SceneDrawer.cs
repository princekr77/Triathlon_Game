using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
namespace SuperHorizon.xRulerPro
{
    /// <summary>
    /// Handles all drawing operations in Scene View using Handles API.
    /// Optimized to avoid allocations.
    /// </summary>
    public static class SceneDrawer
    {
        private static GUIStyle _labelStyle;
        private static GUIStyle _boldLabelStyle;

        /// <summary>
        /// Initialize cached styles.
        /// </summary>
        private static void InitializeStyles()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label);
                _labelStyle.normal.textColor = Color.white;
                _labelStyle.fontStyle = FontStyle.Bold;
                _labelStyle.alignment = TextAnchor.MiddleCenter;
                
                _boldLabelStyle = new GUIStyle(_labelStyle);
                _boldLabelStyle.fontSize = 14;
            }
        }

        /// <summary>
        /// Draw measurement line between two points.
        /// </summary>
        public static void DrawMeasurementLine(Vector3 start, Vector3 end, float distance, UnitType unit)
        {
            InitializeStyles();
            
            xRulerProSettings settings = xRulerProSettings.Instance;
            
            // Draw line
            Handles.color = settings.lineColor;
            Handles.DrawLine(start, end, settings.lineThickness);
            
            // Draw end point indicators
            Handles.SphereHandleCap(0, start, Quaternion.identity, 0.05f, EventType.Repaint);
            Handles.SphereHandleCap(0, end, Quaternion.identity, 0.05f, EventType.Repaint);
            
            // Draw distance label at midpoint
            Vector3 midPoint = (start + end) * 0.5f;
            string distanceText = UnitConverter.FormatMeasurement(distance, unit);
            
            _labelStyle.fontSize = Mathf.RoundToInt(settings.textSize);
            
            Handles.Label(midPoint + Vector3.up * 0.2f, distanceText, _labelStyle);
        }

        /// <summary>
        /// Draw bounding box with size labels.
        /// </summary>
        public static void DrawBoundingBox(Bounds bounds, UnitType unit)
        {
            InitializeStyles();
            
            xRulerProSettings settings = xRulerProSettings.Instance;
            
            // Draw wireframe box
            Handles.color = Color.white;
            Handles.DrawWireCube(bounds.center, bounds.size);
            
            // Get size in each axis
            Vector3 size = bounds.size;
            Vector3 center = bounds.center;
            
            // Draw X dimension (Red)
            Vector3 xStart = center + new Vector3(-size.x * 0.5f, size.y * 0.5f, 0);
            Vector3 xEnd = center + new Vector3(size.x * 0.5f, size.y * 0.5f, 0);
            Handles.color = settings.xAxisColor;
            Handles.DrawLine(xStart, xEnd, settings.lineThickness);
            
            string xText = "X: " + UnitConverter.FormatMeasurement(size.x, unit);
            Handles.Label(center + new Vector3(0, size.y * 0.5f + 0.3f, 0), xText, _labelStyle);
            
            // Draw Y dimension (Green)
            Vector3 yStart = center + new Vector3(size.x * 0.5f, -size.y * 0.5f, 0);
            Vector3 yEnd = center + new Vector3(size.x * 0.5f, size.y * 0.5f, 0);
            Handles.color = settings.yAxisColor;
            Handles.DrawLine(yStart, yEnd, settings.lineThickness);
            
            string yText = "Y: " + UnitConverter.FormatMeasurement(size.y, unit);
            Handles.Label(center + new Vector3(size.x * 0.5f + 0.3f, 0, 0), yText, _labelStyle);
            
            // Draw Z dimension (Blue)
            Vector3 zStart = center + new Vector3(size.x * 0.5f, size.y * 0.5f, -size.z * 0.5f);
            Vector3 zEnd = center + new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);
            Handles.color = settings.zAxisColor;
            Handles.DrawLine(zStart, zEnd, settings.lineThickness);
            
            string zText = "Z: " + UnitConverter.FormatMeasurement(size.z, unit);
            Handles.Label(center + new Vector3(size.x * 0.5f, size.y * 0.5f + 0.3f, 0), zText, _labelStyle);
        }

        /// <summary>
        /// Draw reference object preview at position.
        /// </summary>
        public static void DrawReferenceObject(Vector3 position, float size)
        {
            InitializeStyles();
            
            xRulerProSettings settings = xRulerProSettings.Instance;
            
            // Draw semi-transparent cube
            Handles.color = settings.referenceObjectColor;
            Handles.CubeHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
            
            // Draw wireframe for clarity
            Handles.color = Color.white;
            Handles.DrawWireCube(position, Vector3.one * size);
            
            // Draw size label
            string sizeText = UnitConverter.FormatMeasurement(size, settings.currentUnit);
            Handles.Label(position + Vector3.up * (size * 0.5f + 0.3f), $"Reference: {sizeText}", _boldLabelStyle);
        }

        /// <summary>
        /// Draw active measurement cursor.
        /// </summary>
        public static void DrawCursor(Vector3 position)
        {
            //Handles.color = Color.yellow;
            //Handles.DrawWireDisc(position, Vector3.up, 0.1f);
            //Handles.DrawLine(position + Vector3.up * 0.2f, position - Vector3.up * 0.2f, 2f);
        }
    }
}
#endif