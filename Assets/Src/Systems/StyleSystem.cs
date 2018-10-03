﻿using System;
using Rendering;
using Src.StyleBindings;
using System.Collections.Generic;
using Src.Extensions;
using Src.Layout;
using Src.Rendering;
using Src.Util;
using TMPro;
using UnityEngine;

namespace Src.Systems {

    public delegate void PaintChanged(UIElement element, Paint paint);

    public delegate void RectPropertyChanged(UIElement element, Dimensions rect);

    public delegate void ContentBoxChanged(UIElement element, ContentBoxRect rect);

    public delegate void ConstraintChanged(UIElement element, LayoutConstraints constraints);

    public delegate void LayoutChanged(UIElement element, LayoutParameters layoutParameters);

    public delegate void BorderRadiusChanged(UIElement element, BorderRadius radius);

    public delegate void FontPropertyChanged(UIElement element, TextStyle textStyle);

    public delegate void AvailableStatesChanged(UIElement element, StyleState state);

    public delegate void TextContentChanged(UIElement element, string text);

    public class StyleSystem : ISystem, IStyleSystem {

        private const UIElementFlags FlagCheck = UIElementFlags.RequiresRendering | UIElementFlags.RequiresLayout | UIElementFlags.TextElement;

        public event PaintChanged onPaintChanged;
        public event LayoutChanged onLayoutChanged;
        public event RectPropertyChanged onRectChanged;
        public event ContentBoxChanged onMarginChanged;
        public event ContentBoxChanged onBorderChanged;
        public event ContentBoxChanged onPaddingChanged;
        public event ConstraintChanged onConstraintChanged;
        public event BorderRadiusChanged onBorderRadiusChanged;
        public event FontPropertyChanged onFontPropertyChanged;
        public event AvailableStatesChanged onAvailableStatesChanged;
        public event TextContentChanged onTextContentChanged;
        public event Action<UIElement, UITransform> onTransformChanged;
        public event Action<UIElement> onOverflowPropertyChanged;
        
        public event Action<UIElement, MainAxisAlignment, MainAxisAlignment> onMainAxisAlignmentChanged;
        public event Action<UIElement, CrossAxisAlignment, CrossAxisAlignment> onCrossAxisAlignmentChanged;
        public event Action<UIElement, LayoutWrap, LayoutWrap> onLayoutWrapChanged;
        public event Action<UIElement, LayoutDirection> onLayoutDirectionChanged;
        public event Action<UIElement, LayoutType> onLayoutTypeChanged;
        public event Action<UIElement, UIMeasurement, UIMeasurement> onMinWidthChanged;
        public event Action<UIElement, UIMeasurement, UIMeasurement> onMaxWidthChanged;
        public event Action<UIElement, UIMeasurement, UIMeasurement> onPreferredWidthChanged;
        public event Action<UIElement, UIMeasurement, UIMeasurement> onMinHeightChanged;
        public event Action<UIElement, UIMeasurement, UIMeasurement> onMaxHeightChanged;
        public event Action<UIElement, UIMeasurement, UIMeasurement> onPreferredHeightChanged;
        public event Action<UIElement, int, int> onGrowthFactorChanged;
        public event Action<UIElement, int, int> onShrinkFactorChanged;
        public event Action<UIElement, int> onFlexItemOrderOverrideChanged;
        public event Action<UIElement, CrossAxisAlignment, CrossAxisAlignment> onFlexItemSelfAlignmentChanged;

        private readonly SkipTree<UIElement> fontTree;

        public StyleSystem() {
            this.fontTree = new SkipTree<UIElement>();
        }

        public void OnReset() { }

        public void OnElementCreatedFromTemplate(MetaData elementData) {
            UIElement element = elementData.element;

            if ((element.flags & UIElementFlags.TextElement) != 0) {
                ((UITextElement) element).onTextChanged += HandleTextChanged;
            }

            if ((element.flags & FlagCheck) != 0) {
                UITemplateContext context = elementData.context;
                List<UIBaseStyleGroup> baseStyles = elementData.baseStyles;
                List<StyleBinding> constantStyleBindings = elementData.constantStyleBindings;

                element.style = new UIStyleSet(element, this);
                for (int i = 0; i < constantStyleBindings.Count; i++) {
                    constantStyleBindings[i].Apply(element.style, context);
                }

                for (int i = 0; i < baseStyles.Count; i++) {
                    element.style.AddBaseStyleGroup(baseStyles[i]);
                }

                element.style.Initialize();
            }

            // todo -- probably not the right move
            fontTree.AddItem(element);

            for (int i = 0; i < elementData.children.Count; i++) {
                OnElementCreatedFromTemplate(elementData.children[i]);
            }
        }

        public void OnUpdate() { }

        public void OnDestroy() { }

        public void OnReady() { }

        public void OnInitialize() { }

        public void OnElementCreated(UIElement element) {
            if (element.style == null) {
                element.style = new UIStyleSet(element, this);
            }
        }

        public void OnElementMoved(UIElement element, int newIndex, int oldIndex) { }

        public void OnElementEnabled(UIElement element) { }

        public void OnElementDisabled(UIElement element) { }

        public void OnElementDestroyed(UIElement element) { }

        public void OnElementShown(UIElement element) { }

        public void OnElementHidden(UIElement element) { }

        public void OnElementParentChanged(UIElement element, UIElement oldParent, UIElement newParent) {
            if (element.style == null) {
                element.style = new UIStyleSet(element, this);
            }
        }

        public void SetDimensions(UIElement element, Dimensions rect) {
            onRectChanged?.Invoke(element, rect);
        }

        public void SetMargin(UIElement element, ContentBoxRect margin) {
            onMarginChanged?.Invoke(element, margin);
        }

        public void SetPadding(UIElement element, ContentBoxRect padding) {
            onPaddingChanged?.Invoke(element, padding);
        }

        public void SetBorder(UIElement element, ContentBoxRect border) {
            onBorderChanged?.Invoke(element, border);
        }

        public void SetBorderRadius(UIElement element, BorderRadius radius) {
            onBorderRadiusChanged?.Invoke(element, radius);
        }

        public void SetLayout(UIElement element, LayoutParameters layoutParameters) {
            onLayoutChanged?.Invoke(element, layoutParameters);
        }

        public void SetConstraints(UIElement element, LayoutConstraints constraints) {
            onConstraintChanged?.Invoke(element, constraints);
        }

        public void SetTextStyle(UIElement element, TextStyle textStyle) {
            onFontPropertyChanged?.Invoke(element, textStyle);
        }

        public void SetPaint(UIElement element, Paint paint) {
            onPaintChanged?.Invoke(element, paint);
        }

        public void SetAvailableStates(UIElement element, StyleState availableStates) {
            onAvailableStatesChanged?.Invoke(element, availableStates);
        }

        public void SetTransform(UIElement element, UITransform transform) {
            onTransformChanged?.Invoke(element, transform);
        }

        public void SetMainAxisAlignment(UIElement element, MainAxisAlignment alignment, MainAxisAlignment oldAlignment) {
            onMainAxisAlignmentChanged?.Invoke(element, alignment, oldAlignment);
        }

        public void SetCrossAxisAlignment(UIElement element, CrossAxisAlignment alignment, CrossAxisAlignment oldAlignment) {
            onCrossAxisAlignmentChanged?.Invoke(element, alignment, oldAlignment);
        }

        public void SetLayoutWrap(UIElement element, LayoutWrap layoutWrap) {
        }

        public void SetLayoutWrap(UIElement element, LayoutWrap layoutWrap, LayoutWrap oldWrap) {
            onLayoutWrapChanged?.Invoke(element, layoutWrap, oldWrap);
        }

        public void SetLayoutDirection(UIElement element, LayoutDirection direction) {
            onLayoutDirectionChanged?.Invoke(element, direction);
        }

        public void SetLayoutType(UIElement element, LayoutType layoutType) {
            onLayoutTypeChanged?.Invoke(element, layoutType);
        }

        public void SetMinWidth(UIElement element, UIMeasurement newMinWidth, UIMeasurement oldMinWidth) {
            onMinWidthChanged?.Invoke(element, newMinWidth, oldMinWidth);
        }

        public void SetMaxWidth(UIElement element, UIMeasurement newMaxWidth, UIMeasurement oldMaxWidth) {
            onMaxWidthChanged?.Invoke(element, newMaxWidth, oldMaxWidth);
        }

        public void SetPreferredWidth(UIElement element, UIMeasurement newPrefWidth, UIMeasurement oldPrefWidth) {
            onPreferredWidthChanged?.Invoke(element, newPrefWidth, oldPrefWidth);
        }

        public void SetMinHeight(UIElement element, UIMeasurement newMinHeight, UIMeasurement oldMinHeight) {
            onMinHeightChanged?.Invoke(element, newMinHeight, oldMinHeight);
        }

        public void SetMaxHeight(UIElement element, UIMeasurement newMaxHeight, UIMeasurement oldMaxHeight) {
            onMaxHeightChanged?.Invoke(element, newMaxHeight, oldMaxHeight);
        }

        public void SetPreferredHeight(UIElement element, UIMeasurement newPrefHeight, UIMeasurement oldPrefHeight) {
            onPreferredHeightChanged?.Invoke(element, newPrefHeight, oldPrefHeight);
        }

        public void SetFlexItemShrinkFactor(UIElement element, int factor, int oldFactor) {
        }

        public void SetFlexItemGrowthFactor(UIElement element, int factor, int oldFactor) {
        }

        public void SetFlexItemOrderOverride(UIElement element, int order, int oldOrder) {
        }

        public void SetFlexItemSelfAlignment(UIElement element, CrossAxisAlignment alignment, CrossAxisAlignment oldAlignment) {
        }

        public void SetFontAsset(UIElement styleSetElement, AssetPointer<TMP_FontAsset> fontAsset) {
        }

        public void SetFontStyle(UIElement styleSetElement, TextUtil.FontStyle fontStyle) {
        }

        public void SetTextAnchor(UIElement styleSetElement, TextUtil.TextAnchor textAnchor) {
        }

        public void SetFontSize(UIElement element, int fontSize) {
          
        }

        public void SetFontColor(UIElement element, Color fontColor) {
        }

        public void SetFlexItemProperties(UIElement styleSetElement) {
        }

        public void SetOverflowX(UIElement styleSetElement, Overflow overflowX) {
        }

        public void SetOverflowY(UIElement styleSetElement, Overflow overflowY) {
        }

        private void HandleTextChanged(UITextElement element, string text) {
            element.style.textContent = text;
            onTextContentChanged?.Invoke(element, text);
        }

    }

}

