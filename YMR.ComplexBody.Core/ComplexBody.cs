﻿using Duality;
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
        Transform trans = null;
        [DontSerialize]
        RigidBody rb = null;
        [DontSerialize]
        BorderInfo[] borderInfo = null;
        [DontSerialize]
        CPolygonShape cutPolygon = null;
        [DontSerialize]
        Vector3 mousePos;

        private List<Vector2> points = new List<Vector2>()
        {
            new Vector2(0, 0), 
            new Vector2(100, 0), 
            new Vector2(100, 100), 
        };
        private bool showBorderMaterial = true;
        private bool showBorderGeometry = true;
        private bool showDummies = false;
        private bool showPolygons = true;
        private bool showMaterial = true;
        private bool updatableUsingMouse = true;
        private ContentRef<Material> sharedMaterial = Material.Checkerboard;
        private ContentRef<Material> borderMaterial = Material.Checkerboard;
        private float borderWidth = 2;
        private float lineWidth = 2;
        private ColorRgba polygonColor = new ColorRgba(255, 0, 0, 200);
        private ColorRgba borderGeometryColor = new ColorRgba(0, 255, 0, 200);
        private ColorRgba dummyColor = new ColorRgba(0, 0, 255, 200);
        private bool scaleTexture = false;
        private BodyShapeMode shapeMode = BodyShapeMode.Triangulation;
        private BoderMode borderType = BoderMode.Inside;
        private Camera camera3D = null;

        #endregion

        #region Public Members

        /// <summary>
        /// Body vertices
        /// </summary>
        public List<Vector2> Points { get { return points; } set { points = value; } }
        public bool ShowBorderMaterial { get { return showBorderMaterial; } set { showBorderMaterial = value; } }
        public bool ShowBorderGeometry { get { return showBorderGeometry; } set { showBorderGeometry = value; } }
        public bool ShowDummies { get { return showDummies; } set { showDummies = value; } }
        public bool ShowPolygons { get { return showPolygons; } set { showPolygons = value; } }
        public bool ShowMaterial { get { return showMaterial; } set { showMaterial = value; } }
        public bool UpdatableUsingMouse { get { return updatableUsingMouse; } set { updatableUsingMouse = value; } }
        public ContentRef<Material> SharedMaterial { get { return sharedMaterial; } set { sharedMaterial = value; } }
        public ContentRef<Material> BorderMaterial { get { return borderMaterial; } set { borderMaterial = value; } }
        public float BoundRadius { get { return this.GameObj.GetComponent<RigidBody>().BoundRadius; } }
        public float BorderWidth { get { return borderWidth; } set { borderWidth = value; UpdateBody(true); } }
        public float LineWidth { get { return lineWidth; } set { lineWidth = value; } }
        public ColorRgba PolygonColor { get { return polygonColor; } set { polygonColor = value; } }
        public ColorRgba DummyColor { get { return dummyColor; } set { dummyColor = value; } }
        public ColorRgba BorderGeometryColor { get { return borderGeometryColor; } set { borderGeometryColor = value; } }
        public bool ScaleTexture { get { return scaleTexture; } set { scaleTexture = value; } }
        public BodyShapeMode ShapeMode { get { return shapeMode; } set { shapeMode = value; UpdateBody(true); } }
        public BoderMode BorderType { get { return borderType; } set { borderType = value; UpdateBody(true); } }
        /// <summary>
        /// If set, the 3D effect will use the camera as center point.
        /// </summary>
        public Camera Camera3D { get { return camera3D; } set { camera3D = value; UpdateBody(true); } }

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

        #region Private Methods

        private static void TransformVertices(Transform trans, VertexC1P3T2[] vertices)
        {
            int t = vertices.Length;
            for(int i = 0; i < t; i++)
            {
                VertexC1P3T2 vertex = vertices[i];
                TransformPoint(trans, ref vertex.Pos.X, ref vertex.Pos.Y);
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
            float angle = applyAngle ? invertAngle ? -trans.Angle : trans.Angle : 0;
            float scale = applyScale ? trans.Scale : 1;
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
            //Rect pointRect = new Rect(radius, radius);
            Vector3 tempPos = new Vector3(point);
            float tempScale = 1f;
            device.PreprocessCoords(ref tempPos, ref tempScale);
            float angleStep = MathF.RadAngle360 / 8f;
            VertexC1P3T2[] vertices = new VertexC1P3T2[10];
            vertices[0].Pos = tempPos;
            vertices[0].Color = color;
            //vertices[0].TexCoord = pointRect.Center;
            float pointAngle = 0f;
            for (int j = 1; j < 10; j++)
            {
                float sin = (float)Math.Sin(pointAngle);
                float cos = (float)Math.Cos(pointAngle);
                vertices[j].Pos.X = tempPos.X + sin * radius;
                vertices[j].Pos.Y = tempPos.Y - cos * radius;
                vertices[j].Pos.Z = tempPos.Z;
                vertices[j].Color = color;
                //vertices[j].TexCoord.X = pointRect.X + (0.5f + 0.5f * sin) * pointRect.W;
                //vertices[j].TexCoord.Y = pointRect.Y + (0.5f - 0.5f * cos) * pointRect.H;
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
            float tempScale = 1f;
            device.PreprocessCoords(ref tempPosA, ref tempScale);
            device.PreprocessCoords(ref tempPosB, ref tempScale);
            Vector2 dir = (tempPosB.Xy - tempPosA.Xy).Normalized;
            if (!scaleWidth) tempScale = 1f;
            Vector2 left = dir.PerpendicularLeft * width * 0.5f * tempScale;
            Vector2 right = dir.PerpendicularRight * width * 0.5f * tempScale;
            Vector2 left2 = dir.PerpendicularLeft * width * 0.5f * tempScale;
            Vector2 right2 = dir.PerpendicularRight * width * 0.5f * tempScale;
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
            return GetPoly(device, points, color, new Vector2[] { Vector2.Zero, Vector2.One });
        }
        private VertexC1P3T2[] GetPoly(IDrawDevice device, Vector2[] points, ColorRgba color, Vector2[] texCoord)
        {
            Vector3 tempPos = new Vector3(trans.Pos.X, trans.Pos.Y, trans.Pos.Z);

            float tempScale = 1.0f;
            device.PreprocessCoords(ref tempPos, ref tempScale);

            ColorRgba shapeColor = color;

            // Determine bounding box
            Rect pointBoundingRect = points.BoundingBox();

            // Set up vertex array
            int t = points.Length;
            VertexC1P3T2[] vertices = new VertexC1P3T2[t];
            for (int i = 0; i < t; i++)
            {
                vertices[i].Pos.X = points[i].X * tempScale + tempPos.X;
                vertices[i].Pos.Y = points[i].Y * tempScale + tempPos.Y;
                vertices[i].Pos.Z = tempPos.Z;
                vertices[i].TexCoord.X = texCoord[0].X + ((points[i].X - pointBoundingRect.X) / pointBoundingRect.W) * texCoord[1].X;
                vertices[i].TexCoord.Y = texCoord[0].Y + ((points[i].Y - pointBoundingRect.Y) / pointBoundingRect.H) * texCoord[1].X;
                vertices[i].Color = shapeColor;
            }

            TransformVertices(trans, vertices);

            return vertices;
        }

        #endregion

        #region Public Methods

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
                    IEnumerable<ShapeInfo> shapes = rb.Shapes.Where(x => x.GetType() == typeof(PolyShapeInfo));

                    Rect boundingRect = points.BoundingBox();

                    float brW = scaleTexture ? boundingRect.W : this.borderMaterial.Res.MainTexture.Res.Size.X;
                    float brH = scaleTexture ? boundingRect.H : this.borderMaterial.Res.MainTexture.Res.Size.Y;
                    float ratioW = brH / brW;
                    float ratioH = brW / brH;

                    Vector2 p0, p1;
                    p0 = new Vector2(points[i].X, points[i].Y);
                    if (i < t - 1) p1 = new Vector2(points[i + 1].X, points[i + 1].Y);
                    else p1 = new Vector2(points[0].X, points[0].Y);
                        
                    if (borderInfo == null) borderInfo = new BorderInfo[t];
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
        }

        private void UpdateShapes()
        {
            UpdateShapes(borderInfo);
        }
        private void UpdateShapes(BorderInfo[] borderInfo)
        {
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
            float angle = trans.Angle;
            Vector2 scale = new Vector2(trans.Scale, trans.Scale);

            if (updatableUsingMouse && DualityApp.ExecEnvironment != DualityApp.ExecutionEnvironment.Editor)
            {
                ctrlPressed = DualityApp.Keyboard[Duality.Input.Key.ControlLeft] || DualityApp.Keyboard[Duality.Input.Key.ControlRight];
                mouseLeft = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Left);
                mouseRight = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Right);
                this.mouseX = DualityApp.Mouse.X;
                this.mouseY = DualityApp.Mouse.Y;
            }
            

            Vector3 mouse = device.GetSpaceCoord(new Vector2(this.mouseX, this.mouseY));

            int t = points.Count;
            
            Rect boundingRect = points.BoundingBox();
            Texture mainTex = this.sharedMaterial.Res.MainTexture.Res;
            Texture borderTex = this.borderMaterial.Res.MainTexture.Res;
            ColorRgba mainColor = this.sharedMaterial.Res.MainColor;
            ColorRgba borderColor = this.borderMaterial.Res.MainColor;
            float brW = scaleTexture ? boundingRect.W : mainTex.Size.X;
            float brH = scaleTexture ? boundingRect.H : mainTex.Size.Y;
            float ratioW = brH / brW;
            float ratioH = brW / brH;

            if (cutPolygon != null && points.Count > 2)
            {
                // Texture
                if (showMaterial || showPolygons)
                {
                    //Rect boundingRect = points.BoundingBox();

                    //float brW = scaleTexture ? boundingRect.W : this.sharedMaterial.Res.MainTexture.Res.Size.X;
                    //float brH = scaleTexture ? boundingRect.H : this.sharedMaterial.Res.MainTexture.Res.Size.Y;
                    //float ratioW = brH / brW;
                    //float ratioH = brW / brH;

                    int tPol = cutPolygon.NumberOfPolygons;
                    for (int i = 0; i < tPol; i++)
                    {
                        CPoint2D[] points = cutPolygon.Polygons(i);
                        int nPoints = cutPolygon.Polygons(i).Length;
                        Vector2[] tempArray = new Vector2[nPoints];
                        for (int j = 0; j < nPoints; j++)
                        {
                            tempArray[j].X = (int)cutPolygon.Polygons(i)[j].X;
                            tempArray[j].Y = (int)cutPolygon.Polygons(i)[j].Y;
                        }

                        //Material
                        if (showMaterial)
                        {
                            Rect localRect = AABB(tempArray);
                            Vector2[] texCoord = new Vector2[] {
                                new Vector2((1 / brW) * localRect.X, (1 / brH) * localRect.Y),
                                new Vector2((1 / brW) * localRect.W, (1 / brH) * localRect.H)
                            };
                            device.AddVertices(sharedMaterial, VertexMode.TriangleFan, GetPoly(device, tempArray, mainColor, texCoord));
                        }
                        // Polygons
                        if (showPolygons)
                        {
                            TransformPoint(trans, ref tempArray[0].X, ref tempArray[0].Y);
                            TransformPoint(trans, ref tempArray[1].X, ref tempArray[1].Y);
                            TransformPoint(trans, ref tempArray[2].X, ref tempArray[2].Y);
                            device.AddVertices(Material.SolidWhite, VertexMode.Quads, GetLine(device, tempArray[0], tempArray[1], lineWidth, polygonColor));
                            device.AddVertices(Material.SolidWhite, VertexMode.Quads, GetLine(device, tempArray[1], tempArray[2], lineWidth, polygonColor));
                            device.AddVertices(Material.SolidWhite, VertexMode.Quads, GetLine(device, tempArray[0], tempArray[2], lineWidth, polygonColor));
                        }
                    }
                }
            }

            BorderInfo[] bis3D = new BorderInfo[t];
            for (int i = 0; i < t; i++)
            {
                BorderInfo bi = borderInfo[i].Clone();

                // Point selection
                if (MathF.Distance(mouse.X, mouse.Y, bi.outerA.X, bi.outerA.Y) < lineWidth * 4f)
                {
                    if (selectedPointId == -1) selectedPointId = i;
                }

                // Border and points
                if (showBorderMaterial || showBorderGeometry || showDummies || isSelected)
                {
                    Vector2 p0, p1;
                    p0 = new Vector2(MathF.RoundToInt(points[i].X), MathF.RoundToInt(points[i].Y));
                    if (i < t - 1) p1 = new Vector2(MathF.RoundToInt(points[i + 1].X), MathF.RoundToInt(points[i + 1].Y));
                    else p1 = new Vector2(points[0].X, points[0].Y);

                    if (camera3D != null)
                    {
                        Transform camTrans = camera3D.GameObj.AddComponent<Transform>();
                        if (borderType == BoderMode.Inside)
                        {
                            bi.innerA = new Vector2(bi.innerA.X - (trans.Pos.X - camTrans.Pos.X) / MathF.Abs(camTrans.Pos.Z * .01f),
                                                    bi.innerA.Y - (trans.Pos.Y - camTrans.Pos.Y) / MathF.Abs(camTrans.Pos.Z * .01f));
                            bi.innerB = new Vector2(bi.innerB.X - (trans.Pos.X - camTrans.Pos.X) / MathF.Abs(camTrans.Pos.Z * .01f),
                                                    bi.innerB.Y - (trans.Pos.Y - camTrans.Pos.Y) / MathF.Abs(camTrans.Pos.Z * .01f));
                        }
                        else // Outside
                        {
                            bi.innerA = new Vector2(bi.innerA.X + (trans.Pos.X - camTrans.Pos.X) / MathF.Abs(camTrans.Pos.Z * .01f),
                                                    bi.innerA.Y + (trans.Pos.Y - camTrans.Pos.Y) / MathF.Abs(camTrans.Pos.Z * .01f));
                            bi.innerB = new Vector2(bi.innerB.X + (trans.Pos.X - camTrans.Pos.X) / MathF.Abs(camTrans.Pos.Z * .01f),
                                                    bi.innerB.Y + (trans.Pos.Y - camTrans.Pos.Y) / MathF.Abs(camTrans.Pos.Z * .01f));
                        }
                        bis3D[i] = bi;
                    }

                    Vector2[] borderPoly = bi.Polygon;
                    bi.Transform(trans);

                    //if (showBorderMaterial)
                    //{
                    //    Rect localRect = AABB(borderPoly);

                    //    canvas.PushState();
                    //    canvas.State.SetMaterial(this.borderMaterial);
                    //    canvas.State.TransformAngle = angle;
                    //    canvas.State.TransformScale = scale;
                    //    canvas.State.TextureCoordinateRect = new Rect(
                    //        (1 / brW) * localRect.X,
                    //        (1 / brH) * localRect.Y,
                    //        (1 / brW) * localRect.W,
                    //        (1 / brH) * localRect.H
                    //    );
                    //    canvas.FillPolygon(borderPoly, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                    //    canvas.PopState();
                    //}

                    if (showDummies)
                    {
                        device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.outerCenter, lineWidth * 2f, dummyColor));
                        device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.innerCenter, lineWidth * 2f, dummyColor));
                        device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.dummyInnerA, lineWidth * 2f, dummyColor));
                        device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.dummyInnerB, lineWidth * 2f, dummyColor));
                        device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.innerA, lineWidth * 2f, dummyColor));
                        device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.innerB, lineWidth * 2f, dummyColor));
                        device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.center, lineWidth * 2f, dummyColor));

                        device.AddVertices(Material.SolidWhite, VertexMode.Quads, GetLine(device, bi.outerCenter, bi.innerCenter, lineWidth, dummyColor));
                        device.AddVertices(Material.SolidWhite, VertexMode.Quads, GetLine(device, bi.dummyInnerA, bi.dummyInnerB, lineWidth, dummyColor));
                    }

                    if (showBorderGeometry)
                    {
                        device.AddVertices(Material.SolidWhite, VertexMode.Quads, GetLine(device, bi.outerA, bi.outerB, lineWidth, borderGeometryColor));
                        device.AddVertices(Material.SolidWhite, VertexMode.Quads, GetLine(device, bi.outerA, bi.innerB, lineWidth, borderGeometryColor));
                        device.AddVertices(Material.SolidWhite, VertexMode.Quads, GetLine(device, bi.innerA, bi.innerB, lineWidth, borderGeometryColor));
                    }

                    if (isSelected)
                    {
                        if (i == selectedPointId)
                        {
                            if (ctrlPressed)
                            {
                                Vector2[] vertices = new Vector2[] {
                                    new Vector2(bi.outerA.X - lineWidth * 4f, bi.outerA.Y - lineWidth * 4f),
                                    new Vector2(bi.outerA.X + lineWidth * 4f, bi.outerA.Y - lineWidth * 4f),
                                    new Vector2(bi.outerA.X + lineWidth * 4f, bi.outerA.Y + lineWidth * 4f),
                                    new Vector2(bi.outerA.X - lineWidth * 4f, bi.outerA.Y + lineWidth * 4f)
                                };
                                device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetPoly(device, vertices, ColorRgba.White));
                            }
                            else device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.outerA, lineWidth * 4f, ColorRgba.White));
                        }
                        else
                        {
                            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.outerA, lineWidth * 4f + 1, ColorRgba.White));
                            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, bi.outerA, lineWidth * 4f, ColorRgba.Black));
                        }
                    }
                }

                // Snap lines
                if (ctrlPressed)
                {
                    //Vector2 vXY = device.TargetSize;
                    //Vector2 a = new Vector2(0f, borderPoly[1].Y)

                    //device.AddVertices(Material.SolidWhite, VertexMode.Quads, GetLine(device, a, b, lineWidth * 8f, ColorRgba.White));

                    //canvas.PushState();
                    //canvas.State.ColorTint = new ColorRgba(255, 255, 255, 50);
                    //canvas.DrawLine(transformedSnapXMin, transformedY, transformedSnapXMax, transformedY);
                    //canvas.DrawLine(transformedX, transformedSnapYMax, transformedX, transformedSnapYMin);
                    //canvas.PopState();
                }
            }

            if (showDummies)
            {
                // Game object center
                device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, trans.Pos.Xy, lineWidth * 4f + 1, ColorRgba.White.WithAlpha(200)));
                // Body center
                Vector2 min = new Vector2(points.Min(x => x.X), points.Min(x => x.Y));
                Vector2 max = new Vector2(points.Max(x => x.X), points.Max(x => x.Y));
                float realX = min.X + (max.X - min.X) * .5f + trans.Pos.X;
                float realY = min.Y + (max.Y - min.Y) * .5f + trans.Pos.Y;
                device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, trans.Pos.Xy, lineWidth * 4f + 1, dummyColor));
            }

            if (camera3D != null)
            {
                UpdateShapes(bis3D);
            }

            // Mouse
            if (updatableUsingMouse)
            {
                Vector2 a = new Vector2(mouse.X - 10, mouse.Y);
                Vector2 b = new Vector2(mouse.X + 10, mouse.Y);
                Vector2 c = new Vector2(mouse.X, mouse.Y - 10);
                Vector2 d = new Vector2(mouse.X, mouse.Y + 10);
                device.AddVertices(Material.InvertWhite, VertexMode.Quads, GetLine(device, a, b, 3, ColorRgba.White));
                device.AddVertices(Material.InvertWhite, VertexMode.Quads, GetLine(device, c, d, 3, ColorRgba.White));
            }
        }

        #endregion

        #region ICmpInitializable

        public void OnInit(InitContext context)
        {
            if (context == InitContext.Loaded)
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

        #region ICmpUpdatable & ICmpEditorUpdatable

        public void OnUpdate()
        {
            if (updatableUsingMouse && !working)
            {
                if (DualityApp.ExecEnvironment != DualityApp.ExecutionEnvironment.Editor)
                {
                    ctrlPressed = DualityApp.Keyboard[Duality.Input.Key.ControlLeft] || DualityApp.Keyboard[Duality.Input.Key.ControlRight];
                    mouseLeft = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Left);
                    mouseRight = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Right);
                    mouseX = DualityApp.Mouse.X;
                    mouseY = DualityApp.Mouse.Y;
                }

                working = true; 

                if (mouseLeft)
                {
                    float transformedMouseX = mousePos.X;
                    float transformedMouseY = mousePos.Y;
                    Transform transInv = new Transform() { Angle = -trans.Angle, Pos = new Vector3(-trans.Pos.X, -trans.Pos.Y, -trans.Pos.Z) };
                    TransformPoint(transInv, ref transformedMouseX, ref transformedMouseY);

                    int t = points.Count;

                    if (selectedPointId > -1) // Move point
                    {
                        if (ctrlPressed)
                        {
                            for (int i = 0; i < t; i++)
                            {
                                // Transformed position i
                                float transformedX = points[i].X;
                                float transformedY = points[i].Y;
                                TransformPoint(trans, ref transformedX, ref transformedY);

                                float xDistance = Math.Abs(transformedMouseX - transformedX);
                                float yDistance = Math.Abs(transformedMouseY - transformedY);

                                if (xDistance < 10) transformedMouseX = transformedX;
                                else if (yDistance < 10) transformedMouseY = transformedY;
                            }
                        }

                        points[selectedPointId] = new Vector2(transformedMouseX, transformedMouseY);
                    }
                    else if (!ctrlPressed) // Add point
                    {
                        // When adding a point, we need to sort the points for a good triangulation
                        float nearestDistance = float.MaxValue;
                        int nearestId = 0;
                        if (t > 2)
                        {
                            for (int i = 0; i < t; i++)
                            {
                                float distance = MathF.Distance(transformedMouseX, transformedMouseY, points[i].X, points[i].Y);
                                if (distance < nearestDistance)
                                {
                                    nearestDistance = distance;
                                    nearestId = i;
                                }
                            }
                        }

                        int id;
                        if (t > 2)
                        {
                            float prevDistance = float.MaxValue, nextDistance = float.MaxValue;
                            if (nearestId > 0) prevDistance = MathF.Distance(transformedMouseX, transformedMouseY, points[nearestId - 1].X, points[nearestId - 1].Y) - nearestDistance;
                            if (nearestId < t - 1) nextDistance = MathF.Distance(transformedMouseX, transformedMouseY, points[nearestId + 1].X, points[nearestId + 1].Y) - nearestDistance;
                            id = prevDistance < nextDistance ? nearestId + 1 : nearestId + 2;
                        }
                        else
                        {
                            id = t;
                        }
                        points.Insert(Math.Min(t, id), new Vector2(transformedMouseX, transformedMouseY));
                    }
                }
                else if (mouseRight)
                {
                    if (selectedPointId > -1) // Delete point
                    {
                        points.Remove(points[selectedPointId]);
                        selectedPointId = -1;
                    }
                }
                else
                {
                    selectedPointId = -1;
                }

                UpdateBody();

                working = false;
            }
        }

        #endregion

        protected void OnCopyDataTo(object targetObj, ICloneOperation operation)
        {
            base.OnCopyDataTo(targetObj, operation);
            ComplexBody target = targetObj as ComplexBody;

            target.polygonColor = this.polygonColor;
            target.borderWidth = this.borderWidth;
            target.sharedMaterial = this.sharedMaterial;
            target.borderMaterial = this.borderMaterial;
            target.points = new List<Vector2>(this.points.ToArray());
            target.scaleTexture = this.scaleTexture;
            target.showBorderMaterial = this.showBorderMaterial;
            target.showPolygons = this.showPolygons;
            target.showMaterial = this.showMaterial;
            target.updatableUsingMouse = this.updatableUsingMouse;
        }
    }
}
