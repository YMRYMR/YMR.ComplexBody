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
            public Vector2 cornerACenter;
            public Vector2 cornerBCenter;
            public Vector2 cornerAA;
            public Vector2 cornerAB;
            public Vector2 cornerBA;
            public Vector2 cornerBB;
            public Vector2 cornerAAInv;
            public Vector2 cornerABInv;
            public Vector2 cornerBAInv;
            public Vector2 cornerBBInv;
            public Vector2 cornerAACenter;
            public Vector2 cornerABCenter;
            public Vector2 cornerBACenter;
            public Vector2 cornerBBCenter;
            public Vector2 centerA;
            public Vector2 centerB;
            public Vector2 cornerAACenterInv;
            public Vector2 cornerABCenterInv;
            public Vector2 cornerBACenterInv;
            public Vector2 cornerBBCenterInv;
            public Vector2 outerACenter;
            public Vector2 outerBCenter;
            public Vector2 innerACenter;
            public Vector2 innerBCenter;
            public float distanceAB;
            public float distanceAA;
            public float distanceBB;
            public float distanceCenterCenter;
            public float angle;
            public float cornerRadius;
            public float cornerAngleA;
            public float cornerAngleB;

            public Vector2[] Polygon { get { return new Vector2[] { outerA, outerB, innerA, innerB }; } }
            public Vector2[] PolygonForCorners { get { return new Vector2[] { outerA, outerB, outerBCenter, outerACenter }; } }
            public Vector2[] PolygonCornerAOutside { get { return new Vector2[] { outerA, outerACenter, innerBCenter }; } }
            public Vector2[] PolygonCornerBOutside { get { return new Vector2[] { outerB, outerBCenter, innerACenter }; } }
            public Vector2[] PolygonForCornersInside { get { return new Vector2[] { innerACenter, innerBCenter, outerBCenter, outerACenter }; } }
            public Vector2[] PolygonCornerAInside { get { return new Vector2[] { outerA, outerBCenter, innerB }; } }
            public Vector2[] PolygonCornerBInside { get { return new Vector2[] { outerB, outerACenter, innerA }; } }

            public Vector2[] AllPoints
            {
                get
                {
                    return new Vector2[] { outerA, outerB, outerCenter, dummyInnerA, dummyInnerB, innerA, innerB, innerCenter, center,
                        cornerACenter, cornerBCenter, cornerAA, cornerAB, cornerBA, cornerBB, cornerAAInv, cornerABInv, cornerBAInv, cornerBBInv,
                        cornerAACenter, cornerABCenter, cornerBACenter, cornerBBCenter, centerA, centerB,
                        cornerAACenterInv, cornerABCenterInv, cornerBACenterInv, cornerBBCenterInv, outerACenter, outerBCenter, innerACenter, innerBCenter };
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
                    cornerACenter = value[9];
                    cornerBCenter = value[10];
                    cornerAA = value[11];
                    cornerAB = value[12];
                    cornerBA = value[13];
                    cornerBB = value[14];
                    cornerAAInv = value[15];
                    cornerABInv = value[16];
                    cornerBAInv = value[17];
                    cornerBBInv = value[18];
                    cornerAACenter = value[19];
                    cornerABCenter = value[20];
                    cornerBACenter = value[21];
                    cornerBBCenter = value[22];
                    centerA = value[23];
                    centerB = value[24];
                    cornerAACenterInv = value[25];
                    cornerABCenterInv = value[26];
                    cornerBACenterInv = value[27];
                    cornerBBCenterInv = value[28];
                    outerACenter = value[29];
                    outerBCenter = value[30];
                    innerACenter = value[31];
                    innerBCenter = value[32];
                }
            }
            public float[] AllFloats
            {
                get
                {
                    return new float[] { distanceAB, distanceAA, distanceBB, distanceCenterCenter, angle, cornerRadius, cornerAngleA, cornerAngleB };
                }
                set
                {
                    distanceAB = value[0];
                    distanceAA = value[1];
                    distanceBB = value[2];
                    distanceCenterCenter = value[3];
                    angle = value[4];
                    cornerRadius = value[5];
                    cornerAngleA = value[6];
                    cornerAngleB = value[7];
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
        private bool updatableInGame = false;
        private ContentRef<Material> mainMaterial = Material.Checkerboard;
        private ContentRef<Material> borderMaterial = Material.Checkerboard;
        private float borderWidth = 2;
        private float lineWidth = 2;
        private ColorRgba polygonColor = new ColorRgba(255, 0, 0, 200);
        private ColorRgba borderGeometryColor = new ColorRgba(0, 255, 0, 200);
        private ColorRgba dummyColor = new ColorRgba(0, 0, 255, 200);
        private ColorRgba cornerColor = new ColorRgba(200, 0, 200, 200);
        private BodyShapeMode shapeMode = BodyShapeMode.Triangulation;
        private BoderMode borderType = BoderMode.Inside;
        private bool borderTexFlip = false;
        private bool staticPosMainMaterial = false;
        private bool staticAngleMainMaterial = false;
        private int cornerSegmentsPerCircle = 0;

        #endregion

        #region Public Members

        /// <summary>
        /// Body vertices
        /// </summary>
        public List<Vector2> Points { get { return points; } set { points = value; } }
        public bool ShowBorderMaterial { get { return showBorderMaterial; } set { showBorderMaterial = value; vertexInfo.discarded = true; } }
        public bool ShowBorderGeometry { get { return showBorderGeometry; } set { showBorderGeometry = value; vertexInfo.discarded = true; } }
        //public bool ShowDummies { get { return showDummies; } set { showDummies = value; vertexInfo.discarded = true; } }
        public bool ShowLimits { get { return showLimits; } set { showLimits = value; vertexInfo.discarded = true; } }
        public bool ShowPolygons { get { return showPolygons; } set { showPolygons = value; vertexInfo.discarded = true; } }
        public bool ShowMaterial { get { return showMaterial; } set { showMaterial = value; vertexInfo.discarded = true; } }
        public bool UpdatableInGame { get { return updatableInGame; } set { updatableInGame = value; } }
        public ContentRef<Material> MainMaterial { get { return mainMaterial; } set { mainMaterial = value; vertexInfo.discarded = true; } }
        public ContentRef<Material> BorderMaterial { get { return borderMaterial; } set { borderMaterial = value; vertexInfo.discarded = true; } }
        [EditorHintFlags(MemberFlags.Invisible)]
        public float BoundRadius { get { return this.GameObj.GetComponent<RigidBody>().BoundRadius; } }
        public float BorderWidth { get { return borderWidth; } set { borderWidth = value; UpdateBody(true); } }
        public float LineWidth { get { return lineWidth; } set { lineWidth = value; vertexInfo.discarded = true; } }
        public ColorRgba PolygonColor { get { return polygonColor; } set { polygonColor = value; vertexInfo.discarded = true; } }
        //public ColorRgba DummyColor { get { return dummyColor; } set { dummyColor = value; vertexInfo.discarded = true; } }
        //public ColorRgba CornerColor { get { return cornerColor; } set { cornerColor = value; vertexInfo.discarded = true; } }
        public ColorRgba BorderGeometryColor { get { return borderGeometryColor; } set { borderGeometryColor = value; vertexInfo.discarded = true; } }
        public BodyShapeMode ShapeMode { get { return shapeMode; } set { shapeMode = value; UpdateBody(true); } }
        //public BoderMode BorderType { get { return borderType; } set { borderType = value; UpdateBody(true); } }
        public bool StaticPosMainMaterial { get { return staticPosMainMaterial; } set { staticPosMainMaterial = value; } }
        public bool StaticAngleMainMaterial { get { return staticAngleMainMaterial; } set { staticAngleMainMaterial = value; } }
        public bool BorderTexFlip { get { return borderTexFlip; } set { borderTexFlip = value; vertexInfo.discarded = true; } }
        public int CornerSegmentsPerCircle { get { return cornerSegmentsPerCircle; } set { cornerSegmentsPerCircle = value; UpdateBody(true); } }

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
            return GetCircle(device, point, radius, color, segments, 0f, MathF.RadAngle360);
        }
        private VertexC1P3T2[] GetCircle(IDrawDevice device, Vector2 point, float radius, ColorRgba color, int segments, float angleFrom, float angleTo)
        {
            return GetCircle(device, point, radius, color, segments, angleFrom, angleTo, Vector2.Zero, Vector2.One);
        }
        private VertexC1P3T2[] GetCircle(IDrawDevice device, Vector2 point, float radius, ColorRgba color, int segments, float angleFrom, float angleTo, Vector2 minTexCoord, Vector2 maxTexCoord)
        {
            //angleTo = angleFrom + MathF.Abs(MathF.Abs(angleTo) - MathF.Abs(angleFrom));
            if (angleTo - angleFrom != MathF.RadAngle360)
            {
                if (angleTo > angleFrom)
                {
                    float temp = angleTo - MathF.RadAngle360;
                    angleTo = angleFrom;
                    angleFrom = temp;
                }
            }
            Vector3 tempPos = new Vector3(point);
            segments = 1 + (int)((((float)segments - 1f) / 360f) * MathF.RadToDeg(MathF.Abs(angleTo - angleFrom)));
            float angleStep = (angleTo - angleFrom) / segments;
            int t = segments + 2;
            VertexC1P3T2[] vertices = new VertexC1P3T2[t];
            vertices[0].Pos = tempPos;
            vertices[0].Color = color;
            vertices[0].TexCoord = minTexCoord;
            float pointAngle = angleFrom;
            float ratio = maxTexCoord.X / (float)t;
            for (int i = 1; i < t; i++)
            {
                float sin = (float)Math.Sin(pointAngle);
                float cos = (float)Math.Cos(pointAngle);
                vertices[i].Pos.X = tempPos.X + sin * radius;
                vertices[i].Pos.Y = tempPos.Y - cos * radius;
                vertices[i].Color = color;
                //vertices[i].TexCoord = new Vector2(ratio * i, maxTexCoord.Y);
                vertices[i].TexCoord = new Vector2(ratio * sin, maxTexCoord.Y);
                pointAngle += angleStep;
            }
            return vertices;
        }
        private VertexC1P3T2[] GetLine(IDrawDevice device, Vector2 pointA, Vector2 pointB, float width, ColorRgba color)
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
                                float x = 1f - tempArray[j].X * ratioX;
                                float y = 1f - tempArray[j].Y * ratioY;
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

                for (int i = 0; i < t; i++)
                {
                    BorderInfo bi = borderInfo[i].Clone();

                    // Border and points
                    if (borderMaterial || borderGeometry || dummies)
                    {
                        //Vector2[] borderPoly = bi.Polygon;

                        if (borderMaterial)
                        {
                            if (borderType == BoderMode.Inside)
                            {
                                Vector2[] texCoord;
                                if (!borderTexFlip) texCoord = new Vector2[] { new Vector2(1f, 0f), Vector2.Zero, new Vector2(0f, 1f), Vector2.One };
                                else texCoord = new Vector2[] { Vector2.One, new Vector2(0f, 1f), Vector2.Zero, new Vector2(1f, 0f) };

                                int prev = i == 0 ? t - 1 : i - 1;
                                if (borderInfo[prev].innerBCenter == borderInfo[prev].dummyInnerA)
                                {
                                    for (int j = 0; j < 4; j++)
                                    {
                                        texCoord[j].X = 1f - texCoord[j].X;
                                    }
                                }

                                vertexInfo.borderMaterial.Add(GetPoly(device, bi.PolygonForCornersInside, borderColor, texCoord));

                                if (cornerSegmentsPerCircle < 1)
                                {
                                    if (borderTexFlip) texCoord = new Vector2[] { new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f) };
                                    else texCoord = new Vector2[] { new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f) };
                                    vertexInfo.borderMaterial.Add(GetPoly(device, bi.PolygonCornerAOutside, borderColor, texCoord));
                                    vertexInfo.borderMaterial.Add(GetPoly(device, bi.PolygonCornerBOutside, borderColor, texCoord));
                                }
                                else
                                {
                                    float ratio = (1f / borderWidth) * bi.cornerRadius;
                                    Vector2 minTexCoord, maxTexCoord;
                                    if (borderTexFlip == (borderInfo[i].innerBCenter == borderInfo[i].dummyInnerA))
                                    {
                                        minTexCoord = new Vector2(0f, 0f);
                                        maxTexCoord = new Vector2(1f, 1f);
                                    }
                                    else
                                    {
                                        minTexCoord = new Vector2(1f, 1f);
                                        maxTexCoord = new Vector2(0f, 0f);
                                    }
                                    vertexInfo.borderMaterial.Add(GetCircle(device, bi.cornerACenter, borderWidth, borderColor, cornerSegmentsPerCircle, bi.cornerAngleB, bi.cornerAngleA, minTexCoord, maxTexCoord));
                                }
                            }
                            else
                            {
                                Vector2[] texCoord;
                                if (borderTexFlip != (borderType == BoderMode.Inside)) texCoord = new Vector2[] { new Vector2(1f, 0f), Vector2.Zero, new Vector2(0f, 1f), Vector2.One };
                                else texCoord = new Vector2[] { Vector2.One, new Vector2(0f, 1f), Vector2.Zero, new Vector2(1f, 0f) };
                                vertexInfo.borderMaterial.Add(GetPoly(device, bi.PolygonForCorners, borderColor, texCoord));

                                if (cornerSegmentsPerCircle < 1)
                                {
                                    if (borderTexFlip) texCoord = new Vector2[] { new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f) };
                                    else texCoord = new Vector2[] { new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f) };
                                    vertexInfo.borderMaterial.Add(GetPoly(device, bi.PolygonCornerAOutside, borderColor, texCoord));
                                    vertexInfo.borderMaterial.Add(GetPoly(device, bi.PolygonCornerBOutside, borderColor, texCoord));
                                }
                                else
                                {
                                    float ratio = (1f / borderWidth) * bi.cornerRadius;
                                    Vector2 minTexCoord, maxTexCoord;
                                    if (borderTexFlip != (borderType == BoderMode.Inside))
                                    {
                                        minTexCoord = new Vector2(0f, 0f);
                                        maxTexCoord = new Vector2(1f, 1f);
                                    }
                                    else
                                    {
                                        minTexCoord = new Vector2(1f, 1f);
                                        maxTexCoord = new Vector2(0f, 0f);
                                    }
                                    vertexInfo.borderMaterial.Add(GetCircle(device, bi.outerB, borderWidth, borderColor, cornerSegmentsPerCircle, bi.cornerAngleB, bi.cornerAngleA, minTexCoord, maxTexCoord));
                                }
                            }
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
                                GetCircle(device, bi.centerA, lineWidth * 2f, dummyColor),
                                GetCircle(device, bi.centerB, lineWidth * 2f, dummyColor),
                                GetCircle(device, bi.outerA, lineWidth * 2f, dummyColor),
                                GetLine(device, bi.outerCenter, bi.innerCenter, lineWidth, dummyColor),
                                GetLine(device, bi.dummyInnerA, bi.dummyInnerB, lineWidth, dummyColor),

                                GetCircle(device, bi.outerACenter, lineWidth * 2f, ColorRgba.Red),
                                GetCircle(device, bi.outerBCenter, lineWidth * 2f, ColorRgba.Red),
                                GetCircle(device, bi.innerACenter, lineWidth * 2f, ColorRgba.Red),
                                GetCircle(device, bi.innerBCenter, lineWidth * 2f, ColorRgba.Red),
                            });

                            // Corners
                            if (borderType != BoderMode.Inside)
                            {
                                vertexInfo.dummies.AddRange(new[] {
                                    GetCircle(device, bi.cornerACenter, lineWidth * 2f, cornerColor),
                                    GetCircle(device, bi.cornerAA, lineWidth * 2f, cornerColor),
                                    GetCircle(device, bi.cornerAB, lineWidth * 2f, cornerColor),
                                    GetCircle(device, bi.cornerAAInv, lineWidth * 2f, cornerColor),
                                    GetCircle(device, bi.cornerABInv, lineWidth * 2f, cornerColor),
                                    GetCircle(device, bi.cornerAACenter, lineWidth * 2f, cornerColor),
                                    GetCircle(device, bi.cornerABCenter, lineWidth * 2f, cornerColor),
                                    GetCircle(device, bi.cornerAACenterInv, lineWidth * 2f, cornerColor),
                                    GetCircle(device, bi.cornerABCenterInv, lineWidth * 2f, cornerColor),
                                    GetLine(device, bi.cornerAA, bi.cornerAAInv, lineWidth, cornerColor),
                                    GetLine(device, bi.cornerAB, bi.cornerABInv, lineWidth, cornerColor),

                                    GetCircle(device, bi.cornerACenter, bi.cornerRadius, cornerColor, cornerSegmentsPerCircle, bi.cornerAngleB, bi.cornerAngleA)
                                });
                            }
                        }
                    }
                }
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
                if (showBorderMaterial || showBorderGeometry || showDummies || isSelected)
                {
                    for (int i = 0; i < t; i++)
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
                    bool changeCornerAngles0 = false;
                    for (int i = 0; i < t; i++)
                    {
                        Vector2[] tempInnerLinePointsA = new Vector2[3];
                        Vector2[] tempInnerLinePointsB = new Vector2[3];
                        float prevAngle, nextAngle;

                        if (i < t - 1)
                        {
                            if (i > 0)
                            {
                                tempInnerLinePointsA[0] = borderInfo[i - 1].dummyInnerA;
                                tempInnerLinePointsB[0] = borderInfo[i - 1].dummyInnerB;
                                prevAngle = borderInfo[i - 1].angle;
                            }
                            else // First point
                            {
                                tempInnerLinePointsA[0] = borderInfo[t - 1].dummyInnerA;
                                tempInnerLinePointsB[0] = borderInfo[t - 1].dummyInnerB;
                                prevAngle = borderInfo[t - 1].angle;
                            }
                            tempInnerLinePointsA[2] = borderInfo[i + 1].dummyInnerA;
                            tempInnerLinePointsB[2] = borderInfo[i + 1].dummyInnerB;
                            nextAngle = borderInfo[i + 1].angle;
                        }
                        else // Last point
                        {
                            tempInnerLinePointsA[0] = borderInfo[i - 1].dummyInnerA;
                            tempInnerLinePointsB[0] = borderInfo[i - 1].dummyInnerB;
                            prevAngle = borderInfo[i - 1].angle;
                            tempInnerLinePointsA[2] = borderInfo[0].dummyInnerA;
                            tempInnerLinePointsB[2] = borderInfo[0].dummyInnerB;
                            nextAngle = borderInfo[0].angle;
                        }
                        tempInnerLinePointsA[1] = borderInfo[i].dummyInnerA;
                        tempInnerLinePointsB[1] = borderInfo[i].dummyInnerB;

                        float crossX, crossY;
                        // Inner point B
                        MathF.LinesCross(tempInnerLinePointsA[1].X, tempInnerLinePointsA[1].Y,
                                         tempInnerLinePointsB[1].X, tempInnerLinePointsB[1].Y,
                                         tempInnerLinePointsA[2].X, tempInnerLinePointsA[2].Y,
                                         tempInnerLinePointsB[2].X, tempInnerLinePointsB[2].Y,
                                         out crossX, out crossY, true);
                        borderInfo[i].innerB = new Vector2(crossX, crossY);
                        // Inner point A
                        MathF.LinesCross(tempInnerLinePointsA[0].X, tempInnerLinePointsA[0].Y,
                                         tempInnerLinePointsB[0].X, tempInnerLinePointsB[0].Y,
                                         tempInnerLinePointsA[1].X, tempInnerLinePointsA[1].Y,
                                         tempInnerLinePointsB[1].X, tempInnerLinePointsB[1].Y,
                                         out crossX, out crossY, true);
                        borderInfo[i].innerA = new Vector2(crossX, crossY);

                        // Corner
                        float angle = MathF.Angle(borderInfo[i].outerB.X, borderInfo[i].outerB.Y, crossX, crossY) + MathF.RadAngle90;
                        float dist = MathF.Distance(borderInfo[i].outerB.X, borderInfo[i].outerB.Y, crossX, crossY);
                        Vector2 cornerCenter = new Vector2(crossX + dist * .5f * MathF.Cos(angle), crossY + dist * .5f * MathF.Sin(angle));
                        borderInfo[i].cornerACenter = cornerCenter;
                        angle = MathF.Angle(crossX, crossY, tempInnerLinePointsA[0].X, tempInnerLinePointsA[0].Y);
                        //if (borderInfo[i].angle < 0) angle += MathF.RadAngle180;
                        Vector2 cornerAA = new Vector2(cornerCenter.X + borderWidth * MathF.Cos(angle), cornerCenter.Y + borderWidth * MathF.Sin(angle));
                        borderInfo[i].cornerAAInv = new Vector2(cornerCenter.X + borderWidth * MathF.Cos(angle + MathF.RadAngle180), cornerCenter.Y + borderWidth * MathF.Sin(angle + MathF.RadAngle180));
                        borderInfo[i].cornerAA = cornerAA;
                        MathF.LinesCross(cornerCenter.X, cornerCenter.Y,
                                         cornerAA.X, cornerAA.Y,
                                         tempInnerLinePointsA[0].X, tempInnerLinePointsA[0].Y,
                                         tempInnerLinePointsB[0].X, tempInnerLinePointsB[0].Y,
                                         out crossX, out crossY, true);
                        float cornerRadius = MathF.Distance(cornerCenter.X, cornerCenter.Y, crossX, crossY);
                        borderInfo[i].cornerRadius = cornerRadius;
                        borderInfo[i].cornerAACenter = new Vector2(crossX, crossY);
                        borderInfo[i].cornerAACenterInv = new Vector2(cornerCenter.X + cornerRadius * MathF.Cos(angle + MathF.RadAngle180), cornerCenter.Y + cornerRadius * MathF.Sin(angle + MathF.RadAngle180));
                        borderInfo[i].cornerAngleA = angle + MathF.RadAngle90;
                        angle = MathF.Angle(tempInnerLinePointsA[1].X, tempInnerLinePointsA[1].Y, crossX, crossY);
                        borderInfo[i].cornerAngleB = borderInfo[i].angle;
                        borderInfo[i].cornerAB = new Vector2(cornerCenter.X + borderWidth * MathF.Cos(angle), cornerCenter.Y + borderWidth * MathF.Sin(angle));
                        borderInfo[i].cornerABInv = new Vector2(cornerCenter.X + borderWidth * MathF.Cos(angle + MathF.RadAngle180), cornerCenter.Y + borderWidth * MathF.Sin(angle + MathF.RadAngle180));
                        borderInfo[i].cornerABCenter = new Vector2(cornerCenter.X + cornerRadius * MathF.Cos(angle), cornerCenter.Y + borderInfo[i].cornerRadius * MathF.Sin(angle));
                        borderInfo[i].cornerABCenterInv = new Vector2(cornerCenter.X + cornerRadius * MathF.Cos(angle + MathF.RadAngle180), cornerCenter.Y + cornerRadius * MathF.Sin(angle + MathF.RadAngle180));

                        //if (borderInfo[i].cornerAngleA > MathF.RadAngle360) borderInfo[i].cornerAngleA -= MathF.RadAngle360;
                        //else if (borderInfo[i].cornerAngleA < -MathF.RadAngle360) borderInfo[i].cornerAngleA += MathF.RadAngle360;
                        //if (borderInfo[i].cornerAngleB > MathF.RadAngle360) borderInfo[i].cornerAngleB -= MathF.RadAngle360;
                        //else if (borderInfo[i].cornerAngleB < -MathF.RadAngle360) borderInfo[i].cornerAngleB += MathF.RadAngle360;
                        //if (MathF.Abs(borderInfo[i].cornerAngleB - borderInfo[i].cornerAngleA) > MathF.RadAngle360) borderInfo[i].cornerAngleA -= MathF.RadAngle360;
                        angle = borderInfo[i].angle;
                        while (prevAngle < 0) prevAngle += MathF.RadAngle360;
                        while (angle < 0 || angle < prevAngle) angle += MathF.RadAngle360;
                        if (borderType == BoderMode.Inside)
                        {
                            if (angle - prevAngle > MathF.RadAngle180)
                            {
                                borderInfo[i].innerACenter = borderInfo[i].innerB;
                                borderInfo[i].innerBCenter = new Vector2(borderInfo[i].outerB.X + borderWidth * MathF.Cos(angle + MathF.RadAngle90), borderInfo[i].outerB.Y + borderWidth * MathF.Sin(angle + MathF.RadAngle90));
                                borderInfo[i].outerACenter = new Vector2(borderInfo[i].innerB.X + borderWidth * MathF.Cos(angle - MathF.RadAngle90), borderInfo[i].innerB.Y + borderWidth * MathF.Sin(angle - MathF.RadAngle90));
                                borderInfo[i].outerBCenter = borderInfo[i].outerB;
                                // Previous border segment vertices correction to avoid polygon clipping
                                int iPrev = i > 0 ? i - 1 : t - 1;
                                borderInfo[iPrev].outerBCenter = borderInfo[i].outerB;
                                borderInfo[iPrev].innerBCenter = borderInfo[iPrev].dummyInnerA;
                                borderInfo[iPrev].cornerACenter = borderInfo[iPrev].outerA;
                                if (i > 0)
                                {
                                    angle = borderInfo[iPrev].cornerAngleB - MathF.RadAngle180;
                                    borderInfo[iPrev].cornerAngleB = borderInfo[iPrev].cornerAngleA - MathF.RadAngle180;
                                    borderInfo[iPrev].cornerAngleA = angle;
                                }
                                else
                                {
                                    changeCornerAngles0 = true;
                                }
                            }
                            else
                            {
                                borderInfo[i].outerACenter = new Vector2(borderInfo[i].innerA.X + borderWidth * MathF.Cos(angle - MathF.RadAngle90), borderInfo[i].innerA.Y + borderWidth * MathF.Sin(angle - MathF.RadAngle90));
                                borderInfo[i].outerBCenter = new Vector2(borderInfo[i].innerB.X + borderWidth * MathF.Cos(angle - MathF.RadAngle90), borderInfo[i].innerB.Y + borderWidth * MathF.Sin(angle - MathF.RadAngle90));
                                borderInfo[i].innerACenter = borderInfo[i].innerA;
                                borderInfo[i].innerBCenter = borderInfo[i].innerB;
                            }
                            int iNext = i < t - 1 ? i + 1 : 0;
                            if (changeCornerAngles0 && i == t - 1)
                            {
                                borderInfo[i].outerBCenter = borderInfo[0].outerB;
                                borderInfo[i].innerBCenter = borderInfo[i].dummyInnerA;
                                borderInfo[i].cornerACenter = borderInfo[i].outerA;
                                borderInfo[i].cornerAngleA = borderInfo[0].angle - MathF.RadAngle180;
                                borderInfo[i].cornerAngleB = borderInfo[i].angle - MathF.RadAngle180;
                            }
                            else
                            {
                                borderInfo[i].cornerAngleA = borderInfo[i].angle;
                                borderInfo[i].cornerAngleB = borderInfo[iNext].angle;
                                borderInfo[i].cornerACenter = borderInfo[i].innerB;
                            }
                        }
                        else
                        {
                            borderInfo[i].outerACenter = new Vector2(borderInfo[i].outerA.X + borderWidth * MathF.Cos(angle - MathF.RadAngle90), borderInfo[i].outerA.Y + borderWidth * MathF.Sin(angle - MathF.RadAngle90));
                            borderInfo[i].outerBCenter = new Vector2(borderInfo[i].outerB.X + borderWidth * MathF.Cos(angle - MathF.RadAngle90), borderInfo[i].outerB.Y + borderWidth * MathF.Sin(angle - MathF.RadAngle90));
                            borderInfo[i].innerACenter = borderInfo[i].innerA;
                            borderInfo[i].innerBCenter = borderInfo[i].innerB;
                        }

                        angle = MathF.Angle(borderInfo[i].dummyInnerA.X, borderInfo[i].dummyInnerA.Y, borderInfo[i].outerA.X, borderInfo[i].outerA.Y) + MathF.RadAngle90;
                        borderInfo[i].centerA = new Vector2(borderInfo[i].outerA.X + borderWidth * .5f * MathF.Cos(angle), borderInfo[i].outerA.Y + borderWidth * .5f * MathF.Sin(angle));
                        borderInfo[i].centerB = new Vector2(borderInfo[i].outerB.X + borderWidth * .5f * MathF.Cos(angle), borderInfo[i].outerB.Y + borderWidth * .5f * MathF.Sin(angle));

                        int prev = i == 0 ? t - 1 : i - 1;
                        borderInfo[prev].cornerBCenter = borderInfo[i].cornerACenter;
                        borderInfo[prev].cornerBA = borderInfo[i].cornerAA;
                        borderInfo[prev].cornerBB = borderInfo[i].cornerAB;
                        borderInfo[prev].cornerBACenter = borderInfo[i].cornerAACenter;
                        borderInfo[prev].cornerBBCenter = borderInfo[i].cornerABCenter;
                        borderInfo[prev].cornerBACenterInv = borderInfo[i].cornerAACenterInv;
                        borderInfo[prev].cornerBBCenterInv = borderInfo[i].cornerABCenterInv;
                        borderInfo[prev].cornerBAInv = borderInfo[i].cornerBAInv;
                        borderInfo[prev].cornerBBInv = borderInfo[i].cornerBBInv;
                    }
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
                    if (borderType == BoderMode.Inside)
                    {
                        if (cornerSegmentsPerCircle < 1)
                        {
                            rb.AddShape(new PolyShapeInfo() { Vertices = bi.Polygon });
                            //rb.AddShape(new PolyShapeInfo() { Vertices = bi.PolygonCornerAOutside });
                            //rb.AddShape(new PolyShapeInfo() { Vertices = bi.PolygonCornerBOutside });
                        }
                        else
                        {
                            rb.AddShape(new PolyShapeInfo() { Vertices = bi.PolygonForCornersInside });
                            if (bi.innerBCenter == bi.dummyInnerA)
                            {
                                
                            }
                            else
                            {
                                rb.AddShape(new CircleShapeInfo() { Position = bi.cornerACenter, Radius = BorderWidth });
                            }
                        }
                    }
                    else
                    {
                        if (cornerSegmentsPerCircle < 1)
                        {
                            rb.AddShape(new PolyShapeInfo() { Vertices = bi.Polygon });
                            //rb.AddShape(new PolyShapeInfo() { Vertices = bi.PolygonCornerAOutside });
                            //rb.AddShape(new PolyShapeInfo() { Vertices = bi.PolygonCornerBOutside });
                        }
                        else
                        {
                            rb.AddShape(new PolyShapeInfo() { Vertices = bi.PolygonForCorners });
                            rb.AddShape(new CircleShapeInfo() { Position = bi.outerB, Radius = BorderWidth });
                        }
                    }
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
                    if (updatableInGame && isSelected && DualityApp.ExecEnvironment != DualityApp.ExecutionEnvironment.Editor)
                    {
                        ctrlPressed = DualityApp.Keyboard[Duality.Input.Key.ControlLeft] || DualityApp.Keyboard[Duality.Input.Key.ControlRight];
                        mouseLeft = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Left);
                        mouseRight = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Right);
                        this.mouseX = DualityApp.Mouse.X;
                        this.mouseY = DualityApp.Mouse.Y;
                    }

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
                        VertexC1P3T2[] cornerVi = vertexInfo.dummies.Last();
                        foreach (VertexC1P3T2[] vi in vertexInfo.dummies)
                        {
                            TransformVertices(device, trans, vi);
                            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, vi);
                        }
                    }

                    // Limits are calculated in real time (no cache)
                    if (showLimits || ctrlPressed)
                    {
                        // Game object center
                        Vector3 p = new Vector3(trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                        float scale = trans.Scale;
                        device.PreprocessCoords(ref p, ref scale);
                        vertexInfo.limits.Add(GetCircle(device, p.Xy, lineWidth * 5f + 2, ColorRgba.Black.WithAlpha(.5f)));
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
                                p = new Vector3(borderInfo[i].outerB, trans.Pos.Z);
                                TransformPoint(trans, ref p.X, ref p.Y);
                                scale = trans.Scale;
                                device.PreprocessCoords(ref p, ref scale);

                                vertexInfo.limits.AddRange(new[] {
                                    GetLine(device, new Vector2(min.X, p.Y), new Vector2(max.X, p.Y), lineWidth, ColorRgba.White.WithAlpha(.5f)),
                                    GetLine(device, new Vector2(p.X, min.Y), new Vector2(p.X, max.Y), lineWidth, ColorRgba.White.WithAlpha(.5f))
                                });
                            }
                        }

                        foreach (VertexC1P3T2[] vi in vertexInfo.limits)
                        {
                            device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, vi);
                        }
                    }

                    if (isSelected) // Edit is calculated in real time (no cache)
                    {
                        Vector3 mouse = device.GetSpaceCoord(new Vector2(this.mouseX, this.mouseY));
                        float scale = trans.Scale;
                        device.PreprocessCoords(ref mouse, ref scale);

                        for (int i = 0; i < t; i++)
                        {
                            Vector3 p = new Vector3(borderInfo[i].outerB, trans.Pos.Z + 1f);
                            TransformPoint(trans, ref p.X, ref p.Y);
                            scale = trans.Scale;
                            device.PreprocessCoords(ref p, ref scale);

                            // Point selection
                            float radius = 10f;// lineWidth * 4f;
                            if (MathF.Distance(mouse.X, mouse.Y, p.X, p.Y) < radius)
                            {
                                if (selectedPointId == -1) selectedPointId = i;
                            }

                            int segments = ctrlPressed ? 4 : 8;
                            radius = ctrlPressed ? radius + 1f : radius;
                            ColorRgba fontColor;
                            if (i == selectedPointId)
                            {
                                if (ctrlPressed) device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, p.Xy, radius, ColorRgba.White, segments));
                                else device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, p.Xy, radius, ColorRgba.White, segments));
                                fontColor = ColorRgba.Black;
                            }
                            else
                            {
                                device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, p.Xy, radius + 1, ColorRgba.White, segments));
                                device.AddVertices(Material.SolidWhite, VertexMode.TriangleFan, GetCircle(device, p.Xy, radius, ColorRgba.Black, segments));
                                fontColor = ColorRgba.White;
                            }

                            Font font = Font.GenericMonospace8.Res;
                            string text = i.ToString();
                            VertexC1P3T2[] vertices = new VertexC1P3T2[text.Length * 4];
                            p.Z = 0f;// trans.Pos.Z - 1f;
                            Vector2 textSize = font.MeasureText(text);
                            int vertexCount = font.EmitTextVertices(text, ref vertices, p.X - textSize.X * .5f, (p.Y - font.Metrics.BaseLine * .5f - 1.5f), p.Z, fontColor, 0.0f, 1f);
                            scale = trans.Scale;
                            device.PreprocessCoords(ref p, ref scale);
                            device.AddVertices(font.Material, VertexMode.Quads, vertices);
                        }

                        // Mouse
                        if (updatableInGame || DualityApp.ExecEnvironment == DualityApp.ExecutionEnvironment.Editor)
                        {
                            // Pointer
                            Vector2 a = new Vector2(mouse.X - 10, mouse.Y);
                            Vector2 b = new Vector2(mouse.X + 10, mouse.Y);
                            Vector2 c = new Vector2(mouse.X, mouse.Y - 10);
                            Vector2 d = new Vector2(mouse.X, mouse.Y + 10);
                            device.AddVertices(Material.InvertWhite, VertexMode.Quads, GetLine(device, a, b, 3, ColorRgba.White));
                            device.AddVertices(Material.InvertWhite, VertexMode.Quads, GetLine(device, c, d, 3, ColorRgba.White));

                            mouse = device.GetSpaceCoord(new Vector2(this.mouseX, this.mouseY));
                            mouse.X -= trans.Pos.X;
                            mouse.Y -= trans.Pos.Y;
                            MathF.TransformCoord(ref mouse.X, ref mouse.Y, -trans.Angle, 1f / trans.Scale);

                            if (isSelected)
                            {
                                if (mouseLeft)
                                {
                                    if (selectedPointId > -1) // Move point
                                    {
                                        if (ctrlPressed)
                                        {
                                            for (int i = 0; i < t; i++)
                                            {
                                                // Transformed position i
                                                float transformedX = points[i].X;
                                                float transformedY = points[i].Y;

                                                float xDistance = Math.Abs(mouse.X - transformedX);
                                                float yDistance = Math.Abs(mouse.Y - transformedY);

                                                if (xDistance < 10) mouse.X = transformedX;
                                                else if (yDistance < 10) mouse.Y = transformedY;
                                            }
                                        }

                                        points[selectedPointId] = new Vector2(mouse.X, mouse.Y);
                                        UpdateBody(true);
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
                                                float distance = MathF.Distance(mouse.X, mouse.Y, borderInfo[i].outerB.X, borderInfo[i].outerB.Y);
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
                                            id = nearestId;
                                        }
                                        else
                                        {
                                            id = t;
                                        }
                                        points.Insert(Math.Min(t, id), new Vector2(mouse.X, mouse.Y));
                                        UpdateBody(true);
                                    }
                                }
                                else if (mouseRight)
                                {
                                    if (selectedPointId > -1) // Delete point
                                    {
                                        points.Remove(points[selectedPointId]);
                                        selectedPointId = -1;
                                        UpdateBody(true);
                                    }
                                }
                                else
                                {
                                    selectedPointId = -1;
                                }
                            }
                        }
                    }
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
            target.updatableInGame = this.updatableInGame;
            target.showBorderGeometry = this.showBorderGeometry;
            target.showDummies = this.showDummies;
            target.lineWidth = this.lineWidth;
            target.borderGeometryColor = this.borderGeometryColor;
            target.dummyColor = this.dummyColor;
            target.shapeMode = this.shapeMode;
            target.borderType = this.borderType;
            target.showLimits = this.showLimits;
            target.borderTexFlip = this.borderTexFlip;
            target.staticPosMainMaterial = this.staticPosMainMaterial;
            target.staticAngleMainMaterial = this.staticAngleMainMaterial;
            target.cornerSegmentsPerCircle = this.cornerSegmentsPerCircle;
            target.UpdateBody(true);
        } 
    }
}
