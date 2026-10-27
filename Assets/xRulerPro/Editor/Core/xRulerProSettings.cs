using UnityEngine;

#if UNITY_EDITOR
namespace SuperHorizon.xRulerPro
{
    /// <summary>
    /// Settings data for xRuler Pro tool. Auto-created in Resources folder.
    /// </summary>
    public class xRulerProSettings : ScriptableObject
    {
        private static xRulerProSettings _instance;
        
        public static xRulerProSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<xRulerProSettings>("xRulerProSettings");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        _instance = CreateInstance<xRulerProSettings>();
                        
                        // Create Resources folder if it doesn't exist
                        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                        {
                            UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
                        }
                        
                        UnityEditor.AssetDatabase.CreateAsset(_instance, "Assets/Resources/xRulerProSettings.asset");
                        UnityEditor.AssetDatabase.SaveAssets();
                    }
#endif
                }
                return _instance;
            }
        }

        [Header("Unit Settings")]
        public UnitType currentUnit = UnitType.Meters;

        [Header("Visual Settings")]
        public Color xAxisColor = Color.red;
        public Color yAxisColor = Color.green;
        public Color zAxisColor = Color.blue;
        public Color lineColor = Color.yellow;
        public float lineThickness = 2f;
        public float textSize = 12f;

        [Header("Reference Object")]
        public float referenceObjectSize = 1f;
        public Color referenceObjectColor = new Color(1f, 1f, 1f, 0.3f);

        [Header("Behavior")]
        public bool enableMeasurementMode = true;
    }

    public enum UnitType
    {
        Millimeters,
        Centimeters,
        Meters,
        Kilometers,
        Feet,
        Inches
    }
}
#endif