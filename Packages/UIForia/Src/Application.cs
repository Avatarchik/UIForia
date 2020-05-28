using System;
using System.Collections.Generic;
using System.Diagnostics;
using Src.Systems;
using UIForia.Animation;
using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Layout;
using UIForia.Rendering;
using UIForia.Routing;
using UIForia.Sound;
using UIForia.Systems;
using UIForia.Systems.Input;
using UIForia.Util;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace UIForia {

    public abstract class Application : IDisposable {

        private static SizeInt UIApplicationSize;

        public static float dpiScaleFactor = Mathf.Max(1, Screen.dpi / 100f);

        public static readonly float originalDpiScaleFactor = Mathf.Max(1, Screen.dpi / 100f);

        public float DPIScaleFactor {
            get => dpiScaleFactor;
            set => dpiScaleFactor = value;
        }

        public static SizeInt UiApplicationSize => UIApplicationSize;

        public static List<Application> Applications = new List<Application>();

        internal Stopwatch layoutTimer = new Stopwatch();
        internal Stopwatch renderTimer = new Stopwatch();
        internal Stopwatch bindingTimer = new Stopwatch();
        internal Stopwatch loopTimer = new Stopwatch();

        public readonly string id;
        internal StyleSystem styleSystem;
        internal LayoutSystem layoutSystem;
        internal RenderSystem renderSystem;
        internal InputSystem inputSystem;
        internal RoutingSystem routingSystem;
        internal AnimationSystem animationSystem;
        internal UISoundSystem soundSystem;
        internal LinqBindingSystem linqBindingSystem;
        internal ElementSystem elementSystem;

        private int elementIdGenerator;

        protected ResourceManager resourceManager;

        protected List<ISystem> systems;

        public event Action<UIElement> onElementRegistered;
        public event Action onElementDestroyed;
        public event Action<UIElement> onElementEnabled;
        public event Action<UIView[]> onViewsSorted;
        public event Action<UIView> onViewRemoved;
        public event Action onRefresh;

        public MaterialDatabase materialDatabase;

        internal CompiledTemplateData templateData;

        internal int frameId;
        protected internal List<UIView> views;

        internal static Dictionary<string, Type> s_CustomPainters;

        private UITaskSystem m_BeforeUpdateTaskSystem;
        private UITaskSystem m_AfterUpdateTaskSystem;

        public static readonly UIForiaSettings Settings;

        static Application() {
            ArrayPool<UIElement>.SetMaxPoolSize(64);
            s_CustomPainters = new Dictionary<string, Type>();
            Settings = Resources.Load<UIForiaSettings>("UIForiaSettings");
            if (Settings == null) {
                throw new Exception("UIForiaSettings are missing. Use the UIForia/Create UIForia Settings to create it");
            }
        }

        public UIForiaSettings settings => Settings;

        private int NextElementId => elementIdGenerator++;

        public TemplateMetaData[] zz_Internal_TemplateMetaData => templateData.templateMetaData;

        private TemplateSettings templateSettings;
        private bool isPreCompiled;

        internal LightList<HierarchyChange> hierarchyChanges;

        public struct HierarchyChange {

            public ElementId elementId;

        }

        protected Application(bool isPreCompiled, TemplateSettings templateSettings, ResourceManager resourceManager, Action<UIElement> onElementRegistered) {
            this.isPreCompiled = isPreCompiled;
            this.templateSettings = templateSettings;
            this.onElementRegistered = onElementRegistered;
            this.id = templateSettings.applicationName;
            this.resourceManager = resourceManager ?? new ResourceManager();
            this.hierarchyChanges = new LightList<HierarchyChange>(64);

            Applications.Add(this);
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnEditorReload;
#endif
        }

#if UNITY_EDITOR
        private void OnEditorReload() {
            templateData?.Destroy();
            templateData = null;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnEditorReload;
        }
#endif

        protected virtual void CreateSystems() {
            styleSystem = new StyleSystem();
            elementSystem = new ElementSystem(1024);
            renderSystem = new RenderSystem(Camera ?? Camera.current, this, elementSystem);
            routingSystem = new RoutingSystem();
            linqBindingSystem = new LinqBindingSystem();
            soundSystem = new UISoundSystem();
            animationSystem = new AnimationSystem(elementSystem);
            layoutSystem = new LayoutSystem(this, elementSystem);
            inputSystem = new GameInputSystem(layoutSystem, new KeyboardInputManager());
        }

        internal void Initialize() {
            systems = new List<ISystem>();
            views = new List<UIView>();

            CreateSystems();

            systems.Add(linqBindingSystem);
            systems.Add(routingSystem);
            systems.Add(inputSystem);
            systems.Add(animationSystem);
            systems.Add(renderSystem);

            m_BeforeUpdateTaskSystem = new UITaskSystem();
            m_AfterUpdateTaskSystem = new UITaskSystem();

            UIView view = null;

            // Stopwatch timer = Stopwatch.StartNew();

            if (isPreCompiled) {
                templateData = TemplateLoader.LoadPrecompiledTemplates(templateSettings);
            }
            else {
                templateData = TemplateLoader.LoadRuntimeTemplates(templateSettings.rootType, templateSettings);
            }

            materialDatabase = templateData.materialDatabase;

            view = new UIView(this, "Default", Matrix4x4.identity, new Size(Width, Height));
            UIElement rootElement = templateData.templates[0].Invoke(null, new TemplateScope(this));
            view.Init(rootElement);

            views.Add(view);

            layoutSystem.OnViewAdded(view);
            renderSystem.OnViewAdded(view);

            //timer.Stop();
            //Debug.Log("Initialized UIForia application in " + timer.Elapsed.TotalSeconds.ToString("F2") + " seconds");
        }

        public UIView CreateView<T>(string name, Size size, in Matrix4x4 matrix) where T : UIElement {
            if (templateData.TryGetTemplate<T>(out DynamicTemplate dynamicTemplate)) {

                UIElement element = templateData.templates[dynamicTemplate.templateId].Invoke(null, new TemplateScope(this));

                UIView view = new UIView(this, name, element, matrix, size);

                view.Depth = views.Count;
                views.Add(view);

                layoutSystem.OnViewAdded(view);
                renderSystem.OnViewAdded(view);

                return view;
            }

            throw new TemplateNotFoundException($"Unable to find a template for {typeof(T)}. This is probably because you are trying to load this template dynamically and did include the type in the {nameof(TemplateSettings.dynamicallyCreatedTypes)} list.");
        }

        public UIView CreateView<T>(string name, Size size) where T : UIElement {
            return CreateView<T>(name, size, Matrix4x4.identity);
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
                        throw new Exception(
                            $"Failed to register a custom painter with the name {paintAttr.name} from type {type.FullName} because it was already registered.");
                    }

                    s_CustomPainters.Add(paintAttr.name, type);
                }
            }
        }

        public IStyleSystem StyleSystem => styleSystem;
        public IRenderSystem RenderSystem => renderSystem;
        public ILayoutSystem LayoutSystem => layoutSystem;
        public InputSystem InputSystem => inputSystem;
        public RoutingSystem RoutingSystem => routingSystem;
        public UISoundSystem SoundSystem => soundSystem;

        public Camera Camera { get; private set; }

        public ResourceManager ResourceManager => resourceManager;

        public void SetScreenSize(int width, int height) {
            UIApplicationSize.width = width;
            UIApplicationSize.height = height;
        }

        public float Width => UiApplicationSize.width / dpiScaleFactor;

        public float Height => UiApplicationSize.height / dpiScaleFactor;

        public void SetCamera(Camera camera) {
            Rect rect = camera.pixelRect;
            UIApplicationSize.height = (int) rect.height;
            UIApplicationSize.width = (int) rect.width;

            if (views.Count > 0) {
                views[0].Viewport = new Rect(0, 0, Width, Height);
            }

            Camera = camera;
            RenderSystem.SetCamera(camera);
        }

        public UIView RemoveView(UIView view) {
            if (!views.Remove(view)) return null;

            for (int i = 0; i < systems.Count; i++) {
                systems[i].OnViewRemoved(view);
            }

            DestroyElement(view.dummyRoot);
            onViewRemoved?.Invoke(view);
            return view;
        }

        public void Refresh() {
            if (isPreCompiled) {
                Debug.Log("Cannot refresh application because it is using precompiled templates");
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            foreach (ISystem system in systems) {
                system.OnDestroy();
            }

            elementSystem.Dispose();

            for (int i = views.Count - 1; i >= 0; i--) {
                views[i].Destroy();
            }

            resourceManager.Reset();

            materialDatabase.Destroy();
            templateData.Destroy();

            m_AfterUpdateTaskSystem.OnDestroy();
            m_BeforeUpdateTaskSystem.OnDestroy();

            elementIdGenerator = 0;

            Initialize();

            onRefresh?.Invoke();

            stopwatch.Stop();
            Debug.Log("Refreshed " + id + " in " + stopwatch.Elapsed.TotalSeconds.ToString("F2") + " seconds");
        }

        public void Destroy() {
            Applications.Remove(this);
            templateData?.Destroy();

            foreach (ISystem system in systems) {
                system.OnDestroy();
            }

            for (int i = views.Count - 1; i >= 0; i--) {
                views[i].Destroy();
            }

            onElementEnabled = null;
            onElementDestroyed = null;
            onElementRegistered = null;
        }

        public static void DestroyElement(UIElement element) {
            element.View.application.DoDestroyElement(element);
        }

        internal void DoDestroyElement(UIElement element, bool removingChildren = false) {
            // do nothing if already destroyed

            throw new NotImplementedException("Reimplement destroy");

            ElementTable<ElementMetaInfo> metaTable = elementSystem.metaTable;

            if ((metaTable[element.id].flags & UIElementFlags.Alive) == 0) {
                return;
            }

            LightStack<UIElement> stack = LightStack<UIElement>.Get();
            LightList<UIElement> toInternalDestroy = LightList<UIElement>.Get();

            stack.Push(element);

            while (stack.Count > 0) {
                UIElement current = stack.array[--stack.size];

                ref ElementMetaInfo metaInfo = ref metaTable[current.id];

                if (metaInfo.generation != current.id.generation && (metaInfo.flags & UIElementFlags.Alive) == 0) {
                    continue;
                }

                metaInfo.flags &= ~(UIElementFlags.Alive);

                current.isAlive = false;

                // todo -- tick generation

                current.OnDestroy();
                toInternalDestroy.Add(current);

                // UIElement[] children = current.children.array;
                int childCount = current.ChildCount;

                if (stack.size + childCount >= stack.array.Length) {
                    Array.Resize(ref stack.array, stack.size + childCount + 16);
                }

                UIElement ptr = current.GetLastChild();
                while (ptr != null) {
                    // inline stack push
                    stack.array[stack.size++] = ptr;
                    ptr = ptr.GetPreviousSibling();
                }

                //for (int i = childCount - 1; i >= 0; i--) {
                //}
            }

            if (element.parent != null && !removingChildren) {

                elementSystem.RemoveChild(element.parent.id, element.id);

                // todo -- remove when children are gone
                element.parent.children.Remove(element);
                for (int i = 0; i < element.parent.children.Count; i++) {
                    element.parent.children[i].siblingIndex = i;
                }

            }

            for (int i = 0; i < systems.Count; i++) {
                systems[i].OnElementDestroyed(element);
            }

            for (int i = 0; i < toInternalDestroy.size; i++) {
                toInternalDestroy[i].InternalDestroy();
            }

            LightList<UIElement>.Release(ref toInternalDestroy);
            LightStack<UIElement>.Release(ref stack);

            onElementDestroyed?.Invoke();
        }

        public void Update() {
            // input queries against last frame layout
            inputSystem.Read();
            loopTimer.Reset();
            loopTimer.Start();

            Rect rect;
            if (!ReferenceEquals(Camera, null)) {
                rect = Camera.pixelRect;
            }
            else {
                rect = new Rect(0, 0, 1920, 1080);
            }

            UIApplicationSize.height = (int) rect.height;
            UIApplicationSize.width = (int) rect.width;

            for (int i = 0; i < views.Count; i++) {
                views[i].Viewport = new Rect(0, 0, Width, Height);
            }

            inputSystem.OnUpdate();
            m_BeforeUpdateTaskSystem.OnUpdate();

            linqBindingSystem.BeginFrame();
            bindingTimer.Reset();
            bindingTimer.Start();

            // right now, out of order elements wont get bindings until next frame. this miiight be ok but probably will cause weirdness. likely want this to change
            for (int i = 0; i < views.Count; i++) {
                linqBindingSystem.NewUpdateFn(views[i].RootElement);
            }

            bindingTimer.Stop();

            elementSystem.FilterEnabledDisabledElements(views[0].dummyRoot.id); // todo -- not right?
            animationSystem.OnUpdate();

            routingSystem.OnUpdate();

            // lists also contain elements created this frame

            if (elementSystem.disabledElementsThisFrame.size > 0) {
                layoutSystem.HandleElementDisabled(elementSystem.disabledElementsThisFrame);
            }

            if (elementSystem.enabledElementsThisFrame.size > 0) {
                layoutSystem.HandleElementEnabled(elementSystem.enabledElementsThisFrame);
            }

            // styleSystem.FlushChangeSets(elementSystem, layoutSystem, renderSystem);

            // todo -- read changed data into layout/render thread
            // layoutTimer.Restart();
            layoutSystem.RunLayout();
            // layoutTimer.Stop();
            //
            // renderTimer.Restart();
            // renderSystem.OnUpdate();
            // renderTimer.Stop();

            m_AfterUpdateTaskSystem.OnUpdate();

            elementSystem.CleanupFrame();
            frameId++;
            loopTimer.Stop();
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

        internal void DoEnableElement(UIElement element) {

            // if element is not enabled (ie has a disabled ancestor or is not alive), no-op 
            if (!element.isAncestorEnabled || element.isDestroyed) {
                return;
            }

            ElementTable<ElementMetaInfo> metaTable = elementSystem.metaTable;

            StructStack<ElemRef> stack = StructStack<ElemRef>.Get();
            // if element is now enabled we need to walk it's children
            // and set enabled ancestor flags until we find a self-disabled child
            stack.array[stack.size++].element = element;

            elementSystem.enabledElementsThisFrame.Add(element.id);
            element.isSelfEnabled = true;
            metaTable[element.id].flags |= UIElementFlags.Enabled | UIElementFlags.EnabledRoot;

            // stack operations in the following code are inlined since this is a very hot path
            while (stack.size > 0) {
                // inline stack pop
                UIElement child = stack.array[--stack.size].element;

                ref ElementMetaInfo metaInfo = ref metaTable[child.id];

                metaInfo.flags |= UIElementFlags.AncestorEnabled;
                child.isAncestorEnabled = true;

                // if the element is itself disabled or destroyed, keep going
                if ((metaInfo.flags & UIElementFlags.Enabled) == 0) {
                    continue;
                }

                elementSystem.enabledElementsThisFrame.Add(child.id);

                child.style.UpdateInheritedStyles(); // todo -- move this
                try {
                    child.OnEnable();
                }
                catch (Exception e) {
                    Debug.Log(e);
                }

                // todo -- move this
                // We need to run all runCommands now otherwise animations in [normal] style groups won't run after enabling.
                child.style.RunCommands();

                // register the flag set even if we get disabled via OnEnable, we just want to track that OnEnable was called at least once
                metaInfo.flags |= UIElementFlags.HasBeenEnabled;

                // only continue if calling enable didn't re-disable the element
                if ((metaInfo.flags & UIElementFlags.EnabledFlagSet) == UIElementFlags.EnabledFlagSet) {

                    int childCount = elementSystem.hierarchyTable[child.id].childCount;
                    child.enableStateChangedFrameId = frameId;
                    if (stack.size + childCount >= stack.array.Length) {
                        Array.Resize(ref stack.array, stack.size + childCount + 16);
                    }

                    UIElement ptr = child.GetLastChild();

                    while (ptr != null) {
                        // inline stack push
                        stack.array[stack.size++].element = ptr;
                        ptr = ptr.GetPreviousSibling();
                    }

                }
            }

            for (int i = 0; i < systems.Count; i++) {
                systems[i].OnElementEnabled(element);
            }

            StructStack<ElemRef>.Release(ref stack);

            onElementEnabled?.Invoke(element);
        }

        public void DoDisableElement(UIElement element) {
            // if element is already disabled or destroyed, no op
            if (element.isDisabled) {
                return;
            }

            bool wasDisabled = element.isDisabled;
            ref ElementTable<ElementMetaInfo> metaTable = ref elementSystem.metaTable;
            metaTable[element.id].flags &= ~UIElementFlags.Enabled;
            metaTable[element.id].flags |= UIElementFlags.DisableRoot;
            element.isSelfEnabled = false;

            if (wasDisabled) {
                return;
            }

            // if element is now enabled we need to walk it's children
            // and set enabled ancestor flags until we find a self-disabled child
            StructStack<ElemRef> stack = StructStack<ElemRef>.Get();
            stack.array[stack.size++].element = element;

            elementSystem.disabledElementsThisFrame.Add(element.id);

            // stack operations in the following code are inlined since this is a very hot path
            while (stack.size > 0) {
                // inline stack pop
                UIElement child = stack.array[--stack.size].element;

                ref ElementMetaInfo metaInfo = ref metaTable[child.id];

                child.isAncestorEnabled = false;
                metaInfo.flags &= ~(UIElementFlags.AncestorEnabled);

                // if destroyed the whole subtree is also destroyed, do nothing.
                // if already disabled the whole subtree is also disabled, do nothing.

                if ((metaInfo.flags & (UIElementFlags.Alive | UIElementFlags.Enabled)) == 0) {
                    continue;
                }

                elementSystem.disabledElementsThisFrame.Add(child.id);

                // todo -- profile not calling disable when it's not needed
                // if (child.flags & UIElementFlags.RequiresEnableCall) {
                try {
                    child.OnDisable();
                }
                catch (Exception e) {
                    Debug.Log(e);
                }
                // }

                // todo -- maybe do this on enable instead
                if (child.style.currentState != StyleState.Normal) {
                    // todo -- maybe just have a clear states method
                    // todo -- change to ExitState(State.Hover|State.Active|State.Focus) and see if its better / faster
                    child.style.ExitState(StyleState.Hover);
                    child.style.ExitState(StyleState.Active);
                    child.style.ExitState(StyleState.Focused);
                }

                // if child is still disabled after OnDisable, traverse it's children
                if (child.isDisabled) {
                    UIElement[] children = child.children.array;
                    int childCount = child.children.size;

                    child.enableStateChangedFrameId = frameId;

                    if (stack.size + childCount >= stack.array.Length) {
                        Array.Resize(ref stack.array, stack.size + childCount + 16);
                    }

                    for (int i = childCount - 1; i >= 0; i--) {
                        // inline stack push
                        stack.array[stack.size++].element = children[i];
                    }
                }
            }

            // was disabled in loop, need to reset it here
            element.isAncestorEnabled = true;

            StructStack<ElemRef>.Release(ref stack);

            inputSystem.BlurOnDisableOrDestroy();

        }

        public UIElement GetElement(ElementId elementId) {
            LightStack<UIElement> stack = LightStack<UIElement>.Get();

            for (int i = 0; i < views.Count; i++) {
                stack.Push(views[i].RootElement);

                while (stack.size > 0) {
                    UIElement element = stack.PopUnchecked();

                    if (element.id == elementId) {
                        LightStack<UIElement>.Release(ref stack);
                        return element;
                    }

                    if (element.children == null) continue;

                    for (int j = 0; j < element.children.size; j++) {
                        stack.Push(element.children.array[j]);
                    }
                }
            }

            LightStack<UIElement>.Release(ref stack);
            return null;
        }

        public void OnAttributeSet(UIElement element, string attributeName, string currentValue, string previousValue) {
            for (int i = 0; i < systems.Count; i++) {
                systems[i].OnAttributeSet(element, attributeName, currentValue, previousValue);
            }
        }

        public static void RefreshAll() {

            for (int i = 0; i < Applications.Count; i++) {
                Applications[i].Refresh();
            }
        }

        public UIView GetView(int i) {
            if (i < 0 || i >= views.Count) return null;
            return views[i];
        }

        public UIView GetView(string name) {
            for (int i = 0; i < views.Count; i++) {
                UIView v = views[i];
                if (v.name == name) {
                    return v;
                }
            }

            return null;
        }

        public static Application Find(string appId) {
            return Applications.Find((app) => app.id == appId);
        }

        public static bool HasCustomPainter(string name) {
            return s_CustomPainters.ContainsKey(name);
        }

        public UIView[] GetViews() {
            return views.ToArray();
        }

        internal void InitializeElement(UIElement child) {
            bool parentEnabled = child.parent.isEnabled;

            UIView view = child.parent.View;

            StructStack<ElemRef> elemRefStack = StructStack<ElemRef>.Get();
            elemRefStack.Push(new ElemRef() {element = child});

            ElementTable<ElementMetaInfo> metaTable = elementSystem.metaTable;

            while (elemRefStack.size > 0) {
                UIElement current = elemRefStack.array[--elemRefStack.size].element;

                current.hierarchyDepth = current.parent.hierarchyDepth + 1;

                current.View = view;

                ref ElementMetaInfo metaInfo = ref metaTable[current.id];

                current.isAncestorEnabled = current.parent.isEnabled;
                current.isAlive = true;
                current.isSelfEnabled = true;

                if (current.parent.isEnabled) {
                    metaInfo.flags |= UIElementFlags.AncestorEnabled;
                }
                else {
                    metaInfo.flags &= ~UIElementFlags.AncestorEnabled;
                }

                // always true, oder?
                if ((metaInfo.flags & UIElementFlags.Created) == 0) {
                    metaInfo.flags |= UIElementFlags.Created;
                    routingSystem.OnElementCreated(current);

                    try {
                        onElementRegistered?.Invoke(current);
                        current.OnCreate();
                    }
                    catch (Exception e) {
                        Debug.LogWarning(e);
                    }
                }

                UIElement[] children = current.children.array;
                int childCount = current.children.size;
                // reverse this? inline stack push
                for (int i = 0; i < childCount; i++) {
                    children[i].siblingIndex = i;
                    elemRefStack.Push(new ElemRef() {element = children[i]});
                }
            }

            if (parentEnabled && child.isEnabled) {
                DoEnableElement(child);
            }

            StructStack<ElemRef>.Release(ref elemRefStack);
        }

        public void SortViews() {
            // let's bubble sort the views since only once view is out of place
            for (int i = (views.Count - 1); i > 0; i--) {
                for (int j = 1; j <= i; j++) {
                    if (views[j - 1].Depth > views[j].Depth) {
                        UIView tempView = views[j - 1];
                        views[j - 1] = views[j];
                        views[j] = tempView;
                    }
                }
            }

            onViewsSorted?.Invoke(views.ToArray());
        }

        internal void GetElementCount(out int totalElementCount, out int enabledElementCount, out int disabledElementCount) {
            LightStack<UIElement> stack = LightStack<UIElement>.Get();
            totalElementCount = 0;
            enabledElementCount = 0;

            for (int i = 0; i < views.Count; i++) {
                stack.Push(views[i].RootElement);

                while (stack.size > 0) {
                    totalElementCount++;
                    UIElement element = stack.PopUnchecked();

                    if (element.isEnabled) {
                        enabledElementCount++;
                    }

                    if (element.children == null) continue;

                    for (int j = 0; j < element.children.size; j++) {
                        stack.Push(element.children.array[j]);
                    }
                }
            }

            disabledElementCount = totalElementCount - enabledElementCount;
            LightStack<UIElement>.Release(ref stack);
        }

        public UIElement CreateSlot(string slotName, TemplateScope scope, int defaultSlotId, UIElement root, UIElement parent) {
            int slotId = ResolveSlotId(slotName, scope.slotInputs, defaultSlotId, out UIElement contextRoot);
            if (contextRoot == null) {
                Assert.AreEqual(slotId, defaultSlotId);
                contextRoot = root;
            }

            // context 0 = innermost
            // context[context.size - 1] = outermost

            scope.innerSlotContext = root;
            // for each override with same name add to reference array at index?
            // will have to be careful with names but can change to unique ids when we need alias support and match on that
            UIElement retn = templateData.slots[slotId](contextRoot, parent, scope);
            retn.View = parent.View;
            return retn;
        }

        public UIElement CreateTemplate(int templateSpawnId, UIElement contextRoot, UIElement parent, TemplateScope scope) {
            UIElement retn = templateData.slots[templateSpawnId](contextRoot, parent, scope);

            InitializeElement(retn);

            return retn;
        }

        /// Returns the shell of a UI Element, space is allocated for children but no child data is associated yet, only a parent, view, and depth
        public UIElement CreateElementFromPool(int typeId, UIElement parent, int childCount, int attributeCount, int originTemplateId) {
            // children get assigned in the template function but we need to setup the list here
            ConstructedElement retn = templateData.ConstructElement(typeId);
            UIElement element = retn.element;

            element.application = this;
            element.templateMetaData = templateData.templateMetaData[originTemplateId];

            const UIElementFlags flags = UIElementFlags.Enabled | UIElementFlags.Alive;

            element.id = elementSystem.CreateElement(element, parent?.hierarchyDepth + 1 ?? 0, -999, -999, flags);

            element.style = new UIStyleSet(element);
            element.children = new LightList<UIElement>(childCount);

            if (attributeCount > 0) {
                element.attributes = new StructList<ElementAttribute>(attributeCount);
                element.attributes.size = attributeCount;
            }

            element.parent = parent;

            if (parent != null) {
                parent.children.Add(element);
                elementSystem.AddChild(parent.id, element.id);
            }

            return element;
        }

        public TemplateMetaData GetTemplateMetaData(int metaDataId) {
            return templateData.templateMetaData[metaDataId];
        }

        public UIElement CreateElementFromPoolWithType(int typeId, UIElement parent, int childCount, int attrCount, int originTemplateId) {
            return CreateElementFromPool(typeId, parent, childCount, attrCount, originTemplateId);
        }

        public static int ResolveSlotId(string slotName, StructList<SlotUsage> slotList, int defaultId, out UIElement contextRoot) {
            if (slotList == null) {
                contextRoot = null;
                return defaultId;
            }

            for (int i = 0; i < slotList.size; i++) {
                if (slotList.array[i].slotName == slotName) {
                    contextRoot = slotList.array[i].context;
                    return slotList.array[i].slotId;
                }
            }

            contextRoot = null;
            return defaultId;
        }

        // Doesn't expect to create the root
        public void HydrateTemplate(int templateId, UIElement root, TemplateScope scope) {
            templateData.templates[templateId](root, scope);
            scope.Release();
        }

        public static void SetCustomPainters(Dictionary<string, Type> dictionary) {
            s_CustomPainters = dictionary;
        }

        public void Dispose() {
            elementSystem?.Dispose();
        }

    }

}