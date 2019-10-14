using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Mono.Linq.Expressions;
using Src.Systems;
using UIForia.Animation;
using UIForia.AttributeProcessors;
using UIForia.Bindings;
using UIForia.Compilers;
using UIForia.Compilers.Style;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Extensions;
using UIForia.Layout;
using UIForia.Parsing;
using UIForia.Parsing.Expressions;
using UIForia.Rendering;
using UIForia.Routing;
using UIForia.Systems;
using UIForia.Systems.Input;
using UIForia.Util;
using UnityEngine;

namespace UIForia {

    public abstract class Application {

#if UNITY_EDITOR
        public static List<Application> Applications = new List<Application>();
#endif

        public readonly string id;
        private static int ElementIdGenerator;
        public static int NextElementId => ElementIdGenerator++;
        private string templateRootPath;

      //  protected readonly BindingSystem m_BindingSystem;
        protected readonly IStyleSystem m_StyleSystem;
        protected ILayoutSystem m_LayoutSystem;
        protected IRenderSystem m_RenderSystem;
        protected IInputSystem m_InputSystem;
        protected RoutingSystem m_RoutingSystem;
        protected AnimationSystem m_AnimationSystem;
        protected LinqBindingSystem linqBindingSystem;

        protected ResourceManager resourceManager;

        public readonly StyleSheetImporter styleImporter;
        private readonly IntMap<UIElement> elementMap;
        protected readonly List<ISystem> m_Systems;

        public event Action<UIElement> onElementRegistered;
        public event Action<UIElement> onElementCreated;
        public event Action<UIElement> onElementDestroyed;
        public event Action<UIElement> onElementEnabled;
        public event Action<UIElement> onElementDisabled;

        public event Action onWillRefresh;
        public event Action onRefresh;
        public event Action onUpdate;
        public event Action onReady;
        public event Action onDestroy;
        public event Action onNextRefresh;
        public event Action<UIView> onViewAdded;
        public event Action<UIView[]> onViewsSorted;
        public event Action<UIView> onViewRemoved;

        internal TemplateData templateData;
        internal TemplateCompiler templateCompiler;

        protected internal readonly List<UIView> m_Views;
        
        internal static readonly Dictionary<string, Type> s_CustomPainters;
        internal static readonly Dictionary<string, Scrollbar> s_Scrollbars;

        public readonly TemplateParser templateParser;

        private static readonly LightList<Application> s_ApplicationList;

        private readonly UITaskSystem m_BeforeUpdateTaskSystem;
        private readonly UITaskSystem m_AfterUpdateTaskSystem;

        public static readonly UIForiaSettings Settings;
        private ElementPool elementPool;
        internal LightList<SlotUsageTemplate> slotUsageTemplates = new LightList<SlotUsageTemplate>(128);

        static Application() {
            ArrayPool<UIElement>.SetMaxPoolSize(64);
            s_ApplicationList = new LightList<Application>();
            s_CustomPainters = new Dictionary<string, Type>();
            s_Scrollbars = new Dictionary<string, Scrollbar>();
            Settings = Resources.Load<UIForiaSettings>("UIForiaSettings");
            if (Settings == null) {
                throw new Exception("UIForiaSettings are missing. Use the UIForia/Create UIForia Settings to create it");
            }
        }

        // todo -- replace the static version with this one
        public UIForiaSettings settings => Settings;

        protected Application(string id, string templateRootPath = null, ResourceManager resourceManager = null) {
            this.id = id;
            this.templateRootPath = templateRootPath;

            // todo -- exceptions in constructors aren't good practice
            if (s_ApplicationList.Find(id, (app, _id) => app.id == _id) != null) {
                throw new Exception($"Applications must have a unique id. Id {id} was already taken.");
            }

            s_ApplicationList.Add(this);

            this.templateData = new TemplateData(); // todo -- load this from elsewhere in the pre-generated case
            this.templateCompiler = new TemplateCompiler(this);

            this.elementPool = new ElementPool();

            this.resourceManager = resourceManager ?? new ResourceManager();

            this.m_Systems = new List<ISystem>();
            this.m_Views = new List<UIView>();

            m_StyleSystem = new StyleSystem();
            m_LayoutSystem = new FastLayoutSystem(this, m_StyleSystem);
            m_InputSystem = new GameInputSystem(m_LayoutSystem);
            m_RenderSystem = new VertigoRenderSystem(Camera.current, this);
            //       m_RenderSystem = new SVGXRenderSystem(this, null, m_LayoutSystem);
            m_RoutingSystem = new RoutingSystem();
            m_AnimationSystem = new AnimationSystem();
            linqBindingSystem = new LinqBindingSystem();

            styleImporter = new StyleSheetImporter(this);
            templateParser = new TemplateParser(this);

            elementMap = new IntMap<UIElement>();

            m_Systems.Add(m_StyleSystem);
            m_Systems.Add(linqBindingSystem);
            m_Systems.Add(m_RoutingSystem);
            m_Systems.Add(m_InputSystem);
            m_Systems.Add(m_AnimationSystem);
            m_Systems.Add(m_LayoutSystem);
            m_Systems.Add(m_RenderSystem);

            m_BeforeUpdateTaskSystem = new UITaskSystem();
            m_AfterUpdateTaskSystem = new UITaskSystem();

            if (settings.usePreCompiledTemplates) {
                // todo -- load templates
            }

#if UNITY_EDITOR
            Applications.Add(this);
#endif
        }

        internal static void ProcessClassAttributes(Type type, Attribute[] attrs) {
            for (var i = 0; i < attrs.Length; i++) {
                Attribute attr = attrs[i];
                if (attr is CustomPainterAttribute paintAttr) {
                    if (type.GetConstructor(Type.EmptyTypes) == null || !typeof(RenderBox).IsAssignableFrom(type)) {
                        throw new Exception($"Classes marked with [{nameof(CustomPainterAttribute)}] must provide a parameterless constructor" +
                                            $" and the class must extend {nameof(RenderBox)}. Ensure that {type.FullName} conforms to these rules");
                    }

                    if (s_CustomPainters.ContainsKey(paintAttr.name)) {
                        throw new Exception($"Failed to register a custom painter with the name {paintAttr.name} from type {type.FullName} because it was already registered.");
                    }

                    s_CustomPainters.Add(paintAttr.name, type);
                }
                else if (attr is CustomScrollbarAttribute scrollbarAttr) {
                    if (type.GetConstructor(Type.EmptyTypes) == null || !(typeof(Scrollbar)).IsAssignableFrom(type)) {
                        throw new Exception($"Classes marked with [{nameof(CustomScrollbarAttribute)}] must provide a parameterless constructor" +
                                            $" and the class must extend {nameof(Scrollbar)}. Ensure that {type.FullName} conforms to these rules");
                    }

                    if (s_Scrollbars.ContainsKey(scrollbarAttr.name)) {
                        throw new Exception($"Failed to register a custom scrollbar with the name {scrollbarAttr.name} from type {type.FullName} because it was already registered.");
                    }

                    s_Scrollbars.Add(scrollbarAttr.name, (Scrollbar) Activator.CreateInstance(type));
                }
            }
        }

        public string TemplateRootPath {
            get {
                if (templateRootPath == null) {
                    return string.Empty; // UnityEngine.Application.dataPath;
                }

                return templateRootPath;
            }
            set { templateRootPath = value; }
        }

        public IStyleSystem StyleSystem => m_StyleSystem;
        public IRenderSystem RenderSystem => m_RenderSystem;
        public ILayoutSystem LayoutSystem => m_LayoutSystem;
        public IInputSystem InputSystem => m_InputSystem;
        public RoutingSystem RoutingSystem => m_RoutingSystem;

        public Camera Camera { get; private set; }

        public LinqBindingSystem LinqBindingSystem => linqBindingSystem;
        public ResourceManager ResourceManager => resourceManager;

        public float Width => Screen.width;
        public float Height => Screen.height;

       

        public void SetCamera(Camera camera) {
            Camera = camera;
            RenderSystem.SetCamera(camera);
        }

        private int nextViewId = 0;

        public UIView CreateView(string name, Rect rect, Type type, string template = null) {
            UIView view = GetView(name);

            if (view == null) {
                view = new UIView(nextViewId++, name, this, rect, m_Views.Count, type, template);
                m_Views.Add(view);

                for (int i = 0; i < m_Systems.Count; i++) {
                    m_Systems[i].OnViewAdded(view);
                }

                view.Initialize();

                onViewAdded?.Invoke(view);
            }
            else {
                if (view.RootElement.GetChild(0).GetType() != type) {
                    throw new Exception($"A view named {name} with another root type ({view.RootElement.GetChild(0).GetType()}) already exists.");
                }

                view.Viewport = rect;
            }

            return view;
        }

        public UIView CreateView(string name, Rect rect) {
            UIView view = new UIView(nextViewId++, name, this, rect, m_Views.Count);

            m_Views.Add(view);

            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnViewAdded(view);
            }

            view.Initialize();

            onViewAdded?.Invoke(view);
            return view;
        }

        public UIView RemoveView(UIView view) {
            if (!m_Views.Remove(view)) return null;

            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnViewRemoved(view);
            }

            onViewRemoved?.Invoke(view);
            DestroyElement(view.rootElement);
            return view;
        }

        public UIElement CreateElement(Type type) {
            if (type == null) {
                return null;
            }

            return templateParser.GetParsedTemplate(type)?.Create();
        }

        public T CreateElement<T>() where T : UIElement {
            return templateParser.GetParsedTemplate(typeof(T))?.Create() as T;
        }

        public void Refresh() {
            onWillRefresh?.Invoke();

            foreach (ISystem system in m_Systems) {
                system.OnReset();
            }

            onReady = null;
            onUpdate = null;

            elementMap.Clear();
            templateParser.Reset();
            styleImporter.Reset();
            resourceManager.Reset();

            m_AfterUpdateTaskSystem.OnReset();
            m_BeforeUpdateTaskSystem.OnReset();

            // copy the list here because there might be view-sorting going on during view.initialize() 
            LightList<UIView> views = LightList<UIView>.Get();
            views.AddRange(m_Views);

            // todo -- store root view, rehydrate. kill the rest
            for (int i = 0; i < views.Count; i++) {
                for (int j = 0; j < m_Systems.Count; j++) {
                    m_Systems[j].OnViewAdded(views[i]);
                }

                views[i].Initialize();
            }

            LightList<UIView>.Release(ref views);

            onRefresh?.Invoke();
            onNextRefresh?.Invoke();
            onNextRefresh = null;
            onReady?.Invoke();
        }

        public void Destroy() {
#if UNITY_EDITOR
            Applications.Remove(this);
#endif
            onDestroy?.Invoke();

            foreach (ISystem system in m_Systems) {
                system.OnDestroy();
            }

            foreach (UIView view in m_Views) {
                view.Destroy();
            }

            onRefresh = null;
            onNextRefresh = null;
            onReady = null;
            onUpdate = null;
            onDestroy = null;
            onNextRefresh = null;
            onElementCreated = null;
            onElementEnabled = null;
            onElementDisabled = null;
            onElementDestroyed = null;
            onElementRegistered = null;
        }

        public static void DestroyElement(UIElement element) {
            element.View.application.DoDestroyElement(element);
        }

        internal void DoDestroyElement(UIElement element, bool removingChildren = false) {
            // do nothing if already destroyed
            if ((element.flags & UIElementFlags.Alive) == 0) {
                return;
            }

            LightStack<UIElement> stack = LightStack<UIElement>.Get();
            LightList<UIElement> toInternalDestroy = LightList<UIElement>.Get();

            stack.Push(element);

            while (stack.Count > 0) {
                UIElement current = stack.array[--stack.size];

                if ((current.flags & UIElementFlags.Alive) == 0) {
                    continue;
                }

                current.flags &= ~(UIElementFlags.Alive);
                current.OnDestroy();
                toInternalDestroy.Add(current);

                UIElement[] children = current.children.array;
                int childCount = current.children.size;

                if (stack.size + childCount >= stack.array.Length) {
                    Array.Resize(ref stack.array, stack.size + childCount + 16);
                }

                for (int i = childCount - 1; i >= 0; i--) {
                    // inline stack push
                    stack.array[stack.size++] = children[i];
                }
            }

            if (element.parent != null && !removingChildren) {
                element.parent.children.Remove(element);
                for (int i = 0; i < element.parent.children.Count; i++) {
                    element.parent.children[i].siblingIndex = i;
                }
            }

            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnElementDestroyed(element);
            }

            if (toInternalDestroy.Count > 0) {
                UIView view = toInternalDestroy[0].View;
                for (int i = 0; i < toInternalDestroy.Count; i++) {
                    view.ElementDestroyed(toInternalDestroy[i]);
                    toInternalDestroy[i].InternalDestroy();
                    elementPool.Release(toInternalDestroy[i]);
                    elementMap.Remove(toInternalDestroy[i].id);
                }
            }

            LightList<UIElement>.Release(ref toInternalDestroy);
            LightStack<UIElement>.Release(ref stack);

            onElementDestroyed?.Invoke(element);
        }

        internal void DestroyChildren(UIElement element) {
            if (element.isDestroyed) {
                return;
            }

            if (element.children == null || element.children.Count == 0) {
                return;
            }

            for (int i = 0; i < element.children.size; i++) {
                DoDestroyElement(element.children[i], true);
            }

            element.children.QuickClear();
        }

        public void Update() {
            m_InputSystem.OnUpdate();

            linqBindingSystem.OnUpdate();

            m_StyleSystem.OnUpdate();

            m_AnimationSystem.OnUpdate();

            m_InputSystem.OnLateUpdate();

            m_RoutingSystem.OnUpdate();

            SetTraversalIndex();

            m_LayoutSystem.OnUpdate();

            m_BeforeUpdateTaskSystem.OnUpdate();

            m_RenderSystem.OnUpdate();

            m_AfterUpdateTaskSystem.OnUpdate();

            onUpdate?.Invoke();

            m_Views[0].SetSize(Screen.width, Screen.height);

            UnsetEnabledThisFrame();
        }

        // todo -- get rid of this
        private void UnsetEnabledThisFrame() {
            LightStack<UIElement> stack = LightStack<UIElement>.Get();

            for (int i = 0; i < m_Views.Count; i++) {
                stack.Push(m_Views[i].rootElement);
            }

            while (stack.size > 0) {
                UIElement currentElement = stack.array[--stack.size];

                currentElement.flags &= ~(UIElementFlags.EnabledThisFrame | UIElementFlags.DisabledThisFrame);

                if (currentElement.children == null) {
                    continue;
                }

                UIElement[] childArray = currentElement.children.array;

                int childCount = currentElement.children.size;

                stack.EnsureAdditionalCapacity(childCount);

                for (int i = 0; i < childCount; i++) {
                    stack.array[stack.size++] = childArray[i];
                }
            }

            LightStack<UIElement>.Release(ref stack);
        }

        private void SetTraversalIndex() {
            LightStack<UIElement> stack = LightStack<UIElement>.Get();

            for (int i = 0; i < m_Views.Count; i++) {
                stack.Push(m_Views[i].rootElement);
            }

            int idx = 0;

            while (stack.size > 0) {
                UIElement currentElement = stack.array[--stack.size];

                currentElement.depthTraversalIndex = idx++;

                UIElement[] childArray = currentElement.children.array;
                int childCount = currentElement.children.size;

                stack.EnsureAdditionalCapacity(childCount);

                for (int i = childCount - 1; i >= 0; i--) {
                    // todo -- direct flag check
                    if (childArray[i].isDisabled) {
                        continue;
                    }

                    stack.array[stack.size++] = childArray[i];
                }
            }

            LightStack<UIElement>.Release(ref stack);
        }

        /// <summary>
        /// Note: you don't need to remove tasks from the system. Any canceled or otherwise completed task gets removed
        /// from the system automatically.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public UITask RegisterBeforeUpdateTask(UITask task) {
            return m_BeforeUpdateTaskSystem.AddTask(task);
        }

        /// <summary>
        /// Note: you don't need to remove tasks from the system. Any canceled or otherwise completed task gets removed
        /// from the system automatically.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public UITask RegisterAfterUpdateTask(UITask task) {
            return m_AfterUpdateTaskSystem.AddTask(task);
        }

        public static void EnableElement(UIElement element) {
            element.View.application.DoEnableElement(element);
        }

        public static void DisableElement(UIElement element) {
            element.View.application.DoDisableElement(element);
        }

        private static void RunEnableBinding(UIElement element) {
            Binding[] enabledBindings = element.OriginTemplate?.triggeredBindings;

            if (enabledBindings != null) {
                for (int i = 0; i < enabledBindings.Length; i++) {
                    if (enabledBindings[i].bindingType == BindingType.OnEnable) {
                        enabledBindings[i].Execute(element, element.templateContext);
                    }
                }
            }
        }

        public void DoEnableElement(UIElement element) {
            element.flags |= UIElementFlags.Enabled;

            // if element is not enabled (ie has a disabled ancestor or is not alive), no-op 
            if ((element.flags & UIElementFlags.SelfAndAncestorEnabled) != UIElementFlags.SelfAndAncestorEnabled) {
                return;
            }

            // don't really need the stack here but it should give us a properly sized array since so many systems need light stacks of elements
            LightStack<UIElement> stack = LightStack<UIElement>.Get();

            // if element is now enabled we need to walk it's children
            // and set enabled ancestor flags until we find a self-disabled child
            stack.array[stack.size++] = element;

            // stack operations in the following code are inlined since this is a very hot path
            while (stack.size > 0) {
                // inline stack pop
                UIElement child = stack.array[--stack.size];

                child.flags |= UIElementFlags.AncestorEnabled;

                // if the element is itself disabled or destroyed, keep going
                if ((child.flags & UIElementFlags.Enabled) == 0) {
                    continue;
                }

                // todo -- profile not calling enable when it's not needed
                // if (child.flags & UIElementFlags.RequiresEnableCall) {
                child.style.UpdateInheritedStyles();
                child.OnEnable();
                // }

                // We need to run all runCommands now otherwise animations in [normal] style groups won't run after enabling.
                child.style.RunCommands();

                if ((child.flags & UIElementFlags.HasBeenEnabled) == 0) {
                    child.View.ElementCreated(child);
                }

                // register the flag set even if we get disabled via OnEnable, we just want to track that OnEnable was called at least once
                child.flags |= UIElementFlags.HasBeenEnabled;

                // only continue if calling enable didn't re-disable the element
                if ((child.flags & UIElementFlags.SelfAndAncestorEnabled) == UIElementFlags.SelfAndAncestorEnabled) {
                    child.flags |= UIElementFlags.EnabledThisFrame;
                    UIElement[] children = child.children.array;
                    int childCount = child.children.size;
                    if (stack.size + childCount >= stack.array.Length) {
                        Array.Resize(ref stack.array, stack.size + childCount + 16);
                    }

                    for (int i = childCount - 1; i >= 0; i--) {
                        // inline stack push
                        stack.array[stack.size++] = children[i];
                    }
                }
            }

            LightStack<UIElement>.Release(ref stack);

            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnElementEnabled(element);
            }

            onElementEnabled?.Invoke(element);
        }

        // todo bad things happen if we add children during disabling or enabling (probably)

        public void DoDisableElement(UIElement element) {
            // if element is already disabled or destroyed, no op
            if ((element.flags & UIElementFlags.Alive) == 0) {
                return;
            }

            if (element.isDisabled) {
                return;
            }

            element.flags &= ~(UIElementFlags.Enabled);

            // don't really need the stack here but it should give us a properly sized array since so many systems need light stacks of elements
            LightStack<UIElement> stack = LightStack<UIElement>.Get();

            // if element is now enabled we need to walk it's children
            // and set enabled ancestor flags until we find a self-disabled child
            stack.array[stack.size++] = element;

            // stack operations in the following code are inlined since this is a very hot path
            while (stack.size > 0) {
                // inline stack pop
                UIElement child = stack.array[--stack.size];

                child.flags &= ~(UIElementFlags.AncestorEnabled);

                // if destroyed the whole subtree is also destroyed, do nothing.
                // if already disabled the whole subtree is also disabled, do nothing.

                if ((child.flags & (UIElementFlags.Alive | UIElementFlags.Enabled)) == 0) {
                    continue;
                }

                // todo -- profile not calling disable when it's not needed
                // if (child.flags & UIElementFlags.RequiresEnableCall) {
                child.OnDisable();
                // }

                // todo -- maybe do this on enable instead
                if (child.style.currentState != StyleState.Normal) {
                    // todo -- maybe just have a clear states method
                    child.style.ExitState(StyleState.Hover);
                    child.style.ExitState(StyleState.Active);
                    child.style.ExitState(StyleState.Focused);
                }

                child.flags |= UIElementFlags.DisabledThisFrame;

                // if child is still disabled after OnDisable, traverse it's children
                if (!child.isEnabled) {
                    UIElement[] children = child.children.array;
                    int childCount = child.children.size;
                    if (stack.size + childCount >= stack.array.Length) {
                        Array.Resize(ref stack.array, stack.size + childCount + 16);
                    }

                    for (int i = childCount - 1; i >= 0; i--) {
                        // inline stack push
                        stack.array[stack.size++] = children[i];
                    }
                }
            }

            // avoid checking in the loop if this is the originally disabled element
            if (element.parent.isEnabled) {
                element.flags |= UIElementFlags.AncestorEnabled;
            }

            LightStack<UIElement>.Release(ref stack);

            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnElementDisabled(element);
            }
        }

        public UIElement GetElement(int elementId) {
            return elementMap.GetOrDefault(elementId);
        }

        public void OnAttributeSet(UIElement element, string attributeName, string currentValue, string previousValue) {
            for (int i = 0; i < m_Systems.Count; i++) {
                m_Systems[i].OnAttributeSet(element, attributeName, currentValue, previousValue);
            }
        }

        public static void RefreshAll() {
            for (int i = 0; i < s_ApplicationList.Count; i++) {
                s_ApplicationList[i].Refresh();
            }
        }

        public UIView GetView(int i) {
            if (i < 0 || i >= m_Views.Count) return null;
            return m_Views[i];
        }

        public UIView GetView(string name) {
            for (int i = 0; i < m_Views.Count; i++) {
                UIView v = m_Views[i];
                if (v.name == name) {
                    return v;
                }
            }

            return null;
        }

        public static Application Find(string appId) {
            return s_ApplicationList.Find(appId, (app, _id) => app.id == _id);
        }

        public static bool HasCustomPainter(string name) {
            return s_CustomPainters.ContainsKey(name);
        }

        public static ISVGXElementPainter GetCustomPainter(string name) {
            return null; //s_CustomPainters.GetOrDefault(name);
        }

        public static Scrollbar GetCustomScrollbar(string name) {
            if (string.IsNullOrEmpty(name)) {
                return s_Scrollbars["UIForia.Default"];
            }

            return s_Scrollbars.GetOrDefault(name);
        }

        public void Animate(UIElement element, AnimationData animation) {
            m_AnimationSystem.Animate(element, animation);
        }

        public void PauseAnimation(UIElement element, AnimationData animationData) {
            m_AnimationSystem.PauseAnimation(element, animationData);
        }

        public void StopAnimation(UIElement element, AnimationData animationData) {
            m_AnimationSystem.StopAnimation(element, animationData);
        }

        public UIView[] GetViews() {
            return m_Views.ToArray();
        }

        public AnimationData GetAnimationFromFile(string fileName, string animationName) {
            AnimationData data;
            styleImporter.ImportStyleSheetFromFile(fileName).TryGetAnimationData(animationName, out data);
            return data;
        }

        internal void InsertChild(UIElement parent, CompiledTemplate template, int index) {
            UIElement ptr = parent;
            LinqBindingNode bindingNode = null;

            while (ptr != null) {
                bindingNode = ptr.bindingNode;

                if (bindingNode != null) {
                    break;
                }

                ptr = ptr.parent;
            }

            TemplateScope2 templateScope = new TemplateScope2(this, bindingNode, null);
            UIElement root = elementPool.Get(template.elementType);
            root.siblingIndex = index;

            if (parent.isEnabled) {
                root.flags |= UIElementFlags.AncestorEnabled;
            }

            root.depth = parent.depth + 1;
            root.View = parent.View;
            template.Create(root, templateScope);

            parent.children.Insert(index, root);
        }
        
        internal void InsertChild(UIElement parent, UIElement child, uint index) {
            if (child.parent != null) {
                throw new NotImplementedException("Reparenting is not supported");
            }

            bool hasView = child.View != null;

            // we don't know the hierarchy at this point.
            // could be made up of a mix of elements in various states

            child.parent = parent;
            parent.children.Insert((int) index, child);

            if (hasView) {
                throw new NotImplementedException("Changing views is not supported");
            }
            
            bool parentEnabled = parent.isEnabled;

            LightStack<UIElement> stack = LightStack<UIElement>.Get();
            UIView view = parent.View;
            stack.Push(child);

            while (stack.Count > 0) {
                UIElement current = stack.Pop();

                current.depth = current.parent.depth + 1;

                current.View = view;

                if (current.parent.isEnabled) {
                    current.flags |= UIElementFlags.AncestorEnabled;
                }
                else {
                    current.flags &= ~UIElementFlags.AncestorEnabled;
                }

                elementMap[current.id] = current;

                if (!current.isRegistered) {
                    current.style.Initialize();
                    current.flags |= UIElementFlags.Registered;
                    for (int i = 0; i < m_Systems.Count; i++) {
                        m_Systems[i].OnElementCreated(current);
                    }

                    current.OnCreate();
                    view.ElementRegistered(current);
                    onElementRegistered?.Invoke(current);
                }

                UIElement[] children = current.children.array;
                int childCount = current.children.size;
                // reverse this?
                for (int i = 0; i < childCount; i++) {
                    children[i].siblingIndex = i;
                    stack.Push(children[i]);
                }
            }

            for (int i = 0; i < parent.children.size; i++) {
                parent.children.array[i].siblingIndex = i;
            }

            LightStack<UIElement>.Release(ref stack);

            linqBindingSystem.AddElementHierarchy(child);

            if (parentEnabled && child.isEnabled) {
                child.flags |= UIElementFlags.EnabledThisFrame;
                child.flags &= ~UIElementFlags.Enabled;
                DoEnableElement(child);
            }
        }

        public void SortViews() {
            // let's bubble sort the views since only once view is out of place
            for (int i = (m_Views.Count - 1); i > 0; i--) {
                for (int j = 1; j <= i; j++) {
                    if (m_Views[j - 1].Depth > m_Views[j].Depth) {
                        UIView tempView = m_Views[j - 1];
                        m_Views[j - 1] = m_Views[j];
                        m_Views[j] = tempView;
                    }
                }
            }

            onViewsSorted?.Invoke(m_Views.ToArray());
        }
        
        // todo we will want to not compile this here, explore jitting this
        internal int AddSlotUsageTemplate(Expression<SlotUsageTemplate> lambda) {
            slotUsageTemplates.Add(lambda.Compile());
            return slotUsageTemplates.Count - 1;
        }

        internal UIElement CreateElementRoot(Type type) {
            CompiledTemplate compiledTemplate = templateCompiler.GetCompiledTemplate(type);
            LinqBindingNode bindingNode = new LinqBindingNode();
            bindingNode.system = linqBindingSystem;
            UIElement element = compiledTemplate.Create(null, new TemplateScope2(this, bindingNode, null));
            element.bindingNode = bindingNode;
            bindingNode.element = element;
            return element;
        }

        internal UIElement CreateSlot(StructList<SlotUsage> slots, string targetSlot, LinqBindingNode bindingNode, UIElement parent, UIElement root, CompiledTemplate defaultTemplateData, int defaultTemplateId) {
            UIElement element;

            // if we have no slot usages for this slot, create the default version of the slot
            if (slots == null) {
                element = slotUsageTemplates[defaultTemplateId].Invoke(this, bindingNode, parent, new LexicalScope(root, defaultTemplateData, null));
                element.View = parent.View;
                element.parent = parent;
                return element;
            }

            // handle creating slot override
            SlotUsage[] array = slots.array;
            for (int i = 0; i < slots.size; i++) {
                if (array[i].slotName == targetSlot) {
                    element = slotUsageTemplates[array[i].templateId].Invoke(this, bindingNode, parent, array[i].lexicalScope);
                    element.parent = parent;
                    element.View = parent.View;
                    return element;
                }
            }

            // handle creating slot default if no match was found
            element = slotUsageTemplates[defaultTemplateId].Invoke(this, bindingNode, parent, new LexicalScope(root, defaultTemplateData, slots));
            element.View = parent.View;
            element.parent = parent;
            return element;
        }
        
        // todo -- override that accepts an index into an array instead of a type, to save a dictionary lookup
        // todo -- don't create a list for every type, maybe a single pool list w/ sorting & a jump search or similar
        /// Returns the shell of a UI Element, space is allocated for children but no child data is associated yet, only a parent, view, and depth
        public UIElement CreateElementFromPool(ProcessedType type, UIElement parent, int childCount) {
            UIElement retn = elementPool.Get(type);
            // todo -- register element in type map for selectors
            retn.id = ElementIdGenerator++;
            // todo -- make children a linked list instead
            retn.children = LightList<UIElement>.GetMinSize(childCount);
            retn.children.size = childCount; // children get assigned in the template function but we need to setup the list here
            retn.style = new UIStyleSet(retn); // todo -- pool this
            retn.flags = UIElementFlags.Enabled | UIElementFlags.Alive; 
            retn.parent = parent;
            retn.layoutResult = new LayoutResult(); // todo pool
            return retn;
        }

        public UIElement CreateElementFromPool(Type type, UIElement parent, int childCount) {
            return CreateElementFromPool(TypeProcessor.GetProcessedType(type), parent, childCount);
        }

        // Doesn't expect to create the root
        internal void HydrateTemplate(int templateId, UIElement root, TemplateScope2 scope) {
            templateData.templateFns[templateId](root, scope);
        }

    }

}