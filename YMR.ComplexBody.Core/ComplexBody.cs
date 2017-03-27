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
    public class ComplexBody : Renderer, ICmpInitializable, ICmpUpdatable, ICmpEditorUpdatable
    {
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
            public float distanceAB;
            public float distanceAA;
            public float distanceBB;
            public float distanceCenterCenter;

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
                    return new Vector2[] { outerA, outerB, outerCenter, dummyInnerA, dummyInnerB, innerA, innerB, innerCenter };
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
                }
            }

            public void Transform(Transform trans)
            {
                Vector2[] allPoints = AllPoints;
                for (int i = 0; i < 8; i++)
                {
                    TransformPoint(trans, ref allPoints[i].X, ref allPoints[i].Y);
                }
                AllPoints = allPoints;
            }

            public BorderInfo Clone()
            {
                return new BorderInfo() { AllPoints = this.AllPoints };
            }
        }

        #region Private Members

        [DontSerialize]
        private List<CPoint2D> lastVertices = new List<CPoint2D>();
        [DontSerialize]
        private CanvasBuffer vertexBuffer = new CanvasBuffer();
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
        private bool mustBeUpdated = true;
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

        private List<Vector2> points = new List<Vector2>()
        {
            new Vector2(0, 0), 
            new Vector2(100, 0), 
            new Vector2(100, 100), 
        };
        private bool showBorderMaterial = true;
        private bool showBorderGeometry = true;
        private bool showBorderDummies = false;
        private bool showPolygons = true;
        private bool showMaterial = true;
        private bool updatableUsingMouse = true;
        private ContentRef<Material> sharedMaterial = Material.Checkerboard;
        private ContentRef<Material> borderMaterial = Material.Checkerboard;
        private float borderWidth = 2;
        private float lineWidth = 2;
        private ColorRgba polygonColor = new ColorRgba(255, 0, 0, 200);
        private ColorRgba borderGeometryColor = new ColorRgba(0, 255, 0, 200);
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
        public bool ShowBorderDummies { get { return showBorderDummies; } set { showBorderDummies = value; } }
        public bool ShowPolygons { get { return showPolygons; } set { showPolygons = value; } }
        public bool ShowMaterial { get { return showMaterial; } set { showMaterial = value; } }
        public bool UpdatableUsingMouse { get { return updatableUsingMouse; } set { updatableUsingMouse = value; } }
        public ContentRef<Material> SharedMaterial { get { return sharedMaterial; } set { sharedMaterial = value; } }
        public ContentRef<Material> BorderMaterial { get { return borderMaterial; } set { borderMaterial = value; } }
        public override float BoundRadius { get { return this.GameObj.GetComponent<RigidBody>().BoundRadius; } }
        public float BorderWidth { get { return borderWidth; } set { borderWidth = value; UpdateBody(true); } }
        public float LineWidth { get { return lineWidth; } set { lineWidth = value; } }
        public ColorRgba PolygonColor { get { return polygonColor; } set { polygonColor = value; } }
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
        public bool MustBeUpdated { get { return mustBeUpdated; } set { mustBeUpdated = value; } }
        [EditorHintFlags(MemberFlags.Invisible)]
        public float MouseX { get { return mouseX; } set { mouseX = value; } }
        [EditorHintFlags(MemberFlags.Invisible)]
        public float MouseY { get { return mouseY; } set { mouseY = value; } }
        [EditorHintFlags(MemberFlags.Invisible)]
        public bool IsSelected { get { return isSelected; } set { isSelected = value; } }

        #endregion

        #region Private Methods

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

        #endregion

        #region Public Methods[EditorHintFlags(MemberFlags.Invisible)]

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
                if (showBorderMaterial || showBorderGeometry || showBorderDummies || isSelected)
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
                    float angle01 = MathF.Angle(p0.X, p0.Y, p1.X, p1.Y);
                    angle01 += borderType == BoderMode.Inside ? MathF.RadAngle90 : -MathF.RadAngle90;

                    borderInfo[i].distanceAB = MathF.Distance(p0.X, p0.Y, p1.X, p1.Y);
                    float halfDistance = borderInfo[i].distanceAB * .5f;
                    Vector2 outerCenterPoint = new Vector2(p0.X + halfDistance * MathF.Cos(angle01), p0.Y + halfDistance * MathF.Sin(angle01));
                    Vector2 innerCenterPoint = new Vector2(outerCenterPoint.X + borderWidth * MathF.Cos(angle01 - MathF.RadAngle90), outerCenterPoint.Y + borderWidth * MathF.Sin(angle01 - MathF.RadAngle90));

                    borderInfo[i].dummyInnerA = new Vector2(innerCenterPoint.X + halfDistance * MathF.Cos(angle01), innerCenterPoint.Y + halfDistance * MathF.Sin(angle01));
                    borderInfo[i].dummyInnerB = new Vector2(innerCenterPoint.X - halfDistance * MathF.Cos(angle01), innerCenterPoint.Y - halfDistance * MathF.Sin(angle01));

                    borderInfo[i].outerCenter = outerCenterPoint;
                    borderInfo[i].innerCenter = innerCenterPoint;

                    borderInfo[i].distanceAA = MathF.Distance(borderInfo[i].outerA.X, borderInfo[i].outerA.Y, borderInfo[i].dummyInnerA.X, borderInfo[i].dummyInnerA.Y);
                    borderInfo[i].distanceBB = MathF.Distance(borderInfo[i].outerB.X, borderInfo[i].outerB.Y, borderInfo[i].dummyInnerB.X, borderInfo[i].dummyInnerB.Y);
                    borderInfo[i].distanceCenterCenter = MathF.Distance(borderInfo[i].outerCenter.X, borderInfo[i].outerCenter.Y, borderInfo[i].innerCenter.X, borderInfo[i].innerCenter.Y);
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

        public override void Draw(IDrawDevice device)
        {
            Canvas canvas = new Canvas(device, this.vertexBuffer);

            Draw(canvas, trans.Angle, new Vector2(trans.Scale, trans.Scale));
        }
        public void Draw(Canvas canvas, float angle, Vector2 scale)
        {
            IDrawDevice device = canvas.DrawDevice;

            if (mustBeUpdated)
            {
                mustBeUpdated = false;
                OnUpdate(canvas);
            }
            else if (updatableUsingMouse && DualityApp.ExecEnvironment != DualityApp.ExecutionEnvironment.Editor)
            {
                ctrlPressed = DualityApp.Keyboard[Duality.Input.Key.ControlLeft] || DualityApp.Keyboard[Duality.Input.Key.ControlRight];
                mouseLeft = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Left);
                mouseRight = DualityApp.Mouse.ButtonPressed(Duality.Input.MouseButton.Right);
                mouseX = DualityApp.Mouse.X;
                mouseY = DualityApp.Mouse.Y;
            }

            // Transformed mouse position
            Vector3 mousePos = device.GetSpaceCoord(new Vector2(mouseX, mouseY));
            float transformedMouseX = mousePos.X;
            float transformedMouseY = mousePos.Y;

            int t = points.Count;

            if (cutPolygon != null && points.Count > 2)
            {
                // Texture
                if (showMaterial || showPolygons)
                {
                    Rect boundingRect = points.BoundingBox();
                    
                    float brW = scaleTexture ? boundingRect.W : this.sharedMaterial.Res.MainTexture.Res.Size.X;
                    float brH = scaleTexture ? boundingRect.H : this.sharedMaterial.Res.MainTexture.Res.Size.Y;
                    float ratioW = brH / brW;
                    float ratioH = brW / brH;

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

                            canvas.PushState();
                            canvas.State.SetMaterial(this.sharedMaterial);
                            canvas.State.TransformAngle = angle;
                            canvas.State.TransformScale = scale;
                            canvas.State.TextureCoordinateRect = new Rect(
                                (1 / brW) * localRect.X,
                                (1 / brH) * localRect.Y,
                                (1 / brW) * localRect.W,
                                (1 / brH) * localRect.H
                            );
                            canvas.FillPolygon(tempArray, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                            canvas.PopState();
                        }
                        // Polygons
                        if (showPolygons)
                        {
                            canvas.PushState();
                            canvas.State.ColorTint = polygonColor;
                            canvas.State.TransformAngle = angle;
                            canvas.State.TransformScale = scale;
                            IEnumerable<ShapeInfo> shapes = rb.Shapes.Where(x => x.GetType() == typeof(PolyShapeInfo));
                            canvas.FillPolygonOutline(tempArray, lineWidth, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                            canvas.PopState();
                        }
                    }
                }
            }

            BorderInfo[] bis3D = new BorderInfo[t];
            for (int i = 0; i < t; i++)
            {
                // Transformed position 0
                float transformedX0 = points[0].X;
                float transformedY0 = points[0].Y;
                TransformPoint(trans, ref transformedX0, ref transformedY0);
                // Transformed position i
                float transformedX = points[i].X;
                float transformedY = points[i].Y;
                TransformPoint(trans, ref transformedX, ref transformedY);
                // Transformed position i + 1
                float transformedX1 = 0, transformedY1 = 0;
                // Zoomed vertices
                Vector3 pointPos = new Vector3(transformedX, transformedY, trans.Pos.Z);
                float pointScale = 1;
                device.PreprocessCoords(ref pointPos, ref pointScale);

                if (i < t - 1)
                {
                    transformedX1 = points[i + 1].X;
                    transformedY1 = points[i + 1].Y;
                    TransformPoint(trans, ref transformedX1, ref transformedY1);
                }

                // Point selection
                if (MathF.Distance(transformedMouseX, transformedMouseY, transformedX, transformedY) < pointPos.Z / 50)
                {
                    if (selectedPointId == -1) selectedPointId = i;
                }

                // Border and points
                if (showBorderMaterial || showBorderGeometry || showBorderDummies || isSelected)
                {
                    IEnumerable<ShapeInfo> shapes = rb.Shapes.Where(x => x.GetType() == typeof(PolyShapeInfo));

                    Rect boundingRect = points.BoundingBox();

                    float brW = scaleTexture ? boundingRect.W : this.borderMaterial.Res.MainTexture.Res.Size.X;
                    float brH = scaleTexture ? boundingRect.H : this.borderMaterial.Res.MainTexture.Res.Size.Y;
                    float ratioW = brH / brW;
                    float ratioH = brW / brH;

                    Vector2 p0, p1;
                    p0 = new Vector2(MathF.RoundToInt(points[i].X), MathF.RoundToInt(points[i].Y));
                    if (i < t - 1) p1 = new Vector2(MathF.RoundToInt(points[i + 1].X), MathF.RoundToInt(points[i + 1].Y));
                    else p1 = new Vector2(points[0].X, points[0].Y);
                        
                    BorderInfo bi = borderInfo[i].Clone();

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

                    if (showBorderMaterial)
                    {
                        Rect localRect = AABB(borderPoly);

                        canvas.PushState();
                        canvas.State.SetMaterial(this.borderMaterial);
                        canvas.State.TransformAngle = angle;
                        canvas.State.TransformScale = scale;
                        canvas.State.TextureCoordinateRect = new Rect(
                            (1 / brW) * localRect.X,
                            (1 / brH) * localRect.Y,
                            (1 / brW) * localRect.W,
                            (1 / brH) * localRect.H
                        );
                        canvas.FillPolygon(borderPoly, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                        canvas.PopState();
                    }

                    if (showBorderDummies)
                    {
                        canvas.PushState();
                        canvas.State.ColorTint = ColorRgba.Blue.WithAlpha(200);
                        canvas.FillCircle(bi.outerCenter.X, bi.outerCenter.Y, lineWidth * 2f);
                        canvas.FillCircle(bi.innerCenter.X, bi.innerCenter.Y, lineWidth * 2f);
                        canvas.FillThickLine(bi.outerCenter.X, bi.outerCenter.Y, bi.innerCenter.X, bi.innerCenter.Y, lineWidth);
                        canvas.FillThickLine(bi.dummyInnerA.X, bi.dummyInnerA.Y, bi.dummyInnerB.X, bi.dummyInnerB.Y, lineWidth);
                        canvas.FillCircle(bi.dummyInnerA.X, bi.dummyInnerA.Y, lineWidth * 2f);
                        canvas.FillCircle(bi.dummyInnerB.X, bi.dummyInnerB.Y, lineWidth * 2f);
                        canvas.FillCircle(bi.innerA.X, bi.innerA.Y, lineWidth * 2f);
                        canvas.FillCircle(bi.innerB.X, bi.innerB.Y, lineWidth * 2f);

                        canvas.PopState();
                    }

                    if (showBorderGeometry)
                    {
                        canvas.PushState();
                        canvas.State.TransformAngle = angle;
                        canvas.State.TransformScale = scale;
                        canvas.State.ColorTint = borderGeometryColor;
                        canvas.FillPolygonOutline(borderPoly, lineWidth, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                        for (int j = 0; j < 4; j++)
                        {
                            TransformPoint(trans, ref borderPoly[j].X, ref borderPoly[j].Y);
                            canvas.FillCircle(borderPoly[j].X, borderPoly[j].Y, lineWidth * 2f);
                        }
                        canvas.PopState();
                    }

                    // Vertices
                    if (isSelected)
                    {
                        if (!showBorderGeometry) TransformPoint(trans, ref borderPoly[1].X, ref borderPoly[1].Y);

                        if (i == selectedPointId)
                        {
                            canvas.PushState();
                            canvas.State.TransformAngle = angle;
                            canvas.State.TransformScale = scale;
                            canvas.State.ColorTint = new ColorRgba(255, 255, 255, 200);
                            if (ctrlPressed) canvas.FillRect(borderPoly[1].X - lineWidth * 4f, borderPoly[1].Y - lineWidth * 4f, lineWidth * 8f, lineWidth * 8f);
                            else canvas.FillCircle(borderPoly[1].X, borderPoly[1].Y, lineWidth * 4f);
                            canvas.PopState();
                        }
                        else
                        {
                            canvas.PushState();
                            canvas.State.TransformAngle = angle;
                            canvas.State.TransformScale = scale;
                            canvas.State.ColorTint = new ColorRgba(255, 255, 255, 200);
                            canvas.FillCircle(borderPoly[1].X, borderPoly[1].Y, lineWidth * 4f + 1f);
                            canvas.PopState();

                            canvas.PushState();
                            canvas.State.TransformAngle = angle;
                            canvas.State.TransformScale = scale;
                            canvas.State.ColorTint = new ColorRgba(0, 0, 0, 200);
                            canvas.FillCircle(borderPoly[1].X, borderPoly[1].Y, lineWidth * 4f);
                            canvas.PopState();
                        }

                        canvas.PushState();
                        canvas.State.TransformAngle = angle;
                        canvas.State.TransformScale = scale;
                        canvas.State.TextFont = Font.GenericMonospace8;
                        canvas.State.ColorTint = i == selectedPointId ? new ColorRgba(0, 0, 0, 255) : new ColorRgba(255, 255, 255, 255);
                        canvas.DrawText(i.ToString(), borderPoly[1].X, borderPoly[1].Y, 0f, Alignment.Center, false);
                        canvas.PopState();
                    }
                }

                // Snap lines
                if (ctrlPressed)
                {
                    Vector3 v00 = device.GetSpaceCoord(Vector2.Zero);
                    Vector3 vXY = device.GetSpaceCoord(new Vector2(DualityApp.TargetResolution.X, DualityApp.TargetResolution.Y));

                    // Transformed position i
                    float transformedSnapXMin = v00.X;
                    float transformedSnapYMin = v00.Y;
                    float transformedSnapXMax = vXY.X;
                    float transformedSnapYMax = vXY.Y;
                    TransformPoint(trans, ref transformedSnapXMin, ref transformedSnapYMin, true, true, true);
                    TransformPoint(trans, ref transformedSnapXMax, ref transformedSnapYMax, true, true, true);

                    canvas.PushState();
                    canvas.State.ColorTint = new ColorRgba(255, 255, 255, 50);
                    canvas.DrawLine(transformedSnapXMin, transformedY, transformedSnapXMax, transformedY);
                    canvas.DrawLine(transformedX, transformedSnapYMax, transformedX, transformedSnapYMin);
                    canvas.PopState();
                }
            }

            if (camera3D != null)
            {
                UpdateShapes(bis3D);
            }

            // Mouse
            if (updatableUsingMouse)
            {
                canvas.PushState();
                canvas.State.SetMaterial(Material.InvertWhite);
                canvas.FillRect(transformedMouseX - 10, transformedMouseY - 1, 20, 3);
                canvas.FillRect(transformedMouseX - 1, transformedMouseY - 10, 3, 20);
                canvas.PopState();

                canvas.PushState();
                canvas.State.ColorTint = new ColorRgba(255, 255, 255, 255);
                canvas.DrawLine(transformedMouseX - 10, transformedMouseY, transformedMouseX + 10, transformedMouseY);
                canvas.DrawLine(transformedMouseX, transformedMouseY - 10, transformedMouseX, transformedMouseY + 10);
                canvas.PopState();
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

        public void OnUpdate() { }
        public void OnUpdate(Canvas canvas)
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

                IDrawDevice device = canvas.DrawDevice;

                working = true;

                if (mouseLeft)
                {
                    Vector3 mousePos = device.GetSpaceCoord(new Vector2(mouseX, mouseY));
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

        protected override void OnCopyDataTo(object targetObj, ICloneOperation operation)
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
