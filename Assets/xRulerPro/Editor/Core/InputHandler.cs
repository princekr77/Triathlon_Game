using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
namespace SuperHorizon.xRulerPro
{
    /// <summary>
    /// Handles all input processing for xRuler Pro.
    /// </summary>
    public static class InputHandler
    {
        private static bool _isShiftPressed;
        private static bool _isRPressed;
        
        /// <summary>
        /// Check if measurement mode should be active (Shift + R held).
        /// </summary>
        public static bool IsMeasurementModeActive()
        {
            Event e = Event.current;
            
            // Update key states
            if (e.type == EventType.KeyDown)
            {
                if (e.shift) _isShiftPressed = true;
                if (e.keyCode == KeyCode.R) _isRPressed = true;
            }
            else if (e.type == EventType.KeyUp)
            {
                if (!e.shift) _isShiftPressed = false;
                if (e.keyCode == KeyCode.R) _isRPressed = false;
            }

            // Check for Escape to exit
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _isShiftPressed = false;
                _isRPressed = false;
                return false;
            }

            return _isShiftPressed && _isRPressed && xRulerProSettings.Instance.enableMeasurementMode;
        }

        /// <summary>
        /// Get world position from mouse using raycast.
        /// </summary>
        public static bool GetWorldPositionFromMouse(out Vector3 worldPos, out GameObject hitObject)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                worldPos = hit.point;
                hitObject = hit.collider.gameObject;
                return true;
            }

            // If no collider hit, use plane at distance
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            float distance;
            if (plane.Raycast(ray, out distance))
            {
                worldPos = ray.GetPoint(distance);
                hitObject = null;
                return true;
            }

            worldPos = Vector3.zero;
            hitObject = null;
            return false;
        }

        /// <summary>
        /// Check if mouse button was clicked this frame.
        /// </summary>
        public static bool IsMouseClick()
        {
            Event e = Event.current;
            return e.type == EventType.MouseDown && e.button == 0;
        }

        /// <summary>
        /// Get scroll delta for reference object sizing.
        /// </summary>
        public static float GetScrollDelta()
        {
            Event e = Event.current;
            if (e.type == EventType.ScrollWheel)
            {
                return e.delta.y;
            }
            return 0f;
        }

        /// <summary>
        /// Get bounds from selected objects.
        /// </summary>
        public static BoundsMeasurement GetSelectedObjectsBounds()
        {
            GameObject[] selected = Selection.gameObjects;
            
            if (selected == null || selected.Length == 0)
            {
                return BoundsMeasurement.Invalid;
            }

            Bounds combinedBounds = new Bounds();
            bool hasBounds = false;

            foreach (GameObject obj in selected)
            {
                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (!hasBounds)
                    {
                        combinedBounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return hasBounds ? new BoundsMeasurement(combinedBounds) : BoundsMeasurement.Invalid;
        }

        /// <summary>
        /// Get bounds from clicked object.
        /// </summary>
        public static BoundsMeasurement GetClickedObjectBounds(GameObject obj)
        {
            if (obj == null) return BoundsMeasurement.Invalid;

            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                return new BoundsMeasurement(renderer.bounds);
            }

            return BoundsMeasurement.Invalid;
        }
    }
}
#endif