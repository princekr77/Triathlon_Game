using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
namespace SuperHorizon.xRulerPro
{
    /// <summary>
    /// Core manager for xRuler Pro. Handles Scene View integration and measurement logic.
    /// </summary>
    [InitializeOnLoad]
    public static class xRulerProCore
    {
        private static MeasurementSession _session;
        private static bool _isInitialized;
        private static float _referenceObjectSize = 1f;
        private static bool _showReferenceObject;
        private static BoundsMeasurement _currentBounds;

        static xRulerProCore()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (_isInitialized) return;

            _session = new MeasurementSession();
            _referenceObjectSize = xRulerProSettings.Instance.referenceObjectSize;
            
            SceneView.duringSceneGui += OnSceneGUI;
            _isInitialized = true;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;
            
            // Check if measurement mode is active
            bool isMeasurementMode = InputHandler.IsMeasurementModeActive();
            
            if (!isMeasurementMode)
            {
                // Reset session when exiting measurement mode
                if (_session.hasFirstPoint || _session.hasSecondPoint)
                {
                    _session.Reset();
                    _currentBounds = BoundsMeasurement.Invalid;
                    _showReferenceObject = false;
                    sceneView.Repaint();
                }
                return;
            }

            // We're in measurement mode
            HandleMeasurementMode(sceneView, e);
            
            // Request repaint to update visual feedback
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                sceneView.Repaint();
            }
        }

        private static void HandleMeasurementMode(SceneView sceneView, Event e)
        {
            xRulerProSettings settings = xRulerProSettings.Instance;
            
            // Handle scroll for reference object
            float scrollDelta = InputHandler.GetScrollDelta();
            if (scrollDelta != 0f)
            {
                _showReferenceObject = !_showReferenceObject;
                e.Use();
                sceneView.Repaint();
            }

            // Get mouse world position
            Vector3 mouseWorldPos;
            GameObject hitObject;
            bool hasHit = InputHandler.GetWorldPositionFromMouse(out mouseWorldPos, out hitObject);
            
            if (!hasHit) return;

            _session.currentMousePosition = mouseWorldPos;

            // Handle mouse click
            if (InputHandler.IsMouseClick())
            {
                HandleMouseClick(mouseWorldPos, hitObject);
                e.Use();
                sceneView.Repaint();
                return;
            }

            // Draw active measurement
            if (_session.hasFirstPoint && !_session.hasSecondPoint)
            {
                float distance = _session.GetDistance();
                SceneDrawer.DrawMeasurementLine(_session.pointA.position, mouseWorldPos, distance, settings.currentUnit);
                SceneDrawer.DrawCursor(mouseWorldPos);
            }
            else if (_session.hasSecondPoint)
            {
                float distance = _session.GetDistance();
                SceneDrawer.DrawMeasurementLine(_session.pointA.position, _session.pointB.position, distance, settings.currentUnit);
            }

            // Draw bounding box if active
            if (_currentBounds.isValid)
            {
                SceneDrawer.DrawBoundingBox(_currentBounds.bounds, settings.currentUnit);
            }

            // Draw reference object
            if (_showReferenceObject)
            {
                SceneDrawer.DrawReferenceObject(mouseWorldPos, _referenceObjectSize);
            }
        }

        private static void HandleMouseClick(Vector3 position, GameObject hitObject)
        {
            if (!_session.hasFirstPoint)
            {
                // First point - start measurement
                _session.pointA = new MeasurementPoint(position);
                _session.hasFirstPoint = true;
                _currentBounds = BoundsMeasurement.Invalid;

                // Check if we clicked an object - show its bounds
                if (hitObject != null)
                {
                    _currentBounds = InputHandler.GetClickedObjectBounds(hitObject);
                }
            }
            else if (!_session.hasSecondPoint)
            {
                // Second point - complete measurement
                _session.pointB = new MeasurementPoint(position);
                _session.hasSecondPoint = true;

                // If clicked another object, show its bounds instead
                if (hitObject != null)
                {
                    _currentBounds = InputHandler.GetClickedObjectBounds(hitObject);
                }
            }
            else
            {
                // Reset and start new measurement
                _session.Reset();
                _session.pointA = new MeasurementPoint(position);
                _session.hasFirstPoint = true;
                _currentBounds = BoundsMeasurement.Invalid;

                if (hitObject != null)
                {
                    _currentBounds = InputHandler.GetClickedObjectBounds(hitObject);
                }
            }
        }

        /// <summary>
        /// Reset measurement session (called from settings window).
        /// </summary>
        public static void ResetMeasurement()
        {
            _session.Reset();
            _currentBounds = BoundsMeasurement.Invalid;
            _showReferenceObject = false;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Update reference object size.
        /// </summary>
        public static void SetReferenceObjectSize(float size)
        {
            _referenceObjectSize = Mathf.Max(0.01f, size);
        }
    }
}
#endif