using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UIForia.Attributes;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Extensions;
using UIForia.Rendering;
using UIForia.Util;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace UIForia.Parsing.Expression {

    public static class TypeProcessor {

        public struct TypeData {

            public readonly Type type;
            public readonly string tagName;

            public TypeData(Type type) {
                this.type = type;
                object attr = type.GetCustomAttribute(typeof(TemplateTagNameAttribute), false);
                if (attr != null) {
                    tagName = ((TemplateTagNameAttribute) attr).tagName;
                }
                else {
                    tagName = type.Name;
                }
            }

        }

        private static readonly Dictionary<string, ProcessedType> typeMap = new Dictionary<string, ProcessedType>();
        private static LightList<Assembly> filteredAssemblies;
        private static LightList<Type> loadedTypes;
        private static TypeData[] templateTypes;
        private static readonly Dictionary<string, ProcessedType> templateTypeMap = new Dictionary<string, ProcessedType>();
        private static readonly Dictionary<string, LightList<Assembly>> s_NamespaceMap = new Dictionary<string, LightList<Assembly>>();

        private static void FilterAssemblies() {
            if (filteredAssemblies != null) return;
            Stopwatch watch = new Stopwatch();
            watch.Start();

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            filteredAssemblies = new LightList<Assembly>();
            loadedTypes = new LightList<Type>();

            
            for (int i = 0; i < assemblies.Length; i++) {
                Assembly assembly = assemblies[i];

                if (assembly == null) {
                    continue;
                }

                if (!FilterAssembly(assembly)) continue;

                filteredAssemblies.Add(assembly);
                try {

                    Type[] types = assembly.GetTypes();

                    for (int j = 0; j < types.Length; j++) {

                        Type currentType = types[j];
                        // can be null if assembly referenced is unavailable
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        if (currentType == null) {
                            continue;
                        }

                        loadedTypes.Add(currentType);

                        IEnumerable<Attribute> attrs = currentType.GetCustomAttributes();

                        Application.ProcessClassAttributes(currentType, attrs);

                        if (!s_NamespaceMap.TryGetValue(currentType.Namespace ?? "null", out LightList<Assembly> list)) {
                            list = new LightList<Assembly>();
                            s_NamespaceMap.Add(currentType.Namespace ?? "null", list);
                        }

                        if (!list.Contains(assembly)) {
                            list.Add(assembly);
                        }

                    }
                }
                catch (ReflectionTypeLoadException) {
                    Debug.Log($"{assembly.FullName}");
                    throw;
                }
            }

            loadedTypes.Add(typeof(Color));
            watch.Stop();
            Debug.Log("Types loaded in: " + watch.ElapsedMilliseconds + "ms");
            GC.Collect();
        }

        public static bool IsNamespace(string name) {
            return s_NamespaceMap.ContainsKey(name);
        }

        private static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

        public static bool TryFindType(string typeName, out Type t) {
            if (!typeCache.TryGetValue(typeName, out t)) {
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) {
                    if (a.IsDynamic) {
                        continue;
                    }

                    t = a.GetType(typeName);
                    if (t != null)
                        break;
                }

                typeCache[typeName] = t;
            }

            return t != null;
        }

        public static Type ResolveType(Type originType, string name, IList<string> namespaces) {
            string subtypeName = originType.FullName + "+" + name;
            subtypeName = subtypeName + ", " + originType.Assembly.FullName;
            Type retn = Type.GetType(subtypeName);

            if (retn != null) {
                return retn;
            }

            FilterAssemblies();

            LightList<Assembly> assemblies = s_NamespaceMap.GetOrDefault(originType.Namespace ?? "null");
            if (assemblies != null) {

                string typeName = originType.Namespace ?? "null" + "." + name + ", ";
                foreach (Assembly assembly in assemblies) {
                    string fullTypeName = typeName + assembly.FullName;

                    retn = Type.GetType(fullTypeName);

                    if (retn != null) {
                        return retn;
                    }
                }

            }

            if (originType.FullName.Contains("+")) {
                Assembly assembly = originType.Assembly;
                string[] parentTypePath = originType.FullName.Split('+');
                string typeName = string.Empty;
                string assemblyName = ", " + assembly.FullName;
                for (int i = 0; i < parentTypePath.Length - 1; i++) {
                    typeName += parentTypePath[i] + "+";
                    string fullTypeName = typeName + name + assemblyName;
                    retn = Type.GetType(fullTypeName);
                    if (retn != null) {
                        return retn;
                    }
                }

            }

            return ResolveType(name, namespaces);
        }

        // todo -- handle generics too 
        // todo -- handle nested types
        public static Type ResolveType(string name, IList<string> namespaces) {
            FilterAssemblies();

            for (int i = 0; i < namespaces.Count; i++) {
                LightList<Assembly> assemblies = s_NamespaceMap.GetOrDefault(namespaces[i]);
                if (assemblies == null) {
                    continue;
                }

                string typeName = namespaces[i] + "." + name + ", ";
                foreach (Assembly assembly in assemblies) {
                    string fullTypeName = typeName + assembly.FullName;

                    Type retn = Type.GetType(fullTypeName);

                    if (retn != null) {
                        return retn;
                    }
                }
            }

            return null;
        }

        public static ProcessedType GetType(string typeName, List<ImportDeclaration> importPaths = null) {
            FilterAssemblies();
            if (typeMap.ContainsKey(typeName)) {
                return typeMap[typeName];
            }

            for (int i = 0; i < TemplateParser.IntrinsicElementTypes.Length; i++) {
                if (typeName == TemplateParser.IntrinsicElementTypes[i].name) {
                    return new ProcessedType(TemplateParser.IntrinsicElementTypes[i].type);
                }
            }

            Type type = Type.GetType(typeName);

            if (type == null) {
                for (int i = 0; i < loadedTypes.Count; i++) {
                    if (loadedTypes[i].Name == typeName) {
                        type = loadedTypes[i];
                        break;
                    }
                }
            }

            Assert.IsNotNull(type, $"type != null, unable to find type {typeName}");

            return GetType(type);
        }

        // todo -- handle imports
        public static ProcessedType GetTemplateType(string tagName) {
            GetTemplateTypes();

            ProcessedType processedType;
            if (templateTypeMap.TryGetValue(tagName, out processedType)) {
                return processedType;
            }

            throw new ParseException("Unable to find type for tag name: " + tagName);
        }

        public static Type ResolveTypeName(string typeName) {
            switch (typeName) {
                case "bool": return typeof(bool);
                case "byte": return typeof(byte);
                case "sbyte": return typeof(sbyte);
                case "char": return typeof(char);
                case "decimal": return typeof(decimal);
                case "double": return typeof(double);
                case "float": return typeof(float);
                case "int": return typeof(int);
                case "uint": return typeof(uint);
                case "long": return typeof(long);
                case "ulong": return typeof(ulong);
                case "object": return typeof(object);
                case "short": return typeof(short);
                case "ushort": return typeof(ushort);
                case "string": return typeof(string);
            }

            return GetRuntimeType(typeName);
        }

        public static Type GetRuntimeType(string typeName) {
            FilterAssemblies();

            Type type = Type.GetType(typeName);

            if (type == null) {
                for (int i = 0; i < loadedTypes.Count; i++) {
                    if (loadedTypes[i].FullName.EndsWith(typeName)) {
                        type = loadedTypes[i];
                        break;
                    }
                }
            }

            return type;
        }

        public static Type GetStyleExportType(string typeName) {
            FilterAssemblies();

            Type type = Type.GetType(typeName);

            if (type == null) {
                for (int i = 0; i < loadedTypes.Count; i++) {
                    if (loadedTypes[i].Name.EndsWith(typeName)) {
                        type = loadedTypes[i];
                        break;
                    }
                }
            }

            return type;
        }

        public static ProcessedType GetType(Type type) {
            ProcessedType processedType = new ProcessedType(type);
            typeMap[type.Name] = processedType;
            return processedType;
        }

        private static bool FilterAssembly(Assembly assembly) {
            string name = assembly.FullName;

            if (assembly.IsDynamic ||
                name.StartsWith("System,") ||
                name.StartsWith("Accessibility") ||
                name.StartsWith("Boo") ||
                name.StartsWith("I18N") ||
                name.StartsWith("TextMeshPro") ||
                name.StartsWith("nunit") ||
                name.StartsWith("System.") ||
                name.StartsWith("Microsoft.") ||
                name.StartsWith("Mono") ||
                name.StartsWith("Unity.") ||
                name.StartsWith("ExCSS.") ||
                name.Contains("mscorlib") ||
                name.Contains("JetBrains") ||
                name.Contains("UnityEngine.") ||
                name.Contains("UnityEditor") ||
                name.Contains("Jetbrains")) {
                return false;
            }

            return name.IndexOf("-firstpass", StringComparison.Ordinal) == -1;
        }

        public static TypeData[] GetTemplateTypes() {
            if (templateTypes == null) {
                FilterAssemblies();
                List<Type> types = new List<Type>();
                for (int i = 0; i < loadedTypes.Count; i++) {
                    if (typeof(UIElement).IsAssignableFrom(loadedTypes[i])) {
//                        
//                        if (loadedTypes[i].Assembly.FullName.StartsWith("UIForia")) {
//                            continue;
//                        }

                        types.Add(loadedTypes[i]);
                    }
                }

                templateTypes = new TypeData[types.Count];
                for (int i = 0; i < templateTypes.Length; i++) {
                    templateTypes[i] = new TypeData(types[i]);
                    templateTypeMap.Add(templateTypes[i].tagName, new ProcessedType(templateTypes[i].type));
                }
            }

            return templateTypes;
        }

        public static void FindNamespace(string namespaceName) { }

        public static bool IsTypeName(string name) {
            return Type.GetType(name) != null;
        }

    }

}