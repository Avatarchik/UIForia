using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rendering {
    
    [Flags]
    public enum UIFlags {
        TransformChanged = 1 << 0,
        InLayoutFlow = 1 << 1,
    }

    // transform.isInLayoutFlow = false;
    // transform.layoutParameters.grow = 1;
    // transform.layoutParameters.shrink = 1;
    // transform.layoutType = LayoutType.None;
    
    // if parent is set to fill content
    // and child set to fill parent
    // child = 0 && log error
    
    // style.SizeToContent(percent);
    // style.FillParentWidth();
    // style.FillParentHeight();
    // style.FillParent();
    // style.FitContentWidth();
    // style.FitContentHeight();
    // style.FitContent();
    // style.FillView();
    
    public class UITransform {
        
        internal UIFlags flags;
        public readonly UITransform parent;
        public readonly List<UITransform> children;
        public readonly UIView view;
        public Vector2 position;
        public Vector2 scale;
        public Vector2 pivot;
        public float rotation;
        public UIElement element;

        // width and height from transform are only in relation to actual pixel size
        // they are readonly unless not in flow
        
        internal UITransform(UITransform parent, UIView view) {
            this.parent = parent;
            this.view = view;
            flags = 0;
            children = new List<UITransform>();
        }

        public bool IsInLayoutFlow {
            get { return (flags & UIFlags.InLayoutFlow) != 0; }
            set { flags |= value ? UIFlags.InLayoutFlow : UIFlags.InLayoutFlow; }
        }

        public float GetPixelWidth() {
            // element.style.GetContentBox();
            return 0;
        }
        
        public Rect GetLayoutRect() {
            return new Rect() {
                x = position.x,
                y = position.y,
                width = element.style.contentWidth,
                height = element.style.contentHeight
            };
        }
    }
    
}