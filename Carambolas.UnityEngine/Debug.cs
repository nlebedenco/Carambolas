using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

using UnityDebug = UnityEngine.Debug;

// TODO: clean up coding style

namespace Carambolas.UnityEngine
{
    public enum LogLevel
    {
        Default = 0,
        Error,
        Assert,
        Warning,
        Info
    }

    public abstract class Debug: UnityDebug
    {
#pragma warning disable IDE1006

        private static LogLevel _logLevel;
        public static LogLevel logLevel
        {
            get
            {
                if (_logLevel != LogLevel.Default)
                {
                    // Make sure _logLevel remains consistent in case Debug.unityLogger.filterLogType
                    // has been changed directly
                    switch (Debug.unityLogger.filterLogType)
                    {
                        case LogType.Error:
                            if (_logLevel != LogLevel.Error)
                                _logLevel = LogLevel.Error;
                            break;
                        case LogType.Assert:
                            if (_logLevel != LogLevel.Assert)
                                _logLevel = LogLevel.Assert;
                            break;
                        case LogType.Warning:
                            if (_logLevel != LogLevel.Warning)
                                _logLevel = LogLevel.Warning;
                            break;
                        case LogType.Log:
                            if (_logLevel != LogLevel.Info)
                                _logLevel = LogLevel.Info;
                            break;
                        case LogType.Exception:
                            _logLevel = LogLevel.Info;
                            break;
                        default:
                            break;
                    }
                }

                return _logLevel;
            }

            set
            {
                if (_logLevel != value)
                {
                    switch (value)
                    {
                        case LogLevel.Default:
                            Debug.unityLogger.filterLogType = Debug.isDebugBuild ? LogType.Log : LogType.Warning;
                            break;
                        case LogLevel.Error:
                            Debug.unityLogger.filterLogType = LogType.Error;
                            break;
                        case LogLevel.Assert:
                            Debug.unityLogger.filterLogType = LogType.Assert;
                            break;
                        case LogLevel.Warning:
                            Debug.unityLogger.filterLogType = LogType.Warning;
                            break;
                        case LogLevel.Info:
                            Debug.unityLogger.filterLogType = LogType.Log;
                            break;
                        default:
                            Debug.LogErrorFormat("Ignored invalid value assigned to {0}: {1}{2}", nameof(Debug), nameof(Debug.logLevel), value);
                            break;
                    }

                    _logLevel = value;
                }
            }
        }

#pragma warning restore IDE1006

        private static Material _material;
        private static Material material
        {
            get
            {
                if (_material == null)
                {
                    _material = new Material(Shader.Find("Standard"));
                    // Don't set the hide flags or we get odd behavior like multiple
                    // material renders at the same time for the same object
                    // material.hideFlags = HideFlags.HideAndDontSave;
                }

                return _material;
            }
        }

        private static MaterialPropertyBlock _materialBlock;
        private static MaterialPropertyBlock materialBlock
        {
            get
            {
                if (_materialBlock == null)
                    _materialBlock = new MaterialPropertyBlock();

                return _materialBlock;
            }
        }

        private static Mesh _disc;
        private static Mesh disc
        {
            get
            {
                if (_disc == null)
                    _disc = (new Geometry.Disc()).CreateMesh();
                return _disc;
            }
        }

        private static Mesh _tetrahedron;
        private static Mesh tetrahedron
        {
            get
            {
                if (_tetrahedron == null)
                    _tetrahedron = (new Geometry.Tetrahedron()).CreateMesh();
                return _tetrahedron;
            }
        }

        private static Mesh _cube;
        private static Mesh cube
        {
            get
            {
                if (_cube == null)
                    _cube = (new Geometry.Cube()).CreateMesh();
                return _cube;
            }
        }

        private static Mesh _octahedron;
        private static Mesh octahedron
        {
            get
            {
                if (_octahedron == null)
                    _octahedron = (new Geometry.Octahedron()).CreateMesh();
                return _octahedron;
            }
        }

        private static Mesh _dodecahedron;
        private static Mesh dodecahedron
        {
            get
            {
                if (_dodecahedron == null)
                    _dodecahedron = (new Geometry.Dodecahedron()).CreateMesh();
                return _dodecahedron;
            }
        }

        private static Mesh _sphere;
        private static Mesh sphere
        {
            get
            {
                if (_sphere == null)
                    _sphere = (new Geometry.IcoSphere()).CreateMesh();
                return _sphere;
            }
        }

        private static Mesh _bone;
        private static Mesh bone
        {
            get
            {
                if (_bone == null)
                    _bone = (new Geometry.Bone()).CreateMesh();
                return _bone;
            }
        }

        private static Mesh _cone;
        private static Mesh cone
        {
            get
            {
                if (_cone == null)
                    _cone = (new Geometry.Cone()).CreateMesh();
                return _cone;
            }
        }

        /// <summary>
        /// Draw a series of connected lines optionally closing to loop to form a polygon.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawLines(List<Vector3> points, Color color, float duration = 0, bool depthTest = true, bool closedLoop = false)
        {
            var count = points.Count;
            for (int i = 1; i < count; ++i)
                DrawLine(points[i - 1], points[i], color, duration, depthTest);

            if (closedLoop && count > 1)
                DrawLine(points[count - 1], points[0], color, duration, depthTest);
        }

        /// <summary>
        /// Draw point
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawPoint(Vector3 position, Color color, float scale = 0.2f, float duration = 0, bool depthTest = true)
        {
            DrawWireSphere(position, scale / 8f, color, duration, depthTest, 4);
            DrawRay(position + (Vector3.up * (scale * 0.5f)), -Vector3.up * scale, color, duration, depthTest);
            DrawRay(position + (Vector3.right * (scale * 0.5f)), -Vector3.right * scale, color, duration, depthTest);
            DrawRay(position + (Vector3.forward * (scale * 0.5f)), -Vector3.forward * scale, color, duration, depthTest);
        }

        /// <summary>
        /// Draw an axis-aligned bounding box.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawWireCube(Bounds bounds, Color color, float duration = 0, bool depthtest = true)
        {
            Vector3 center = bounds.center;

            float x = bounds.extents.x;
            float y = bounds.extents.y;
            float z = bounds.extents.z;

            Vector3 ruf = center + new Vector3(x, y, z);
            Vector3 rub = center + new Vector3(x, y, -z);
            Vector3 luf = center + new Vector3(-x, y, z);
            Vector3 lub = center + new Vector3(-x, y, -z);

            Vector3 rdf = center + new Vector3(x, -y, z);
            Vector3 rdb = center + new Vector3(x, -y, -z);
            Vector3 lfd = center + new Vector3(-x, -y, z);
            Vector3 lbd = center + new Vector3(-x, -y, -z);

            DrawLine(ruf, luf, color, duration, depthtest);
            DrawLine(ruf, rub, color, duration, depthtest);
            DrawLine(luf, lub, color, duration, depthtest);
            DrawLine(rub, lub, color, duration, depthtest);

            DrawLine(ruf, rdf, color, duration, depthtest);
            DrawLine(rub, rdb, color, duration, depthtest);
            DrawLine(luf, lfd, color, duration, depthtest);
            DrawLine(lub, lbd, color, duration, depthtest);

            DrawLine(rdf, lfd, color, duration, depthtest);
            DrawLine(rdf, rdb, color, duration, depthtest);
            DrawLine(lfd, lbd, color, duration, depthtest);
            DrawLine(lbd, rdb, color, duration, depthtest);
        }

        /// <summary>
        /// Draw a box
        /// </summary>
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawWireCube(Vector3 center, Vector3 size, Color color, float duration = 0, bool depthTest = true)
            => DrawWireCube(new Bounds(center, size), color, duration, depthTest);

        /// <summary>
        /// Draw a box in a transform local space
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawWireCube(Transform transform, Vector3 center, Vector3 size, Color color, float duration = 0, bool depthTest = true)
        {
            Vector3 lbb = transform.TransformPoint(center + ((-size) * 0.5f));
            Vector3 rbb = transform.TransformPoint(center + (new Vector3(size.x, -size.y, -size.z) * 0.5f));

            Vector3 lbf = transform.TransformPoint(center + (new Vector3(size.x, -size.y, size.z) * 0.5f));
            Vector3 rbf = transform.TransformPoint(center + (new Vector3(-size.x, -size.y, size.z) * 0.5f));

            Vector3 lub = transform.TransformPoint(center + (new Vector3(-size.x, size.y, -size.z) * 0.5f));
            Vector3 rub = transform.TransformPoint(center + (new Vector3(size.x, size.y, -size.z) * 0.5f));

            Vector3 luf = transform.TransformPoint(center + ((size) * 0.5f));
            Vector3 ruf = transform.TransformPoint(center + (new Vector3(-size.x, size.y, size.z) * 0.5f));

            DrawLine(lbb, rbb, color, duration, depthTest);
            DrawLine(rbb, lbf, color, duration, depthTest);
            DrawLine(lbf, rbf, color, duration, depthTest);
            DrawLine(rbf, lbb, color, duration, depthTest);

            DrawLine(lub, rub, color, duration, depthTest);
            DrawLine(rub, luf, color, duration, depthTest);
            DrawLine(luf, ruf, color, duration, depthTest);
            DrawLine(ruf, lub, color, duration, depthTest);

            DrawLine(lbb, lub, color, duration, depthTest);
            DrawLine(rbb, rub, color, duration, depthTest);
            DrawLine(lbf, luf, color, duration, depthTest);
            DrawLine(rbf, ruf, color, duration, depthTest);
        }

        /// <summary>
        /// Draw a box give a space defined by a transformation matrix
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawWireCube(Matrix4x4 space, Vector3 center, Vector3 size, Color color, float duration = 0, bool depthTest = true)
        {
            Vector3 lbb = space.MultiplyPoint3x4(center + ((-size) * 0.5f));
            Vector3 rbb = space.MultiplyPoint3x4(center + (new Vector3(size.x, -size.y, -size.z) * 0.5f));

            Vector3 lbf = space.MultiplyPoint3x4(center + (new Vector3(size.x, -size.y, size.z) * 0.5f));
            Vector3 rbf = space.MultiplyPoint3x4(center + (new Vector3(-size.x, -size.y, size.z) * 0.5f));

            Vector3 lub = space.MultiplyPoint3x4(center + (new Vector3(-size.x, size.y, -size.z) * 0.5f));
            Vector3 rub = space.MultiplyPoint3x4(center + (new Vector3(size.x, size.y, -size.z) * 0.5f));

            Vector3 luf = space.MultiplyPoint3x4(center + ((size) * 0.5f));
            Vector3 ruf = space.MultiplyPoint3x4(center + (new Vector3(-size.x, size.y, size.z) * 0.5f));

            DrawLine(lbb, rbb, color, duration, depthTest);
            DrawLine(rbb, lbf, color, duration, depthTest);
            DrawLine(lbf, rbf, color, duration, depthTest);
            DrawLine(rbf, lbb, color, duration, depthTest);

            DrawLine(lub, rub, color, duration, depthTest);
            DrawLine(rub, luf, color, duration, depthTest);
            DrawLine(luf, ruf, color, duration, depthTest);
            DrawLine(ruf, lub, color, duration, depthTest);

            DrawLine(lbb, lub, color, duration, depthTest);
            DrawLine(rbb, rub, color, duration, depthTest);
            DrawLine(lbf, luf, color, duration, depthTest);
            DrawLine(rbf, ruf, color, duration, depthTest);
        }

        /// <summary>
        /// Draw a box in a space defined by a translation, rotation and scale
        /// </summary>
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawWireCube(Vector3 translation, Quaternion rotation, Vector3 scale, Vector3 center, Vector3 size, Color color, float duration = 0, bool depthTest = true)
            => DrawWireCube(Matrix4x4.TRS(translation, rotation, scale), center, size, color, duration, depthTest);

        /// <summary>
        /// Draw a horizontal circle following the given rotation.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawCircle(Vector3 origin, float radius, Quaternion rotation, Color color, float duration = 0, bool depthTest = true, int segments = 36)
        {
            // Need at least 4 segments
            segments = Mathf.Max(segments, 4);
            radius = Mathf.Max(0f, radius);

            Matrix4x4 matrix = Matrix4x4.TRS(origin, rotation, Vector3.one);
            Vector3 circleXAxis = matrix * Vector3.right;
            Vector3 circleZAxis = matrix * Vector3.forward;

            float angleStep = 2f * Mathf.PI / segments;
            float angle = 0f;
            Vector3 from;
            Vector3 to = origin + radius * (circleXAxis * Mathf.Cos(angle) + circleZAxis * Mathf.Sin(angle));
            for (; segments > 0; --segments)
            {
                from = to;
                angle += angleStep;
                to = origin + radius * (circleXAxis * Mathf.Cos(angle) + circleZAxis * Mathf.Sin(angle));
                DrawLine(from, to, color, duration, depthTest);
            }
        }

        /// <summary>
        /// Draw a horizontal circle around the axis defined by <paramref name="upwards"/>
        /// </summary>
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawCircle(Vector3 origin, float radius, Vector3 upwards, Color color, float duration = 0, bool depthTest = true, int segments = 36)
            => DrawCircle(origin, radius, Quaternion.FromToRotation(Vector3.up, upwards), color, duration, depthTest, segments);

        /// <summary>
        /// Draw a wire sphere with an arbitrary number of segments in each axial plane.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawWireSphere(Vector3 origin, float radius, Color color, float duration = 0, bool depthTest = true, int segments = 12)
        {
            // Need at least 4 segments
            segments = Mathf.Max(segments, 4);
            radius = Mathf.Max(0f, radius);

            float angleInc = 2f * Mathf.PI / segments;
            float latitude = angleInc;
            float sinY1 = 0.0f, cosY1 = 1.0f;

            for (int i = segments; i > 0; --i)
            {
                float sinY2 = Mathf.Sin(latitude);
                float cosY2 = Mathf.Cos(latitude);

                Vector3 v1 = new Vector3(sinY1, cosY1, 0.0f) * radius + origin;
                Vector3 v3 = new Vector3(sinY2, cosY2, 0.0f) * radius + origin;
                float longitude = angleInc;

                for (int j = segments; j > 0; --j)
                {
                    float sinX = Mathf.Sin(longitude);
                    float cosX = Mathf.Cos(longitude);

                    Vector3 v2 = new Vector3((cosX * sinY1), cosY1, (sinX * sinY1)) * radius + origin;
                    Vector3 v4 = new Vector3((cosX * sinY2), cosY2, (sinX * sinY2)) * radius + origin;

                    DrawLine(v1, v2, color, duration, depthTest);
                    DrawLine(v1, v3, color, duration, depthTest);

                    v1 = v2;
                    v3 = v4;
                    longitude += angleInc;
                }

                sinY1 = sinY2;
                cosY1 = cosY2;
                latitude += angleInc;
            }
        }

        /// <summary>
        /// Draw a debug capsule
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawWireCapsule(Vector3 start, Vector3 end, float radius, Color color, float duration = 0, bool depthTest = true)
        {
            radius = Mathf.Max(0f, radius);

            Vector3 direction = (end - start).normalized;
            Quaternion rotation = (direction.sqrMagnitude == 0f ? Quaternion.identity : Quaternion.LookRotation(direction, Vector3.up));

            start += direction * radius;
            end -= direction * radius;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            DrawArc(start, forward, up, 360f, radius, color, duration, depthTest);
            DrawArc(start, up, right, 180f, radius, color, duration, depthTest);
            DrawArc(start, right, -up, 180f, radius, color, duration, depthTest);

            DrawArc(end, forward, up, 360f, radius, color, duration, depthTest);
            DrawArc(end, up, -right, 180f, radius, color, duration, depthTest);
            DrawArc(end, right, up, 180f, radius, color, duration, depthTest);

            DrawLine(start + (right * radius), end + (right * radius), color, duration, depthTest);
            DrawLine(start + (-right * radius), end + (-right * radius), color, duration, depthTest);
            DrawLine(start + (up * radius), end + (up * radius), color, duration, depthTest);
            DrawLine(start + (-up * radius), end + (-up * radius), color, duration, depthTest);
        }

        /// <summary>
        /// Draw a horizontal arc following a given rotation with a central angle in in degrees.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawArc(Vector3 origin, Quaternion rotation, float radius, float centralAngle, Color color, float duration = 0, bool depthTest = true, int segments = 36)
        {
            // Need at least 4 segments
            segments = Mathf.Max(segments, 4);
            centralAngle = Mathf.Clamp(centralAngle, 0f, 360f);

            float angleStep = centralAngle * Mathf.Deg2Rad / segments;

            Matrix4x4 matrix = Matrix4x4.TRS(origin, rotation, Vector3.one);

            float angle = 0f;
            Vector3 from;
            Vector3 to = origin + radius * (Vector3.right * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle));
            for (; segments > 0; --segments)
            {
                from = to;
                angle += angleStep;
                to = origin + radius * (Vector3.right * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle));
                DrawLine(matrix.MultiplyPoint3x4(from), matrix.MultiplyPoint3x4(to), color, duration, depthTest);
            }
        }

        /// <summary>
        /// Draw a horizontal arc starting at <paramref name="fromDirection"/> around the axis defined by <paramref name="upwards"/>. Central angle is in degrees.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawArc(Vector3 origin, Vector3 upwards, Vector3 fromDirection, float radius, float centralAngle, Color color, float duration = 0f, bool depthTest = true, int segments = 36)
        {
            // Need at least 2 segments
            segments = Mathf.Max(segments, 2);
            centralAngle = Mathf.Clamp(centralAngle, 0f, 360f);

            float angleStep = centralAngle / segments;

            Quaternion rotation = Quaternion.AngleAxis(angleStep, upwards);
            Vector3 v = fromDirection.normalized * radius;

            Vector3 from;
            Vector3 to = origin + v;
            for (; segments > 0; --segments)
            {
                from = to;
                to = origin + rotation * v;
                DrawLine(from, to, color, duration, depthTest);
            }
        }

        [Conditional("DEBUG")]
        public static void DrawWireCone(Vector3 origin, Quaternion rotation, float length, float angleWidth, float angleHeight, Color color, float duration = 0, bool depthTest = true, int segments = 36, bool connectedToCenter = true)
        {
            // Need at least 4 sides
            segments = Mathf.Max(segments, 4);
            angleWidth = Mathf.Clamp(angleWidth, 0f, 360f) * 0.5f;
            angleHeight = Mathf.Clamp(angleHeight, 0f, 360f) * 0.5f;

            var angle1 = Mathf.Clamp(angleHeight * Mathf.Deg2Rad, 1e-4f, Mathf.PI - 1e-4f);
            var angle2 = Mathf.Clamp(angleWidth * Mathf.Deg2Rad, 1e-4f, Mathf.PI - 1e-4f);
            var sinZ_2 = Mathf.Sin(0.5f * angle1);
            var sinX_2 = Mathf.Sin(0.5f * angle2);
            var sinSqZ_2 = sinZ_2 * sinZ_2;
            var sinSqX_2 = sinX_2 * sinX_2;

            // Cone verts must be transformed based on the center and rotation
            var matrix = Matrix4x4.TRS(origin, rotation, Vector3.one);

            var firstPoint = default(Vector3);
            var currentPoint = default(Vector3);
            var prevPoint = default(Vector3);
            for (int i = 0; i < segments; i++)
            {
                var fraction = ((float)i) / segments;
                var thi = 2f * Mathf.PI * fraction;
                var phi = Mathf.Atan2(Mathf.Sin(thi) * sinX_2, Mathf.Cos(thi) * sinZ_2);
                var sinPhi = Mathf.Sin(phi);
                var cosPhi = Mathf.Cos(phi);
                var sinSqPhi = sinPhi * sinPhi;
                var cosSqPhi = cosPhi * cosPhi;

                var rSq = sinSqZ_2 * sinSqX_2 / (sinSqZ_2 * sinSqPhi + sinSqX_2 * cosSqPhi);
                var r = Mathf.Sqrt(rSq);
                var sqr = Mathf.Sqrt(1 - rSq);
                var alpha = r * cosPhi;
                var beta = r * sinPhi;

                var coneVert = new Vector3(
                    2 * sqr * beta,
                    2 * sqr * alpha,
                    1 - 2 * rSq + length
                );

                currentPoint = matrix.MultiplyPoint3x4(coneVert);
                if (connectedToCenter)
                    DrawLine(origin, currentPoint, color, duration, depthTest);

                if (i > 0)
                    DrawLine(prevPoint, currentPoint, color, duration, depthTest);
                else
                    firstPoint = currentPoint;

                prevPoint = currentPoint;
            }

            if (connectedToCenter)
                DrawLine(currentPoint, firstPoint, color, duration, depthTest);
        }

        [Conditional("DEBUG")]
        public static void DrawArrow(Vector3 start, Vector3 end, Color color, float duration = 0, bool depthTest = true, float sqrArrowLength = 0.025f)
        {
            sqrArrowLength = Mathf.Max(sqrArrowLength, 0f);

            DrawLine(start, end, color, duration, depthTest);
            var forward = end - start;
            forward.Normalize();
            forward.FindOrthogonals(out Vector3 lUp, out Vector3 lRight);

            var position = new Vector4(end.x, end.y, end.z, 1f);
            var space = new Matrix4x4(lRight, lUp, forward, position);
            float arrowLength = Mathf.Sqrt(sqrArrowLength);
            DrawLine(end, space.MultiplyPoint3x4(new Vector3(arrowLength, 0f, -arrowLength)), color, duration, depthTest);
            DrawLine(end, space.MultiplyPoint3x4(new Vector3(-arrowLength, 0f, -arrowLength)), color, duration, depthTest);
        }

        /// <summary>
        /// Draw a solid line
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawLine(Vector3 start, Vector3 end, float thickness, Color color)
        {
            var position = new Vector3(start.x + ((end.x - start.x) / 2f), start.y + ((end.y - start.y) / 2f), start.z + ((end.z - start.z) / 2f));
            var rotation = Quaternion.FromToRotation(Vector3.right, (end - start).normalized);
            var size = new Vector3(Vector3.Distance(start, end), thickness, thickness);
            var matrix = Matrix4x4.TRS(position, rotation, size);

            var emission = color * 0.5f;
            emission.a = color.a;

            materialBlock.Clear();
            materialBlock.SetColor("_Color", color);
            materialBlock.SetColor("_Emission", emission);

            Graphics.DrawMesh(cube, matrix, material, 0, null, 0, materialBlock);
        }

        /// <summary>
        /// Draws a solid tetrahedron (a piramid with 4 faces)
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawTetrahedron(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            var matrix = Matrix4x4.TRS(position, rotation, scale);

            var emission = color * 0.5f;
            emission.a = color.a;

            materialBlock.Clear();

            materialBlock.SetColor("_Color", color);
            materialBlock.SetColor("_Emission", emission);

            Graphics.DrawMesh(tetrahedron, matrix, material, 0, null, 0, materialBlock);
        }

        /// <summary>
        /// Draw a solid box
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawCube(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            var lMatrix = Matrix4x4.TRS(position, rotation, scale);

            var emission = color * 0.5f;
            emission.a = color.a;

            materialBlock.Clear();
            materialBlock.SetColor("_Color", color);
            materialBlock.SetColor("_Emission", emission);

            Graphics.DrawMesh(cube, lMatrix, material, 0, null, 0, materialBlock);
        }

        /// <summary>
        /// Draws a solid Octahedron
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawOctahedron(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            var matrix = Matrix4x4.TRS(position, rotation, scale);

            var emission = color * 0.5f;
            emission.a = color.a;

            materialBlock.Clear();
            materialBlock.SetColor("_Color", color);
            materialBlock.SetColor("_Emission", emission);

            Graphics.DrawMesh(octahedron, matrix, material, 0, null, 0, materialBlock);
        }

        /// <summary>
        /// Draw a solid dodecahedron
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawDodecahedron(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            var matrix = Matrix4x4.TRS(position, rotation, scale);

            var emission = color * 0.5f;
            emission.a = color.a;

            materialBlock.Clear();
            materialBlock.SetColor("_Color", color);
            materialBlock.SetColor("_Emission", emission);

            Graphics.DrawMesh(dodecahedron, matrix, material, 0, null, 0, materialBlock);
        }

        /// <summary>
        /// Draw a solid sphere
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawSphere(Vector3 position, Vector3 scale, Color color)
        {
            var matrix = Matrix4x4.TRS(position, Quaternion.identity, scale);

            var emission = color * 0.5f;
            emission.a = color.a;

            materialBlock.Clear();
            materialBlock.SetColor("_Color", color);
            materialBlock.SetColor("_Emission", emission);

            Graphics.DrawMesh(sphere, matrix, material, 0, null, 0, materialBlock);
        }

        /// <summary>
        /// Draws an actual cube mesh
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawDisc(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            var matrix = Matrix4x4.TRS(position, rotation, scale);

            var emission = color * 0.5f;
            emission.a = color.a;

            materialBlock.Clear();
            materialBlock.SetColor("_Color", color);
            materialBlock.SetColor("_Emission", emission);

            Graphics.DrawMesh(disc, matrix, material, 0, null, 0, materialBlock);
        }

        /// <summary>
        /// Draw a bone
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawBone(Transform transform, Color color)
        {
            if (transform == null)
                return;

            float size = 0.02f;

            // Always draw at least one bone
            int childCount = Mathf.Max(transform.childCount, 1);
            for (int i = 0; i < childCount; i++)
            {
                var rotation = transform.rotation;

                // If we have a child, we can use it to determine the length
                if (transform.childCount > i)
                {
                    var childBone = transform.GetChild(i);
                    size = Vector3.Distance(transform.position, childBone.position);
                    rotation = Quaternion.FromToRotation(Vector3.up, childBone.position - transform.position);
                }

                // Render the bone position
                var matrix = Matrix4x4.TRS(transform.position, rotation, size * Vector3.one);

                var emission = color * 0.5f;
                emission.a = color.a;

                materialBlock.Clear();
                materialBlock.SetColor("_Color", color);
                materialBlock.SetColor("_Emission", emission);

                Graphics.DrawMesh(bone, matrix, material, 0, null, 0, materialBlock);
            }
        }

        /// <summary>
        /// Draws a solid skeleton
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawSkeleton(Transform rootTransform, Color color)
        {
            if (rootTransform == null)
                return;

            DrawBone(rootTransform, color);

            for (int i = 0; i < rootTransform.childCount; i++)
                DrawSkeleton(rootTransform.GetChild(i), color);
        }

        /// <summary>
        /// Draw the full skeleton with some bones being colored with a specific color
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawSkeleton(Transform rootTransform, Color color, bool drawAxis, List<Transform> selectedBones, Color selectedColor)
        {
            if (rootTransform == null)
                return;

            DrawBone(rootTransform, (selectedBones != null && selectedBones.IndexOf(rootTransform) >= 0) ? selectedColor : color);

            for (int i = 0; i < rootTransform.childCount; i++)
                DrawSkeleton(rootTransform.GetChild(i), color, drawAxis, selectedBones, selectedColor);
        }

        /// <summary>
        /// Draws the full skeleton
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawHumanoidSkeleton(GameObject gameObject, Color color)
        {
            var animator = gameObject.GetComponent<Animator>();
            if (animator == null)
                return;

            var unityBones = System.Enum.GetNames(typeof(HumanBodyBones));

            for (int i = 0; i < unityBones.Length; i++)
            {
                Transform boneTransform = animator.GetBoneTransform((HumanBodyBones)i);
                if (boneTransform != null)
                    DrawBone(boneTransform, color);
            }
        }

        /// <summary>
        /// Renders out a transform object so we can see where an object is positioned an how it's oriented.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawTransform(Transform transform, float scale, float thickness = 0.002f)
        {
            Vector3 position = transform.position;
            Quaternion rotation = transform.rotation;
            DrawTransform(position, rotation, scale, thickness);
        }

        /// <summary>
        /// Renders out a transform object so we can see where an object is positioned an how it's oriented.
        /// </summary>
        [Conditional("DEBUG")]
        public static void DrawTransform(Vector3 position, Quaternion rotation, float scale, float thickness = 0.002f)
        {
            DrawLine(position, position + (rotation * Vector3.right * scale), Color.red, thickness);
            DrawLine(position, position + (rotation * Vector3.up * scale), Color.green, thickness);
            DrawLine(position, position + (rotation * Vector3.forward * scale), Color.blue, thickness);
        }

        internal static class Geometry
        {
            internal abstract class Shape
            {
                public Vector3[] Vertices;
                public Vector2[] UVs;
                public int[] Triangles;

                public virtual Mesh CreateMesh()
                {
                    var mesh = new Mesh
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        vertices = Vertices,
                        triangles = Triangles
                    };
                    if (UVs != null)
                        mesh.uv = UVs;

                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    return mesh;
                }
            }

            /// <summary>
            /// Support class for a 4 sided polygon
            /// </summary>
            internal class Tetrahedron: Shape
            {
                public Tetrahedron()
                {
                    Vertices = CreateVertices();
                    Triangles = CreateTriangles();

                    // We want the edges of the polygon to look crisp. So,
                    // we're going to create individual vertices for each index
                    Vector3[] newVertices = new Vector3[Triangles.Length];
                    for (int i = 0; i < Triangles.Length; i++)
                    {
                        newVertices[i] = Vertices[Triangles[i]];
                        Triangles[i] = i;
                    }

                    Vertices = newVertices;
                }

                private Vector3[] CreateVertices()
                {
                    int stride = 3;

                    float[] verticesFloat = new float[] { -0.352500f, -0.498510f, -0.610548f, -0.352500f, -0.498510f, 0.610548f, 0.705000f, -0.498510f, -0.000000f, 0.000000f, 0.498510f, 0.000000f };

                    Vector3[] vertices = new Vector3[verticesFloat.Length / stride];
                    for (int i = 0; i < verticesFloat.Length; i += stride)
                    {
                        vertices[i / stride] = new Vector3(verticesFloat[i], verticesFloat[i + 1], verticesFloat[i + 2]);
                    }

                    return vertices;
                }

                private int[] CreateTriangles()
                {
                    return new int[] { 2, 1, 0, 2, 3, 1, 3, 2, 0, 1, 3, 0 };
                }
            }

            /// <summary>
            /// Support class for a 6 sided polygon
            /// </summary>
            internal class Cube: Shape
            {
                public Cube()
                {
                    Vertices = CreateVertices();
                    Triangles = CreateTriangles();

                    // We want the edges of the polygon to look crisp. So,
                    // we're going to create individual vertices for each index
                    Vector3[] newVertices = new Vector3[Triangles.Length];
                    for (int i = 0; i < Triangles.Length; i++)
                    {
                        newVertices[i] = Vertices[Triangles[i]];
                        Triangles[i] = i;
                    }

                    Vertices = newVertices;
                }

                private Vector3[] CreateVertices()
                {
                    int stride = 3;

                    float[] verticesFloat = new float[] { -0.500000f, -0.500000f, 0.500000f, 0.500000f, -0.500000f, 0.500000f, -0.500000f, 0.500000f, 0.500000f, 0.500000f, 0.500000f, 0.500000f, -0.500000f, 0.500000f, -0.500000f, 0.500000f, 0.500000f, -0.500000f, -0.500000f, -0.500000f, -0.500000f, 0.500000f, -0.500000f, -0.500000f };

                    Vector3[] vertices = new Vector3[verticesFloat.Length / stride];
                    for (int i = 0; i < verticesFloat.Length; i += stride)
                    {
                        vertices[i / stride] = new Vector3(verticesFloat[i], verticesFloat[i + 1], verticesFloat[i + 2]);
                    }

                    return vertices;
                }

                private int[] CreateTriangles()
                {
                    return new int[] { 3, 2, 0, 3, 0, 1, 3, 5, 2, 2, 5, 4, 7, 6, 4, 7, 4, 5, 1, 0, 6, 1, 6, 7, 3, 1, 5, 1, 7, 5, 2, 6, 0, 6, 2, 4 };
                }
            }

            /// <summary>
            /// Support class for a 8 sided polygon
            /// </summary>
            internal class Octahedron: Shape
            {
                public Octahedron()
                {
                    Vertices = CreateVertices();
                    Triangles = CreateTriangles();

                    // We want the edges of the polygon to look crisp. So,
                    // we're going to create individual vertices for each index
                    Vector3[] newVertices = new Vector3[Triangles.Length];
                    for (int i = 0; i < Triangles.Length; i++)
                    {
                        newVertices[i] = Vertices[Triangles[i]];
                        Triangles[i] = i;
                    }

                    Vertices = newVertices;
                }

                private Vector3[] CreateVertices()
                {
                    int stride = 3;

                    float[] verticesFloat = new float[] { 0.000000f, 0.500000f, 0.000000f, 0.500000f, 0.000000f, 0.000000f, 0.000000f, 0.000000f, -0.500000f, -0.500000f, 0.000000f, 0.000000f, 0.000000f, -0.000000f, 0.500000f, 0.000000f, -0.500000f, -0.000000f };

                    Vector3[] vertices = new Vector3[verticesFloat.Length / stride];
                    for (int i = 0; i < verticesFloat.Length; i += stride)
                    {
                        vertices[i / stride] = new Vector3(verticesFloat[i], verticesFloat[i + 1], verticesFloat[i + 2]);
                    }

                    return vertices;
                }

                private int[] CreateTriangles()
                {
                    return new int[] { 1, 2, 0, 2, 3, 0, 3, 4, 0, 0, 4, 1, 5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4 };
                }
            }

            /// <summary>
            /// Support class for a 12 sided polygon
            /// </summary>
            internal class Dodecahedron: Shape
            {
                public Dodecahedron()
                {
                    Vertices = CreateVertices();
                    Triangles = CreateTriangles();

                    // We want the edges of the polygon to look crisp. So,
                    // we're going to create individual vertices for each index
                    Vector3[] newVertices = new Vector3[Triangles.Length];
                    for (int i = 0; i < Triangles.Length; i++)
                    {
                        newVertices[i] = Vertices[Triangles[i]];
                        Triangles[i] = i;
                    }

                    Vertices = newVertices;
                }

                private Vector3[] CreateVertices()
                {
                    int stride = 3;

                    float[] verticesFloat = new float[] { 0.351283f, -0.499921f, -0.000000f, 0.595112f, -0.138430f, 0.000000f, 0.180745f, -0.121914f, -0.570779f, 0.489714f, 0.095191f, -0.352761f, 0.095191f, -0.489714f, -0.352761f, 0.180745f, -0.121914f, 0.570779f, 0.095191f, -0.489714f, 0.352761f, 0.489714f, 0.095191f, 0.352761f, -0.595112f, 0.138430f, -0.000000f, -0.351283f, 0.499921f, 0.000000f, -0.180745f, 0.121914f, 0.570779f, -0.489714f, -0.095191f, 0.352761f, -0.095191f, 0.489714f, 0.352761f, -0.180745f, 0.121914f, -0.570779f, -0.095191f, 0.489714f, -0.352761f, -0.489714f, -0.095191f, -0.352761f, -0.319176f, -0.473197f, 0.218018f, 0.319176f, 0.473197f, 0.218018f, 0.319176f, 0.473197f, -0.218018f, -0.319176f, -0.473197f, -0.218018f };

                    Vector3[] vertices = new Vector3[verticesFloat.Length / stride];
                    for (int i = 0; i < verticesFloat.Length; i += stride)
                    {
                        vertices[i / stride] = new Vector3(verticesFloat[i], verticesFloat[i + 1], verticesFloat[i + 2]);
                    }

                    return vertices;
                }

                private int[] CreateTriangles()
                {
                    return new int[] { 2, 1, 4, 1, 2, 3, 4, 1, 0, 1, 5, 6, 1, 6, 0, 5, 1, 7, 0, 16, 19, 16, 0, 6, 0, 19, 4, 16, 10, 11, 10, 16, 5, 5, 16, 6, 8, 19, 16, 19, 8, 15, 16, 11, 8, 19, 2, 4, 2, 19, 13, 13, 19, 15, 13, 18, 2, 18, 13, 14, 18, 3, 2, 18, 1, 3, 1, 17, 7, 17, 1, 18, 10, 17, 12, 17, 5, 7, 5, 17, 10, 12, 8, 10, 8, 12, 9, 8, 11, 10, 9, 17, 18, 17, 9, 12, 9, 18, 14, 13, 8, 14, 8, 13, 15, 14, 8, 9 };
                }
            }

            internal class Icosahedron: Shape
            {
                public Icosahedron()
                {
                    Vertices = CreateVertices();
                    Triangles = CreateTriangles();
                }

                private Vector3[] CreateVertices()
                {
                    Vector3[] vertices = new Vector3[12];

                    float halSize = 0.5f;
                    float a = (halSize + Mathf.Sqrt(5)) / 2.0f;

                    vertices[0] = new Vector3(a, 0.0f, halSize);
                    vertices[9] = new Vector3(-a, 0.0f, halSize);
                    vertices[11] = new Vector3(-a, 0.0f, -halSize);
                    vertices[1] = new Vector3(a, 0.0f, -halSize);
                    vertices[2] = new Vector3(halSize, a, 0.0f);
                    vertices[5] = new Vector3(halSize, -a, 0.0f);
                    vertices[10] = new Vector3(-halSize, -a, 0.0f);
                    vertices[8] = new Vector3(-halSize, a, 0.0f);
                    vertices[3] = new Vector3(0.0f, halSize, a);
                    vertices[7] = new Vector3(0.0f, halSize, -a);
                    vertices[6] = new Vector3(0.0f, -halSize, -a);
                    vertices[4] = new Vector3(0.0f, -halSize, a);

                    for (int i = 0; i < 12; i++)
                        vertices[i].Normalize();

                    return vertices;
                }

                private int[] CreateTriangles()
                {
                    return new int[] {
                    1,2,0,
                    2,3,0,
                    3,4,0,
                    4,5,0,
                    5,1,0,
                    6,7,1,
                    2,1,7,
                    7,8,2,
                    2,8,3,
                    8,9,3,
                    3,9,4,
                    9,10,4,
                    10,5,4,
                    10,6,5,
                    6,1,5,
                    6,11,7,
                    7,11,8,
                    8,11,9,
                    9,11,10,
                    10,11,6,
                };
                }
            }

            /// <summary>
            /// Support class for a flat circle
            /// </summary>
            internal class Disc: Shape
            {
                public Disc()
                {
                    Vertices = CreateVertices();
                    Triangles = CreateTriangles();

                    // We want the edges of the polygon to look crisp. So,
                    // we're going to create individual vertices for each index
                    Vector3[] newVertices = new Vector3[Triangles.Length];
                    for (int i = 0; i < Triangles.Length; i++)
                    {
                        newVertices[i] = Vertices[Triangles[i]];
                        Triangles[i] = i;
                    }

                    Vertices = newVertices;
                }

                private Vector3[] CreateVertices()
                {
                    int stride = 3;

                    float[] verticesFloat = new float[] { 0.482963f, -0.001076f, -0.129409f, 0.433012f, -0.001076f, -0.250000f, 0.353553f, -0.001076f, -0.353553f, 0.250000f, -0.001076f, -0.433012f, 0.129410f, -0.001076f, -0.482963f, 0.000000f, -0.001076f, -0.500000f, -0.129409f, -0.001076f, -0.482963f, -0.250000f, -0.001076f, -0.433013f, -0.353553f, -0.001076f, -0.353553f, -0.433013f, -0.001076f, -0.250000f, -0.482963f, -0.001076f, -0.129410f, -0.500000f, -0.001076f, -0.000000f, -0.482963f, -0.001076f, 0.129409f, -0.433013f, -0.001076f, 0.250000f, -0.353553f, -0.001076f, 0.353553f, -0.250000f, -0.001076f, 0.433013f, -0.129410f, -0.001076f, 0.482963f, -0.000000f, -0.001076f, 0.500000f, 0.129409f, -0.001076f, 0.482963f, 0.250000f, -0.001076f, 0.433013f, 0.353553f, -0.001076f, 0.353553f, 0.433013f, -0.001076f, 0.250000f, 0.482963f, -0.001076f, 0.129410f, 0.500000f, -0.001076f, 0.000000f, 0.482963f, 0.001076f, -0.129409f, 0.433012f, 0.001076f, -0.250000f, 0.353553f, 0.001076f, -0.353553f, 0.250000f, 0.001076f, -0.433012f, 0.129410f, 0.001076f, -0.482963f, 0.000000f, 0.001076f, -0.500000f, -0.129409f, 0.001076f, -0.482963f, -0.250000f, 0.001076f, -0.433013f, -0.353553f, 0.001076f, -0.353553f, -0.433013f, 0.001076f, -0.250000f, -0.482963f, 0.001076f, -0.129410f, -0.500000f, 0.001076f, -0.000000f, -0.482963f, 0.001076f, 0.129409f, -0.433013f, 0.001076f, 0.250000f, -0.353553f, 0.001076f, 0.353553f, -0.250000f, 0.001076f, 0.433013f, -0.129410f, 0.001076f, 0.482963f, -0.000000f, 0.001076f, 0.500000f, 0.129409f, 0.001076f, 0.482963f, 0.250000f, 0.001076f, 0.433013f, 0.353553f, 0.001076f, 0.353553f, 0.433013f, 0.001076f, 0.250000f, 0.482963f, 0.001076f, 0.129410f, 0.500000f, 0.001076f, 0.000000f, 0.000000f, -0.001076f, 0.000000f, 0.000000f, 0.001076f, 0.000000f };

                    Vector3[] vertices = new Vector3[verticesFloat.Length / stride];
                    for (int i = 0; i < verticesFloat.Length; i += stride)
                    {
                        vertices[i / stride] = new Vector3(verticesFloat[i], verticesFloat[i + 1], verticesFloat[i + 2]);
                    }

                    return vertices;
                }

                private int[] CreateTriangles()
                {
                    int[] indexes = { 25, 0, 24, 1, 0, 2, 0, 3, 1, 4, 25, 5, 26, 6, 25, 7, 1, 8, 1, 9, 2, 10, 26, 11, 2, 12, 3, 13, 27, 14, 2, 15, 27, 16, 26, 17, 28, 18, 3, 19, 4, 20, 3, 21, 28, 22, 27, 23, 4, 24, 5, 25, 29, 26, 4, 27, 29, 28, 28, 29, 30, 30, 5, 31, 6, 32, 5, 33, 30, 34, 29, 35, 31, 36, 6, 37, 7, 38, 6, 39, 31, 40, 30, 41, 7, 42, 8, 43, 32, 44, 7, 45, 32, 46, 31, 47, 33, 48, 8, 49, 9, 50, 8, 51, 33, 52, 32, 53, 34, 54, 9, 55, 10, 56, 9, 57, 34, 58, 33, 59, 35, 60, 10, 61, 11, 62, 10, 63, 35, 64, 34, 65, 36, 66, 11, 67, 12, 68, 11, 69, 36, 70, 35, 71, 37, 72, 12, 73, 13, 74, 12, 75, 37, 76, 36, 77, 38, 78, 13, 79, 14, 80, 13, 81, 38, 82, 37, 83, 39, 84, 38, 85, 14, 86, 39, 87, 14, 88, 15, 89, 40, 90, 39, 91, 15, 92, 40, 93, 15, 94, 16, 95, 41, 96, 40, 97, 16, 98, 41, 99, 16, 100, 17, 101, 42, 102, 41, 103, 17, 104, 42, 105, 17, 106, 18, 107, 43, 108, 42, 109, 18, 110, 43, 111, 18, 112, 19, 113, 44, 114, 43, 115, 19, 116, 44, 117, 19, 118, 20, 119, 45, 120, 44, 121, 20, 122, 20, 123, 21, 124, 45, 125, 46, 126, 45, 127, 21, 128, 21, 129, 22, 130, 46, 131, 47, 132, 46, 133, 22, 134, 22, 135, 23, 136, 47, 137, 24, 138, 47, 139, 23, 140, 23, 141, 0, 142, 24, 143, 1, 144, 0, 145, 48, 146, 2, 147, 1, 148, 48, 149, 3, 150, 2, 151, 48, 152, 4, 153, 3, 154, 48, 155, 5, 156, 4, 157, 48, 158, 6, 159, 5, 160, 48, 161, 7, 162, 6, 163, 48, 164, 8, 165, 7, 166, 48, 167, 9, 168, 8, 169, 48, 170, 10, 171, 9, 172, 48, 173, 11, 174, 10, 175, 48, 176, 12, 177, 11, 178, 48, 179, 13, 180, 12, 181, 48, 182, 14, 183, 13, 184, 48, 185, 15, 186, 14, 187, 48, 188, 16, 189, 15, 190, 48, 191, 17, 192, 16, 193, 48, 194, 18, 195, 17, 196, 48, 197, 19, 198, 18, 199, 48, 200, 20, 201, 19, 202, 48, 203, 21, 204, 20, 205, 48, 206, 22, 207, 21, 208, 48, 209, 23, 210, 22, 211, 48, 212, 0, 213, 23, 214, 48, 215, 24, 216, 25, 217, 49, 218, 25, 219, 26, 220, 49, 221, 26, 222, 27, 223, 49, 224, 27, 225, 28, 226, 49, 227, 28, 228, 29, 229, 49, 230, 29, 231, 30, 232, 49, 233, 30, 234, 31, 235, 49, 236, 31, 237, 32, 238, 49, 239, 32, 240, 33, 241, 49, 242, 33, 243, 34, 244, 49, 245, 34, 246, 35, 247, 49, 248, 35, 249, 36, 250, 49, 251, 36, 252, 37, 253, 49, 254, 37, 255, 38, 256, 49, 257, 38, 258, 39, 259, 49, 260, 39, 261, 40, 262, 49, 263, 40, 264, 41, 265, 49, 266, 41, 267, 42, 268, 49, 269, 42, 270, 43, 271, 49, 272, 43, 273, 44, 274, 49, 275, 44, 276, 45, 277, 49, 278, 45, 279, 46, 280, 49, 281, 46, 282, 47, 283, 49, 284, 47, 285, 24, 286, 49, 287 };

                    int[] triangles = new int[indexes.Length / 2];
                    for (int i = 0; i < triangles.Length; i++)
                        triangles[i] = indexes[i * 2];

                    return triangles;
                }
            }

            /// <summary>
            /// Support class for a 6 sided bone
            /// </summary>
            internal class Bone: Shape
            {
                public static readonly Vector3[] BoneVertices = new Vector3[] {
                        new Vector3(0.000000f, 1.000000f,  0.000000f),    // Top
                        new Vector3(0.100000f, 0.100000f,  0.000000f),    // Mid-Right
                        new Vector3(0.000000f, 0.100000f, -0.100000f),    // Mid-Back
                        new Vector3(-0.100000f, 0.100000f,  0.000000f),   // Mid-Left
                        new Vector3(0.000000f, 0.100000f,  0.100000f),    // Mid-Forward
                        new Vector3(0.000000f, 0.000000f,  0.000000f)     // Bottom
                    };

                public Bone()
                {
                    Vertices = CreateVertices();
                    Triangles = CreateTriangles();

                    // We want the edges of the polygon to look crisp. So,
                    // we're going to create individual vertices for each index
                    Vector3[] newVertices = new Vector3[Triangles.Length];
                    for (int i = 0; i < Triangles.Length; i++)
                    {
                        newVertices[i] = Vertices[Triangles[i]];
                        Triangles[i] = i;
                    }

                    Vertices = newVertices;
                }

                private Vector3[] CreateVertices()
                {
                    int stride = 3;

                    float[] verticesFloat = new float[] {
                    BoneVertices[0].x, BoneVertices[0].y, BoneVertices[0].z, // Top
                    BoneVertices[1].x, BoneVertices[1].y, BoneVertices[1].z, // Mid-Right
                    BoneVertices[2].x, BoneVertices[2].y, BoneVertices[2].z, // Mid-Back
                    BoneVertices[3].x, BoneVertices[3].y, BoneVertices[3].z, // Mid-Left
                    BoneVertices[4].x, BoneVertices[4].y, BoneVertices[4].z, // Mid-Forward
                    BoneVertices[5].x, BoneVertices[5].y, BoneVertices[5].z  // Bottom
                };

                    Vector3[] vertices = new Vector3[verticesFloat.Length / stride];
                    for (int i = 0; i < verticesFloat.Length; i += stride)
                    {
                        vertices[i / stride] = new Vector3(verticesFloat[i], verticesFloat[i + 1], verticesFloat[i + 2]);
                    }

                    return vertices;
                }

                private int[] CreateTriangles()
                {
                    return new int[] { 1, 2, 0, 2, 3, 0, 3, 4, 0, 0, 4, 1, 5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4 };
                }
            }

            /// <summary>
            /// Support class for a cone
            /// </summary>
            internal class Cone: Shape
            {
                public Cone(float height = 1f, float bottomRadius = 0.5f, float topRadius = 0f, int sides = 36)
                {
                    int verticesCap = sides + 1;

                    #region Vertices

                    // bottom + top + sides
                    Vertices = new Vector3[verticesCap + verticesCap + sides * 2 + 2];
                    int vert = 0;
                    float _2pi = Mathf.PI * 2f;

                    // Bottom cap
                    Vertices[vert++] = new Vector3(0f, 0f, 0f);
                    while (vert <= sides)
                    {
                        float rad = (float)vert / sides * _2pi;
                        Vertices[vert] = new Vector3(Mathf.Cos(rad) * bottomRadius, 0f, Mathf.Sin(rad) * bottomRadius);
                        vert++;
                    }

                    // Top cap
                    Vertices[vert++] = new Vector3(0f, height, 0f);
                    while (vert <= sides * 2 + 1)
                    {
                        float rad = (float)(vert - sides - 1) / sides * _2pi;
                        Vertices[vert] = new Vector3(Mathf.Cos(rad) * topRadius, height, Mathf.Sin(rad) * topRadius);
                        vert++;
                    }

                    // Sides
                    int v = 0;
                    while (vert <= Vertices.Length - 4)
                    {
                        float rad = (float)v / sides * _2pi;
                        Vertices[vert] = new Vector3(Mathf.Cos(rad) * topRadius, height, Mathf.Sin(rad) * topRadius);
                        Vertices[vert + 1] = new Vector3(Mathf.Cos(rad) * bottomRadius, 0, Mathf.Sin(rad) * bottomRadius);
                        vert += 2;
                        v++;
                    }
                    Vertices[vert] = Vertices[sides * 2 + 2];
                    Vertices[vert + 1] = Vertices[sides * 2 + 3];
                    #endregion

                    #region Normals

                    // bottom + top + sides
                    Vector3[] Normals = new Vector3[Vertices.Length];
                    vert = 0;

                    // Bottom cap
                    while (vert <= sides)
                    {
                        Normals[vert++] = Vector3.down;
                    }

                    // Top cap
                    while (vert <= sides * 2 + 1)
                    {
                        Normals[vert++] = Vector3.up;
                    }

                    // Sides
                    v = 0;
                    while (vert <= Vertices.Length - 4)
                    {
                        float rad = (float)v / sides * _2pi;
                        float cos = Mathf.Cos(rad);
                        float sin = Mathf.Sin(rad);

                        Normals[vert] = new Vector3(cos, 0f, sin);
                        Normals[vert + 1] = Normals[vert];

                        vert += 2;
                        v++;
                    }
                    Normals[vert] = Normals[sides * 2 + 2];
                    Normals[vert + 1] = Normals[sides * 2 + 3];
                    #endregion

                    #region UVs

                    UVs = new Vector2[Vertices.Length];

                    // Bottom cap
                    int u = 0;
                    UVs[u++] = new Vector2(0.5f, 0.5f);
                    while (u <= sides)
                    {
                        float rad = (float)u / sides * _2pi;
                        UVs[u] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
                        u++;
                    }

                    // Top cap
                    UVs[u++] = new Vector2(0.5f, 0.5f);
                    while (u <= sides * 2 + 1)
                    {
                        float rad = (float)u / sides * _2pi;
                        UVs[u] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
                        u++;
                    }

                    // Sides
                    int u_sides = 0;
                    while (u <= UVs.Length - 4)
                    {
                        float t = (float)u_sides / sides;
                        UVs[u] = new Vector3(t, 1f);
                        UVs[u + 1] = new Vector3(t, 0f);
                        u += 2;
                        u_sides++;
                    }
                    UVs[u] = new Vector2(1f, 1f);
                    UVs[u + 1] = new Vector2(1f, 0f);

                    #endregion

                    #region Triangles

                    int nbTriangles = sides + sides + sides * 2;
                    Triangles = new int[nbTriangles * 3 + 3];

                    // Bottom cap
                    int tri = 0;
                    int i = 0;
                    while (tri < sides - 1)
                    {
                        Triangles[i] = 0;
                        Triangles[i + 1] = tri + 1;
                        Triangles[i + 2] = tri + 2;
                        tri++;
                        i += 3;
                    }
                    Triangles[i] = 0;
                    Triangles[i + 1] = tri + 1;
                    Triangles[i + 2] = 1;
                    tri++;
                    i += 3;

                    // Top cap
                    //tri++;
                    while (tri < sides * 2)
                    {
                        Triangles[i] = tri + 2;
                        Triangles[i + 1] = tri + 1;
                        Triangles[i + 2] = verticesCap;
                        tri++;
                        i += 3;
                    }

                    Triangles[i] = verticesCap + 1;
                    Triangles[i + 1] = tri + 1;
                    Triangles[i + 2] = verticesCap;
                    tri++;
                    i += 3;
                    tri++;

                    // Sides
                    while (tri <= nbTriangles)
                    {
                        Triangles[i] = tri + 2;
                        Triangles[i + 1] = tri + 1;
                        Triangles[i + 2] = tri + 0;
                        tri++;
                        i += 3;

                        Triangles[i] = tri + 1;
                        Triangles[i + 1] = tri + 2;
                        Triangles[i + 2] = tri + 0;
                        tri++;
                        i += 3;
                    }

                    #endregion
                }
            }

            // Based on cobe by: Kevin Tritz (tritz at yahoo *spamfilter* com)
            // http://codescrib.blogspot.com/
            // copyright (c) 2014  
            // license: BSD style  
            // derived from python version: Icosphere.py  
            //  
            //         Author: William G.K. Martin (wgm2111@cu where cu=columbia.edu)  
            //         copyright (c) 2010  
            //         license: BSD style  
            //        https://code.google.com/p/mesh2d-mpl/source/browse/icosphere.py  
            internal class IcoSphere: Shape
            {
                // int[N,3] triangle verticies index list, N = 20*(num+1)^2  
                private int[,] trianglesTable;

                public IcoSphere(int subdivisions = 20)
                {
                    CalculateTriangulation(subdivisions, new Icosahedron());
                    UVs = GetUV(Vertices);
                }

                // main function to subdivide and triangulate Icosahedron  
                private void CalculateTriangulation(int num, Icosahedron shape)
                {
                    Dictionary<Vector3, int> vertDict = new Dictionary<Vector3, int>();    // dict lookup to speed up vertex indexing  
                    float[,] subdivision = GetSubMatrix(num + 2);                            // vertex subdivision matrix calculation  
                    Vector3 p1, p2, p3;
                    int index = 0;
                    int len = subdivision.GetLength(0);
                    int triNum = (num + 1) * (num + 1) * 20;            // number of triangle faces  
                    Vertices = new Vector3[triNum / 2 + 2];        // allocate verticies, triangles, etc...  
                    Triangles = new int[triNum * 3];
                    trianglesTable = new int[triNum, 3];
                    Vector3[] tempVerts = new Vector3[len];        // temporary structures for subdividing each Icosahedron face  
                    int[] tempIndices = new int[len];
                    int[,] triIndices = Triangulate(num);        // precalculate generic subdivided triangle indices  
                    int triLength = triIndices.GetLength(0);
                    for (int i = 0; i < 20; i++)                    // calculate subdivided vertices and triangles for each face  
                    {
                        p1 = shape.Vertices[shape.Triangles[i * 3]];    // get 3 original vertex locations for each face  
                        p2 = shape.Vertices[shape.Triangles[i * 3 + 1]];
                        p3 = shape.Vertices[shape.Triangles[i * 3 + 2]];
                        for (int j = 0; j < len; j++)                // calculate new subdivided vertex locations  
                        {
                            tempVerts[j].x = subdivision[j, 0] * p1.x + subdivision[j, 1] * p2.x + subdivision[j, 2] * p3.x;
                            tempVerts[j].y = subdivision[j, 0] * p1.y + subdivision[j, 1] * p2.y + subdivision[j, 2] * p3.y;
                            tempVerts[j].z = subdivision[j, 0] * p1.z + subdivision[j, 1] * p2.z + subdivision[j, 2] * p3.z;
                            tempVerts[j].Normalize();
                            if (!vertDict.TryGetValue(tempVerts[j], out int vertIndex))    // quick lookup to avoid vertex duplication  
                            {
                                vertDict[tempVerts[j]] = index;    // if vertex not in dict, add it to dictionary and final array  
                                vertIndex = index;
                                Vertices[index] = tempVerts[j];
                                index += 1;
                            }
                            tempIndices[j] = vertIndex;            // assemble vertex indices for triangle assignment  
                        }
                        for (int j = 0; j < triLength; j++)        // map precalculated subdivided triangle indices to vertex indices  
                        {
                            trianglesTable[triLength * i + j, 0] = tempIndices[triIndices[j, 0]];
                            trianglesTable[triLength * i + j, 1] = tempIndices[triIndices[j, 1]];
                            trianglesTable[triLength * i + j, 2] = tempIndices[triIndices[j, 2]];
                            Triangles[3 * triLength * i + 3 * j] = tempIndices[triIndices[j, 0]];
                            Triangles[3 * triLength * i + 3 * j + 1] = tempIndices[triIndices[j, 1]];
                            Triangles[3 * triLength * i + 3 * j + 2] = tempIndices[triIndices[j, 2]];
                        }
                    }
                }

                // fuction to precalculate generic triangle indices for subdivided vertices  
                private int[,] Triangulate(int num)
                {
                    int n = num + 2;
                    int[,] triangles = new int[(n - 1) * (n - 1), 3];
                    int shift = 0;
                    int ind = 0;
                    for (int row = 0; row < n - 1; row++)
                    {
                        triangles[ind, 0] = shift + 1;
                        triangles[ind, 1] = shift + n - row;
                        triangles[ind, 2] = shift;
                        ind += 1;
                        for (int col = 1; col < n - 1 - row; col++)
                        {
                            triangles[ind, 0] = shift + col;
                            triangles[ind, 1] = shift + n - row + col;
                            triangles[ind, 2] = shift + n - row + col - 1;
                            ind += 1;
                            triangles[ind, 0] = shift + col + 1;
                            triangles[ind, 1] = shift + n - row + col;
                            triangles[ind, 2] = shift + col;
                            ind += 1;
                        }
                        shift += n - row;
                    }
                    return triangles;
                }

                // standard Longitude/Latitude mapping to (0,1)/(0,1)  
                private Vector2[] GetUV(Vector3[] vertices)
                {
                    int num = vertices.Length;
                    float pi = (float)System.Math.PI;
                    Vector2[] UV = new Vector2[num];
                    for (int i = 0; i < num; i++)
                    {
                        UV[i] = CartesianToLatLong(vertices[i]);
                        UV[i].x = (UV[i].x + pi) / (2.0f * pi);
                        UV[i].y = (UV[i].y + pi / 2.0f) / pi;
                    }
                    return UV;
                }

                // transform 3D cartesion coordinates to longitude, latitude  
                private Vector2 CartesianToLatLong(Vector3 point)
                {
                    Vector2 coord = new Vector2();
                    float norm = point.magnitude;
                    if (point.x != 0.0f || point.y != 0.0f)
                        coord.x = -(float)System.Math.Atan2(point.y, point.x);
                    else
                        coord.x = 0.0f;
                    if (norm > 0.0f)
                        coord.y = (float)System.Math.Asin(point.z / norm);
                    else
                        coord.y = 0.0f;
                    return coord;
                }

                // vertex subdivision matrix, num=3 subdivides 1 triangle into 4  
                private float[,] GetSubMatrix(int num)
                {
                    int numrows = num * (num + 1) / 2;
                    float[,] subdivision = new float[numrows, 3];
                    float[] values = new float[num];
                    int[] offsets = new int[num];
                    int[] starts = new int[num];
                    int[] stops = new int[num];
                    int index;
                    for (int i = 0; i < num; i++)
                    {
                        values[i] = (float)i / (float)(num - 1);
                        offsets[i] = (num - i);
                        if (i > 0)
                            starts[i] = starts[i - 1] + offsets[i - 1];
                        else
                            starts[i] = 0;
                        stops[i] = starts[i] + offsets[i];
                    }
                    for (int i = 0; i < num; i++)
                    {
                        for (int j = 0; j < offsets[i]; j++)
                        {
                            index = starts[i] + j;
                            subdivision[index, 0] = values[offsets[i] - 1 - j];
                            subdivision[index, 1] = values[j];
                            subdivision[index, 2] = values[i];
                        }
                    }
                    return subdivision;
                }
            }
        }
    }
}

    
