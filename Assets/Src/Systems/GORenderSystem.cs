﻿using System.Collections.Generic;
using Rendering;
using Src.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Src.Systems {

    public class GORenderSystem : IRenderSystem, IGraphicUpdateManager {

        private readonly RectTransform rectTransform;

        private readonly ILayoutSystem layoutSystem;
        private readonly IStyleSystem styleSystem;
        private readonly UIView elementRegistry;

        private readonly SkipTree<RenderData> renderSkipTree;
        private readonly Dictionary<int, RectTransform> m_TransformMap;

        private bool ready;

        private readonly List<IGraphicElement> m_DirtyGraphicList;
        private readonly Dictionary<int, CanvasRenderer> m_CanvasRendererMap;
        private readonly Dictionary<int, RectMask2D> m_MaskMap;
        private readonly List<RenderData> m_MaskElements;

        public GORenderSystem(ILayoutSystem layoutSystem, IStyleSystem styleSystem, UIView elementRegistry, RectTransform rectTransform) {
            this.layoutSystem = layoutSystem;
            this.rectTransform = rectTransform;
            this.elementRegistry = elementRegistry;
            this.renderSkipTree = new SkipTree<RenderData>();

            this.m_DirtyGraphicList = new List<IGraphicElement>();
            this.m_CanvasRendererMap = new Dictionary<int, CanvasRenderer>();
            this.m_TransformMap = new Dictionary<int, RectTransform>();
            this.m_MaskMap = new Dictionary<int, RectMask2D>();
            this.m_MaskElements = new List<RenderData>();

            this.renderSkipTree.onItemParentChanged += (item, newParent, oldParent) => {
                item.unityTransform.SetParent(newParent == null ? rectTransform : newParent.unityTransform);
                item.unityTransform.anchorMin = new Vector2(0, 1);
                item.unityTransform.anchorMax = new Vector2(0, 1);
                item.unityTransform.pivot = new Vector2(0, 1);
                item.unityTransform.anchoredPosition = Vector3.zero;
            };

            this.styleSystem = styleSystem;
        }

        public void OnReady() {
            ready = true;
        }

        public void OnInitialize() {
            this.styleSystem.onFontPropertyChanged += HandleFontPropertyChanged;
            this.styleSystem.onBorderChanged += HandleStyleChange;
            this.styleSystem.onPaddingChanged += HandleStyleChange;
            this.styleSystem.onBorderChanged += HandleStyleChange;
            this.styleSystem.onPaintChanged += HandlePaintChange;
            this.styleSystem.onBorderRadiusChanged += HandleBorderRadiusChange;
        }

        public void OnElementCreated(MetaData creationData) {
            OnElementStyleChanged(creationData.element);

            for (int i = 0; i < creationData.children.Count; i++) {
                OnElementCreated(creationData.children[i]);
            }

            UITextContainerElement container = creationData.element as UITextContainerElement;
            UIGraphicElement directDraw = creationData.element as UIGraphicElement;

            if (directDraw != null) {
                directDraw.updateManager = this;

                RectTransform transform = m_TransformMap[directDraw.id];
                CanvasRenderer renderer = transform.gameObject.AddComponent<CanvasRenderer>();
                renderer.SetMaterial(directDraw.GetMaterial(), Texture2D.whiteTexture);
                renderer.SetMesh(directDraw.GetMesh());
                m_CanvasRendererMap[directDraw.id] = renderer;
            }

            if (container != null) {
                RectTransform containerTransform = m_TransformMap[container.id];
                Transform child = containerTransform.GetChild(0);
                // todo -- need the font ref to update when font changes
                container.textInfo = child.GetComponent<TextMeshProUGUI>().textInfo;
                container.fontAsset = child.GetComponent<TextMeshProUGUI>().font;
                container.textInfo.textComponent.text = container.textInfo.textComponent.text ?? string.Empty;
            }
        }

        /*
         * ClipTree
         *     traverse
         *         foreach item
         *             item.setclipRect(parent.rect)
         *         
         *
         * add mask ->
         *     foreach other mask
         *         if child should be in new mask
         *         old mask.remove
         *         new mask.add
         */
        public void OnUpdate() {
            int count = layoutSystem.RectCount;
            LayoutResult[] layoutResults = layoutSystem.LayoutResults;

            for (int i = 0; i < count; i++) {
                RectTransform transform;

                if (!m_TransformMap.TryGetValue(layoutResults[i].element.id, out transform)) {
                    continue;
                }

                RenderData renderData = renderSkipTree.GetItem(layoutResults[i].element.id);
                ContentBoxRect margin = renderData.element.style.margin;
                UIElement element = renderData.element;
                
                Vector2 position = layoutResults[i].localRect.position;
                position.x = Mathf.CeilToInt(position.x + margin.left);
                position.y = -Mathf.CeilToInt(position.y + margin.top);
                
                Vector2 size = layoutResults[i].localRect.size;
                size.x = Mathf.CeilToInt(size.x - (margin.left + margin.right));
                size.y = Mathf.CeilToInt(size.y - (margin.top + margin.bottom));
                
                if (element.style.HandlesOverflow && element.measurements.IsOverflowing) {
                    RectMask2D mask;
                    if (!m_MaskMap.TryGetValue(element.id, out mask)) {
                        RectTransform contentTransform = transform;
                        GameObject maskHolder = new GameObject("Mask");
                        mask = maskHolder.AddComponent<RectMask2D>();
                        RectTransform parent = transform.parent as RectTransform;
                        m_MaskMap[element.id] = mask;
                        transform.SetParent(mask.transform);
                        mask.transform.SetParent(parent);
                        transform = mask.transform as RectTransform;
                        transform.anchorMin = new Vector2(0, 1);
                        transform.anchorMax = new Vector2(0, 1);
                        transform.pivot = new Vector2(0, 1);
                        contentTransform.SetParent(transform);
                        contentTransform.anchoredPosition = new Vector2();
                    }
                    else {
                        RectTransform contentTransform = transform;
                        contentTransform.anchoredPosition = new Vector2();
                        transform = mask.transform as RectTransform;
                    }

                    // if has mask
                    // get transform parent
                    // set size to size - scroll bar

//                    if (element.measurements.IsOverflowingX && renderData.horizontalScrollbar == null) {
//                        ScrollBar horizontal = new ScrollBar();
//                        horizontal.SetParent(element);
////                            horizontal.SetStyle(element.style.scrollStyle);
//                        horizontal.flags |= UIElementFlags.ImplicitElement;
//                        horizontal.onScrollUpdate += (f) => { };
//                        renderData.horizontalScrollbar = horizontal;
//                        if (renderData.verticalScrollbar == null) {
//                            m_MaskElements.Add(renderData);
//                        }
//                    }
                    if (element.measurements.IsOverflowingY && renderData.verticalScrollbar == null) {
                        ScrollBar vertical = new ScrollBar();
                        vertical.flags |= UIElementFlags.ImplicitElement;
                        vertical.SetParent(element);
//                            vertical.SetStyle(element.style.scrollStyle);
                        vertical.style.width = 5f;
                        vertical.style.height = size.y;
                        vertical.style.backgroundColor = Color.green;
                        var data = renderSkipTree.GetItem(vertical);
                        data.unityTransform.anchoredPosition = position;
                        data.unityTransform.sizeDelta = new Vector2(5f, size.y);
                        ApplyStyles(data);
                        vertical.onScrollUpdate += (f) => { };
                        renderData.verticalScrollbar = vertical;
                        if (renderData.horizontalScrollbar == null) {
                            m_MaskElements.Add(renderData);
                        }
                    }

                    //mask.SetClipRect(new Rect(0, 0, element.measurements.width - 5f, element.measurements.height - 5f));
                }

                if (transform.anchoredPosition != position) {
                    transform.anchoredPosition = position;
                }


                if (transform.sizeDelta != size) {
                    transform.sizeDelta = size;
                }


                // Text elements give me lots of trouble. Here is what needs to happen:
                // Layout needs to measure the preferred size of the string. It does this on it's own
                // once that is computed the layout system uses it as normal. When it comes to rendering
                // we only want to set the text if we have to and then force the mesh to update because
                // this needs to happen BEFORE we update dirty graphics because things like highlighting
                // and caret placement need to have up to date data on rendered character layout which 
                // may differ from what the layout system says. 

                UITextElement textElement = renderData.element as UITextElement;
                if (textElement != null) {
                    TextMeshProUGUI tmp = renderData.renderComponent as TextMeshProUGUI;
                    if (tmp != null) {
                        TextMeshProUGUI textMesh = renderData.renderComponent as TextMeshProUGUI;
                        if (textMesh != null && textMesh.text != textElement.GetText()) {
                            textMesh.text = textElement.GetText();
                            textMesh.ForceMeshUpdate();
                        }
                    }
                }
            }

            for (int i = 0; i < m_MaskElements.Count; i++) {
                RenderData maskedData = m_MaskElements[i];
                if (maskedData.horizontalScrollbar != null) {
                    //  maskedData.horizontalScrollbar.SetStyleAndSize();
                }

                if (maskedData.verticalScrollbar != null) {
                    //  maskedData.verticalScrollbar.SetStyleAndSize();
                }
            }

            for (int i = 0; i < m_DirtyGraphicList.Count; i++) {
                IGraphicElement graphic = m_DirtyGraphicList[i];
                CanvasRenderer canvasRenderer = m_CanvasRendererMap[graphic.Id];

                if (graphic.IsGeometryDirty) {
                    graphic.RebuildGeometry();
                    canvasRenderer.SetMesh(graphic.GetMesh());
                }

                if (graphic.IsMaterialDirty) {
                    graphic.RebuildMaterial();
                    canvasRenderer.SetMaterial(graphic.GetMaterial(), Texture2D.whiteTexture);
                }
            }

            m_DirtyGraphicList.Clear();
        }


        public void OnReset() {
            ready = false;
            renderSkipTree.TraversePreOrder((data => { data.unityTransform = null; }));

            foreach (KeyValuePair<int, RectTransform> kvp in m_TransformMap) {
                Object.Destroy(kvp.Value.gameObject);
            }

            this.styleSystem.onBorderChanged -= HandleStyleChange;
            this.styleSystem.onPaddingChanged -= HandleStyleChange;
            this.styleSystem.onBorderChanged -= HandleStyleChange;
            this.styleSystem.onPaintChanged -= HandlePaintChange;
            this.styleSystem.onBorderRadiusChanged -= HandleBorderRadiusChange;

            renderSkipTree.Clear();
            m_TransformMap.Clear();
            m_CanvasRendererMap.Clear();
            m_DirtyGraphicList.Clear();
        }

        public void OnDestroy() {
            OnReset();
        }

        private void OnElementStyleChanged(UIElement element) {
            RenderData data = renderSkipTree.GetItem(element);

            RenderPrimitiveType primitiveType = DeterminePrimitiveType(element);

            if (data == null) {
                GameObject obj = new GameObject(element.ToString());

                RectTransform unityTransform = obj.AddComponent<RectTransform>();
                unityTransform.anchorMin = new Vector2(0, 1);
                unityTransform.anchorMax = new Vector2(0, 1);
                unityTransform.pivot = new Vector2(0, 1);
                m_TransformMap[element.id] = unityTransform;

                obj.SetActive(element.isEnabled);
                data = new RenderData(element, primitiveType, unityTransform);
                CreateComponents(data);
                renderSkipTree.AddItem(data);

                return;
            }

            if (primitiveType == data.primitiveType) {
                ApplyStyles(data);
                return;
            }

            data.primitiveType = primitiveType;

            if (data.renderComponent != null) {
                Object.Destroy(data.renderComponent);
            }

            if (primitiveType == RenderPrimitiveType.None) {
                return;
            }

            data.primitiveType = primitiveType;
            CreateComponents(data);
        }

        public void OnElementDestroyed(UIElement element) {
            RenderData data = renderSkipTree.GetItem(element);

            if (data == null) return;

            renderSkipTree.RemoveItem(data);
            m_TransformMap.Remove(element.id);

            if (data.renderComponent != null) {
                Object.Destroy(data.renderComponent);
            }

            Object.Destroy(data.unityTransform);
            data.element = null;
        }

        public void OnElementShown(UIElement element) { }

        public void OnElementHidden(UIElement element) { }

        public void OnElementParentChanged(UIElement element, UIElement oldParent, UIElement newParent) {
            RenderData data = renderSkipTree.GetItem(element);
            if (data != null) {
                renderSkipTree.UpdateItemParent(element);
            }
            else {
                OnElementStyleChanged(element);
            }
        }

        public void OnRender() {
            OnUpdate();
        }

        public void OnElementEnabled(UIElement element) {
            if (m_TransformMap.ContainsKey(element.id)) {
                m_TransformMap[element.id].gameObject.SetActive(true);

                CanvasRenderer canvasRenderer;
                if (m_CanvasRendererMap.TryGetValue(element.id, out canvasRenderer)) {
                    IGraphicElement graphic = (IGraphicElement) element;
                    canvasRenderer.SetMesh(graphic.GetMesh());
                    canvasRenderer.SetMaterial(graphic.GetMaterial(), Texture2D.whiteTexture);
                }
            }

            renderSkipTree.ConditionalTraversePreOrder(element, this, (item, self) => {
                if (item.element.isDisabled) return false;
                item.unityTransform.gameObject.SetActive(true);

                CanvasRenderer canvasRenderer;
                if (self.m_CanvasRendererMap.TryGetValue(item.element.id, out canvasRenderer)) {
                    IGraphicElement graphic = (IGraphicElement) item.element;
                    canvasRenderer.SetMesh(graphic.GetMesh());
                    canvasRenderer.SetMaterial(graphic.GetMaterial(), Texture2D.whiteTexture);
                }

                return true;
            });
        }

        public void OnElementDisabled(UIElement element) {
            if (m_TransformMap.ContainsKey(element.id)) {
                m_TransformMap[element.id].gameObject.SetActive(false);
            }

            if (m_CanvasRendererMap.ContainsKey(element.id)) {
                m_CanvasRendererMap[element.id].Clear();
            }

            renderSkipTree.TraversePreOrder(element, this, (self, item) => {
                item.unityTransform.gameObject.SetActive(false);
                CanvasRenderer canvasRenderer;

                if (self.m_CanvasRendererMap.TryGetValue(item.element.id, out canvasRenderer)) {
                    canvasRenderer.Clear();
                }
            });
        }

        public void MarkGeometryDirty(IGraphicElement element) {
            if (!m_DirtyGraphicList.Contains(element)) {
                m_DirtyGraphicList.Add(element);
            }
        }

        public void MarkMaterialDirty(IGraphicElement element) {
            if (!m_DirtyGraphicList.Contains(element)) {
                m_DirtyGraphicList.Add(element);
            }
        }

        private void CreateComponents(RenderData data) {
            GameObject gameObject = data.unityTransform.gameObject;
            switch (data.primitiveType) {
                case RenderPrimitiveType.RawImage:
                    data.renderComponent = gameObject.AddComponent<RawImage>();
                    break;

                case RenderPrimitiveType.ProceduralImage:
                    data.renderComponent = gameObject.AddComponent<BorderedImage>();
                    break;

                case RenderPrimitiveType.Mask:
                case RenderPrimitiveType.Mask2D:
                    break;

                case RenderPrimitiveType.Text:
                    data.renderComponent = gameObject.AddComponent<TextMeshProUGUI>();
                    break;
            }

            ApplyStyles(data);
        }

        private void ApplyStyles(RenderData data) {
            UIElement element = data.element;
            UIStyleSet style = element.style;

            switch (data.primitiveType) {
                case RenderPrimitiveType.RawImage:
                    RawImage rawImage = (RawImage) data.renderComponent;
                    rawImage.texture = style.backgroundImage;
                    rawImage.color = style.backgroundColor;
                    rawImage.uvRect = new Rect(0, 0, 1, 1);
                    break;

                case RenderPrimitiveType.ProceduralImage:
//                    Shape shape = data.shape;
//                    shape.settings.outlineColor = style.borderColor;
//                    shape.settings.outlineSize = style.borderLeft;
//                    shape.settings.fillColor = style.backgroundColor;
//                    shape.settings.roundnessPerCorner = false;
//                    shape.settings.roundness = style.borderRadiusBottomLeft;
//                    shape.settings.fillType = FillType.SolidColor;
//                    shape.settings.fill
                    BorderedImage procImage = (BorderedImage) data.renderComponent;
                    procImage.color = style.backgroundColor;
                    procImage.borderColor = style.borderColor;
                    procImage.border = style.border;
                    break;

                case RenderPrimitiveType.Text:
                    TextMeshProUGUI textMesh = (TextMeshProUGUI) data.renderComponent;
                    textMesh.text = style.textContent;
                    textMesh.fontSize = style.fontSize;
                    textMesh.color = style.textColor;
                    break;

                case RenderPrimitiveType.Mask:
                case RenderPrimitiveType.Mask2D:
                    break;
            }
        }

        private RenderPrimitiveType DeterminePrimitiveType(UIElement element) {
            if ((element.flags & UIElementFlags.RequiresRendering) == 0) {
                return RenderPrimitiveType.None;
            }

            if ((element.flags & UIElementFlags.TextElement) != 0) {
                return RenderPrimitiveType.Text;
            }

            UIStyleSet styleSet = element.style;
            if (styleSet.backgroundImage == null
                && styleSet.borderColor == ColorUtil.UnsetValue
                && styleSet.backgroundColor == ColorUtil.UnsetValue) {
                return RenderPrimitiveType.None;
            }

            ContentBoxRect border = styleSet.border;
            if (border.left > 0 || border.right > 0 || border.top > 0 || border.bottom > 0) {
                return RenderPrimitiveType.ProceduralImage;
            }

            return RenderPrimitiveType.RawImage;
        }


        private void HandleBorderRadiusChange(UIElement element, BorderRadius radius) {
            if (!ready) return;
            OnElementStyleChanged(element);
        }

        private void HandlePaintChange(UIElement element, Paint paint) {
            if (!ready) return;
            OnElementStyleChanged(element);
        }

        private void HandleStyleChange(UIElement element, ContentBoxRect rect) {
            if (!ready) return;
            OnElementStyleChanged(element);
        }

        private void HandleFontPropertyChanged(UIElement element, TextStyle style) {
            if (!ready) return;
            RenderData data = renderSkipTree.GetItem(element);
            if (data == null) return;
            TextMeshProUGUI textMesh = data.renderComponent as TextMeshProUGUI;
            if (textMesh != null) {
                ApplyStyles(data);
            }
        }

    }

}