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
            /// Border Shapes
            /// </summary>
            Border = 1,
            /// <summary>
            /// Hollow body with shapes in the border
            /// </summary>
            Path = 2
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
        private BodyShapeMode collisionType = BodyShapeMode.Triangulation;
        private BoderMode borderType = BoderMode.Inside;
        private bool local3D = false;
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
        public float BorderWidth { get { return borderWidth; } set { borderWidth = value; } }
        public float LineWidth { get { return lineWidth; } set { lineWidth = value; } }
        public ColorRgba PolygonColor { get { return polygonColor; } set { polygonColor = value; } }
        public ColorRgba BorderGeometryColor { get { return borderGeometryColor; } set { borderGeometryColor = value; } }
        public bool ScaleTexture { get { return scaleTexture; } set { scaleTexture = value; } }
        public BodyShapeMode ShapeMode { get { return collisionType; } set { collisionType = value; } }
        public BoderMode BorderType { get { return borderType; } set { borderType = value; } }
        /// <summary>
        /// If set, applies a 3D like effect. If Camera3D is set, this property is ignored.
        /// </summary>
        public bool Local3D { get { return local3D; } set { local3D = value; } }
        /// <summary>
        /// If set, the 3D effect will use the camera as center point. Overrides the Local3D porperty.
        /// </summary>
        public Camera Camera3D { get { return camera3D; } set { camera3D = value; } }

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

        public void UpdateShapes()
        {
            try
            {
                Transform trans = this.GameObj.GetComponent<Transform>();
                RigidBody rb = this.GameObj.GetComponent<RigidBody>();

                int nLastVertices = lastVertices.Count;
                int nVertices = points.Count;
                if (nVertices < 3)
                {
                    rb.ClearShapes();

                    return;
                }

                bool changed = nLastVertices != nVertices;
                if (!changed)
                {
                    for (int i = 0; i < nVertices; i++)
                    {
                        if (lastVertices[i].X != points[i].X || lastVertices[i].Y != points[i].Y)
                        {
                            changed = true;
                            lastVertices.Clear();
                            break;
                        }
                    }
                }

                if (changed)
                {
                    CPoint2D[] vertices = new CPoint2D[nVertices];
                    for (int i = 0; i < nVertices; i++)
                    {
                        if (nLastVertices != nVertices || vertices[i] == null || vertices[i].X != points[i].X || vertices[i].Y != points[i].Y)
                        {
                            CPoint2D v = new CPoint2D(points[i].X, points[i].Y);
                            vertices[i] = v;
                            lastVertices.Add(v);
                        }
                    }
                    CPolygonShape cutPolygon = new CPolygonShape(vertices);
                    cutPolygon.CutEar();

                    rb.ClearShapes();

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
            catch (Exception ex) { }
        }

        public override void Draw(IDrawDevice device)
        {
            Transform trans = this.GameObj.GetComponent<Transform>();
            Canvas canvas = new Canvas(device, this.vertexBuffer);

            Draw(canvas, trans.Angle, new Vector2(trans.Scale, trans.Scale));
        }
        public void Draw(Canvas canvas, float angle, Vector2 scale)
        {
            IDrawDevice device = canvas.DrawDevice;
            Transform trans = this.GameObj.GetComponent<Transform>();
            RigidBody rb = this.GameObj.GetComponent<RigidBody>();

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

            if (rb != null && points.Count > 2)
            {
                // Texture
                if (showMaterial)
                {
                    IEnumerable<ShapeInfo> shapes = rb.Shapes.Where(x => x.GetType() == typeof(PolyShapeInfo));
                    
                    Rect boundingRect = points.BoundingBox();
                    
                    float brW = scaleTexture ? boundingRect.W : this.sharedMaterial.Res.MainTexture.Res.Size.X;
                    float brH = scaleTexture ? boundingRect.H : this.sharedMaterial.Res.MainTexture.Res.Size.Y;
                    float ratioW = brH / brW;
                    float ratioH = brW / brH;

                    foreach (PolyShapeInfo shape in shapes)
                    {
                        int tShapes = shape.Vertices.Count();
                        Vector2[] vs = new Vector2[tShapes];
                        for (int i = 0; i < tShapes; i++)
                        {
                            vs[i] = shape.Vertices[i];
                        }

                        Rect localRect = shape.AABB;

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

                        canvas.FillPolygon(vs, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);

                        canvas.PopState();
                    }
                }

                // Polygons
                if (showPolygons)
                {
                    canvas.PushState();
                    canvas.State.ColorTint = polygonColor;
                    canvas.State.TransformAngle = angle;
                    canvas.State.TransformScale = scale;
                    IEnumerable<ShapeInfo> shapes = rb.Shapes.Where(x => x.GetType() == typeof(PolyShapeInfo));
                    foreach (PolyShapeInfo shape in shapes)
                    {
                        int tShapes = shape.Vertices.Count();
                        Vector2[] vs = new Vector2[tShapes];
                        for (int i = 0; i < tShapes; i++)
                        {
                            vs[i] = new Vector2(shape.Vertices[i].X, shape.Vertices[i].Y);
                        }

                        canvas.FillPolygonOutline(vs, lineWidth, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                        //canvas.DrawPolygon(vs, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                    }
                    canvas.PopState();
                }
            }


            // Border
            BorderInfo[] borderInfo = new BorderInfo[t];
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
                    p0 = new Vector2(MathF.RoundToInt(points[i].X), MathF.RoundToInt(points[i].Y));
                    if (i < t - 1) p1 = new Vector2(MathF.RoundToInt(points[i + 1].X), MathF.RoundToInt(points[i + 1].Y));
                    else p1 = new Vector2(points[0].X, points[0].Y);

                    Vector2[] ordererdTriangle = new Vector2[3];
                    bool found = false;
                    foreach (PolyShapeInfo shape in shapes)
                    {
                        int tShapes = shape.Vertices.Count();
                        int matchingPoints = 0;
                        for (int j = 0; j < tShapes; j++)
                        {
                            if ((MathF.Abs(shape.Vertices[j].X - p0.X) <= 1 && MathF.Abs(shape.Vertices[j].Y - p0.Y) <= 1) ||
                                (MathF.Abs(shape.Vertices[j].X - p1.X) <= 1 && MathF.Abs(shape.Vertices[j].Y - p1.Y) <= 1))
                            {
                                ordererdTriangle[matchingPoints] = shape.Vertices[j];
                                matchingPoints++;
                            }
                            else
                            {
                                ordererdTriangle[matchingPoints] = shape.Vertices[j];
                            }
                        }
                        if (matchingPoints == 2)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found)
                    {
                        borderInfo[i].outerA = ordererdTriangle[0];
                        borderInfo[i].outerB = ordererdTriangle[1];
                        Vector2[] borderPoly = new Vector2[4];
                        borderPoly[0] = ordererdTriangle[0];
                        borderPoly[1] = ordererdTriangle[1];
                        float angle01 = MathF.Angle(borderPoly[0].X, borderPoly[0].Y, borderPoly[1].X, borderPoly[1].Y);
                        angle01 += borderType == BoderMode.Inside ? -MathF.RadAngle90 : MathF.RadAngle90;

                        borderInfo[i].distanceAB = MathF.Distance(borderPoly[0].X, borderPoly[0].Y, borderPoly[1].X, borderPoly[1].Y);
                        float halfDistance = borderInfo[i].distanceAB * .5f;
                        Vector2 outerCenterPoint = new Vector2(borderPoly[0].X + halfDistance * MathF.Cos(angle01), borderPoly[0].Y + halfDistance * MathF.Sin(angle01));
                        Vector2 innerCenterPoint = new Vector2(outerCenterPoint.X + borderWidth * MathF.Cos(angle01 - MathF.RadAngle90), outerCenterPoint.Y + borderWidth * MathF.Sin(angle01 - MathF.RadAngle90));

                        //if (camera3D != null)
                        //{
                        //    Transform camTrans = camera3D.GameObj.AddComponent<Transform>();
                        //    innerCenterPoint = new Vector2(innerCenterPoint.X + (trans.Pos.X - camTrans.Pos.X) / MathF.Abs(camTrans.Pos.Z * .01f), 
                        //                                   innerCenterPoint.Y + (trans.Pos.Y - camTrans.Pos.Y) / MathF.Abs(camTrans.Pos.Z * .01f));
                        //}

                        borderInfo[i].dummyInnerA = new Vector2(innerCenterPoint.X + halfDistance * MathF.Cos(angle01), innerCenterPoint.Y + halfDistance * MathF.Sin(angle01));
                        borderInfo[i].dummyInnerB = new Vector2(innerCenterPoint.X - halfDistance * MathF.Cos(angle01), innerCenterPoint.Y - halfDistance * MathF.Sin(angle01));

                        borderInfo[i].outerCenter = outerCenterPoint;
                        borderInfo[i].innerCenter = innerCenterPoint;

                        borderInfo[i].distanceAA = MathF.Distance(borderInfo[i].outerA.X, borderInfo[i].outerA.Y, borderInfo[i].dummyInnerA.X, borderInfo[i].dummyInnerA.Y);
                        borderInfo[i].distanceBB = MathF.Distance(borderInfo[i].outerB.X, borderInfo[i].outerB.Y, borderInfo[i].dummyInnerB.X, borderInfo[i].dummyInnerB.Y);
                        borderInfo[i].distanceCenterCenter = MathF.Distance(borderInfo[i].outerCenter.X, borderInfo[i].outerCenter.Y, borderInfo[i].innerCenter.X, borderInfo[i].innerCenter.Y);
                    }
                }
            }
            for(int i = 0; i < t; i++)
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

                // Border
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
                        
                    Vector2[] orderedTriangle = new Vector2[3];
                    bool found = true;
                    if (found)
                    {
                        Vector2[] borderPoly = borderInfo[i].Polygon;
                        borderInfo[i].Transform(trans);

                        //if (!local3D || camera3D != null)
                        //{
                        //    //TransformPoint(trans, ref borderPoly[0].X, ref borderPoly[0].Y);
                        //    //TransformPoint(trans, ref borderPoly[1].X, ref borderPoly[1].Y);
                        //    TransformPoint(trans, ref borderPoly[2].X, ref borderPoly[2].Y, true);
                        //    TransformPoint(trans, ref borderPoly[3].X, ref borderPoly[3].Y, true);
                        //}

                        //if (showBorderMaterial)
                        //{
                        //    Rect localRect = AABB(borderPoly);

                        //    //float ang = MathF.Angle(borderPoly[0].X, borderPoly[0].Y, borderPoly[1].X, borderPoly[1].Y);

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

                        if (showBorderDummies)
                        {
                            canvas.PushState();
                            canvas.State.ColorTint = ColorRgba.Blue.WithAlpha(200);
                            canvas.FillPolygonOutline(orderedTriangle, lineWidth, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                            canvas.FillCircle(borderInfo[i].outerCenter.X, borderInfo[i].outerCenter.Y, lineWidth * 2f);
                            canvas.FillCircle(borderInfo[i].innerCenter.X, borderInfo[i].innerCenter.Y, lineWidth * 2f);
                            canvas.FillThickLine(borderInfo[i].outerCenter.X, borderInfo[i].outerCenter.Y, borderInfo[i].innerCenter.X, borderInfo[i].innerCenter.Y, lineWidth);
                            canvas.FillThickLine(borderInfo[i].dummyInnerA.X, borderInfo[i].dummyInnerA.Y, borderInfo[i].dummyInnerB.X, borderInfo[i].dummyInnerB.Y, lineWidth);
                            canvas.FillCircle(borderInfo[i].dummyInnerA.X, borderInfo[i].dummyInnerA.Y, lineWidth * 2f);
                            canvas.FillCircle(borderInfo[i].dummyInnerB.X, borderInfo[i].dummyInnerB.Y, lineWidth * 2f);
                            canvas.FillCircle(borderInfo[i].innerA.X, borderInfo[i].innerA.Y, lineWidth * 2f);
                            canvas.FillCircle(borderInfo[i].innerB.X, borderInfo[i].innerB.Y, lineWidth * 2f);

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

                        //// Vertices
                        //if (isSelected)
                        //{
                        //    if(!showBorderGeometry) TransformPoint(trans, ref borderPoly[1].X, ref borderPoly[1].Y);

                        //    if (i == selectedPointId)
                        //    {
                        //        canvas.PushState();
                        //        canvas.State.TransformAngle = angle;
                        //        canvas.State.TransformScale = scale;
                        //        canvas.State.ColorTint = new ColorRgba(255, 255, 255, 200);
                        //        if (ctrlPressed) canvas.FillRect(borderPoly[1].X - lineWidth * 4f, borderPoly[1].Y - lineWidth * 4f, lineWidth * 8f, lineWidth * 8f);
                        //        else canvas.FillCircle(borderPoly[1].X, borderPoly[1].Y, lineWidth * 4f);
                        //        canvas.PopState();
                        //    }
                        //    else
                        //    {
                        //        canvas.PushState();
                        //        canvas.State.TransformAngle = angle;
                        //        canvas.State.TransformScale = scale;
                        //        canvas.State.ColorTint = new ColorRgba(255, 255, 255, 200);
                        //        canvas.FillCircle(borderPoly[1].X, borderPoly[1].Y, lineWidth * 4f + 1f);
                        //        canvas.PopState();

                        //        canvas.PushState();
                        //        canvas.State.TransformAngle = angle;
                        //        canvas.State.TransformScale = scale;
                        //        canvas.State.ColorTint = new ColorRgba(0, 0, 0, 200);
                        //        canvas.FillCircle(borderPoly[1].X, borderPoly[1].Y, lineWidth * 4f);
                        //        canvas.PopState();
                        //    }

                        //    canvas.PushState();
                        //    canvas.State.TransformAngle = angle;
                        //    canvas.State.TransformScale = scale;
                        //    canvas.State.TextFont = Font.GenericMonospace8;
                        //    canvas.State.ColorTint = i == selectedPointId ? new ColorRgba(0, 0, 0, 255) : new ColorRgba(255, 255, 255, 255);
                        //    canvas.DrawText(i.ToString(), borderPoly[1].X, borderPoly[1].Y, 0f, Alignment.Center, false);
                        //    canvas.PopState();
                        //}
                    }
                    //canvas.PushState();
                    //if (isSelected) canvas.State.ColorTint = lineColor;
                    //else canvas.State.SetMaterial(borderMaterial);

                    //Rect boundingRect = points.BoundingBox();

                    //float brW = scaleTexture ? boundingRect.W : this.sharedMaterial.Res.MainTexture.Res.Size.X;
                    //float brH = scaleTexture ? boundingRect.H : this.sharedMaterial.Res.MainTexture.Res.Size.Y;
                    //float ratioW = brH / brW;
                    //float ratioH = brW / brH;

                    ////float minX = MathF.Min(transformedX, transformedX1);
                    ////float minY = MathF.Min(transformedY, transformedY1);
                    ////Rect localRect = new Rect(minX, minY, MathF.Max(transformedX, transformedX1) - minX, MathF.Max(transformedY, transformedY1) - minY);

                    //canvas.State.TransformAngle = angle;
                    //canvas.State.TransformScale = scale;
                    ////canvas.State.TextureCoordinateRect = new Rect(
                    ////    (1 / brW) * localRect.X,
                    ////    (1 / brH) * localRect.Y,
                    ////    (1 / brW) * localRect.W,
                    ////    (1 / brH) * localRect.H
                    ////);

                    //if (i < t - 1) canvas.FillThickLine(transformedX, transformedY, transformedX1, transformedY1, lineWidth);
                    //else canvas.FillThickLine(transformedX, transformedY, transformedX0, transformedY0, lineWidth);

                    //canvas.PushState();
                    //if (i < t - 1) canvas.State.TransformAngle = MathF.Angle(transformedX, transformedY, transformedX1, transformedY1);
                    //else canvas.State.TransformAngle = MathF.Angle(transformedX, transformedY, transformedX0, transformedY0);
                    //canvas.FillCircle(transformedX, transformedY, lineWidth * .5f);
                    //canvas.PopState();

                    //canvas.PopState();
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
            //throw new NotImplementedException();
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
                    Transform trans = this.GameObj.GetComponent<Transform>();

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

                UpdateShapes();

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
