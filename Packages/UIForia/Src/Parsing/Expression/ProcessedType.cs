using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UIForia.Attributes;

namespace UIForia.Parsing.Expression {

    [DebuggerDisplay("{rawType.Name}")]
    public struct ProcessedType {

        public readonly Type rawType;
        private readonly TemplateAttribute templateAttr;
        
        public ProcessedType(Type rawType) {
            this.rawType = rawType;
            templateAttr = rawType.GetCustomAttribute<TemplateAttribute>();
        }

        public string GetTemplate(string templateRoot) {
            if (templateAttr == null) {
                throw new Exception($"Template not defined for {rawType.Name}");
            }

            switch (templateAttr.templateType) {
                case TemplateType.Internal:
                    string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "UIForia", "Src", templateAttr.template));
                    return TryReadFile(path);
                case TemplateType.File:
                    return TryReadFile(Path.GetFullPath(Path.Combine(templateRoot, templateAttr.template)));
                default:
                    return templateAttr.template;
            }
        }

        private static string TryReadFile(string path) {
            if (!path.EndsWith(".xml")) {
                path += ".xml";
            }
            
            // todo should probably be cached, but be careful about reloading

            try {
                return File.ReadAllText(path);
            }
            catch (FileNotFoundException) {
                throw;
            }
            catch (Exception) {
                return null;
            }
        }

        public bool HasTemplatePath() {
            return templateAttr.templateType == TemplateType.File;
        }

        // path from Assets directory
        public string GetTemplatePath() {
            return !HasTemplatePath() ? rawType.AssemblyQualifiedName : templateAttr.template;
        }

    }

}