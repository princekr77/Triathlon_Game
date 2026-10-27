using UnityEngine;

#if UNITY_EDITOR
namespace SuperHorizon.xRulerPro
{
    /// <summary>
    /// Lightweight data structure for storing measurement points.
    /// Uses struct to avoid allocations.
    /// </summary>
    public struct MeasurementPoint
    {
        public Vector3 position;
        public bool isValid;

        public MeasurementPoint(Vector3 pos)
        {
            position = pos;
            isValid = true;
        }

        public static MeasurementPoint Invalid => new MeasurementPoint { isValid = false };
    }

    /// <summary>
    /// Data for active measurement session.
    /// </summary>
    public class MeasurementSession
    {
        public MeasurementPoint pointA;
        public MeasurementPoint pointB;
        public Vector3 currentMousePosition;
        public bool hasFirstPoint;
        public bool hasSecondPoint;

        public void Reset()
        {
            pointA = MeasurementPoint.Invalid;
            pointB = MeasurementPoint.Invalid;
            hasFirstPoint = false;
            hasSecondPoint = false;
        }

        public float GetDistance()
        {
            if (hasFirstPoint && pointA.isValid)
            {
                Vector3 endPoint = hasSecondPoint ? pointB.position : currentMousePosition;
                return Vector3.Distance(pointA.position, endPoint);
            }
            return 0f;
        }
    }

    /// <summary>
    /// Bounding box measurement data.
    /// </summary>
    public struct BoundsMeasurement
    {
        public Bounds bounds;
        public bool isValid;

        public BoundsMeasurement(Bounds b)
        {
            bounds = b;
            isValid = true;
        }

        public static BoundsMeasurement Invalid => new BoundsMeasurement { isValid = false };
    }
}
#endif