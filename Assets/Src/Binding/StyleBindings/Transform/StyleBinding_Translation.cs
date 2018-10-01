using Rendering;
using Src.Rendering;

namespace Src.StyleBindings {

    public class StyleBinding_Translation : StyleBinding {

        private readonly Expression<MeasurementVector2> expression;
        
        public StyleBinding_Translation(StyleState state, Expression<MeasurementVector2> expression) 
            : base(RenderConstants.Translation, state) {
            this.expression = expression;
        }

        public override void Execute(UIElement element, UITemplateContext context) {
            if (!element.style.IsInState(state)) return;
            MeasurementVector2 current = element.style.computedStyle.TransformPosition;
            MeasurementVector2 newTranslation = expression.EvaluateTyped(context);
            if (current != newTranslation) {
                element.style.SetTransformPosition(newTranslation, state);
            }
        }

        public override bool IsConstant() {
            return expression.IsConstant();
        }

        public override void Apply(UIStyle style, UITemplateContext context) {
            MeasurementVector2 vec = expression.EvaluateTyped(context);
            style.TransformPositionX = vec.x;
            style.TransformPositionY = vec.y;
        }

        public override void Apply(UIStyleSet styleSet, UITemplateContext context) {
            styleSet.SetTransformPosition(expression.EvaluateTyped(context), state);
        }

    }

}