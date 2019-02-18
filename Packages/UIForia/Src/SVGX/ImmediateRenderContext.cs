using System.Collections.Generic;
using UIForia.Text;
using UIForia.Util;
using UnityEngine;

namespace SVGX {

    // todo this saves 2 list allocations + operations
    public struct SVGXDrawState {

        public SVGXMatrix matrix;
        public SVGXStyle style;

        public Vector2 lastPoint;
        // clip state lives here too

    }

    public class ImmediateRenderContext {

        internal readonly LightList<Vector2> points;
        internal readonly LightList<SVGXStyle> styles;
        internal readonly LightList<SVGXMatrix> transforms;
        internal readonly LightList<SVGXDrawCall> drawCalls;
        internal readonly LightList<SVGXShape> shapes;
        internal readonly LightList<SVGXClipGroup> clipGroups;
        internal readonly Stack<int> clipStack;
        internal readonly LightList<SVGXGradient> gradients;
        internal readonly LightList<Texture2D> textures;

        private Vector2 lastPoint;
        private SVGXMatrix currentMatrix;
        private SVGXStyle currentStyle;
        private RangeInt currentShapeRange;
        private SVGXGradient currentGradient;
        private Texture2D currentTexture;

        public ImmediateRenderContext() {
            points = new LightList<Vector2>(128);
            styles = new LightList<SVGXStyle>();
            transforms = new LightList<SVGXMatrix>();
            currentMatrix = SVGXMatrix.identity;
            drawCalls = new LightList<SVGXDrawCall>();
            shapes = new LightList<SVGXShape>();
            clipGroups = new LightList<SVGXClipGroup>();
            gradients = new LightList<SVGXGradient>();
            shapes.Add(new SVGXShape(SVGXShapeType.Unset, default));
            textures = new LightList<Texture2D>();
            clipStack = new Stack<int>();
            currentStyle = SVGXStyle.Default();
        }

        public void SetFill(Color color) {
            currentStyle.fillMode = FillMode.Color;
            currentStyle.fillColor = color;
        }

        public void SetFill(SVGXGradient gradient) {
            currentStyle.fillMode = FillMode.Gradient;
            currentGradient = gradient;
        }

        public void SetFill(Texture2D texture, Color tintColor) {
            currentStyle.fillMode = FillMode.TextureTint;
            currentStyle.fillTintColor = tintColor;
            currentTexture = texture;
        }

        public void SetFill(Texture2D texture, SVGXGradient gradient) {
            currentStyle.fillMode = FillMode.TextureGradient;
            currentStyle.gradientId = gradient.id;
            currentTexture = texture;
            currentGradient = gradient;
        }

        public void SetFill(Texture2D texture) {
            currentStyle.fillMode = FillMode.Texture;
            currentStyle.fillTintColor = Color.white;
            currentStyle.textureId = texture.GetInstanceID();
            currentTexture = texture;
        }

        public void SetFill(Texture2D texture, SVGXGradient gradient, float mix) {
            currentStyle.fillMode = FillMode.Texture;
            currentStyle.fillTintColor = Color.white;
            currentStyle.textureId = texture.GetInstanceID();
            currentTexture = texture;
        }

        public void SetStrokeColor(Color color) {
            this.currentStyle.strokeColor = color;
        }

        public void MoveTo(float x, float y) {
            // todo -- if last was move to, set point and return
            lastPoint = new Vector2(x, y);
            SVGXShape currentShape = shapes[shapes.Count - 1];
            if (currentShape.type != SVGXShapeType.Unset) {
                shapes.Add(new SVGXShape(SVGXShapeType.Unset, default));
                currentShapeRange.length++;
            }
        }

        public void Text(float x, float y, string text) { }

        public void Text(float x, float y, TextInfo text) { }

        public void LineTo(float x, float y) {
            SVGXShape currentShape = shapes[shapes.Count - 1];

            Vector2 point = new Vector2(x, y);

            switch (currentShape.type) {
                case SVGXShapeType.Path:
                    currentShape.pointRange.length++;
                    shapes[shapes.Count - 1] = currentShape;
                    break;
                case SVGXShapeType.Unset:
                    currentShape = new SVGXShape(SVGXShapeType.Path, new RangeInt(points.Count, 2));
                    shapes[shapes.Count - 1] = currentShape;
                    currentShapeRange.length++;
                    points.Add(lastPoint);
                    break;
                default:
                    currentShape = new SVGXShape(SVGXShapeType.Path, new RangeInt(points.Count, 2));
                    shapes.Add(currentShape);
                    points.Add(lastPoint);
                    currentShapeRange.length++;
                    break;
            }

            lastPoint = point;
            points.Add(point);
        }

        public void HorizontalLineTo(float x) {
            LineTo(x, lastPoint.y);
        }

        public void VerticalLineTo(float y) {
            LineTo(lastPoint.x, y);
        }

        public void ArcTo(float rx, float ry, float angle, bool isLargeArc, bool isSweepArc, float endX, float endY) {
            Vector2 end = new Vector2(endX, endY);

            int pointStart = points.Count;
            int pointCount = SVGXBezier.Arc(points, lastPoint, rx, ry, angle, isLargeArc, isSweepArc, end);
            UpdateShape(pointStart, pointCount);
            lastPoint = end;
        }

        public void ClosePath() {
            SVGXShape currentShape = shapes[shapes.Count - 1];
            if (currentShape.type != SVGXShapeType.Path) {
                return;
            }

            Vector2 startPoint = points[currentShape.pointRange.start];
            LineTo(startPoint.x, startPoint.y);
            currentShape.isClosed = true;
            shapes[shapes.Count - 1] = currentShape;
            shapes.Add(new SVGXShape(SVGXShapeType.Unset, default));
            lastPoint = startPoint;
        }

        public void CubicCurveTo(Vector2 ctrl0, Vector2 ctrl1, Vector2 end) {
            int pointStart = points.Count;
            int pointCount = SVGXBezier.CubicCurve(points, lastPoint, ctrl0, ctrl1, end);
            UpdateShape(pointStart, pointCount);
            lastPoint = end;
        }

        public void QuadraticCurveTo(Vector2 ctrl, Vector2 end) {
            int pointStart = points.Count;
            int pointCount = SVGXBezier.QuadraticCurve(points, lastPoint, ctrl, end);
            UpdateShape(pointStart, pointCount);

            lastPoint = end;
        }

        public void RoundedRect(Rect rect, float rtl, float rtr, float rbl, float rbr) {
            float halfW = rect.width * 0.5f;
            float halfH = rect.height * 0.5f;
            float rxBL = rbl < halfW ? rbl : halfW;
            float ryBL = rbl < halfH ? rbl : halfH;
            float rxBR = rbr < halfW ? rbr : halfW;
            float ryBR = rbr < halfH ? rbr : halfH;
            float rxTL = rtl < halfW ? rtl : halfW;
            float ryTL = rtl < halfH ? rtl : halfH;
            float rxTR = rtr < halfW ? rtr : halfW;
            float ryTR = rtr < halfH ? rtr : halfH;

            float x = rect.x;
            float y = rect.y;
            float w = rect.width;
            float h = rect.height;

            SVGXShapeType lastType = shapes[shapes.Count - 1].type;
            SVGXShape currentShape = new SVGXShape();

            int pointRangeStart = points.Count;
            const float OneMinusKappa90 = 0.4477152f;

            points.Add(new Vector2(x, y + ryTL)); // move to
            Vector2 last = new Vector2(x, y + h - ryBL);

            points.Add(last); // line to

            SVGXBezier.CubicCurve(
                points,
                last,
                new Vector2(x, y + h - ryBL * OneMinusKappa90),
                new Vector2(x + rxBL * OneMinusKappa90, y + h),
                new Vector2(x + rxBL, y + h)
            );

            last = new Vector2(x + w - rxBR, y + h); // line to
            points.Add(last);

            SVGXBezier.CubicCurve(
                points,
                last,
                new Vector2(x + w - rxBR * OneMinusKappa90, y + h),
                new Vector2(x + w, y + h - ryBR * OneMinusKappa90),
                new Vector2(x + w, y + h - ryBR)
            );

            last = new Vector2(x + w, y + ryTR); // line to
            points.Add(last);

            SVGXBezier.CubicCurve(
                points,
                last,
                new Vector2(x + w, y + ryTR * OneMinusKappa90),
                new Vector2(x + w - rxTR * OneMinusKappa90, y),
                new Vector2(x + w - rxTR, y)
            );

            last = new Vector2(x + rxTL, y); // line to
            points.Add(last);

            SVGXBezier.CubicCurve(
                points,
                last,
                new Vector2(x + rxTL * OneMinusKappa90, y),
                new Vector2(x, y + ryTL * OneMinusKappa90),
                new Vector2(x, y + ryTL)
            );

            RangeInt pointRange = new RangeInt(pointRangeStart, points.Count - pointRangeStart);
            currentShape = new SVGXShape(SVGXShapeType.RoundedRect, pointRange, new Vector2(x, y));
            currentShape.bounds = new SVGXBounds(rect.min, rect.max);
//            currentShape.isClosed = true; // todo -- isClosed yields the wrong behavior

            if (lastType != SVGXShapeType.Unset) {
                shapes.Add(currentShape);
            }
            else {
                shapes[shapes.Count - 1] = currentShape;
            }

            lastPoint = points[points.Count - 1];
            currentShapeRange.length++;
        }

        // todo -- diamond / other sdf shapes

        private void UpdateShape(int pointStart, int pointCount) {
            SVGXShape currentShape = shapes[shapes.Count - 1];
            switch (currentShape.type) {
                case SVGXShapeType.Path:
                    currentShape.pointRange.length += pointCount;
                    shapes[shapes.Count - 1] = currentShape;
                    break;
                case SVGXShapeType.Unset:
                    currentShape = new SVGXShape(SVGXShapeType.Path, new RangeInt(pointStart, pointCount));
                    shapes[shapes.Count - 1] = currentShape;
                    currentShapeRange.length++;
                    break;
                default:
                    currentShape = new SVGXShape(SVGXShapeType.Path, new RangeInt(pointStart, pointCount));
                    shapes.Add(currentShape);
                    currentShapeRange.length++;
                    break;
            }
        }

        public void Clear() {
            points.Clear();
            styles.Clear();
            transforms.Clear();
            drawCalls.Clear();
            shapes.Clear();
            currentStyle = SVGXStyle.Default();
            currentMatrix = SVGXMatrix.identity;
            lastPoint = Vector2.zero;
            shapes.Add(new SVGXShape(SVGXShapeType.Unset, default));
            currentShapeRange = new RangeInt();
            gradients.Clear();
            textures.Clear();
            clipStack.Clear();
            clipGroups.Clear();
        }

        public void Save() {
            transforms.Add(currentMatrix);
            styles.Add(currentStyle);
        }

        public void Restore() {
            if (transforms.Count > 0) {
                currentMatrix = transforms.RemoveLast();
            }

            if (styles.Count > 0) {
                currentStyle = styles.RemoveLast();
            }
        }

        internal SVGXClipGroup GetClipGroup(int id) {
            if (id >= 0 && id < clipGroups.Count) {
                return clipGroups[id];
            }

            return default;
        }

        public void PushClip() {
            int parentId = clipStack.Count > 0 ? clipStack.Peek() : -1;
            SVGXClipGroup clipGroup = new SVGXClipGroup(parentId, currentShapeRange);
            clipStack.Push(clipGroups.Count);
            clipGroups.Add(clipGroup);
            BeginPath();
        }

        public void PopClip() {
            if (clipStack.Count > 0) {
                clipStack.Pop();
            }
        }

        public void Rect(float x, float y, float width, float height) {
            SimpleShape(SVGXShapeType.Rect, x, y, width, height);
        }

        public void Ellipse(float x, float y, float dx, float dy) {
            SimpleShape(SVGXShapeType.Ellipse, x, y, dx, dy);
        }

        public void Circle(float x, float y, float radius) {
            SimpleShape(SVGXShapeType.Circle, x, y, radius * 2f, radius * 2f);
        }

        public void CircleFromCenter(float cx, float cy, float radius) {
            SimpleShape(SVGXShapeType.Circle, cx - radius, cy - radius, radius * 2f, radius * 2f);
        }

        public void FillRect(float x, float y, float width, float height) {
            BeginPath();
            Rect(x, y, width, height);
            Fill();
            BeginPath();
        }

        public void FillRect(Rect rect) {
            BeginPath();
            Rect(rect.x, rect.y, rect.width, rect.height);
            Fill();
            BeginPath();
        }

        public void FillCircle(float x, float y, float radius) {
            BeginPath();
            Circle(x, y, radius);
            Fill();
            BeginPath();
        }

        public void FillEllipse(float x, float y, float dx, float dy) {
            BeginPath();
            Ellipse(x, y, dx, dy);
            Fill();
            BeginPath();
        }

        private void SimpleShape(SVGXShapeType shapeType, float x, float y, float width, float height) {
            SVGXShape currentShape = shapes[shapes.Count - 1];
            SVGXShapeType lastType = currentShape.type;

            Vector2 x0y0 = new Vector2(x, y);
            Vector2 x1y0 = new Vector2(x + width, y);
            Vector2 x1y1 = new Vector2(x + width, y + height);
            Vector2 x0y1 = new Vector2(x, y + height);

            currentShape = new SVGXShape(shapeType, new RangeInt(points.Count, 4));
            currentShape.bounds = new SVGXBounds(x0y0, x1y1);
            currentShape.origin = x0y0;

            points.EnsureAdditionalCapacity(4);
            points.AddUnchecked(x0y0);
            points.AddUnchecked(x1y0);
            points.AddUnchecked(x1y1);
            points.AddUnchecked(x0y1);

            currentShape.isClosed = true;

            if (lastType != SVGXShapeType.Unset) {
                shapes.Add(currentShape);
            }
            else {
                shapes[shapes.Count - 1] = currentShape;
            }

            currentShapeRange.length++;
        }

        public void BeginPath() {
            SVGXShape currentShape = shapes[shapes.Count - 1];
            if (currentShape.type != SVGXShapeType.Unset) {
                shapes.Add(new SVGXShape(SVGXShapeType.Unset, default));
                currentShapeRange = new RangeInt(shapes.Count - 1, 0);
            }
        }

        public void Fill() {
            if ((currentStyle.fillMode & FillMode.Texture) != 0) {
                if (!textures.Contains(currentTexture)) {
                    textures.Add(currentTexture);
                }

                currentStyle.textureId = currentTexture.GetInstanceID();
            }

            if ((currentStyle.fillMode & FillMode.Gradient) != 0) {
                if (!gradients.Contains(currentGradient)) {
                    gradients.Add(currentGradient);
                }

                currentStyle.gradientId = currentGradient.id;
            }

            int clipId = clipStack.Count > 0 ? clipStack.Peek() : -1;
            drawCalls.Add(new SVGXDrawCall(DrawCallType.StandardFill, clipId, currentStyle, currentMatrix, currentShapeRange));
        }

        public void Stroke() {
            int clipId = clipStack.Count > 0 ? clipStack.Peek() : -1;
            drawCalls.Add(new SVGXDrawCall(DrawCallType.StandardStroke, clipId, currentStyle, currentMatrix, currentShapeRange));
        }

        public void SetStrokeOpacity(float opacity) {
            currentStyle.strokeOpacity = opacity;
        }

        public void SetStrokeWidth(float width) {
            currentStyle.strokeWidth = width;
        }

        public void SetTransform(SVGXMatrix trs) {
            currentMatrix = trs;
        }

        public void SaveState() { }

        public void RestoreState() { }

        public void SetFillOpacity(float fillOpacity) {
            currentStyle.fillOpacity = fillOpacity;
        }

    }

}