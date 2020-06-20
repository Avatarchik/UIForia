using System;
using System.Collections.Generic;
using UIForia.Animation;
using UIForia.Exceptions;
using UIForia.Graphics;
using UIForia.Parsing.Style.AstNodes;
using UIForia.Rendering;
using UIForia.Sound;
using UIForia.Util;
using UnityEngine;

namespace UIForia.Compilers.Style {

    public class StyleSheetCompiler {

        private StyleCompileContext context;
        private readonly StyleSheetImporter styleSheetImporter;
        private ResourceManager resourceManager;

        private LightList<UIStyleGroup> scratchGroupList;

        private static readonly UIStyle s_ScratchStyle = new UIStyle();
        private Dictionary<string, StylePainterDefinition> painterCache;
        private StylePainterCompiler stylePainterCompiler;
        private int idGenerator;

        private int painterFunctionIdGenerator;

        private StructList<PainterVariableDeclaration> painterVariableBuffer;

        public StyleSheetCompiler(StyleSheetImporter styleSheetImporter, ResourceManager resourceManager) {
            this.styleSheetImporter = styleSheetImporter;
            this.resourceManager = resourceManager;
            this.painterCache = new Dictionary<string, StylePainterDefinition>();
            this.scratchGroupList = new LightList<UIStyleGroup>(32);
            this.painterVariableBuffer = new StructList<PainterVariableDeclaration>();
            this.idGenerator = 1000;
            this.stylePainterCompiler = new StylePainterCompiler();
        }

        // todo -- deprecate, use other method
        public StyleSheet Compile(string filePath, LightList<StyleASTNode> rootNodes, MaterialDatabase materialDatabase = default) {
            try {
                context = new StyleSheetConstantImporter(styleSheetImporter).CreateContext(rootNodes, materialDatabase);
                context.resourceManager = resourceManager;
            }
            catch (CompileException e) {
                e.SetFileName(filePath);
                throw;
            }

            context.fileName = filePath;

            // todo add imported style groups

            rootNodes.Sort((node1, node2) => {
                int left = (int) node1.type;
                int right = (int) node2.type;
                return left - right;
            });

            int containerCount = 0;
            int animationCount = 0;
            int soundCount = 0;

            for (int index = 0; index < rootNodes.Count; index++) {
                switch (rootNodes[index]) {
                    case StyleRootNode _:
                        containerCount++;
                        break;

                    case AnimationRootNode _:
                    case SpriteSheetNode _:
                        animationCount++;
                        break;

                    case SoundRootNode _:
                        soundCount++;
                        break;
                }
            }

            StyleSheet styleSheet = new StyleSheet(
                styleSheetImporter.ImportedStyleSheetCount,
                context.constants?.ToArray(),
                containerCount > 0
                    ? new UIStyleGroupContainer[containerCount]
                    : ArrayPool<UIStyleGroupContainer>.Empty,
                animationCount > 0
                    ? new AnimationData[animationCount]
                    : ArrayPool<AnimationData>.Empty,
                soundCount > 0
                    ? new UISoundData[soundCount]
                    : ArrayPool<UISoundData>.Empty
            );

            int containerIndex = 0;
            int animationIndex = 0;
            int soundIndex = 0;

            for (int index = 0; index < rootNodes.Count; index++) {
                switch (rootNodes[index]) {
                    // we sorted the root nodes so all animations run first
                    case SpriteSheetNode spriteSheetNode:
                        styleSheet.animations[animationIndex] = CompileSpriteSheetAnimation(spriteSheetNode, styleSheet.animations, styleSheet.sounds);
                        animationIndex++;
                        break;

                    case AnimationRootNode animNode:
                        styleSheet.animations[animationIndex] = CompileAnimation(animNode, styleSheet.animations, styleSheet.sounds);
                        animationIndex++;
                        break;

                    case SoundRootNode soundRootNode:
                        styleSheet.sounds[soundIndex] = CompileSound(soundRootNode);
                        soundIndex++;
                        break;

                    case StyleRootNode styleRoot:
                        styleSheet.styleGroupContainers[containerIndex] = CompileStyleGroup(styleRoot, styleSheet.animations, styleSheet.sounds);
                        styleSheet.styleGroupContainers[containerIndex].styleSheet = styleSheet;
                        containerIndex++;
                        break;

                    case PainterDefinitionNode painterNode:
                        CompilePainterNode(painterNode);
                        break;

                }
            }

            context.Release();
            return styleSheet;
        }

        private bool TryParsePainterVariableBlock(ref CharStream stream, out PainterVariableDeclaration[] painterVariables) {

            painterVariableBuffer.Clear();

            painterVariables = default;

            // type name = defaultValue;
            while (stream.HasMoreTokens) {

                stream.ConsumeWhiteSpaceAndComments();

                if (!stream.TryGetStreamUntil(';', out CharStream lineStream)) {
                    return false;
                }

                stream.Advance();
                stream.ConsumeWhiteSpaceAndComments();

                if (!lineStream.TryParseIdentifier(out CharSpan typeNameSpan, false)) {
                    return false;
                }

                if (!lineStream.TryParseIdentifier(out CharSpan propertyName, false)) {
                    return false;
                }

                if (HasDuplicateVariableName(painterVariableBuffer, propertyName)) {
                    return false;
                }

                if (!lineStream.TryParseCharacter('=')) {
                    return false;
                }

                // float, int, Color, Color32, material, texture2d, Vector3, Vector2, Matrix, string, enums

                string typeName = typeNameSpan.ToString();

                Type propertyType = null;

                if (typeName == "float") {
                    propertyType = typeof(float);
                    if (lineStream.TryParseFloat(out float floatValue)) {
                        painterVariableBuffer.Add(new PainterVariableDeclaration(propertyType, propertyName.ToString(), floatValue));
                    }
                }
                else if (typeName == "color") { }
                else if (typeName == "vector2") { }
                else if (typeName == "texture") { }
                else if (typeName == "int") { }
                else {
                    return false;
                }

            }

            painterVariables = painterVariableBuffer.ToArray();
            return true;
        }

        private static bool HasDuplicateVariableName(StructList<PainterVariableDeclaration> buffer, CharSpan propertyName) {
            for (int i = 0; i < buffer.size; i++) {
                if (buffer.array[i].name == propertyName) {
                    return true;
                }
            }

            return false;
        }

        private unsafe void CompilePainterNode(PainterDefinitionNode painterNode) {

            if (painterCache.ContainsKey(painterNode.painterName)) {
                throw new CompileException("Duplicate painter with name: " + painterNode.painterName);
            }

            PainterVariableDeclaration[] variables = null;

            bool hasVariables = false;
            bool hasbg = false;
            bool hasFg = false;

            int bgPainterId = -1;
            int fgPainterId = -1;

            string drawBgFn = null;
            string drawFgFn = null;

            fixed (char* ptr = painterNode.shapeBody) {
                CharStream stream = new CharStream(ptr, 0, (uint) painterNode.shapeBody.Length);

                while (stream.HasMoreTokens) {
                    uint start = stream.Ptr;

                    if (stream.TryMatchRangeIgnoreCase("[variables]")) {

                        if (hasVariables) {
                            throw new CompileException("Duplicate [variables] section in style painter");
                        }

                        hasVariables = true;

                        if (!stream.TryGetSubStream('{', '}', out CharStream bodyStream)) {
                            throw new CompileException("Expected a { } delimited body block after [variables]");
                        }

                        if (!TryParsePainterVariableBlock(ref bodyStream, out variables)) {
                            throw new CompileException("Failed to compile painter variable block");
                        }

                    }
                    else if (stream.TryMatchRangeIgnoreCase("[draw:background]")) {

                        if (hasbg) {
                            throw new CompileException("Duplicate [draw:background] section in style painter");
                        }

                        hasbg = true;
                        // want buffer this so we can handle variables first
                        if (!stream.TryGetSubStream('{', '}', out CharStream bodyStream)) {
                            throw new CompileException("Expected a { } delimited body block after [draw:background]");
                        }

                        bgPainterId = painterFunctionIdGenerator++;

                        CharSpan span = new CharSpan(bodyStream);
                        span = span.Trim();
                        drawBgFn = span.ToString();

                    }
                    else if (stream.TryMatchRangeIgnoreCase("[draw:foreground]")) {

                        if (hasFg) {
                            throw new CompileException("Duplicate [draw:foreground] section in style painter");
                        }

                        hasFg = true;
                        // want buffer this so we can handle variables first
                        if (!stream.TryGetSubStream('{', '}', out CharStream bodyStream)) {
                            throw new CompileException("Expected a { } delimited body block after [draw:foreground]");
                        }

                        fgPainterId = painterFunctionIdGenerator++;

                        CharSpan span = new CharSpan(bodyStream);
                        span = span.Trim();
                        drawFgFn = span.ToString();

                    }

                    stream.ConsumeWhiteSpaceAndComments();

                    if (start == stream.Ptr) {
                        throw new CompileException("Failed to parse painter " + painterNode.painterName);
                    }

                }

            }

            if (variables != null) {
                for (int i = 0; i < variables.Length; i++) {
                    variables[i].propertyId = idGenerator++;
                }
            }

            painterCache.Add(painterNode.painterName, new StylePainterDefinition() {
                definedVariables = variables
            });

            resourceManager.AddStylePainter(painterNode.painterName, new StylePainterDefinition() {
                fileName = context.fileName,
                painterName = painterNode.painterName,
                definedVariables = variables,
                drawBgSrc = drawBgFn,
                drawFgSrc = drawFgFn,
                backgroundFnIndex = bgPainterId,
                foregroundFnIndex = fgPainterId
            });

        }

        private AnimationData CompileSpriteSheetAnimation(SpriteSheetNode node, AnimationData[] styleSheetAnimations, UISoundData[] uiSoundData) {
            AnimationData data = new AnimationData();
            data.name = node.identifier;
            data.fileName = context.fileName;
            data.animationType = AnimationType.SpriteSheet;
            data.options = CompileSpriteSheetOptions(node);
            return data;
        }

        private AnimationData CompileAnimation(AnimationRootNode animNode, AnimationData[] styleSheetAnimations, UISoundData[] uiSoundData) {
            AnimationData data = new AnimationData();
            data.name = animNode.animName;
            data.fileName = context.fileName;
            data.animationType = AnimationType.KeyFrame;
            data.frames = CompileKeyFrames(animNode, styleSheetAnimations, uiSoundData);
            data.options = CompileAnimationOptions(animNode);
            return data;
        }

        private UISoundData CompileSound(SoundRootNode soundRootNode) {
            UISoundData data = CompileSoundProperties(soundRootNode);
            data.name = soundRootNode.identifier;
            data.styleSheetFileName = context.fileName;
            return data;
        }

        private unsafe StyleAnimationKeyFrame[] CompileKeyFrames(AnimationRootNode animNode, AnimationData[] styleSheetAnimations, UISoundData[] uiSoundData) {
            if (animNode.keyframeNodes == null) {
                // todo throw error or log warning?
                return new StyleAnimationKeyFrame[0];
            }

            int keyframeCount = 0;

            for (int i = 0; i < animNode.keyframeNodes.Count; i++) {
                keyframeCount += animNode.keyframeNodes[i].keyframes.Count;
            }

            StyleAnimationKeyFrame[] frames = new StyleAnimationKeyFrame[keyframeCount];

            int nextKeyframeIndex = 0;

            for (int i = 0; i < animNode.keyframeNodes.Count; i++) {
                KeyFrameNode keyFrameNode = animNode.keyframeNodes[i];

                // todo -- this is madness and not working, fix it!!!!!!!
                for (int j = 0; j < keyFrameNode.children.Count; j++) {
                    StyleASTNode keyFrameProperty = keyFrameNode.children[j];
                    if (keyFrameProperty is PropertyNode propertyNode) {
                        StylePropertyMappers.MapProperty(s_ScratchStyle, propertyNode, context);
                    }
                    else if (keyFrameProperty is MaterialPropertyNode materialPropertyNode) {

                        string materialName = materialPropertyNode.materialName;

                        if (context.materialDatabase.TryGetBaseMaterialId(materialName, out MaterialId materialId)) {

                            if (context.materialDatabase.TryGetMaterialProperty(materialId, materialPropertyNode.identifier, out MaterialPropertyInfo propertyInfo)) {

                                fixed (char* charptr = materialPropertyNode.value) {

                                    CharStream stream = new CharStream(charptr, 0, (uint) materialPropertyNode.value.Length);
                                    MaterialKeyFrameValue kfv = default;
                                    switch (propertyInfo.propertyType) {

                                        case MaterialPropertyType.Color:

                                            if (stream.TryParseColorProperty(out Color32 color)) {
                                                kfv = new MaterialKeyFrameValue(materialId, propertyInfo.propertyId, new MaterialPropertyValue2() {colorValue = color});
                                            }

                                            break;

                                        case MaterialPropertyType.Float:
                                            if (stream.TryParseFloat(out float floatVal)) {
                                                kfv = new MaterialKeyFrameValue(materialId, propertyInfo.propertyId, new MaterialPropertyValue2() {floatValue = floatVal});
                                            }

                                            break;

                                        case MaterialPropertyType.Vector:
                                            break;

                                        case MaterialPropertyType.Range:
                                            break;

                                        case MaterialPropertyType.Texture:
                                            break;

                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }

                                }
                            }

                        }

                    }
                }

                StructList<StyleKeyFrameValue> styleKeyValues = new StructList<StyleKeyFrameValue>(s_ScratchStyle.PropertyCount);

                for (int j = 0; j < s_ScratchStyle.PropertyCount; j++) {
                    styleKeyValues[j] = new StyleKeyFrameValue(s_ScratchStyle[j]);
                }

                styleKeyValues.size = s_ScratchStyle.PropertyCount;

                for (int keyframeIndex = 0; keyframeIndex < keyFrameNode.keyframes.Count; keyframeIndex++) {
                    float time = keyFrameNode.keyframes[keyframeIndex] / 100f;

                    frames[nextKeyframeIndex] = new StyleAnimationKeyFrame(time) {
                        properties = styleKeyValues
                    };

                    nextKeyframeIndex++;
                }

                s_ScratchStyle.PropertyCount = 0;
            }

            return frames;
        }

        private AnimationOptions CompileAnimationOptions(AnimationRootNode animNode) {
            AnimationOptions options = new AnimationOptions();

            if (animNode.optionNodes == null) {
                return options;
            }

            LightList<AnimationOptionNode> optionNodes = animNode.optionNodes;
            if (optionNodes == null) {
                return options;
            }

            for (int i = 0; i < optionNodes.Count; i++) {
                string optionName = optionNodes[i].optionName.ToLower();
                StyleASTNode value = optionNodes[i].value;

                switch (optionName) {
                    case "duration":
                        options.duration = StylePropertyMappers.MapUITimeMeasurement(value, context);
                        break;

                    case "iterations":
                        options.iterations = (int) StylePropertyMappers.MapNumberOrInfinite(value, context);
                        break;

                    case "looptime":
                        options.loopTime = StylePropertyMappers.MapNumber(value, context);
                        break;

                    case "delay":
                        options.delay = StylePropertyMappers.MapUITimeMeasurement(value, context);
                        break;

                    case "direction":
                        options.direction = StylePropertyMappers.MapEnum<AnimationDirection>(value, context);
                        break;

                    case "looptype":
                        options.loopType = StylePropertyMappers.MapEnum<AnimationLoopType>(value, context);
                        break;

                    case "forwardstartdelay":
                        options.forwardStartDelay = (int) StylePropertyMappers.MapNumber(value, context);
                        break;

                    case "reversestartdelay":
                        options.reverseStartDelay = (int) StylePropertyMappers.MapNumber(value, context);
                        break;

                    case "timingfunction":
                        options.timingFunction = StylePropertyMappers.MapEnum<EasingFunction>(value, context);
                        break;

                    default:
                        throw new CompileException(optionNodes[i], "Invalid option argument for animation");
                }
            }

            return options;
        }

        private AnimationOptions CompileSpriteSheetOptions(SpriteSheetNode node) {
            AnimationOptions options = new AnimationOptions();

            LightList<StyleASTNode> spriteSheetProperties = node.children;
            if (spriteSheetProperties == null) {
                return options;
            }

            for (int i = 0; i < spriteSheetProperties.Count; i++) {
                if (spriteSheetProperties[i] is PropertyNode property) {
                    string optionName = property.identifier.ToLower();
                    StyleASTNode value = property.children[0];

                    switch (optionName) {
                        case "iterations":
                            options.iterations = (int) StylePropertyMappers.MapNumberOrInfinite(value, context);
                            break;

                        case "delay":
                            options.delay = StylePropertyMappers.MapUITimeMeasurement(value, context);
                            break;

                        case "duration":
                            options.duration = StylePropertyMappers.MapUITimeMeasurement(value, context);
                            break;

                        case "looptype":
                            options.loopType = StylePropertyMappers.MapEnum<AnimationLoopType>(value, context);
                            break;

                        case "direction":
                            options.direction = StylePropertyMappers.MapEnum<AnimationDirection>(value, context);
                            break;

                        case "forwardstartdelay":
                            options.forwardStartDelay = (int) StylePropertyMappers.MapNumber(value, context);
                            break;

                        case "reversestartdelay":
                            options.reverseStartDelay = (int) StylePropertyMappers.MapNumber(value, context);
                            break;

                        case "fps":
                            options.fps = (int) StylePropertyMappers.MapNumber(value, context);
                            break;

                        case "startframe":
                            options.startFrame = (int) StylePropertyMappers.MapNumber(value, context);
                            break;

                        case "endframe":
                            options.endFrame = (int) StylePropertyMappers.MapNumber(value, context);
                            break;

                        case "pathprefix":
                            options.pathPrefix = StylePropertyMappers.MapString(value, context);
                            break;

                        default:
                            throw new CompileException(property, "Invalid option argument for animation");
                    }
                }
                else {
                    throw new CompileException(spriteSheetProperties[i], "Invalid option argument for animation");
                }
            }

            return options;
        }

        private UISoundData CompileSoundProperties(SoundRootNode soundRootNode) {
            UISoundData soundData = new UISoundData();
            // set defaults
            soundData.duration = new UITimeMeasurement(1, UITimeMeasurementUnit.Percentage);
            soundData.pitch = 1;
            soundData.iterations = 1;
            soundData.tempo = 1;

            for (int i = 0; i < soundRootNode.children.Count; i++) {
                StyleASTNode property = soundRootNode.children[i];
                if (property is SoundPropertyNode soundPropertyNode) {
                    StyleASTNode value = soundPropertyNode.value;
                    switch (soundPropertyNode.name.ToLower()) {
                        case "asset":
                            soundData.asset = StylePropertyMappers.MapString(value, context);
                            break;

                        case "duration":
                            soundData.duration = StylePropertyMappers.MapUITimeMeasurement(value, context);
                            break;

                        case "iterations":
                            soundData.iterations = (int) StylePropertyMappers.MapNumberOrInfinite(value, context);
                            break;

                        case "pitch":
                            soundData.pitch = StylePropertyMappers.MapNumber(value, context);
                            break;

                        case "pitchrange":
                            soundData.pitchRange = StylePropertyMappers.MapFloatRange(value, context);
                            break;

                        case "tempo":
                            soundData.tempo = StylePropertyMappers.MapNumber(value, context);
                            break;

                        case "volume":
                            soundData.volume = StylePropertyMappers.MapNumber(value, context);
                            break;

                        case "mixergroup":
                            soundData.mixerGroup = StylePropertyMappers.MapString(value, context);
                            break;
                    }
                }
                else {
                    throw new CompileException(property, "Expected a sound property.");
                }
            }

            return soundData;
        }

        private UIStyleGroupContainer CompileStyleGroup(StyleRootNode styleRoot, AnimationData[] styleSheetAnimations, UISoundData[] uiSoundData) {
            UIStyleGroup defaultGroup = new UIStyleGroup();
            defaultGroup.normal = UIStyleRunCommand.CreateInstance();
            defaultGroup.name = styleRoot.identifier ?? styleRoot.tagName;
            StyleType styleType = styleRoot.tagName != null ? StyleType.Implicit : StyleType.Shared;

            scratchGroupList.size = 0;

            scratchGroupList.Add(defaultGroup);

            CompileStyleGroups(styleRoot, styleType, scratchGroupList, defaultGroup, styleSheetAnimations, uiSoundData);

            return new UIStyleGroupContainer(styleSheetImporter.NextStyleGroupId, defaultGroup.name, styleType, scratchGroupList.ToArray());
        }

        private void CompileStyleGroups(StyleNodeContainer root, StyleType styleType, LightList<UIStyleGroup> groups, UIStyleGroup targetGroup, AnimationData[] styleSheetAnimations, UISoundData[] uiSoundData) {
            for (int index = 0; index < root.children.Count; index++) {
                StyleASTNode node = root.children[index];
                switch (node) {

                    case PainterPropertyNode painterPropertyNode: {

                        if (painterCache.TryGetValue(painterPropertyNode.painterName, out StylePainterDefinition painterDefinition)) {
                            StylePropertyMappers.MapPainterProperty(targetGroup.normal.style, painterPropertyNode, painterDefinition);
                        }
                        else {
                            throw new CompileException($"Unable to find a painter with name: {painterPropertyNode.painterName}. If the painter exists, make sure its imported.");
                        }

                        break;
                    }

                    case PropertyNode propertyNode:
                        // add to normal ui style set
                        StylePropertyMappers.MapProperty(targetGroup.normal.style, propertyNode, context);
                        break;

                    case AttributeNodeContainer attribute:
                        if (root is AttributeNodeContainer) {
                            throw new CompileException(attribute, "You cannot nest attribute group definitions.");
                        }

                        UIStyleGroup attributeGroup = new UIStyleGroup();
                        attributeGroup.normal = UIStyleRunCommand.CreateInstance();
                        attributeGroup.name = root.identifier;
                        attributeGroup.rule = MapAttributeContainerToRule(attribute);
                        attributeGroup.styleType = styleType;
                        groups.Add(attributeGroup);
                        CompileStyleGroups(attribute, styleType, groups, attributeGroup, styleSheetAnimations, uiSoundData);

                        break;

                    case RunNode runNode:
                        UIStyleRunCommand cmd = new UIStyleRunCommand() {
                            style = targetGroup.normal.style,
                            runCommands = targetGroup.normal.runCommands ?? new LightList<IRunCommand>(4)
                        };

                        if (runNode.command is AnimationCommandNode animationCommandNode) {
                            MapAnimationCommand(styleSheetAnimations, cmd, animationCommandNode);
                        }
                        else if (runNode.command is SoundCommandNode soundCommandNode) {
                            MapSoundCommand(uiSoundData, cmd, soundCommandNode);
                        }

                        targetGroup.normal = cmd;
                        break;

                    case StyleStateContainer styleContainer:
                        if (styleContainer.identifier == "hover") {
                            UIStyleRunCommand uiStyleRunCommand = targetGroup.hover;
                            uiStyleRunCommand.style = uiStyleRunCommand.style ?? new UIStyle();
                            MapProperties(styleSheetAnimations, uiSoundData, ref uiStyleRunCommand, styleContainer.children);
                            targetGroup.hover = uiStyleRunCommand;
                        }
                        else if (styleContainer.identifier == "focus") {
                            UIStyleRunCommand uiStyleRunCommand = targetGroup.focused;
                            uiStyleRunCommand.style = uiStyleRunCommand.style ?? new UIStyle();
                            MapProperties(styleSheetAnimations, uiSoundData, ref uiStyleRunCommand, styleContainer.children);
                            targetGroup.focused = uiStyleRunCommand;
                        }
                        else if (styleContainer.identifier == "active") {
                            UIStyleRunCommand uiStyleRunCommand = targetGroup.active;
                            uiStyleRunCommand.style = uiStyleRunCommand.style ?? new UIStyle();
                            MapProperties(styleSheetAnimations, uiSoundData, ref uiStyleRunCommand, styleContainer.children);
                            targetGroup.active = uiStyleRunCommand;
                        }
                        else throw new CompileException(styleContainer, $"Unknown style state '{styleContainer.identifier}'. Please use [hover], [focus] or [active] instead.");

                        break;

                    default:
                        throw new CompileException(node, $"You cannot have a {node} at this level.");
                }
            }
        }

        private void MapAnimationCommand(AnimationData[] styleSheetAnimations, UIStyleRunCommand cmd, AnimationCommandNode animationCommandNode) {
            cmd.runCommands.Add(MapAnimationRunCommand(styleSheetAnimations, animationCommandNode));
        }

        private AnimationRunCommand MapAnimationRunCommand(AnimationData[] styleSheetAnimations, AnimationCommandNode animationCommandNode) {
            return new AnimationRunCommand(animationCommandNode.cmdType, animationCommandNode.runAction) {
                animationData = FindAnimationData(styleSheetAnimations, animationCommandNode.animationName),
            };
        }

        private void MapSoundCommand(UISoundData[] soundData, UIStyleRunCommand cmd, SoundCommandNode soundCommandNode) {
            cmd.runCommands.Add(MapSoundRunCommand(soundData, soundCommandNode));
        }

        private SoundRunCommand MapSoundRunCommand(UISoundData[] soundData, SoundCommandNode soundCommandNode) {
            return new SoundRunCommand(soundCommandNode.cmdType, soundCommandNode.runAction) {
                soundData = FindSoundData(soundData, soundCommandNode.name),
            };
        }

        private AnimationData FindAnimationData(in AnimationData[] animations, StyleASTNode animationName) {
            for (int index = 0; index < animations.Length; index++) {
                AnimationData animation = animations[index];
                StyleASTNode value = context.GetValueForReference(animationName);
                if (value is StyleIdentifierNode identifier) {
                    if (animation.name == identifier.name) {
                        return animation;
                    }
                }
                else {
                    throw new CompileException(animationName, $"Could not find an animation with that name or reference: {animationName}");
                }
            }

            throw new CompileException(animationName, $"Could not find an animation with that name or reference: {animationName}");
        }

        private UISoundData FindSoundData(in UISoundData[] soundData, StyleASTNode name) {
            for (int index = 0; index < soundData.Length; index++) {
                UISoundData sound = soundData[index];
                StyleASTNode value = context.GetValueForReference(name);
                if (value is StyleIdentifierNode identifier) {
                    if (sound.name == identifier.name) {
                        return sound;
                    }
                }
                else {
                    throw new CompileException(name, $"Could not find an sound with that name or reference: {name}");
                }
            }

            throw new CompileException(name, $"Could not find an sound with that name or reference: {name}");
        }

        private void MapProperties(AnimationData[] animations, UISoundData[] soundData, ref UIStyleRunCommand targetStyle, LightList<StyleASTNode> styleContainerChildren) {
            for (int i = 0; i < styleContainerChildren.Count; i++) {
                StyleASTNode node = styleContainerChildren[i];
                switch (node) {
                    case PropertyNode propertyNode:
                        // add to normal ui style set
                        StylePropertyMappers.MapProperty(targetStyle.style, propertyNode, context);
                        break;

                    case RunNode runNode:
                        targetStyle.runCommands = targetStyle.runCommands ?? new LightList<IRunCommand>(4);
                        if (runNode.command is AnimationCommandNode animationCommandNode) {
                            MapAnimationCommand(animations, targetStyle, animationCommandNode);
                        }
                        else if (runNode.command is SoundCommandNode soundCommandNode) {
                            MapSoundCommand(soundData, targetStyle, soundCommandNode);
                        }

                        break;

                    default:
                        throw new CompileException(node, $"You cannot have a {node} at this level.");
                }
            }
        }

        private UIStyleRule MapAttributeContainerToRule(ChainableNodeContainer nodeContainer) {

            if (nodeContainer == null) return null;

            if (nodeContainer is AttributeNodeContainer attribute) {
                return new UIStyleRule(attribute.invert, attribute.identifier, attribute.value, MapAttributeContainerToRule(attribute.next));
            }

            throw new NotImplementedException("Sorry this feature experiences a slight delay.");
        }

    }

}