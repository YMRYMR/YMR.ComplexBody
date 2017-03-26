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
        #region Private Members

        [DontSerialize]
        private List<CPoint2D> lastVertices = new List<CPoint2D>();
        [DontSerialize]
        private CanvasBuffer vertexBuffer = new CanvasBuffer();
        [DontSerialize]
        private int selectedPointId = -1;
        [DontSerialize]
        private bool working = false;

        private List<Vector2> points = new List<Vector2>()
        {
            new Vector2(0, 0), 
            new Vector2(100, 0), 
            new Vector2(100, 100), 
        };
        private bool showPoints = true;
        private bool showBorderMaterial = true;
        private bool showBorderGeometry = true;
        private bool showBorderDummies = false;
        private bool showPolygons = true;
        private bool showTexture = true;
        private bool updatableUsingMouse = true;
        private ContentRef<Material> sharedMaterial = Material.Checkerboard;
        private ContentRef<Material> borderMaterial = Material.Checkerboard;
        private float borderWidth = 2;
        private float lineWidth = 2;
        private ColorRgba polygonColor = new ColorRgba(255, 0, 0, 200);
        private ColorRgba borderGeometryColor = new ColorRgba(0, 255, 0, 200);
        private bool scaleTexture = false;

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

        #endregion

        #region Public Members

        /// <summary>
        /// Body vertices
        /// </summary>
        public List<Vector2> Points { get { return points; } set { points = value; } }
        public bool ShowPoints { get { return showPoints; } set { showPoints = value; } }
        public bool ShowBorderMaterial { get { return showBorderMaterial; } set { showBorderMaterial = value; } }
        public bool ShowBorderGeometry { get { return showBorderGeometry; } set { showBorderGeometry = value; } }
        public bool ShowBorderDummies { get { return showBorderDummies; } set { showBorderDummies = value; } }
        public bool ShowPolygons { get { return showPolygons; } set { showPolygons = value; } }
        public bool ShowTexture { get { return showTexture; } set { showTexture = value; } }
        public bool UpdatableUsingMouse { get { return updatableUsingMouse; } set { updatableUsingMouse = value; } }
        public ContentRef<Material> SharedMaterial { get { return sharedMaterial; } set { sharedMaterial = value; } }
        public ContentRef<Material> BorderMaterial { get { return borderMaterial; } set { borderMaterial = value; } }
        public override float BoundRadius { get { return this.GameObj.GetComponent<RigidBody>().BoundRadius; } }
        public float BorderWidth { get { return borderWidth; } set { borderWidth = value; } }
        public float LineWidth { get { return lineWidth; } set { lineWidth = value; } }
        public ColorRgba PolygonColor { get { return polygonColor; } set { polygonColor = value; } }
        public ColorRgba BorderGeometryColor { get { return borderGeometryColor; } set { borderGeometryColor = value; } }
        public bool ScaleTexture { get { return scaleTexture; } set { scaleTexture = value; } }

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

        private void TransformPoint(Transform trans, ref float x, ref float y)
        {
            TransformPoint(trans, ref x, ref y, true, true, true);
        }
        private void TransformPoint(Transform trans, ref float x, ref float y, bool applyPos, bool applyAngle, bool applyScale)
        {
            float angle = applyAngle ? trans.Angle : 0;
            float scale = applyScale ? trans.Scale : 1;
            MathF.TransformCoord(ref x, ref y, angle, scale);
            if (applyPos)
            {
                x += trans.Pos.X;
                y += trans.Pos.Y;
            }
        }

        #endregion

        #region Public Methods

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
                if (showTexture)
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


            Vector2[] innerLinePointsA = new Vector2[t];
            Vector2[] innerLinePointsB = new Vector2[t];
            Vector2[,] orderedTriangles = new Vector2[t, 3];
            for (int i = 0; i < t; i++)
            {   
                // Border
                if (showBorderMaterial || showBorderGeometry || showBorderDummies)
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
                        orderedTriangles[i, 0] = ordererdTriangle[0];
                        orderedTriangles[i, 1] = ordererdTriangle[1];
                        orderedTriangles[i, 2] = ordererdTriangle[2];
                        Vector2[] borderPoly = new Vector2[4];
                        borderPoly[0] = ordererdTriangle[0];
                        borderPoly[1] = ordererdTriangle[1];
                        float angle01 = MathF.Angle(borderPoly[0].X, borderPoly[0].Y, borderPoly[1].X, borderPoly[1].Y) - MathF.RadAngle90;

                        float halfDistance = MathF.Distance(borderPoly[0].X, borderPoly[0].Y, borderPoly[1].X, borderPoly[1].Y) * .5f;
                        Vector2 outerCenterPoint = new Vector2(borderPoly[0].X + halfDistance * MathF.Cos(angle01), borderPoly[0].Y + halfDistance * MathF.Sin(angle01));
                        outerCenterPoint.X += trans.Pos.X;
                        outerCenterPoint.Y += trans.Pos.Y;
                        Vector2 innerCenterPoint = new Vector2(outerCenterPoint.X + borderWidth * MathF.Cos(angle01 - MathF.RadAngle90), outerCenterPoint.Y + borderWidth * MathF.Sin(angle01 - MathF.RadAngle90));

                        innerLinePointsA[i] = new Vector2(innerCenterPoint.X + halfDistance * MathF.Cos(angle01), innerCenterPoint.Y + halfDistance * MathF.Sin(angle01));
                        innerLinePointsB[i] = new Vector2(innerCenterPoint.X - halfDistance * MathF.Cos(angle01), innerCenterPoint.Y - halfDistance * MathF.Sin(angle01));
                    }
                }
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

                // Vertices
                if (showPoints)
                {
                    /*canvas.PushState();
                    canvas.State.TextFont = Font.GenericMonospace8;
                    canvas.State.ColorTint = i == selectedPointId ? new ColorRgba(0, 0, 0, 255) : new ColorRgba(255, 255, 255, 255);
                    canvas.DrawText(i.ToString(), transformedX, transformedY, trans.Pos.Z - .01f, Alignment.Center, false);
                    canvas.PopState();*/

                    if (i == selectedPointId)
                    {
                        canvas.PushState();
                        canvas.State.ColorTint = new ColorRgba(255, 255, 255, 200);
                        if (ctrlPressed) canvas.FillRect(transformedX - 10, transformedY - 10, 20, 20);
                        else canvas.FillCircle(transformedX, transformedY, pointPos.Z / 50);
                        canvas.PopState();
                    }
                    else
                    {
                        canvas.PushState();
                        canvas.State.ColorTint = new ColorRgba(255, 255, 255, 200);
                        canvas.FillCircle(transformedX, transformedY, (pointPos.Z / 50) + 1);
                        canvas.PopState();
                        canvas.PushState();
                        canvas.State.ColorTint = new ColorRgba(0, 0, 0, 200);
                        canvas.FillCircle(transformedX, transformedY, pointPos.Z / 50);
                        canvas.PopState();
                    }
                }

                // Border
                if (showBorderMaterial || showBorderGeometry || showBorderDummies)
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
                    bool found = true;
                    if (found)
                    {
                        Vector2[] borderPoly = new Vector2[4];
                        borderPoly[0] = orderedTriangles[i, 0];
                        borderPoly[1] = orderedTriangles[i, 1];
                        float angle01 = MathF.Angle(borderPoly[0].X, borderPoly[0].Y, borderPoly[1].X, borderPoly[1].Y) - MathF.RadAngle90;

                        float halfDistance = MathF.Distance(borderPoly[0].X, borderPoly[0].Y, borderPoly[1].X, borderPoly[1].Y) * .5f;
                        Vector2 outerCenterPoint = new Vector2(borderPoly[0].X + halfDistance * MathF.Cos(angle01), borderPoly[0].Y + halfDistance * MathF.Sin(angle01));
                        outerCenterPoint.X += trans.Pos.X;
                        outerCenterPoint.Y += trans.Pos.Y;
                        Vector2 innerCenterPoint = new Vector2(outerCenterPoint.X + borderWidth * MathF.Cos(angle01 - MathF.RadAngle90), outerCenterPoint.Y + borderWidth * MathF.Sin(angle01 - MathF.RadAngle90));

                        Vector2[] tempInnerLinePointsA = new Vector2[3];
                        Vector2[] tempInnerLinePointsB = new Vector2[3];

                        if (i < t - 1)
                        {
                            if (i > 0)
                            {
                                tempInnerLinePointsA[0] = innerLinePointsA[i - 1];
                                tempInnerLinePointsB[0] = innerLinePointsB[i - 1];
                            }
                            else
                            {
                                tempInnerLinePointsA[0] = innerLinePointsA[t - 1];
                                tempInnerLinePointsB[0] = innerLinePointsB[t - 1];
                            }
                            tempInnerLinePointsA[1] = innerLinePointsA[i];
                            tempInnerLinePointsB[1] = innerLinePointsB[i];
                            tempInnerLinePointsA[2] = innerLinePointsA[i + 1];
                            tempInnerLinePointsB[2] = innerLinePointsB[i + 1];
                        }
                        else // Last point
                        {
                            tempInnerLinePointsA[0] = innerLinePointsA[i - 1];
                            tempInnerLinePointsB[0] = innerLinePointsB[i - 1];
                            tempInnerLinePointsA[1] = innerLinePointsA[i];
                            tempInnerLinePointsB[1] = innerLinePointsB[i];
                            tempInnerLinePointsA[2] = innerLinePointsA[0];
                            tempInnerLinePointsB[2] = innerLinePointsB[0];
                        }
                        // Inner point A
                        float crossX, crossY;
                        MathF.LinesCross(tempInnerLinePointsA[0].X, tempInnerLinePointsA[0].Y,
                                         tempInnerLinePointsB[0].X, tempInnerLinePointsB[0].Y,
                                         tempInnerLinePointsA[1].X, tempInnerLinePointsA[1].Y,
                                         tempInnerLinePointsB[1].X, tempInnerLinePointsB[1].Y,
                                         out crossX, out crossY, true);
                        borderPoly[2] = new Vector2(crossX, crossY);
                        // Inner point B
                        MathF.LinesCross(tempInnerLinePointsA[1].X, tempInnerLinePointsA[1].Y,
                                         tempInnerLinePointsB[1].X, tempInnerLinePointsB[1].Y,
                                         tempInnerLinePointsA[2].X, tempInnerLinePointsA[2].Y,
                                         tempInnerLinePointsB[2].X, tempInnerLinePointsB[2].Y,
                                         out crossX, out crossY, true);
                        borderPoly[3] = new Vector2(crossX, crossY);

                        //Rect localRect = new Rect(MathF.Min(borderPoly[0].X, borderPoly[1].X, borderPoly[2].X, borderPoly[3].X),
                        //                          MathF.Min(borderPoly[0].Y, borderPoly[1].Y, borderPoly[2].Y, borderPoly[3].Y),
                        //                          MathF.Max(borderPoly[0].X, borderPoly[1].X, borderPoly[2].X, borderPoly[3].X),
                        //                          MathF.Max(borderPoly[0].Y, borderPoly[1].Y, borderPoly[2].Y, borderPoly[3].Y));

                        if (showBorderMaterial)
                        {
                            canvas.PushState();
                            canvas.State.SetMaterial(this.borderMaterial);
                            canvas.State.TransformAngle = angle;
                            canvas.State.TransformScale = scale;
                            //canvas.State.TextureCoordinateRect = new Rect(
                            //    (1 / brW) * localRect.X,
                            //    (1 / brH) * localRect.Y,
                            //    (1 / brW) * localRect.W,
                            //    (1 / brH) * localRect.H
                            //);
                            canvas.FillPolygon(borderPoly, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                            canvas.PopState();
                        }

                        if (showBorderDummies)
                        {
                            canvas.PushState();
                            canvas.State.TransformAngle = angle;
                            canvas.State.TransformScale = scale;
                            canvas.State.ColorTint = ColorRgba.Blue.WithAlpha(200);
                            canvas.FillPolygonOutline(ordererdTriangle, lineWidth, trans.Pos.X, trans.Pos.Y, trans.Pos.Z);
                            canvas.FillCircle(outerCenterPoint.X, outerCenterPoint.Y, lineWidth * 2f);
                            canvas.FillCircle(innerCenterPoint.X, innerCenterPoint.Y, lineWidth * 2f);
                            canvas.FillThickLine(outerCenterPoint.X, outerCenterPoint.Y, innerCenterPoint.X, innerCenterPoint.Y, lineWidth);
                            canvas.FillThickLine(tempInnerLinePointsA[1].X, tempInnerLinePointsA[1].Y, tempInnerLinePointsB[1].X, tempInnerLinePointsB[1].Y, lineWidth);
                            canvas.FillCircle(tempInnerLinePointsA[1].X, tempInnerLinePointsA[1].Y, lineWidth * 2f);
                            canvas.FillCircle(tempInnerLinePointsB[1].X, tempInnerLinePointsB[1].Y, lineWidth * 2f);
                            canvas.FillCircle(borderPoly[2].X, borderPoly[2].Y, lineWidth * 2f);
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

        Vector2 LineIntersectionPoint(Vector2 ps1, Vector2 pe1, Vector2 ps2, Vector2 pe2)
        {
            try
            {
                // Get A,B,C of first line - points : ps1 to pe1
                float A1 = pe1.Y - ps1.Y;
                float B1 = ps1.X - pe1.X;
                float C1 = A1 * ps1.X + B1 * ps1.Y;

                // Get A,B,C of second line - points : ps2 to pe2
                float A2 = pe2.Y - ps2.Y;
                float B2 = ps2.X - pe2.X;
                float C2 = A2 * ps2.X + B2 * ps2.Y;

                // Get delta and check if the lines are parallel
                float delta = A1 * B2 - A2 * B1;
                if (delta == 0) return Vector2.Zero; //throw new System.Exception("Lines are parallel");

                // now return the Vector2 intersection point
                return new Vector2(
                    (B2 * C1 - B1 * C2) / delta,
                    (A1 * C2 - A2 * C1) / delta
                );
            }
            catch
            {
                return Vector2.Zero;
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
            target.showPoints = this.showPoints;
            target.showPolygons = this.showPolygons;
            target.showTexture = this.showTexture;
            target.updatableUsingMouse = this.updatableUsingMouse;
        }
    }
}
