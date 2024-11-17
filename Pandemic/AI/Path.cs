using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Main.AI {
    public class Path : MonoBehaviour {

        public bool close = false;

        public int curveSmoothness = 10;

        public Tangent[] tangents = new Tangent[0];

        [HideInInspector]
        public Point[] bakedPoints = new Point[0];

        private void Awake() {
            Queue<Point> points = new Queue<Point>();
            var pointIndex = 0;
            for (int i = 0; i < tangents.Length; i++) {
                var tgt = tangents[i];

                if (tgt.isCurve) {
                    var prevTgt = tangents[i - 1];
                    var nextTgt = GetNextTangent(tgt, i);
                    for (int t = 1; t < curveSmoothness; t++) {
                        points.Enqueue(new Point(Curve(prevTgt.position, tgt.position, nextTgt.position, (1F / curveSmoothness) * t), pointIndex++));
                    }
                } else {
                    points.Enqueue(new Point(tgt.position, pointIndex++));
                }
            }
            bakedPoints = points.ToArray();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            foreach (var g in Selection.gameObjects) {
                if (g == gameObject) {
                    Gizmos.color = Color.blue;

                    for (int i = 0; i < tangents.Length; i++) {
                        var p = tangents[i];

                        if (!p.isCurve) {
                            Gizmos.DrawSphere(transform.position + p.position, 0.2F);
                        } else {
                            Gizmos.DrawWireSphere(transform.position + p.position, 0.2F);
                        }

                        if ((i == tangents.Length - 1 && !close) || p.isCurve) {
                            continue;
                        }

                        var p1 = close && i >= tangents.Length - 1 ? tangents[i - (tangents.Length - 2) - 1] : tangents[i + 1];

                        if ((i < tangents.Length - 1 || (close && i == tangents.Length - 1)) && p1.isCurve) {
                            var p2 = close && i >= tangents.Length - 2 ? tangents[i - (tangents.Length - 2)] : tangents[i + 2];
                            // Draw Curve
                            var smoothnessLevel = (1F / curveSmoothness);
                            for (int t = 1; t <= curveSmoothness; t++) {
                                var prevPoint = Curve(p.position, p1.position, p2.position, smoothnessLevel * (t - 1));
                                var point = Curve(p.position, p1.position, p2.position, smoothnessLevel * t);
                                Gizmos.DrawLine(transform.position + prevPoint, transform.position + point);
                            }
                        } else {
                            Gizmos.DrawLine(transform.position + p1.position, transform.position + p.position);
                        }
                    }

                    foreach (var p in bakedPoints) {
                        Gizmos.DrawWireSphere(transform.position + p.position, 0.1F);
                    }
                }
            }
        }
#endif

        private Vector3 Curve(Vector3 a, Vector3 b, Vector3 c, float t) {
            var p1 = Vector3.Lerp(a, b, t);
            var p2 = Vector3.Lerp(b, c, t);
            return Vector3.Lerp(p1, p2, t);
        }

        private Tangent GetNextTangent(Tangent tangent, int index) {
            return close && index == tangents.Length - 1 ? tangents[0] : tangents[index + 1];
        }

        public Vector3 GetPosition(Point point) {
            return transform.position + point.position;
        }

        public Point GetNextPoint(Point point) {
            return close && point.index == bakedPoints.Length - 1 ? bakedPoints[0] : bakedPoints[point.index + 1];
        }

        [Serializable]
        public class Tangent {

            public Vector3 position;

            public bool isCurve = false;
        }

        [Serializable]
        public class Point {
            public Vector3 position;

            public int index;

            public Point(Vector3 position, int index) {
                this.position = position;
                this.index = index;
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(Path)), CanEditMultipleObjects]
    public class PathEditor : UnityEditor.Editor {

        private bool editMode = false;

        protected virtual void OnSceneGUI() {
            if (editMode) {
                Path path = (Path) target;

                foreach (var p in path.tangents) {
                    Tools.hidden = true;
                    EditorGUI.BeginChangeCheck();
                    Vector3 newPosition = Handles.PositionHandle(path.transform.position + p.position, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(path, "Change Path Points");
                        p.position = newPosition - path.transform.position;
                    }
                }
            } else {
                Tools.hidden = false;
            }
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            editMode = GUILayout.Toggle(editMode, "Edit Mode", "Button");
            SceneView.RepaintAll();
        }
    }
# endif
}
