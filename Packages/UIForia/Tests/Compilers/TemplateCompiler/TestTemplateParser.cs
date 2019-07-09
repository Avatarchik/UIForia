using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Xml;
using System.Xml.Linq;
using Mono.Linq.Expressions;
using NUnit.Framework;
using Tests;
using Tests.Mocks;
using UIForia;
using UIForia.Attributes;
using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Parsing.Expression;
using UnityEngine;
using Application = UIForia.Application;

[TestFixture]
public class TestTemplateParser {

    [Test]
    public void ParseTemplate() {
        NameTable nameTable = new NameTable();

        XmlNamespaceManager nameSpaceManager = new XmlNamespaceManager(nameTable);

        nameSpaceManager.AddNamespace("attr", "attr");
        nameSpaceManager.AddNamespace("evt", "evt");

        XmlParserContext parserContext = new XmlParserContext(null, nameSpaceManager, null, XmlSpace.None);

        XmlTextReader txtReader = new XmlTextReader(@"<Contents><X/><Thing attr:thing=""someattr""/></Contents>", XmlNodeType.Element, parserContext);

        XElement elem = XElement.Load(txtReader);

        Assert.AreEqual("thing", (elem.FirstNode as XElement).FirstAttribute.Name.LocalName);
        Assert.AreEqual("attr", (elem.FirstNode as XElement).FirstAttribute.Name.NamespaceName);
    }

    [Test]
    public void CompileTemplate() {
        XMLTemplateParser parser = new XMLTemplateParser(new MockApplication(typeof(InputSystem_DragTests.DragTestThing)));
        parser.Parse(@"
            <UITemplate>
                <Content>
                    <Thing thing=""someattr""/>
                </Content>
            </UITemplate>
        ");

        TemplateCompiler compiler = new TemplateCompiler();
    }

    [Template(TemplateType.String, @"
    <UITemplate>
        <Content>

            <Div attr:id='hello0'/>
            <Div attr:id='hello1'/>
            <Div attr:id='hello2'/>

        </Content>
    </UITemplate>
    ")]
    private class CompileTestElement : UIElement { }

    private class CompileTestChildElement : UIElement {

        public float floatProperty;

    }

    [Test]
    public void ParseTemplate2() {
        
        TemplateCompiler compiler = new TemplateCompiler();
        XMLTemplateParser parser = new XMLTemplateParser(MockApplication.CreateWithoutView());

        TemplateAST ast = parser.Parse(typeof(CompileTestElement));

        CompiledTemplate template = compiler.Compile(ast);

    }
    
    [Test]
    public void CompileTemplate_GenerateAttributes() {
        TemplateCompiler compiler = new TemplateCompiler();

        compiler.application = MockApplication.CreateWithoutView();

        CompiledTemplate result = compiler.Compile(new TemplateAST() {
            root = new TemplateNode() {
                typeLookup = new TypeLookup("CompileTestChildElement"),
                attributes = new[] {
                    new AttributeDefinition2(AttributeType.Attribute, 0, "someAttr", "someAttrValue"),
                    new AttributeDefinition2(AttributeType.Property, 0, "floatProperty", "5 * 12"),
                },
                children = new[] {
                    new TemplateNode() {
                        typeLookup = new TypeLookup("CompileTestChildElement"),
                    },
                    new TemplateNode() {
                        typeLookup = new TypeLookup("CompileTestChildElement"),
                        attributes = new[] {
                            new AttributeDefinition2(AttributeType.Attribute, 0, "someAttr", "someAttrValue"),
                            new AttributeDefinition2(AttributeType.Attribute, 0, "someAttr1", "someAttrValue1"),
                            new AttributeDefinition2(AttributeType.Attribute, 0, "someAttr2", "someAttrValue2"),
                        },
                        children = new[] {
                            new TemplateNode() {
                                typeLookup = new TypeLookup("CompileTestChildElement"),
                                attributes = new[] {
                                    new AttributeDefinition2(AttributeType.Property, 0, "floatProperty", "5 * 12"),
                                },
                            }
                        }
                    }
                }
            }
        });

        LogCode(result.buildExpression);
    }

    private static string PrintCode(IList<Expression> expressions) {
        string retn = "";
        for (int i = 0; i < expressions.Count; i++) {
            retn += expressions[i].ToCSharpCode();
            if (i != expressions.Count - 1) {
                retn += "\n";
            }
        }

        return retn;
    }

    private static string PrintCode(Expression expression) {
        return expression.ToCSharpCode();
    }

    private static void LogCode(Expression expression) {
        Debug.Log(expression.ToCSharpCode());
    }

}