﻿using Rendering;

namespace Src.StyleBindings {

    public class StyleBinding_RectWidth : StyleBinding {

        public readonly Expression<UIMeasurement> expression;

        public StyleBinding_RectWidth(StyleState state, Expression<UIMeasurement> expression) : base(state) {
            this.expression = expression;
        }

        public override void Execute(UIElement element, UITemplateContext context) {
            UIMeasurement width = element.style.GetRectWidth(state);
            UIMeasurement newWidth = expression.EvaluateTyped(context);
            if (width != newWidth) {
                element.style.SetRectWidth(newWidth, state);
            }
        }

        public override bool IsConstant() {
            return expression.IsConstant();
        }

        public override void Apply(UIStyle style, UITemplateContext context) {
            style.rect.width = expression.EvaluateTyped(context);
        }

        public override void Apply(UIStyleSet styleSet, UITemplateContext context) {
            styleSet.SetRectWidth(expression.EvaluateTyped(context), state);
        }

    }

}