using System;
using System.Collections.Generic;
using Rendering;
using UnityEngine;

namespace Src {

    public abstract class UITemplate {

        public ProcessedType processedElementType;
        public List<UITemplate> childTemplates;
        public List<AttributeDefinition> attributes;
        public List<ExpressionEvaluator> generatedBindings;

        public abstract bool TypeCheck();

        public abstract UIElement CreateScoped(TemplateScope scope);

        public virtual Type ElementType => processedElementType.type;

        public bool HasAttribute(string attributeName) {
            if (attributes == null) return false;

            for (int i = 0; i < attributes.Count; i++) {
                if (attributes[i].name == attributeName) return true;
            }

            return false;
        }

        public AttributeDefinition GetAttribute(string attributeName) {
            if (attributes == null) return null;

            for (int i = 0; i < attributes.Count; i++) {
                if (attributes[i].name == attributeName) return attributes[i];
            }

            return null;
        }

        public void ApplyStyles(UIElement element, TemplateScope scope) {

            element.style = new UIStyleSet(element, scope.view);

            if (!HasAttribute("style")) return;

            AttributeDefinition styleAttr = GetAttribute("style");
            StyleTemplate styleTemplate = scope.GetStyleTemplate(styleAttr.value);

            if (styleTemplate == null) {
                Debug.LogWarning("Unable to find style definition for: " + styleAttr.name);
                return;
            }

            UIStyle styleInstance = scope.GetStyleInstance(styleTemplate.id);
            element.style.SetInstanceStyle(styleInstance, StyleStateType.Normal);

        }

    }

}