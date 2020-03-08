using System;
using UIForia.Elements;
using UIForia.Util;
using UnityEngine;

namespace UIForia {

    public class GameApplication : Application {

        protected GameApplication(bool isPreCompiled, TemplateSettings templateData, ResourceManager resourceManager, Action<UIElement> onRegister) : base(isPreCompiled, templateData, resourceManager, onRegister) { }

        public static Application CreateFromRuntimeTemplates(TemplateSettings templateSettings, Camera camera, Action<UIElement> onRegister) {

            TypeResolver.Initialize();

            Type rootModuleType = Module.GetModuleFromElementType(templateSettings.rootType);

            //CompiledTemplateData data = rootModule.CompileApplication();

            if (rootModuleType == null) {
                throw new Exception("Unable to find module for type" + templateSettings.rootType);
            }

            Module module = Module.CreateRootModule(rootModuleType);

            ResourceManager resourceManager = new ResourceManager();

            templateSettings.resourceManager = resourceManager;

            GameApplication retn = new GameApplication(false, templateSettings, resourceManager, onRegister);

            retn.Initialize();

            retn.SetCamera(camera);

            return retn;
        }

        public static Application CreateFromPrecompiledTemplates(TemplateSettings templateSettings, Camera camera, Action<UIElement> onRegister) {
            ResourceManager resourceManager = new ResourceManager();

            templateSettings.resourceManager = resourceManager;

            GameApplication retn = new GameApplication(true, templateSettings, resourceManager, onRegister);

            retn.Initialize();

            retn.SetCamera(camera);

            return retn;
        }

    }

}