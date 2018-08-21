using System;
using System.Reflection;
using Rendering;
using Src.Compilers.AliasSource;
using Src.Layout;
using Src.StyleBindings;
using Src.StyleBindings.Src.StyleBindings;
using UnityEngine;

namespace Src.Compilers {

    public static class StyleTemplateConstants {

        public const string RectX = "rectX";
        public const string RectY = "rectY";
        public const string RectWidth = "rectWidth";
        public const string RectHeight = "rectHeight";

        public const string MinWidth = "minWidth";
        public const string MaxWidth = "maxWidth";
        public const string MinHeight = "minHeight";
        public const string MaxHeight = "maxHeight";

        public const string GrowthFactor = "growthFactor";
        public const string ShrinkFactor = "shrinkFactor";

        public const string BorderColor = "borderColor";
        public const string BackgroundImage = "backgroundImage";
        public const string BackgroundColor = "backgroundColor";

        public const string Padding = "padding";
        public const string PaddingTop = "paddingTop";
        public const string PaddingRight = "paddingRight";
        public const string PaddingBottom = "paddingBottom";
        public const string PaddingLeft = "paddingLeft";

        public const string Border = "border";
        public const string BorderTop = "borderTop";
        public const string BorderRight = "borderRight";
        public const string BorderBottom = "borderBottom";
        public const string BorderLeft = "borderLeft";

        public const string Margin = "margin";
        public const string MarginTop = "marginTop";
        public const string MarginRight = "marginRight";
        public const string MarginBottom = "marginBottom";
        public const string MarginLeft = "marginLeft";

        public const string MainAxisAlignment = "mainAxisAlignment";
        public const string CrossAxisAlignment = "crossAxisAlignment";
        public const string LayoutDirection = "layoutDirection";
        public const string LayoutType = "layoutType";
        public const string LayoutFlow = "layoutFlow";
        public const string LayoutWrap = "layoutWrap";

    }


    public class StyleBindingCompiler {

        private ContextDefinition context;
        private ExpressionCompiler compiler;

        private static readonly MethodAliasSource rgbSource;
        private static readonly MethodAliasSource rgbaSource;
        private static readonly MethodAliasSource rect1Source;
        private static readonly MethodAliasSource rect2Source;
        private static readonly MethodAliasSource rect4Source;
        private static readonly ColorAliasSource colorSource;
        private static readonly EnumAliasSource<LayoutType> layoutTypeSource;
        private static readonly EnumAliasSource<LayoutDirection> layoutDirectionSource;
        private static readonly EnumAliasSource<LayoutFlowType> layoutFlowSource;
        private static readonly EnumAliasSource<LayoutWrap> layoutWrapSource;
        private static readonly EnumAliasSource<MainAxisAlignment> mainAxisAlignmentSource;
        private static readonly EnumAliasSource<CrossAxisAlignment> crossAxisAlignmentSource;

        static StyleBindingCompiler() {
            Type type = typeof(StyleBindingCompiler);
            rgbSource = new MethodAliasSource("rgb", type.GetMethod("Rgb", ReflectionUtil.PublicStatic));
            rgbaSource = new MethodAliasSource("rgba", type.GetMethod("Rgba", ReflectionUtil.PublicStatic));
            rect1Source = new MethodAliasSource("rect", type.GetMethod("Rect", new[] {typeof(float)}));
            rect2Source = new MethodAliasSource("rect", type.GetMethod("Rect", new[] {typeof(float), typeof(float)}));
            rect4Source = new MethodAliasSource("rect", type.GetMethod("Rect", new[] {typeof(float), typeof(float), typeof(float), typeof(float)}));


            colorSource = new ColorAliasSource();
            layoutTypeSource = new EnumAliasSource<LayoutType>();
            layoutDirectionSource = new EnumAliasSource<LayoutDirection>();
            layoutFlowSource = new EnumAliasSource<LayoutFlowType>();
            layoutWrapSource = new EnumAliasSource<LayoutWrap>();
            mainAxisAlignmentSource = new EnumAliasSource<MainAxisAlignment>();
            crossAxisAlignmentSource = new EnumAliasSource<CrossAxisAlignment>();
        }

        public StyleBinding Compile(ContextDefinition context, string key, string value) {
            this.context = context;
            // todo -- drop this restriction if possible
            if (value[0] != '{') {
                value = '{' + value + '}';
            }

            // todo -- dont' compile until we know the type of attribute we are parsing
            // then set contexts to alias constants, get constant returns from url and rgb functions, etc
//            context.SetAliasToConstant("");

            compiler = new ExpressionCompiler(context);
            Target targetState = GetTargetState(key);

            switch (targetState.property) {
                // Paint
                case StyleTemplateConstants.BackgroundImage:
                    return new StyleBinding_BackgroundImage(targetState.state, Compile<Texture2D>(value));

                case StyleTemplateConstants.BackgroundColor:
                    return new StyleBinding_BackgroundColor(targetState.state,
                        Compile<Color>(value, rgbSource, rgbaSource, colorSource));

                case StyleTemplateConstants.BorderColor:
                    return new StyleBinding_BorderColor(targetState.state,
                        Compile<Color>(value, rgbSource, rgbaSource, colorSource));

                // Rect
                case StyleTemplateConstants.RectX:
                    return new StyleBinding_RectX(targetState.state, Compile<UIMeasurement>(value));

                case StyleTemplateConstants.RectY:
                    return new StyleBinding_RectY(targetState.state, Compile<UIMeasurement>(value));

                case StyleTemplateConstants.RectWidth:
                    return new StyleBinding_RectWidth(targetState.state, Compile<UIMeasurement>(value));

                case StyleTemplateConstants.RectHeight:
                    return new StyleBinding_RectHeight(targetState.state, Compile<UIMeasurement>(value));

                // Constraints

                case StyleTemplateConstants.MinWidth:
                    return new StyleBinding_MinWidth(targetState.state, Compile<UIMeasurement>(value));

                case StyleTemplateConstants.MaxWidth:
                    return new StyleBinding_MaxWidth(targetState.state, Compile<UIMeasurement>(value));

                case StyleTemplateConstants.MinHeight:
                    return new StyleBinding_MinHeight(targetState.state, Compile<UIMeasurement>(value));

                case StyleTemplateConstants.MaxHeight:
                    return new StyleBinding_MaxHeight(targetState.state, Compile<UIMeasurement>(value));

                case StyleTemplateConstants.GrowthFactor:
                    return new StyleBinding_GrowthFactor(targetState.state, Compile<int>(value));

                case StyleTemplateConstants.ShrinkFactor:
                    return new StyleBinding_ShrinkFactor(targetState.state, Compile<int>(value));

                // Layout

                case StyleTemplateConstants.MainAxisAlignment:
                    return new StyleBinding_MainAxisAlignment(
                        targetState.state, Compile<MainAxisAlignment>(value, mainAxisAlignmentSource)
                    );

                case StyleTemplateConstants.CrossAxisAlignment:
                    return new StyleBinding_CrossAxisAlignment(
                        targetState.state, Compile<CrossAxisAlignment>(value, crossAxisAlignmentSource)
                    );

                case StyleTemplateConstants.LayoutDirection:
                    return new StyleBinding_LayoutDirection(
                        targetState.state, Compile<LayoutDirection>(value, layoutDirectionSource)
                    );

                case StyleTemplateConstants.LayoutFlow:
                    return new StyleBinding_LayoutFlowType(
                        targetState.state, Compile<LayoutFlowType>(value, layoutFlowSource)
                    );

                case StyleTemplateConstants.LayoutType:
                    return new StyleBinding_LayoutType(targetState.state, Compile<LayoutType>(value, layoutTypeSource));

                case StyleTemplateConstants.LayoutWrap:
                    return new StyleBinding_LayoutWrap(targetState.state, Compile<LayoutWrap>(value, layoutWrapSource));

                // Padding

                case StyleTemplateConstants.Padding:
                    return new StyleBinding_Padding(targetState.state, Compile<ContentBoxRect>(value, rect1Source, rect2Source, rect4Source));

                case StyleTemplateConstants.PaddingTop:
                    return new StyleBinding_PaddingTop(targetState.state, Compile<float>(value));

                case StyleTemplateConstants.PaddingRight:
                    return new StyleBinding_PaddingRight(targetState.state, Compile<float>(value));

                case StyleTemplateConstants.PaddingBottom:
                    return new StyleBinding_PaddingBottom(targetState.state, Compile<float>(value));

                case StyleTemplateConstants.PaddingLeft:
                    return new StyleBinding_PaddingLeft(targetState.state, Compile<float>(value));

                // Border

                case StyleTemplateConstants.Border:
                    return new StyleBinding_Border(targetState.state, Compile<ContentBoxRect>(value, rect1Source, rect2Source, rect4Source));
                case StyleTemplateConstants.BorderTop:
                    return new StyleBinding_BorderTop(targetState.state, Compile<float>(value));

                case StyleTemplateConstants.BorderRight:
                    return new StyleBinding_BorderRight(targetState.state, Compile<float>(value));

                case StyleTemplateConstants.BorderBottom:
                    return new StyleBinding_BorderBottom(targetState.state, Compile<float>(value));

                case StyleTemplateConstants.BorderLeft:
                    return new StyleBinding_PaddingLeft(targetState.state, Compile<float>(value));

                // Margin

                case StyleTemplateConstants.Margin:
                    return new StyleBinding_Margin(targetState.state, Compile<ContentBoxRect>(value, rect1Source, rect2Source, rect4Source));

                case StyleTemplateConstants.MarginTop:
                    return new StyleBinding_MarginTop(targetState.state, Compile<float>(value));

                case StyleTemplateConstants.MarginRight:
                    return new StyleBinding_MarginRight(targetState.state, Compile<float>(value));

                case StyleTemplateConstants.MarginBottom:
                    return new StyleBinding_MarginBottom(targetState.state, Compile<float>(value));

                case StyleTemplateConstants.MarginLeft:
                    return new StyleBinding_MarginLeft(targetState.state, Compile<float>(value));

                default: return null;
            }
        }

        private static Target GetTargetState(string key) {
            if (key.StartsWith("style.hover.")) {
                return new Target(key.Substring("style.hover.".Length), StyleState.Hover);
            }

            if (key.StartsWith("style.disabled.")) {
                return new Target(key.Substring("style.disabled.".Length), StyleState.Disabled);
            }

            if (key.StartsWith("style.focused.")) {
                return new Target(key.Substring("style.focused.".Length), StyleState.Focused);
            }

            if (key.StartsWith("style.active.")) {
                return new Target(key.Substring("style.active.".Length), StyleState.Active);
            }

            if (key.StartsWith("style.")) {
                return new Target(key.Substring("style.".Length), StyleState.Normal);
            }

            throw new Exception("invalid target state");
        }

        private Expression<T> Compile<T>(string value, params IAliasSource[] sources) {
            for (int i = 0; i < sources.Length; i++) {
                context.AddConstAliasSource(sources[i]);
            }

            Expression<T> expression = compiler.Compile<T>(value);

            for (int i = 0; i < sources.Length; i++) {
                context.RemoveConstAliasSource(sources[i]);
            }

            return expression;
        }

        public static Color Rgb(float r, float g, float b) {
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        public static Color Rgba(float r, float g, float b, float a) {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        public static ContentBoxRect Rect(float top, float right, float bottom, float left) {
            return new ContentBoxRect(top, right, bottom, left);
        }

        public static ContentBoxRect Rect(float topBottom, float leftRight) {
            return new ContentBoxRect(topBottom, leftRight, topBottom, leftRight);
        }

        public static ContentBoxRect Rect(float value) {
            return new ContentBoxRect(value, value, value, value);
        }

        public static AssetPointer Url(string url) {
            return new AssetPointer(url);
        }

        public struct AssetPointer {

            public readonly string path;

            public AssetPointer(string path) {
                this.path = path;
            }

        }

        private struct Target {

            public readonly string property;
            public readonly StyleState state;

            public Target(string property, StyleState state) {
                this.property = property;
                this.state = state;
            }

        }

    }

}