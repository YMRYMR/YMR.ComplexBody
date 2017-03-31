using Duality;
using Duality.Cloning;
using Duality.Components;
using Duality.Components.Physics;
using Duality.Drawing;
using Duality.Editor;
using Duality.Properties;
using Duality.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YMR.ComplexBody.Core.GeometryUtility;

namespace YMR.ComplexBody.Core
{
    /// <summary>
    /// Represents a complex body instance for physical simulation, collision detection and response.
    /// </summary>
    [ManuallyCloned]
    [EditorHintCategory(CoreResNames.CategoryPhysics)]
    [EditorHintImage(CoreResNames.ImageRigidBody)]
    [RequiredComponent(typeof(Transform))]
    [RequiredComponent(typeof(RigidBody))]
    public class ComplexBody : Component, ICmpRenderer, ICmpInitializable
    {
        #region Enums

        /// <summary>
        /// Not used yet
        /// </summary>
        public enum BodyShapeMode
        {
            /// <summary>
            /// Ear cutting triangulation
            /// </summary>
            Triangulation = 0,
            /// <summary>
            /// Border shapes
            /// </summary>
            Border = 1
        }

        public enum BoderMode
        {
            Inside = 0,
            Outside = 1
        }

        #endregion

        private struct BorderInfo
        {
            public Vector2 outerA;
            public Vector2 outerB;
            public Vector2 outerCenter;
            public Vector2 dummyInnerA;
            public Vector2 dummyInnerB;
            public Vector2 innerA;
            public Vector2 innerB;
            public Vector2 innerCenter;
            public Vector2 center;
            public float distanceAB;
            public float distanceAA;
            public float distanceBB;
            public float distanceCenterCenter;
            public float angle;

            public Vector2[] Polygon
            {
                get
                {
                    return new Vector2[] { outerA, outerB, innerA, innerB };
                }
            }

            public Vector2[] AllPoints
            {
                get
                {
                    return new Vector2[] { outerA, outerB, outerCenter, dummyInnerA, dummyInnerB, innerA, innerB, innerCenter, center };
                }
                set
                {
                    outerA = value[0];
                    outerB = value[1];
                    outerCenter = value[2];
                    dummyInnerA = value[3];
                    dummyInnerB = value[4];
                    innerA = value[5];
                    innerB = value[6];
                    innerCenter = value[7];
                    center = value[8];
                }
            }
            public float[] AllFloats
            {
                get
                {
                    return new float[] { distanceAB, distanceAA, distanceBB, distanceCenterCenter, angle };
                }
                set
                {
                    distanceAB = value[0];
                    distanceAA = value[1];
                    distanceBB = value[2];
                    distanceCenterCenter = value[3];
                    angle = value[4];
                }
            }

            public void Transform(Transform trans)
            {
                Vector2[] allPoints = AllPoints;
                int t = allPoints.Length;
                for (int i = 0; i < t; i++)
                {
                    TransformPoint(trans, ref allPoints[i].X, ref allPoints[i].Y);
                }
                AllPoints = allPoints;
            }

            public BorderInfo Clone()
            {
                return new BorderInfo() { AllPoints = this.AllPoints, AllFloats = this.AllFloats };
            }
        }

        private class VertexInfo
        {
            public bool discarded = true;
            public List<VertexC1P3T2[]> polygons = new List<VertexC1P3T2[]>();
            public List<VertexC1P3T2[]> dummies = new List<VertexC1P3T2[]>();
            public List<VertexC1P3T2[]> borderGeometry = new List<VertexC1P3T2[]>();
            public List<VertexC1P3T2[]> limits = new List<VertexC1P3T2[]>();
            public List<VertexC1P3T2[]> material = new List<VertexC1P3T2[]>();
            public List<VertexC1P3T2[]> borderMaterial = new List<VertexC1P3T2[]>();
        }

        #region Private Members

        [DontSerialize]
        private List<CPoint2D> lastVertices = new List<CPoint2D>();
        [DontSerialize]
        private int selectedPointId = -1;
        [DontSerialize]
        private bool working = false;

        [DontSerialize]
        private bool ctrlPressed = false;
        [DontSerialize]
        private bool mouseLeft = false;
        [DontSerialize]
        private bool mouseRight = false;
        [DontSerialize]
        private float mouseX;
        [DontSerialize]
        private float mouseY;
        [DontSerialize]
        private bool isSelected = false;
        [DontSerialize]
        private Transform trans = null;
        [DontSerialize]
        private RigidBody rb = null;
        [DontSerialize]
        private BorderInfo[] borderInfo = null;
        [DontSerialize]
        private CPolygonShape cutPolygon = null;
        [DontSerialize]
        private VertexInfo vertexInfo = new VertexInfo();

        private List<Vector2> points = new List<Vector2>()
        {
            new Vector2(0, 0), 
            new Vector2(100, 0), 
            new Vector2(100, 100), 
        };
        private bool showBorderMaterial = true;
        private bool showBorderGeometry = true;
        private bool showDummies = false;
        private bool showLimits = false;
        private bool showPolygons = true;
        private bool showMaterial = true;
        private bool updatableUsingMouse = true;
        private ContentRef<Material> mainMaterial = Material.Checkerboard;
        private ContentRef<Material> borderMaterial = Material.Checkerboard;
        private float borderWidth = 2;
        private float lineWidth = 2;
        private ColorRgba polygonColor = new ColorRgba(255, 0, 0, 200);
        private ColorRgba borderGeometryColor = new ColorRgba(0, 255, 0, 200);
        private ColorRgba dummyColor = new ColorRgba(0, 0, 255, 200);
        private BodyShapeMode shapeMode = BodyShapeMode.Triangulation;
        private BoderMode borderType = BoderMode.Inside;
        private Camera camera3D = null;
        private bool borderTexFlip = false;
        private bool staticPosMainMaterial = false;
        private bool staticAngleMainMaterial = false;

        #endregion

        #region Public Members

        /// <summary>
        /// Body vertices
        /// </summary>
        public List<Vector2> Points { get { return points; } set { points = value; } }
        public bool ShowBorderMaterial { get { return showBorderMaterial; } set { showBorderMaterial = value; vertexInfo.discarded = true; } }
        public bool ShowBorderGeometry { get { return showBorderGeometry; } set { showBorderGeometry = value; vertexInfo.discarded = true; } }
        public bool ShowDummies { get { return showDummies; } set { showDummies = value; vertexInfo.discarded = true; } }
        public bool ShowLimits { get { return showLimits; } set { showLimits = value; vertexInfo.discarded = true; } }
        public bool ShowPolygons { get { return showPolygons; } set { showPolygons = value; vertexInfo.discarded = true; } }
        public bool ShowMaterial { get { return showMaterial; } set { showMaterial = value; vertexInfo.discarded = true; } }
        public bool UpdatableUsingMouse { get { return updatableUsingMouse; } set { updatableUsingMouse = value; } }
        public ContentRef<Material> MainMaterial { get { return mainMaterial; } set { mainMaterial = value; } }
        public ContentRef<Material> BorderMaterial { get { return borderMaterial; } set { borderMaterial = value; } }
        public float BoundRadius { get { return this.GameObj.GetComponent<RigidBody>().BoundRadius; } }
        public float BorderWidth { get { return borderWidth; } set { borderWidth = value; UpdateBody(true); } }
        public float LineWidth { get { return lineWidth; } set { lineWidth = value; vertexInfo.discarded = true; } }
        public ColorRgba PolygonColor { get { return polygonColor; } set { polygonColor = value; vertexInfo.discarded = true; } }
        public ColorRgba DummyColor { get { return dummyColor; } set { dummyColor = value; vertexInfo.discarded = true; } }
        public ColorRgba BorderGeometryColor { get { return borderGeometryColor; } set { borderGeometryColor = value; vertexInfo.discarded = true; } }
        public BodyShapeMode ShapeMode { get { return shapeMode; } set { shapeMode = value; UpdateBody(true); } }
        public BoderMode BorderType { get { return borderType; } set { borderType = value; UpdateBody(true); } }
        public bool StaticPosMainMaterial { get { return staticPosMainMaterial; } set { staticPosMainMaterial = value; } }
        public bool StaticAngleMainMaterial { get { return staticAngleMainMaterial; } set { staticAngleMainMaterial = value; } }
        /// <summary>
        /// If set, the 3D effect will use the camera as center point.
        /// </summary>
        public Camera Camera3D { get { return camera3D; } set { camera3D = value; UpdateBody(true); } }
        public bool BorderTexFlip { get { return borderTexFlip; } set { borderTexFlip = value; vertexInfo.discarded = true; } }

        [EditorHintFlags(MemberFlags.Invisible)]
        public bool CtrlPressed { get { return ctrlPressed; } set { ctrlPressed = value; } }
        [EditorHintFlags(MemberFlags.Invisible)]
        public bool MouseLeft { get { return mouseLeft; } set { mouseLeft = value; } }
        [EditorHintFlags(MemberFlags.Invisible)]
        public bool MouseRight { get { return mouseRight; } set { mouseRight = value; } }
        [EditorHintFlags(MemberFlags.Invisible)]
        public float MouseX { get { return mouseX; } set { mouseX = value; } }
        [EditorHintFlags(MemberFlags.Invisible)]
        public float MouseY { get { return mouseY; } set { mouseY = value; } }
        [EditorHintFlags(MemberFlags.Invisible)]
        public bool IsSelected { get { return isSelected; } set { isSelected = value; } }

        #endregion

        #region Vertex Methods

        private static void TransformVertices(IDrawDevice device, Transform trans, VertexC1P3T2[] vertices)
        {
            int t = vertices.Length;
            for(int i = 0; i < t; i++)
            {
                TransformPoint(trans, ref vertices[i].Pos.X, ref vertices[i].Pos.Y);
                vertices[i].Pos.Z = trans.Pos.Z;
                float scale = trans.Scale;
                device.PreprocessCoords(ref vertices[i].Pos, ref scale);
            }
        }
        private static void TransformPoint(Transform trans, ref float x, ref float y)
        {
            TransformPoint(trans, ref x, ref y, true, true, true, false);
        }
        private static void TransformPoint(Transform trans, ref float x, ref float y, bool invertPos)
        {
            TransformPoint(trans, ref x, ref y, true, true, true, invertPos);
        }
        private static void TransformPoint(Transform trans, ref float x, ref float y, bool applyPos, bool applyAngle, bool applyScale)
        {
            TransformPoint(trans, ref x, ref y, applyPos, applyAngle, applyScale, false);
        }
        private static void TransformPoint(Transform trans, ref float x, ref float y, bool applyPos, bool applyAngle, bool applyScale, bool invertPos)
        {
            TransformPoint(trans, ref x, ref y, applyPos, applyAngle, applyScale, invertPos, false);
        }
        private static void TransformPoint(Transform trans, ref float x, ref float y, bool applyPos, bool applyAngle, bool applyScale, bool invertPos, bool invertAngle)
        {
            TransformPoint(trans, ref x, ref y, applyPos, applyAngle, applyScale, invertPos, invertAngle, false);
        }
        private static void TransformPoint(Transform trans, ref float x, ref float y, bool applyPos, bool applyAngle, bool applyScale, bool invertPos, bool invertAngle, bool invertScale)
        {
            float angle = applyAngle ? invertAngle ? -trans.Angle : trans.Angle : 0;
            float scale = applyScale ? invertScale ? -trans.Scale : trans.Scale : 1;
            MathF.TransformCoord(ref x, ref y, angle, scale);
            if (applyPos)
            {
                if (invertPos)
                {
                    x -= trans.Pos.X;
                    y -= trans.Pos.Y;
                }
                else
                {
                    x += trans.Pos.X;
                    y += trans.Pos.Y;
                }
            }
        }

        private VertexC1P3T2[] GetCircle(IDrawDevice device, Vector2 point, float radius, ColorRgba color)
        {
            return GetCircle(device, point, radius, color, 8);
        }
        private VertexC1P3T2[] GetCircle(IDrawDevice device, Vector2 point, float radius, ColorRgba color, int segments)
        {
            Vector3 tempPos = new Vector3(point);
            float angleStep = MathF.RadAngle360 / (float)segments;
            int t = segments + 2;
            VertexC1P3T2[] vertices = new VertexC1P3T2[t];
            vertices[0].Pos = tempPos;
            vertices[0].Color = color;
            float pointAngle = 0f;
            for (int i = 1; i < t; i++)
            {
                float sin = (float)Math.Sin(pointAngle);
                float cos = (float)Math.Cos(pointAngle);
                vertices[i].Pos.X = tempPos.X + sin * radius;
                vertices[i].Pos.Y = tempPos.Y - cos * radius;
                vertices[i].Color = color;
                pointAngle += angleStep;
            }
            return vertices;
        }
        private VertexC1P3T2[] GetLine(IDrawDevice device, Vector2 pointA, Vector2 pointB, float width, ColorRgba color)
        {
            return GetLine(device, pointA, pointB, width, color, false);
        }
        private VertexC1P3T2[] GetLine(IDrawDevice device, Vector2 pointA, Vector2 pointB, float width, ColorRgba color, bool scaleWidth)
        {
            Vector3 tempPosA = new Vector3(pointA);
            Vector3 tempPosB = new Vector3(pointB);
            Vector2 dir = (tempPosB.Xy - tempPosA.Xy).Normalized;
            Vector2 left = dir.PerpendicularLeft * width * 0.5f;
            Vector2 right = dir.PerpendicularRight * width * 0.5f;
            Vector2 left2 = dir.PerpendicularLeft * width * 0.5f;
            Vector2 right2 = dir.PerpendicularRight * width * 0.5f;
            VertexC1P3T2[] vertices = new VertexC1P3T2[]
            {
                new VertexC1P3T2() { Pos = tempPosA + new Vector3(left), Color = color },
                new VertexC1P3T2() { Pos = tempPosB + new Vector3(left2), Color = color },
                new VertexC1P3T2() { Pos = tempPosB + new Vector3(right2), Color = color },
                new VertexC1P3T2() { Pos = tempPosA + new Vector3(right), Color = color }
            };
            return vertices;
        }
        private VertexC1P3T2[] GetPoly(IDrawDevice device, Vector2[] points, ColorRgba color)
        {
            Vector2[] texCoord = new Vector2[] { Vector2.Zero, new Vector2(1f, 0f), Vector2.One, new Vector2(0f, 1f) };
            return GetPoly(device, points, color, texCoord);
        }
        private VertexC1P3T2[] GetPoly(IDrawDevice device, Vector2[] points, ColorRgba color, Vector2[] texCoord)
        {
            Vector3 tempPos = new Vector3(Vector2.Zero, trans.Pos.Z);
            Rect pointBoundingRect = points.BoundingBox();
            int t = points.Length;
            VertexC1P3T2[] vertices = new VertexC1P3T2[t];
            for (int i = 0; i < t; i++)
            {
                vertices[i].Pos.X = points[i].X + tempPos.X;
                vertices[i].Pos.Y = points[i].Y + tempPos.Y;
                vertices[i].Pos.Z = tempPos.Z;
                vertices[i].TexCoord.X = texCoord[i].X;
                vertices[i].TexCoord.Y = texCoord[i].Y;
                vertices[i].Color = color;
            }

            return vertices;
        }
        private void CreateVertices(IDrawDevice device)
        {
            CreateVertices(device, showMaterial, showBorderMaterial, showPolygons, showBorderGeometry, showDummies, showLimits);
        }
        private void CreateVertices(IDrawDevice device, bool material, bool borderMaterial, bool polygons, bool borderGeometry, bool dummies, bool limits)
        {
            vertexInfo = new VertexInfo();

            try
            {
                int t = points.Count;

                // Double check for added/removed points
                if (borderInfo == null || t != borderInfo.Length)
                {
                    UpdateBody(true);
                    t = points.Count;
                    if (borderInfo == null || t != borderInfo.Length)
                    {
                        working = false;
                        return;
                    }
                }

                Texture mainTex = this.mainMaterial.Res.MainTexture.Res;
                Texture borderTex = this.borderMaterial.Res.MainTexture.Res;
                ColorRgba mainColor = this.mainMaterial.Res.MainColor;
                ColorRgba borderColor = this.borderMaterial.Res.MainColor;

                if ((material || polygons) && cutPolygon != null && points.Count > 2)
                {
                    int tPol = cutPolygon.NumberOfPolygons;
                    for (int i = 0; i < tPol; i++)
                    {
                        CPoint2D[] points = cutPolygon.Polygons(i);
                        int nPoints = cutPolygon.Polygons(i).Length;
                        Vector2[] tempArray = new Vector2[]
                        {
                            new Vector2((int)cutPolygon.Polygons(i)[0].X, (int)cutPolygon.Polygons(i)[0].Y),
                            new Vector2((int)cutPolygon.Polygons(i)[1].X, (int)cutPolygon.Polygons(i)[1].Y),
                            new Vector2((int)cutPolygon.Polygons(i)[2].X, (int)cutPolygon.Polygons(i)[2].Y)
                        };
                        if (material)
                        {
                            float ratioX = 1f / mainTex.Size.X;
                            float ratioY = 1f / mainTex.Size.Y;
                            Vector2[] texCoord = new Vector2[3];
                            for (int j = 0; j < 3; j++)
                            {
                                float x = tempArray[j].X * ratioX;
                                float y = tempArray[j].Y * ratioY;
                                texCoord[j] = new Vector2(x, y);
                            }

                            vertexInfo.material.Add(GetPoly(device, tempArray, mainColor, texCoord));
                        }
                        if (polygons)
                        {
                            vertexInfo.polygons.AddRange(new[] {
                                GetLine(device, tempArray[0], tempArray[1], lineWidth, polygonColor),
                                GetLine(device, tempArray[1], tempArray[2], lineWidth, polygonColor),
                                GetLine(device, tempArray[0], tempArray[2], lineWidth, polygonColor)
                            });
                        }
                    }
                }

                //BorderInfo[] renderedBorderInfo = new BorderInfo[t];
                for (int i = 0; i < t; i++)
                {
                    BorderInfo bi = borderInfo[i].Clone();

                    // Border and points
                    if (borderMaterial || borderGeometry || dummies)
                    {
                        Vector2 p0, p1;
                        p0 = new Vector2(MathF.RoundToInt(points[i].X), MathF.RoundToInt(points[i].Y));
                        if (i < t - 1) p1 = new Vector2(MathF.RoundToInt(points[i + 1].X), MathF.RoundToInt(points[i + 1].Y));
                        else p1 = new Vector2(points[0].X, points[0].Y);

                        //if (camera3D != null)
                        //{
                        //    Transform camTrans = camera3D.GameObj.AddComponent<Transform>();
                        //    float camX = borderType == BoderMode.Inside ? camTrans.Pos.X : -camTrans.Pos.X;
                        //    float camY = borderType == BoderMode.Inside ? camTrans.Pos.Y : -camTrans.Pos.Y;
                        //    TransformPoint(trans, ref camX, ref camY, false, true, true, false, true, true);
                        //    bi.innerA = new Vector2(bi.innerA.X + (trans.Pos.X - camX) / MathF.Abs(camTrans.Pos.Z * .01f),
                        //                            bi.innerA.Y + (trans.Pos.Y - camY) / MathF.Abs(camTrans.Pos.Z * .01f));
                        //    bi.innerB = new Vector2(bi.innerB.X + (trans.Pos.X - camX) / MathF.Abs(camTrans.Pos.Z * .01f),
                        //                            bi.innerB.Y + (trans.Pos.Y - camY) / MathF.Abs(camTrans.Pos.Z * .01f));
                        //}
                        //renderedBorderInfo[i] = bi;

                        Vector2[] borderPoly = bi.Polygon;
                        //bi.Transform(trans);

                        if (borderMaterial)
                        {
                            Vector2[] texCoord;
                            if (borderTexFlip != (borderType == BoderMode.Inside)) texCoord = new Vector2[] { Vector2.Zero, new Vector2(1f, 0f), Vector2.One, new Vector2(0f, 1f) };
                            else texCoord = new Vector2[] { Vector2.One, new Vector2(0f, 1f), Vector2.Zero, new Vector2(1f, 0f) };
                            vertexInfo.borderMaterial.Add(GetPoly(device, bi.Polygon, borderColor, texCoord));
                        }

                        if (borderGeometry)
                        {
                            vertexInfo.borderGeometry.AddRange(new[] {
                                GetLine(device, bi.outerA, bi.outerB, lineWidth, borderGeometryColor),
                                GetLine(device, bi.outerA, bi.innerB, lineWidth, borderGeometryColor),
                                GetLine(device, bi.innerA, bi.innerB, lineWidth, borderGeometryColor)
                            });
                        }

                        if (dummies)
                        {
                            vertexInfo.dummies.AddRange(new[] {
                                GetCircle(device, bi.outerCenter, lineWidth * 2f, dummyColor),
                                GetCircle(device, bi.innerCenter, lineWidth * 2f, dummyColor),
                                GetCircle(device, bi.dummyInnerA, lineWidth * 2f, dummyColor),
                                GetCircle(device, bi.dummyInnerB, lineWidth * 2f, dummyColor),
                                GetCircle(device, bi.innerA, lineWidth * 2f, dummyColor),
                                GetCircle(device, bi.innerB, lineWidth * 2f, dummyColor),
                                GetCircle(device, bi.center, lineWidth * 2f, dummyColor),

                                GetLine(device, bi.outerCenter, bi.innerCenter, lineWidth, dummyColor),
                                GetLine(device, bi.dummyInnerA, bi.dummyInnerB, lineWidth, dummyColor)
                            });
                        }
                    }
                }

                //if (camera3D != null) // We update the RigidBody's shapes only if the borderInfo has been modified at render time (pseudo 3D camera effect must affect the shapes)
                //{
                //    UpdateShapes(renderedBorderInfo);
                //}
            }
            catch (Exception ex) { }

            vertexInfo.discarded = false;
        }

        #endregion

        #region Body and Shape Methods

        public Rect AABB(Vector2[] vertices)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                minX = MathF.Min(minX, vertices[i].X);
                minY = MathF.Min(minY, vertices[i].Y);
                maxX = MathF.Max(maxX, vertices[i].X);
                maxY = MathF.Max(maxY, vertices[i].Y);
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY); 
        }

        public void UpdateBody()
        {
            UpdateBody(false);
        }
        public void UpdateBody(bool force)
        {
            try
            {
                int t = points.Count;
                if (t < 3)
                {
                    rb.ClearShapes();
                    return;
                }
                int nLastVertices = lastVertices.Count;
                bool changed = force || nLastVertices != t;
                if (!changed)
                {
                    for (int i = 0; i < t; i++)
                    {
                        if (lastVertices[i].X != points[i].X || lastVertices[i].Y != points[i].Y)
                        {
                            changed = true;
                            lastVertices.Clear();
                            break;
                        }
                    }
                }
                if (!changed) return;

                for (int i = 0; i < t; i++)
                {
                    points[i] = new Vector2(MathF.RoundToInt(points[i].X), MathF.RoundToInt(points[i].Y));
                }
                // Border
                for (int i = 0; i < t; i++)
                {
                    if (showBorderMaterial || showBorderGeometry || showDummies || isSelected)
                    {
                        Vector2 p0, p1;
                        p0 = new Vector2(points[i].X, points[i].Y);
                        if (i < t - 1) p1 = new Vector2(points[i + 1].X, points[i + 1].Y);
                        else p1 = new Vector2(points[0].X, points[0].Y);

                        if (borderInfo == null || t != borderInfo.Length) borderInfo = new BorderInfo[t];
                        borderInfo[i].outerA = p1;
                        borderInfo[i].outerB = p0;
                        float angle01 = MathF.Angle(p0.X, p0.Y, p1.X, p1.Y) - MathF.RadAngle90;

                        borderInfo[i].distanceAB = MathF.Distance(p0.X, p0.Y, p1.X, p1.Y);
                        float halfDistance = borderInfo[i].distanceAB * .5f;
                        Vector2 outerCenterPoint = new Vector2(p0.X + halfDistance * MathF.Cos(angle01), p0.Y + halfDistance * MathF.Sin(angle01));
                        float mul = borderType == BoderMode.Inside ? -1 : 1;
                        Vector2 innerCenterPoint = new Vector2(outerCenterPoint.X + borderWidth * mul * MathF.Cos(angle01 - MathF.RadAngle90), outerCenterPoint.Y + borderWidth * mul * MathF.Sin(angle01 - MathF.RadAngle90));
                        Vector2 center = new Vector2(outerCenterPoint.X + borderWidth * mul * .5f * MathF.Cos(angle01 - MathF.RadAngle90), outerCenterPoint.Y + borderWidth * mul * .5f * MathF.Sin(angle01 - MathF.RadAngle90));

                        borderInfo[i].dummyInnerA = new Vector2(innerCenterPoint.X + halfDistance * MathF.Cos(angle01), innerCenterPoint.Y + halfDistance * MathF.Sin(angle01));
                        borderInfo[i].dummyInnerB = new Vector2(innerCenterPoint.X - halfDistance * MathF.Cos(angle01), innerCenterPoint.Y - halfDistance * MathF.Sin(angle01));

                        borderInfo[i].outerCenter = outerCenterPoint;
                        borderInfo[i].innerCenter = innerCenterPoint;
                        borderInfo[i].center = center;

                        borderInfo[i].distanceAA = MathF.Distance(borderInfo[i].outerA.X, borderInfo[i].outerA.Y, borderInfo[i].dummyInnerA.X, borderInfo[i].dummyInnerA.Y);
                        borderInfo[i].distanceBB = MathF.Distance(borderInfo[i].outerB.X, borderInfo[i].outerB.Y, borderInfo[i].dummyInnerB.X, borderInfo[i].dummyInnerB.Y);
                        borderInfo[i].distanceCenterCenter = MathF.Distance(borderInfo[i].outerCenter.X, borderInfo[i].outerCenter.Y, borderInfo[i].innerCenter.X, borderInfo[i].innerCenter.Y);

                        borderInfo[i].angle = angle01;
                    }
                }
                for (int i = 0; i < t; i++)
                {
                    Vector2[] tempInnerLinePointsA = new Vector2[3];
                    Vector2[] tempInnerLinePointsB = new Vector2[3];

                    if (i < t - 1)
                    {
                        if (i > 0)
                        {
                            tempInnerLinePointsA[0] = borderInfo[i - 1].dummyInnerA;
                            tempInnerLinePointsB[0] = borderInfo[i - 1].dummyInnerB;
                        }
                        else // First point
                        {
                            tempInnerLinePointsA[0] = borderInfo[t - 1].dummyInnerA;
                            tempInnerLinePointsB[0] = borderInfo[t - 1].dummyInnerB;
                        }
                        tempInnerLinePointsA[2] = borderInfo[i + 1].dummyInnerA;
                        tempInnerLinePointsB[2] = borderInfo[i + 1].dummyInnerB;
                    }
                    else // Last point
                    {
                        tempInnerLinePointsA[0] = borderInfo[i - 1].dummyInnerA;
                        tempInnerLinePointsB[0] = borderInfo[i - 1].dummyInnerB;
                        tempInnerLinePointsA[2] = borderInfo[0].dummyInnerA;
                        tempInnerLinePointsB[2] = borderInfo[0].dummyInnerB;
                    }
                    tempInnerLinePointsA[1] = borderInfo[i].dummyInnerA;
                    tempInnerLinePointsB[1] = borderInfo[i].dummyInnerB;

                    float crossX, crossY; 
                    // Inner point A
                    MathF.LinesCross(tempInnerLinePointsA[0].X, tempInnerLinePointsA[0].Y,
                                        tempInnerLinePointsB[0].X, tempInnerLinePointsB[0].Y,
                                        tempInnerLinePointsA[1].X, tempInnerLinePointsA[1].Y,
                                        tempInnerLinePointsB[1].X, tempInnerLinePointsB[1].Y,
                                        out crossX, out crossY, true);
                    borderInfo[i].innerA = new Vector2(crossX, crossY);
                    // Inner point B
                    MathF.LinesCross(tempInnerLinePointsA[1].X, tempInnerLinePointsA[1].Y,
                                        tempInnerLinePointsB[1].X, tempInnerLinePointsB[1].Y,
                                        tempInnerLinePointsA[2].X, tempInnerLinePointsA[2].Y,
                                        tempInnerLinePointsB[2].X, tempInnerLinePointsB[2].Y,
                                        out crossX, out crossY, true);
                    borderInfo[i].innerB = new Vector2(crossX, crossY);
                }

                UpdateShapes();

                vertexInfo.discarded = true;
            }
            catch (Exception ex) { } 
        }

        private void UpdateShapes()
        {
            UpdateShapes(borderInfo);
        }
        private void UpdateShapes(BorderInfo[] borderInfo)
        {
            if (rb == null) return; 

            // Rigid Body Shapes
            int t = points.Count;
            rb.ClearShapes();

            // Ear cutting triangulation (needed for triangulated shapes and also for the texture)
            int nLastVertices = lastVertices.Count;
            CPoint2D[] vertices = new CPoint2D[t];
            for (int i = 0; i < t; i++)
            {
                if (nLastVertices != t || vertices[i] == null || vertices[i].X != points[i].X || vertices[i].Y != points[i].Y)
                {
                    CPoint2D v = new CPoint2D(points[i].X, points[i].Y);
                    vertices[i] = v;
                    lastVertices.Add(v);
                }
            }
            cutPolygon = new CPolygonShape(vertices);
            cutPolygon.CutEar();

            if (shapeMode == BodyShapeMode.Border)
            {
                foreach (BorderInfo bi in borderInfo)
                {
                    PolyShapeInfo shape = new PolyShapeInfo() { Vertices = bi.Polygon };
                    rb.AddShape(shape);
                }
            }
            else // Triangulated shapes
            {
                if (cutPolygon != null)
                {
                    for (int i = 0; i < cutPolygon.NumberOfPolygons; i++)
                    {
                        int nPoints = cutPolygon.Polygons(i).Length;
                        Vector2[] tempArray = new Vector2[nPoints];
                        for (int j = 0; j < nPoints; j++)
                        {
                            tempArray[j].X = (int)cutPolygon.Polygons(i)[j].X;
                            tempArray[j].Y = (int)cutPolygon.Polygons(i)[j].Y;
                        }
                        PolyShapeInfo shape = new PolyShapeInfo() { Vertices = tempArray };
                        rb.AddShape(shape);
                    }
                }
            }
        }

        #endregion

        #region ICmpRenderer

        public bool IsVisible(IDrawDevice device)
        {
            bool ret = false;

            if (points != null && (device.VisibilityMask & VisibilityFlag.ScreenOverlay) == VisibilityFlag.None)
            {
                Vector2 min = new Vector2(points.Min(x => x.X), points.Min(x => x.Y));
                Vector2 max = new Vector2(points.Max(x => x.X), points.Max(x => x.Y));
                Vector3 screenMin = device.GetScreenCoord(min);
                Vector3 screenMax = device.GetScreenCoord(max);
                ret = (screenMax.X >= 0 && screenMax.Y >= 0) || (screenMin.X <= device.TargetSize.X && screenMin.Y <= device.TargetSize.Y);
            }

            return ret;
        }
        
        public void Draw(IDrawDevice device)
        {
            if (!working)
            {
                working = true;

                try
                {
                    if (updatableUsingMouse && isSelected && DualityApp.ExecEnvironment != DualityApp.ExecutionEnvironment.Editor)
                    {
                        ctrlPressed = DualityApp.Keyboard[Duality.Input.Key.ControlLeft] || DualityApp.Keyboard[Duality.Input.Key.ControlRight];
                        mouseLeft = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Left);
                        mouseRight = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Right);
                        this.mouseX = DualityApp.Mouse.X;
                        this.mouseY = DualityApp.Mouse.Y;
                    }

                    Vector3 mouse = device.GetSpaceCoord(new Vector2(this.mouseX, this.mouseY));

                    int t = points.Count;

                    // Double check for added/removed points
                    if (borderInfo == null || t != borderInfo.Length)
                    {
                        UpdateBody(true);
                        t = points.Count;
                        if (borderInfo == null || t != borderInfo.Length)
                        {
                            working = false;
                            return;
                        }
                    }

                    Texture mainTex = this.mainMaterial.Res.MainTexture.Res;
                    Texture borderTex = this.borderMaterial.Res.MainTexture.Res;
                    
                    if (this.vertexInfo == null || this.vertexInfo.discarded) CreateVertices(device);
                    //else if (camera3D != null) CreateVertices(device, false, true, false, true, false, true);

                    VertexInfo vertexInfo = this.vertexInfo.DeepClone();
                    Transform camTr = this.GameObj.ParentScene.FindComponent<Camera>()?.GameObj.GetComponent<Transform>();

                    if (showMaterial) // Main Texture
                    {
                        foreach (VertexC1P3T2[] vi in vertexInfo.material)
                        {
                            TransformVertices(device, trans, vi);
                            if (staticPosMainMaterial)
                            {
                                float ratioX = 1f / mainTex.Size.X;
                                float ratioY = 1f / mainTex.Size.Y;
                                for (int j = 0; j < 3; j++)
                                {
                                    vi[j].TexCoord.X = vi[j].Pos.X * ratioX;
                                    vi[j].TexCoord.Y = vi[j].Pos.Y * ratioY;
                                    if (!staticAngleMainMaterial) TransformPoint(trans, ref vi[j].TexCoord.X, ref vi[j].TexCoord.Y, false, true, false, false, true);
                                }
                            }
                            else if (staticAngleMainMaterial)
                            {
                                for (int j = 0; j < 3; j++)
                                {
                                    TransformPoint(trans, ref vi[j].TexCoord.X, ref vi[j].TexCoord.Y, false, true, false);
                                }
                            }
                            device.AddVertices(mainMaterial, VertexMode.TriangleFan, vi);
                        }
                    }
                    if (showBorderMaterial)
                    {
                        foreach (VertexC1P3T2[] vi in vertexInfo.borderMaterial)
                        {
                            TransformVertices(device, trans, vi);
                            device.AddVertices(borderMaterial, VertexMode.TriangleFan, vi);
                        }
                    }
                    if (showPolygons)
                    {
                        foreach (VertexC1P3T2[] vi in vertexInfo.polygons)
                        {
                            TransformVertices(device, trans, vi);
                            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, vi);
                        }
                    }
                    if (showBorderGeometry)
                    {
                        foreach (VertexC1P3T2[] vi in vertexInfo.borderGeometry)
                        {
                            TransformVertices(device, trans, vi);
                            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, vi);
                        }
                    }
                    if (showDummies)
                    {
                        foreach (VertexC1P3T2[] vi in vertexInfo.dummies)
                        {
                            TransformVertices(device, trans, vi);
                            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, vi);
                        }
                    }

                    // Limits are calculated in real time
                    if (showLimits)
                    {
                        // Game object center
                        Vector3 p = new Vector3(trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                        float scale = trans.Scale;
                        device.PreprocessCoords(ref p, ref scale);
                        vertexInfo.limits.Add(GetCircle(device, p.Xy, lineWidth * 5f + 1, ColorRgba.White.WithAlpha(.5f)));

                        Vector2[] tempPoints = new Vector2[t];
                        for (int i = 0; i < t; i++)
                        {
                            p = new Vector3(points[i]);
                            TransformPoint(trans, ref p.X, ref p.Y);
                            p.Z = trans.Pos.Z;
                            scale = trans.Scale;
                            device.PreprocessCoords(ref p, ref scale);
                            tempPoints[i] = p.Xy;
                        }
                        Vector2 min = new Vector2(tempPoints.Min(x => x.X), tempPoints.Min(x => x.Y));
                        Vector2 max = new Vector2(tempPoints.Max(x => x.X), tempPoints.Max(x => x.Y));
                        float realX = min.X + (max.X - min.X) * .5f;
                        float realY = min.Y + (max.Y - min.Y) * .5f;
                        vertexInfo.limits.AddRange(new[] {
                            // Body center
                            GetCircle(device, new Vector2(realX, realY), lineWidth * 4f + 1, ColorRgba.Black.WithAlpha(.5f)),
                            // Inner bounding Rect
                            GetLine(device, min, new Vector2(max.X, min.Y), lineWidth, ColorRgba.Black.WithAlpha(.5f)),
                            GetLine(device, new Vector2(max.X, min.Y), max, lineWidth, ColorRgba.Black.WithAlpha(.5f)),
                            GetLine(device, new Vector2(min.X, max.Y), max, lineWidth, ColorRgba.Black.WithAlpha(.5f)),
                            GetLine(device, min, new Vector2(min.X, max.Y), lineWidth, ColorRgba.Black.WithAlpha(.5f))
                        });
                        // Outer bounding Rect
                        min = new Vector2(float.MaxValue, float.MaxValue);
                        max = new Vector2(float.MinValue, float.MinValue);
                        for (int i = 0; i < t; i++)
                        {
                            for (int j = 0; j < 3; j++)
                            {
                                p = new Vector3(borderInfo[i].Polygon[j]);
                                TransformPoint(trans, ref p.X, ref p.Y);
                                p.Z = trans.Pos.Z;
                                scale = trans.Scale;
                                device.PreprocessCoords(ref p, ref scale);

                                if (p.X < min.X) min.X = p.X;
                                else if (p.X > max.X) max.X = p.X;
                                if (p.Y < min.Y) min.Y = p.Y;
                                else if (p.Y > max.Y) max.Y = p.Y;
                            }
                        }
                        realX = min.X + (max.X - min.X) * .5f;
                        realY = min.Y + (max.Y - min.Y) * .5f;
                        vertexInfo.limits.AddRange(new[] {
                            GetCircle(device, new Vector2(realX, realY), lineWidth * 3f + 1, ColorRgba.White.WithAlpha(.5f)),
                            GetLine(device, min, new Vector2(max.X, min.Y), lineWidth, ColorRgba.White.WithAlpha(.5f)),
                            GetLine(device, new Vector2(max.X, min.Y), max, lineWidth, ColorRgba.White.WithAlpha(.5f)),
                            GetLine(device, new Vector2(min.X, max.Y), max, lineWidth, ColorRgba.White.WithAlpha(.5f)),
                            GetLine(device, min, new Vector2(min.X, max.Y), lineWidth, ColorRgba.White.WithAlpha(.5f))
                        });

                        // Snap lines
                        if (isSelected && ctrlPressed)
                        {
                            for (int i = 0; i < t; i++)
                            {
                                vertexInfo.limits.AddRange(new[] {
                                    GetLine(device, new Vector2(min.X, borderInfo[i].outerB.Y), new Vector2(max.X, borderInfo[i].outerB.Y), lineWidth, ColorRgba.White.WithAlpha(.5f)),
                                    GetLine(device, new Vector2(borderInfo[i].outerB.X, min.Y), new Vector2(borderInfo[i].outerB.X, max.Y), lineWidth, ColorRgba.White.WithAlpha(.5f))
                                });
                            }
                        }

                        foreach (VertexC1P3T2[] vi in vertexInfo.limits)
                        {
                            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, vi);
                        }
                    }

                    //if (isSelected)
                    //{
                    //    for (int i = 0; i < t; i++)
                    //    {
                    //        Vector2 point = new Vector2(borderInfo[i].outerB.X, borderInfo[i].outerB.Y);
                    //        TransformPoint(trans, ref point.X, ref point.Y);

                    //        // Point selection
                    //        if (MathF.Distance(mouse.X, mouse.Y, point.X, point.Y) < lineWidth * 4f)
                    //        {
                    //            if (selectedPointId == -1) selectedPointId = i;
                    //        }

                    //        int segments = ctrlPressed ? 4 : 8;
                    //        float radius = ctrlPressed ? lineWidth * 4f + 1f : lineWidth * 4f;
                    //        if (i == selectedPointId)
                    //        {
                    //            if (ctrlPressed) device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, point, radius, ColorRgba.White, segments));
                    //            else device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, point, radius, ColorRgba.White, segments));
                    //        }
                    //        else
                    //        {
                    //            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, point, radius + 1, ColorRgba.White, segments));
                    //            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, point, radius, ColorRgba.Black, segments));
                    //        }
                    //    }

                    //    // Mouse
                    //    if (updatableUsingMouse)
                    //    {
                    //        float transformedMouseX = mouse.X;
                    //        float transformedMouseY = mouse.Y;
                    //        Vector2 a = new Vector2(transformedMouseX - 10, transformedMouseY);
                    //        Vector2 b = new Vector2(transformedMouseX + 10, transformedMouseY);
                    //        Vector2 c = new Vector2(transformedMouseX, transformedMouseY - 10);
                    //        Vector2 d = new Vector2(transformedMouseX, transformedMouseY + 10);
                    //        device.AddVertices(Material.InvertWhite, VertexMode.Quads, GetLine(device, a, b, 3, ColorRgba.White));
                    //        device.AddVertices(Material.InvertWhite, VertexMode.Quads, GetLine(device, c, d, 3, ColorRgba.White));

                    //        if (isSelected)
                    //        {
                    //            if (mouseLeft)
                    //            {
                    //                if (selectedPointId > -1) // Move point
                    //                {
                    //                    if (ctrlPressed)
                    //                    {
                    //                        for (int i = 0; i < t; i++)
                    //                        {
                    //                            // Transformed position i
                    //                            float transformedX = points[i].X;
                    //                            float transformedY = points[i].Y;
                    //                            TransformPoint(trans, ref transformedX, ref transformedY);

                    //                            float xDistance = Math.Abs(mouse.X - transformedX);
                    //                            float yDistance = Math.Abs(mouse.Y - transformedY);

                    //                            if (xDistance < 10) mouse.X = transformedX;
                    //                            else if (yDistance < 10) mouse.Y = transformedY;
                    //                        }
                    //                    }

                    //                    transformedMouseX = mouse.X - trans.Pos.X;
                    //                    transformedMouseY = mouse.Y - trans.Pos.Y;
                    //                    TransformPoint(trans, ref transformedMouseX, ref transformedMouseY, false, true, true, false, true);
                    //                    points[selectedPointId] = new Vector2(transformedMouseX, transformedMouseY);
                    //                    UpdateBody(true);
                    //                }
                    //                else if (!ctrlPressed) // Add point
                    //                {
                    //                    // When adding a point, we need to sort the points for a good triangulation
                    //                    float nearestDistance = float.MaxValue;
                    //                    int nearestId = 0;
                    //                    if (t > 2)
                    //                    {
                    //                        for (int i = 0; i < t; i++)
                    //                        {
                    //                            float distance = MathF.Distance(mouse.X, mouse.Y, borderInfo[i].outerB.X, borderInfo[i].outerB.Y);
                    //                            if (distance < nearestDistance)
                    //                            {
                    //                                nearestDistance = distance;
                    //                                nearestId = i;
                    //                            }
                    //                        }
                    //                    }

                    //                    int id;
                    //                    if (t > 2)
                    //                    {
                    //                        id = nearestId;
                    //                    }
                    //                    else
                    //                    {
                    //                        id = t;
                    //                    }
                    //                    points.Insert(Math.Min(t, id), new Vector2(mouse.X, mouse.Y));
                    //                    UpdateBody(true);
                    //                }
                    //            }
                    //            else if (mouseRight)
                    //            {
                    //                if (selectedPointId > -1) // Delete point
                    //                {
                    //                    points.Remove(points[selectedPointId]);
                    //                    selectedPointId = -1;
                    //                    UpdateBody(true);
                    //                }
                    //            }
                    //            else
                    //            {
                    //                selectedPointId = -1;
                    //            }
                    //        }
                    //    }
                    //}
                }
                catch (Exception ex) { }

                working = false;
            }
        }

        #endregion

        #region ICmpInitializable

        public void OnInit(InitContext context)
        {
            if (context == InitContext.Activate)
            {
                trans = this.GameObj.GetComponent<Transform>();
                rb = this.GameObj.GetComponent<RigidBody>();
                UpdateBody(true);
            }
        }

        public void OnShutdown(ShutdownContext context)
        {
            //throw new NotImplementedException();
        }

        #endregion

        protected override void OnCopyDataTo(object targetObj, ICloneOperation operation)
        {
            base.OnCopyDataTo(targetObj, operation); 
            ComplexBody target = targetObj as ComplexBody;

            target.polygonColor = this.polygonColor;
            target.borderWidth = this.borderWidth;
            target.mainMaterial = this.mainMaterial;
            target.borderMaterial = this.borderMaterial;
            target.points = new List<Vector2>(this.points.ToArray());
            target.showBorderMaterial = this.showBorderMaterial;
            target.showPolygons = this.showPolygons;
            target.showMaterial = this.showMaterial;
            target.updatableUsingMouse = this.updatableUsingMouse;
            target.showBorderGeometry = this.showBorderGeometry;
            target.showDummies = this.showDummies;
            target.lineWidth = this.lineWidth;
            target.borderGeometryColor = this.borderGeometryColor;
            target.dummyColor = this.dummyColor;
            target.shapeMode = this.shapeMode;
            target.borderType = this.borderType;
            target.camera3D = this.camera3D;
            target.showLimits = this.showLimits;
            target.borderTexFlip = this.borderTexFlip;
            target.staticPosMainMaterial = this.staticPosMainMaterial;
            target.staticAngleMainMaterial = this.staticAngleMainMaterial;
            target.UpdateBody(true);
        } 
    }
}
