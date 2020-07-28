﻿using UIForia.Util;
using Unity.Mathematics;
using UnityEngine;

namespace UIForia.Text {

    public static class TextProcessors {

        public static RichTextProcessor RichText = new RichTextProcessor();

    }

    public class RichTextProcessor : ITextProcessor {

        public bool Process(CharStream stream, ref TextSymbolStream textSymbolStream) {

            while (stream.HasMoreTokens) {

                uint start = stream.Ptr;

                if (stream != '[') {
                    textSymbolStream.AddCharacter(stream.Current);
                    stream.Advance();
                }
                else if (stream.Previous == '\\') {
                    textSymbolStream.AddCharacter(stream.Current);
                    stream.Advance();
                }
                else if (stream.Next == '/') {
                    stream.Advance(2);

                    if (stream.TryGetStreamUntil(']', out CharStream bodyStream, '\\')) {
                        stream.Advance(); // step of ']'
                        if (bodyStream.TryParseIdentifier(out CharSpan tag)) {

                            if (tag == "color") {

                                textSymbolStream.PopColor();
                                continue;
                            }

                            if (tag == "size") {
                                textSymbolStream.PopFontSize();
                                continue;
                            }

                            if (tag == "nobreak") {
                                textSymbolStream.PopNoBreak();
                                continue;
                            }

                            if (tag == "opacity") {
                                textSymbolStream.PopOpacity();
                                continue;
                            }
                            
                            if (tag == "uppercase") {
                                // maybe only pop when uppercase is set?
                                textSymbolStream.PopTextTransform();
                                continue;
                            }
                            if (tag == "lowercase") {
                                // maybe only pop when lowercase  is set?
                                textSymbolStream.PopTextTransform();
                                continue;
                            }
                            if (tag == "titlecase") {
                                // maybe only pop when titlecase is set?
                                textSymbolStream.PopTextTransform();
                                continue;
                            }
                            
                            if (tag == "cspace") {
                                textSymbolStream.PopCharacterSpacing();
                                continue;
                            }

                            if (tag == "hinvert") {
                                textSymbolStream.PopHorizontalInvert();
                                continue;
                            }

                            if (tag == "vinvert") {
                                textSymbolStream.PopVerticalInvert();
                                continue;
                            }

                        }
                    }

                    stream.RewindTo(start + 1);
                    textSymbolStream.AddCharacter(stream.Current);

                  
                }
                else {

                    if (stream.TryGetStreamUntil(']', out CharStream bodyStream, '\\')) {
                        stream.Advance();
                        bodyStream.Advance(); // step over '['
                        if (bodyStream.TryParseIdentifier(out CharSpan tag)) {

                            if (tag == "color") {

                                bodyStream.TryParseCharacter('=');

                                if (bodyStream.TryParseColorProperty(out Color32 color)) {
                                    textSymbolStream.PushColor(color);
                                    continue;
                                }

                            }
                            else if (tag == "size") {
                                bodyStream.TryParseCharacter('=');
                                if (bodyStream.TryParseFloat(out float size)) {
                                    textSymbolStream.PushFontSize(size);
                                    continue;
                                }
                            }
                            else if (tag == "hspace") {
                                bodyStream.TryParseCharacter('=');
                                if (bodyStream.TryParseFixedLength(out UIFixedLength size, true)) {
                                    textSymbolStream.HorizontalSpace(size);
                                    continue;
                                }
                            }
                            else if(tag == "opacity") {
                                bodyStream.TryParseCharacter('=');
                                if (bodyStream.TryParseFloat(out float value)) {
                                    value = math.clamp(value, 0, 1);
                                    textSymbolStream.PushOpacity(value);
                                    continue;
                                }
                            }
                            else if (tag == "nobreak") {
                                textSymbolStream.PushNoBreak();
                                continue;
                            }
                            else if (tag == "cspace") {
                                bodyStream.TryParseCharacter('=');
                                if (bodyStream.TryParseFixedLength(out UIFixedLength size, true)) {
                                    textSymbolStream.PushCharacterSpacing(size);
                                    continue;
                                }
                            }
                            else if (tag == "uppercase") {
                                textSymbolStream.PushTextTransform(TextTransform.UpperCase);
                                continue;
                            }
                            else if (tag == "lowercase") {
                                textSymbolStream.PushTextTransform(TextTransform.LowerCase);
                                continue;
                            }
                            else if (tag == "titlecase") {
                                textSymbolStream.PushTextTransform(TextTransform.TitleCase);
                                continue;
                            }
                            else if (tag == "hinvert") {
                                textSymbolStream.PushHorizontalInvert();
                                continue;
                            }
                            else if (tag == "vinvert") {
                                textSymbolStream.PushVerticalInvert();
                                continue;
                            }
                        }
                    }

                    stream.RewindTo(start + 1);
                    textSymbolStream.AddCharacter(stream.Current);

                    // if (stream.TryMatchRange("nobreak]")) {
                    //     textSymbolStream.PushNoBreak();
                    // }
                    // else if (stream.TryMatchRange("color")) { }
                    // else if (stream.TryMatchRange("outline")) { }
                    // else if (stream.TryMatchRange("fx:")) {
                    //     if (stream.TryParseIdentifier(out CharSpan identifier)) {
                    //         // if (fxParserMap.TryGetValue(identifier)) {
                    //         //     if (fxParser.TryParse(ref stream)) {
                    //         //         // if current stream.matchesSoFar
                    //         //         // var instance = textSystem.effectTable[currentStream[i].effectId];
                    //         //         // instance.ParseUpdate(value);
                    //         //         var data = fxParser.TryParse(data);
                    //         //         textSymbolStream.PushTextEffect(data);
                    //         //     }
                    //         // }
                    //     }
                    // }
                    // else if (stream.TryMatchRange("size") && stream.TryParseCharacter('=')) {
                    //
                    //     //[size=3.4em]
                    //     //[size=46px]
                    //
                    //     if (stream.TryParseFixedLength(out UIFixedLength value, true) && stream.TryParseCharacter(']')) {
                    //         textSymbolStream.PushFontSize(value);
                    //         continue;
                    //     }
                    //
                    //     stream.RewindTo(start + 1);
                    //     textSymbolStream.AddCharacter(stream.Current);
                    // }
                    // else if (stream.TryMatchRange("uppercase]")) {
                    //     textSymbolStream.PushTextTransform(TextTransform.UpperCase);
                    // }
                    // else if (stream.TryMatchRange("titlecase]")) {
                    //     textSymbolStream.PushTextTransform(TextTransform.TitleCase);
                    // }
                    // else if (stream.TryMatchRange("lowercase]")) {
                    //     textSymbolStream.PushTextTransform(TextTransform.LowerCase);
                    // }
                    // else {
                    //     stream.RewindTo(start + 1);
                    //     textSymbolStream.AddCharacter(stream.Current);
                    // }
                }
            }

            return true;

        }

    }

}