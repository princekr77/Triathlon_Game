using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
namespace SuperHorizon.xRulerPro
{
   
    public class xRulerProWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private xRulerProSettings _settings;

        [MenuItem("Tools/xRuler Pro Manager")]
        public static void ShowWindow()
        {
            xRulerProWindow window = GetWindow<xRulerProWindow>("xRuler Pro");
            window.minSize = new Vector2(350, 500);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = xRulerProSettings.Instance;
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                _settings = xRulerProSettings.Instance;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Header
            GUILayout.Space(10);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 16;
            headerStyle.alignment = TextAnchor.MiddleCenter;
            EditorGUILayout.LabelField("xRuler Pro Settings", headerStyle);
            GUILayout.Space(10);

            DrawSeparator();

            // Instructions
            DrawInstructionsSection();
            DrawSeparator();

            // Behavior Settings
            DrawBehaviorSection();
            DrawSeparator();

            // Unit Settings
            DrawUnitSection();
            DrawSeparator();

            // Visual Settings
            DrawVisualSection();
            DrawSeparator();

            // Reference Object Settings
            DrawReferenceObjectSection();
            DrawSeparator();

            // Actions
            DrawActionsSection();

            EditorGUILayout.EndScrollView();

            // Save changes
            if (GUI.changed)
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawInstructionsSection()
        {
            EditorGUILayout.LabelField("How to Use", EditorStyles.boldLabel);
            
            GUIStyle instructionStyle = new GUIStyle(EditorStyles.helpBox);
            instructionStyle.richText = true;
            instructionStyle.padding = new RectOffset(10, 10, 10, 10);
            
            EditorGUILayout.TextArea(
                "<b>Shift + R</b> - Enter Measurement Mode\n\n" +
                "<b>Left Click</b> - Set measurement points\n" +
                "   • First click: Start point\n" +
                "   • Second click: End point\n" +
                "   • Third click: New measurement\n\n" +
                "<b>Click Object</b> - Show bounding box\n\n" +
                "<b>Scroll Wheel</b> - Toggle reference object\n\n" +
                "<b>Esc</b> - Exit measurement mode",
                instructionStyle
            );
        }

        private void DrawBehaviorSection()
        {
            EditorGUILayout.LabelField("Behavior", EditorStyles.boldLabel);
            
            _settings.enableMeasurementMode = EditorGUILayout.Toggle(
                new GUIContent("Enable Measurement Mode", "Master toggle for the measurement tool"),
                _settings.enableMeasurementMode
            );
        }

        private void DrawUnitSection()
        {
            EditorGUILayout.LabelField("Unit Settings", EditorStyles.boldLabel);
            
            _settings.currentUnit = (UnitType)EditorGUILayout.EnumPopup(
                new GUIContent("Measurement Unit", "Unit system for displaying measurements"),
                _settings.currentUnit
            );
            
            // Show conversion example
            EditorGUILayout.HelpBox(
                $"Example: 1 meter = {UnitConverter.FormatMeasurement(1f, _settings.currentUnit)}",
                MessageType.Info
            );
        }

        private void DrawVisualSection()
        {
            EditorGUILayout.LabelField("Visual Settings", EditorStyles.boldLabel);
            
            _settings.lineColor = EditorGUILayout.ColorField(
                new GUIContent("Line Color", "Color of measurement lines"),
                _settings.lineColor
            );
            
            _settings.lineThickness = EditorGUILayout.Slider(
                new GUIContent("Line Thickness", "Thickness of measurement lines"),
                _settings.lineThickness,
                1f,
                5f
            );
            
            _settings.textSize = EditorGUILayout.Slider(
                new GUIContent("Text Size", "Size of measurement labels"),
                _settings.textSize,
                8f,
                20f
            );
            
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Axis Colors", EditorStyles.miniBoldLabel);
            
            _settings.xAxisColor = EditorGUILayout.ColorField(
                new GUIContent("X Axis (Red)", "Color for X dimension"),
                _settings.xAxisColor
            );
            
            _settings.yAxisColor = EditorGUILayout.ColorField(
                new GUIContent("Y Axis (Green)", "Color for Y dimension"),
                _settings.yAxisColor
            );
            
            _settings.zAxisColor = EditorGUILayout.ColorField(
                new GUIContent("Z Axis (Blue)", "Color for Z dimension"),
                _settings.zAxisColor
            );
        }

        private void DrawReferenceObjectSection()
        {
            EditorGUILayout.LabelField("Reference Object", EditorStyles.boldLabel);
            
            float newSize = EditorGUILayout.FloatField(
                new GUIContent("Default Size (meters)", "Default size of reference cube"),
                _settings.referenceObjectSize
            );
            
            if (newSize != _settings.referenceObjectSize)
            {
                _settings.referenceObjectSize = Mathf.Max(0.01f, newSize);
                xRulerProCore.SetReferenceObjectSize(_settings.referenceObjectSize);
            }
            
            _settings.referenceObjectColor = EditorGUILayout.ColorField(
                new GUIContent("Reference Color", "Color of reference preview object"),
                _settings.referenceObjectColor
            );
            
            EditorGUILayout.HelpBox(
                "Use scroll wheel while in measurement mode to toggle reference object preview.",
                MessageType.Info
            );
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Reset Current Measurement", GUILayout.Height(30)))
            {
                xRulerProCore.ResetMeasurement();
            }
            
            GUILayout.Space(5);
            
            if (GUILayout.Button("Reset to Default Settings", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Reset Settings",
                    "Are you sure you want to reset all settings to default values?",
                    "Yes",
                    "Cancel"))
                {
                    ResetToDefaults();
                }
            }
        }

        private void ResetToDefaults()
        {
            _settings.currentUnit = UnitType.Meters;
            _settings.xAxisColor = Color.red;
            _settings.yAxisColor = Color.green;
            _settings.zAxisColor = Color.blue;
            _settings.lineColor = Color.yellow;
            _settings.lineThickness = 2f;
            _settings.textSize = 12f;
            _settings.referenceObjectSize = 1f;
            _settings.referenceObjectColor = new Color(1f, 1f, 1f, 0.3f);
            _settings.enableMeasurementMode = true;
            
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            
            xRulerProCore.ResetMeasurement();
        }

        private void DrawSeparator()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Space(10);
        }
    }
}
#endif