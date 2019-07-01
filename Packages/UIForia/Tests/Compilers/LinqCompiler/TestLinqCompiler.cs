using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UIForia.Bindings;
using UIForia.Compilers;
using UIForia.Elements;
using UIForia.Exceptions;
using UIForia.Expressions;
using UIForia.Extensions;
using UIForia.Parsing.Expression;
using UIForia.Parsing.Expression.AstNodes;
using UIForia.UIInput;
using UnityEngine;
using Expression = System.Linq.Expressions.Expression;

[TestFixture]
public class TestLinqCompiler {

    private class LinqThing {

        public float floatValue;

        public ValueHolder<Vector3> refValueHolderVec3 = new ValueHolder<Vector3>();
        public ValueHolder<float> valueHolderFloat = new ValueHolder<float>();
        public ValueHolder<ValueHolder<Vector3>> nestedValueHolder = new ValueHolder<ValueHolder<Vector3>>();
        public StructValueHolder<Vector3> svHolderVec3;
        public Vector3[] vec3Array;
        public List<Vector3> vec3List;
        public int intVal;
        public Dictionary<string, Vector3> vec3Dic;

    }

    public struct StructValueHolder<T> {

        public T value;

        public StructValueHolder(T value) {
            this.value = value;
        }

    }

    public class ValueHolder<T> {

        public T value;

        public ValueHolder(T value = default) {
            this.value = value;
        }

    }

    private static readonly Type LinqType = typeof(LinqThing);

    public abstract class LinqBinding {

        public abstract void Execute(ExpressionContext ctx);

    }

    public class ReadBinding : LinqBinding {

        public override void Execute(ExpressionContext ctx) { }

    }

    public class BindingCompiler : LinqCompiler {

        public LambdaExpression BuildMemberReadBinding(Type root, Type elementType, AttributeDefinition attributeDefinition) {
            LinqCompiler compiler = new LinqCompiler();

            MethodInfo[] changedHandlers = GetPropertyChangedHandlers(elementType, "fieldName");

            compiler.AddParameter(root, "root", ParameterFlags.Implicit | ParameterFlags.NeverNull);
            compiler.AddParameter(elementType, "element");

            LHSStatementChain left = compiler.CreateLHSStatementChain("element", attributeDefinition.key);
            RHSStatementChain right = compiler.CreateRHSStatementChain(left.targetExpression.Type, attributeDefinition.value);

            // if no listeners and field or auto prop then just assign, no need to check
            //compiler.Assign(left, Expression.Constant(34f));

            compiler.IfNotEqual(left, right, () => {
                compiler.Assign(left, right);
//
                if (changedHandlers != null) {
                    for (int i = 0; i < changedHandlers.Length; i++) {
                        //compiler.Invoke(rootParameter, changedHandlers[i], compiler.GetVariable("previousValue"));
                    }
                }

                if (elementType.Implements(typeof(IPropertyChangedHandler))) {
                    //compiler.Invoke("element", "OnPropertyChanged", compiler.GetVariable("currentValue"));
                }
            });

            UnityEngine.Debug.Log(PrintCode(compiler.BuildLambda()));
            return compiler.BuildLambda();
        }

        public LinqBinding CompileMemberReadBinding(Type root, Type elementType, AttributeDefinition attributeDefinition) {
            return null; //BuildMemberReadBinding(root, elementType, attributeDefinition).Compile();
        }

        private MethodInfo[] GetPropertyChangedHandlers(Type targetType, string fieldname) {
            return null;
        }

    }

    // todo -- test bad enum values
    // todo -- test bad constant values
    // todo -- test missing fields & properties
    // todo -- test missing type paths
    // todo -- test valid type path with invalid generic
    // todo -- test non public fields
    // todo -- test non public properties
    // todo -- test non public static fields & properties
    // todo -- test list initializer
    // todo -- test splat operator
    // todo -- test alias identifiers
    // todo -- test alias methods
    // todo -- test alias indexers
    // todo -- test alias constructors
    // todo -- test alias splat
    // todo -- test alias list initializer
    // todo -- test initializer syntax { x: 4 }
    // todo -- test falsy bool handling


    private class TestElement : UIElement {

        public void HandleValueChanged(string value, int idx) { }

    }

    // bindings need to come from some factory so they can be either shared not not. 
    
    // some bindings will want context such as repeats
    
    // some bindings will want their own instances so they can store contextual data
    
    // interface for bindings

    // bindings can be aggressively pooled
    // bindings can invoke their functions with whatever parameters they like
    
    // context
    // closures 
    // events
    // callbacks
    // enable / disable
    // run enable when element disabled
    
    public class Binding {

        public string id;
        public Binding parent;
        public Binding nextSibling;
        public Binding firstChild;

        private Action<UIElement, UIElement> fn;
        
        public virtual void OnEnable() { }

        public virtual void OnDisable() { }

        public virtual void OnElementChanged() { }
        
        public virtual void OnElementDestroyed() { }

        public virtual void Execute(UIElement root, UIElement current) {
            fn(root, current);
        }

    }
    
    //for non const actions probably can't share this binding since we need to know the last action and compare it with the new one

    // need a factory that generates a closure over my arguments
    // need a special instance of binding node or another closure to invoke that factory

    private class CallbackBinding : Binding {

        
        private Action<string, int> previous;
        private Action<string> previousOuter;

        private Func<UIElement, Action<string>> factory => (UIElement el) => { return (string value) => { ((TestElement) el).HandleValueChanged(value, 0); }; };

        private Action<Select<string>, TestElement> binding;

        private void SetBinding() {
            binding = (Select<string> selectElement, TestElement root) => {
                Action<string, int> a = root.HandleValueChanged;
                // do work and check if not constant

                // ... run expression body here

                if (a != previous) {
                    selectElement.onValueChanged -= previousOuter;
                    previousOuter = factory(root);
                    selectElement.onValueChanged += previousOuter;
                }
            };
        }
        

    }

    [Test]
    public void CompileClosure() {
        LinqCompiler compiler = new LinqCompiler();
//        compiler.AddParameter(typeof(LinqThing), "root");
//        // <Element onValueChanged="(evt) => HandleValueChanged($evt.arg0, 4f)"/>
//        compiler.ReturnStatement(compiler.CreateRHSStatementChain("(evt, value) => root.svHolderVec3.value.z"));
//        Action<UIElement, UIElement> action = compiler.Compile<Action<UIElement, UIElement>>();

        void Compile<T, U>() {
            var ps = Expression.Parameter(typeof(Select<T>), "s");
            var pt = Expression.Parameter(typeof(int), "t");

            var ex2 = Expression.Lambda(
                Expression.Quote(
                    Expression.Lambda(
                        Expression.Block(
                            typeof(void),
                            Expression.Add(ps, pt), pt)
                    )
                ),
                ps);
            Debug.Log(PrintCode(ex2));
        }


        Select<string> selectElementString = new Select<string>();
        TestElement testElement = new TestElement();

        selectElementString.onValueChanged += (s) => testElement.HandleValueChanged(s, 0);

//        var f2a = (Func<int, Expression<Func<int, int>>>)ex2.Compile();
//        var f2b = f2a(200).Compile();
    }

    [Test]
    public void CompileReadFromValueChain() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.AddParameter(typeof(LinqThing), "root");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("root.svHolderVec3.value.z"));
        compiler.SetReturnType(typeof(float));
        var expression = compiler.BuildLambda<Func<LinqThing, float>>();
        var fn = compiler.Compile<Func<LinqThing, float>>();
        var thing = new LinqThing();
        thing.svHolderVec3.value.z = 12;
        Assert.AreEqual(12, fn(thing));
        UnityEngine.Debug.Log(PrintCode(expression));
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root) => 
        {
            return root.svHolderVec3.value.z;
        }
        ", PrintCode(expression));
    }

    [Test]
    public void CompileReadFromValueWithNullChecksChain() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.AddParameter(typeof(LinqThing), "root");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("root.refValueHolderVec3.value.z"));
        compiler.SetReturnType(typeof(float));
        var expression = compiler.BuildLambda<Func<LinqThing, float>>();
        Debug.Log(PrintCode(expression));
        var fn = compiler.Compile<Func<LinqThing, float>>();
        var thing = new LinqThing();
        thing.refValueHolderVec3 = new ValueHolder<Vector3>();
        thing.refValueHolderVec3.value.z = 12;
        Assert.AreEqual(12, fn(thing));
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root) =>
        {
            TestLinqCompiler.ValueHolder<UnityEngine.Vector3> nullCheck;
            float rhsOutput;

            rhsOutput = default(float);
            nullCheck = root.refValueHolderVec3;
            if (nullCheck == null)
            {
                goto retn;
            }
            rhsOutput = nullCheck.value.z;
        retn:
            return rhsOutput;
        }
        ", PrintCode(expression));

        compiler.Reset();
        compiler.AddParameter(typeof(LinqThing), "root");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("root.nestedValueHolder.value.value.z"));
        compiler.SetReturnType(typeof(float));
        expression = compiler.BuildLambda<Func<LinqThing, float>>();
        Debug.Log(PrintCode(expression));
        fn = compiler.Compile<Func<LinqThing, float>>();
        thing.nestedValueHolder.value = new ValueHolder<Vector3>();
        thing.nestedValueHolder.value.value = new Vector3(10, 11, 12);
        Assert.AreEqual(12, fn(thing));
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root) =>
        {
            TestLinqCompiler.ValueHolder<TestLinqCompiler.ValueHolder<UnityEngine.Vector3>> nullCheck;
            TestLinqCompiler.ValueHolder<UnityEngine.Vector3> nullCheck0;
            float rhsOutput;

            rhsOutput = default(float);
            nullCheck = root.nestedValueHolder;
            if (nullCheck == null)
            {
                goto retn;
            }
            nullCheck0 = nullCheck.value;
            if (nullCheck0 == null) 
            {
                goto retn;
            }
            rhsOutput = nullCheck0.value.z;
        retn:
            return rhsOutput;
        }
        ", PrintCode(expression));
    }

    [Test]
    public void CompileSimpleMemberRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("floatValue", "4f"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        Assert.AreEqual(0, element.floatValue);
        fn.Invoke(root, element);
        Assert.AreEqual(4, element.floatValue);
    }

    [Test]
    public void CompileDotAccessRefMemberRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // todo handle implicit conversion casting
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("floatValue", "valueHolderFloat.value"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.valueHolderFloat = new ValueHolder<float>(42);
        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.valueHolderFloat.value);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.ValueHolder<float> nullCheck;
            float rhsOutput;

            rhsOutput = default(float);
            nullCheck = root.valueHolderFloat;
            if (nullCheck == null)
            {
                goto retn;
            }
            rhsOutput = nullCheck.value;
        retn:
            if (element.floatValue != rhsOutput)
            {
                element.floatValue = rhsOutput;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileDotAccessStructMemberRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("floatValue", "svHolderVec3.value.z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.svHolderVec3 = new StructValueHolder<Vector3>(new Vector3(0, 0, 42));
        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.svHolderVec3.value.z);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            if (element.floatValue != root.svHolderVec3.value.z)
            {
                element.floatValue = root.svHolderVec3.value.z;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileDotAccessMixedStructRefMemberRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("floatValue", "refValueHolderVec3.value.z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.refValueHolderVec3 = new ValueHolder<Vector3>(new Vector3(0, 0, 42));
        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.refValueHolderVec3.value.z);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.ValueHolder<UnityEngine.Vector3> nullCheck;
            float rhsOutput;

            rhsOutput = default(float);
            nullCheck = root.refValueHolderVec3;
            if (nullCheck == null)
            {
                goto retn;
            }
            rhsOutput = nullCheck.value.z;
        retn:
            if (element.floatValue != rhsOutput)
            {
                element.floatValue = rhsOutput;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_ConstIndex_StructRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("floatValue", "vec3Array[3].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3Array = new[] {
            new Vector3(),
            new Vector3(),
            new Vector3(),
            new Vector3(0, 0, 42)
        };

        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.vec3Array[3].z);
        Debug.Log(PrintCode(expr));

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
         (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
         {
            UnityEngine.Vector3[] toBeIndexed;
            int indexer;
            float rhsOutput;

            rhsOutput = default(float);
            toBeIndexed = root.vec3Array;
            indexer = 3;
            if ((toBeIndexed == null) || ((indexer < 0) || (indexer >= toBeIndexed.Length)))
            {
                goto retn;
            }
            rhsOutput = toBeIndexed[indexer].z;
        retn:
            if (element.floatValue != rhsOutput)
            {
                element.floatValue = rhsOutput;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_NonArray_ConstIndex_StructRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("floatValue", "vec3List[3].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3List = new List<Vector3>();
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3(0, 0, 42));


        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.vec3List[3].z);
        Debug.Log(PrintCode(expr));

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            System.Collections.Generic.List<UnityEngine.Vector3> toBeIndexed;
            int indexer;
            float rhsOutput;

            rhsOutput = default(float);
            toBeIndexed = root.vec3List;
            indexer = 3;
            if ((toBeIndexed == null) || ((indexer < 0) || (indexer >= toBeIndexed.Count)))
            {
                goto retn;
            }
            rhsOutput = toBeIndexed[indexer].z;
        retn:
            if (element.floatValue != rhsOutput)
            {
                element.floatValue = rhsOutput;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_StringDictionary_StructRead() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("floatValue", "vec3Dic['two'].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3Dic = new Dictionary<string, Vector3>();
        root.vec3Dic["one"] = new Vector3(1, 1, 1);
        root.vec3Dic["two"] = new Vector3(2, 2, 2);
        root.vec3Dic["three"] = new Vector3(3, 3, 3);

        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(2, root.vec3Dic["two"].z);

        fn.Invoke(root, element);

        Assert.AreEqual(2, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
        System.Collections.Generic.Dictionary<string, UnityEngine.Vector3> toBeIndexed;
        string indexer;
        float rhsOutput;

        rhsOutput = default(float);
        toBeIndexed = root.vec3Dic;
        indexer = ""two"";
            if (toBeIndexed == null)
            {
                goto retn;
            }
            rhsOutput = toBeIndexed[indexer].z;
            retn:
            if (element.floatValue != rhsOutput)
            {
                element.floatValue = rhsOutput;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileIndexAccess_AttemptTypeCast() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // using a float to index the list but list is indexed by int, should cast float to int
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("floatValue", "vec3List[3f].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3List = new List<Vector3>();
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3(0, 0, 42));

        Assert.AreEqual(0, element.floatValue);
        Assert.AreEqual(42, root.vec3List[3].z);

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.floatValue);
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            System.Collections.Generic.List<UnityEngine.Vector3> toBeIndexed;
            int indexer;
            float rhsOutput;

            rhsOutput = default(float);
            toBeIndexed = root.vec3List;
            indexer = 3;
            if ((toBeIndexed == null) || ((indexer < 0) || (indexer >= toBeIndexed.Count)))
            {
                goto retn;
            }
            rhsOutput = toBeIndexed[indexer].z;
        retn:
            if (element.floatValue != rhsOutput)
            {
                element.floatValue = rhsOutput;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileStructFieldAssignment_Constant() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // using a float to index the list but list is indexed by int, should cast float to int
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("svHolderVec3.value.x", "34"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();

        Assert.AreEqual(0, element.svHolderVec3.value.x);
        Debug.Log(PrintCode(expr));

        fn.Invoke(root, element);

        Assert.AreEqual(34, element.svHolderVec3.value.x);

        AssertStringsEqual(@"
       (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.StructValueHolder<UnityEngine.Vector3> svHolderVec3;
            UnityEngine.Vector3 value;
            float x;

            svHolderVec3 = element.svHolderVec3;
            value = svHolderVec3.value;
            x = value.x;
            if (x != 34)
            {
                value.x = 34;
                svHolderVec3.value = value;
                element.svHolderVec3 = svHolderVec3;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileStructFieldAssignment_Variable() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // using a float to index the list but list is indexed by int, should cast float to int
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("svHolderVec3.value.x", "floatValue"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.floatValue = 35;
        Assert.AreEqual(0, element.svHolderVec3.value.x);

        fn.Invoke(root, element);

        Assert.AreEqual(35, element.svHolderVec3.value.x);

        AssertStringsEqual(@"
               (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.StructValueHolder<UnityEngine.Vector3> svHolderVec3;
            UnityEngine.Vector3 value;
            float x;

            svHolderVec3 = element.svHolderVec3;
            value = svHolderVec3.value;
            x = value.x;
            if (x != root.floatValue)
            {
                value.x = root.floatValue;
                svHolderVec3.value = value;
                element.svHolderVec3 = svHolderVec3;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileStructFieldAssignment_Accessor() {
        BindingCompiler bindingCompiler = new BindingCompiler();
        // using a float to index the list but list is indexed by int, should cast float to int
        LambdaExpression expr = bindingCompiler.BuildMemberReadBinding(LinqType, LinqType, new AttributeDefinition("svHolderVec3.value.x", "vec3List[3].z"));
        Action<LinqThing, LinqThing> fn = (Action<LinqThing, LinqThing>) expr.Compile();
        LinqThing root = new LinqThing();
        LinqThing element = new LinqThing();
        root.vec3List = new List<Vector3>();
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3());
        root.vec3List.Add(new Vector3(0, 0, 42));

        Assert.AreEqual(0, element.svHolderVec3.value.x);
        Assert.AreEqual(42, root.vec3List[3].z);
        Debug.Log(PrintCode(expr));

        fn.Invoke(root, element);

        Assert.AreEqual(42, element.svHolderVec3.value.x);

        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing root, TestLinqCompiler.LinqThing element) =>
        {
            TestLinqCompiler.StructValueHolder<UnityEngine.Vector3> svHolderVec3;
            UnityEngine.Vector3 value;
            float x;
            System.Collections.Generic.List<UnityEngine.Vector3> toBeIndexed;
            int indexer;
            float rhsOutput;

            rhsOutput = default(float);
            svHolderVec3 = element.svHolderVec3;
            value = svHolderVec3.value;
            x = value.x;
            toBeIndexed = root.vec3List;
            indexer = 3;
            if ((toBeIndexed == null) || ((indexer < 0) || (indexer >= toBeIndexed.Count)))
            {
                goto retn;
            }
            rhsOutput = toBeIndexed[indexer].z;
        retn:
            if (x != rhsOutput)
            {
                value.x = rhsOutput;
                svHolderVec3.value = value;
                element.svHolderVec3 = svHolderVec3;
            }
        }
        ", PrintCode(expr));
    }

    [Test]
    public void CompileNumericOperators() {
        LinqCompiler compiler = new LinqCompiler();

        object CompileAndReset<T>(string input) where T : Delegate {
            compiler.ReturnStatement(compiler.CreateRHSStatementChain(input));
            AssertStringsEqual(@"() => 
            {
                return {input};
            }".Replace("{input}", input.Replace("f", "")), PrintCode(compiler.BuildLambda<T>()));
            T retn = compiler.Compile<T>();
            compiler.Reset();
            return retn.DynamicInvoke();
        }

        Assert.AreEqual(5 + 4, CompileAndReset<Func<int>>("5 + 4"));
        Assert.AreEqual(5 - 4, CompileAndReset<Func<int>>("5 - 4"));
        Assert.AreEqual(5 * 4, CompileAndReset<Func<int>>("5 * 4"));
        Assert.AreEqual(5 % 4, CompileAndReset<Func<int>>("5 % 4"));
        Assert.AreEqual(5 / 4, CompileAndReset<Func<int>>("5 / 4"));
        Assert.AreEqual(5 >> 4, CompileAndReset<Func<int>>("5 >> 4"));
        Assert.AreEqual(5 << 4, CompileAndReset<Func<int>>("5 << 4"));
        Assert.AreEqual(5 | 4, CompileAndReset<Func<int>>("5 | 4"));
        Assert.AreEqual(5 & 4, CompileAndReset<Func<int>>("5 & 4"));

        Assert.AreEqual(5f + 4f, CompileAndReset<Func<float>>("5f + 4f"));
        Assert.AreEqual(5f - 4f, CompileAndReset<Func<float>>("5f - 4f"));
        Assert.AreEqual(5f * 4f, CompileAndReset<Func<float>>("5f * 4f"));
        Assert.AreEqual(5f % 4f, CompileAndReset<Func<float>>("5f % 4f"));
        Assert.AreEqual(5f / 4f, CompileAndReset<Func<float>>("5f / 4f"));
    }

    [Test]
    public void CompileNumericOperators_MultipleOperators() {
        LinqCompiler compiler = new LinqCompiler();

        object CompileAndReset<T>(string input) where T : Delegate {
            compiler.ReturnStatement(compiler.CreateRHSStatementChain(input));
            T retn = compiler.Compile<T>();
            compiler.Reset();
            return retn.DynamicInvoke();
        }

        Assert.AreEqual(5 + 4 * 7, CompileAndReset<Func<int>>("5 + 4 * 7"));
        Assert.AreEqual(5 - 4 * 7, CompileAndReset<Func<int>>("5 - 4 * 7"));
        Assert.AreEqual(5 * 4 * 7, CompileAndReset<Func<int>>("5 * 4 * 7"));
        Assert.AreEqual(5 % 4 * 7, CompileAndReset<Func<int>>("5 % 4 * 7"));
        Assert.AreEqual(5 / 4 * 7, CompileAndReset<Func<int>>("5 / 4 * 7"));
        Assert.AreEqual(5 >> 4 * 7, CompileAndReset<Func<int>>("5 >> 4 * 7"));
        Assert.AreEqual(5 << 4 * 7, CompileAndReset<Func<int>>("5 << 4 * 7"));
        Assert.AreEqual(5 | 4 * 7, CompileAndReset<Func<int>>("5 | 4 * 7"));
        Assert.AreEqual(5 & 4 * 7, CompileAndReset<Func<int>>("5 & 4 * 7"));
    }

    [Test]
    public void CompileNumericOperators_MultipleOperators_Parens() {
        LinqCompiler compiler = new LinqCompiler();

        object CompileAndReset<T>(string input) where T : Delegate {
            compiler.ReturnStatement(compiler.CreateRHSStatementChain(input));
            T retn = compiler.Compile<T>();
            compiler.Reset();
            return retn.DynamicInvoke();
        }

        Assert.AreEqual((124 + 4) * 7, CompileAndReset<Func<int>>("(124 + 4) * 7"));
        Assert.AreEqual((124 - 4) * 7, CompileAndReset<Func<int>>("(124 - 4) * 7"));
        Assert.AreEqual((124 * 4) * 7, CompileAndReset<Func<int>>("(124 * 4) * 7"));
        Assert.AreEqual((124 % 4) * 7, CompileAndReset<Func<int>>("(124 % 4) * 7"));
        Assert.AreEqual((124 / 4) * 7, CompileAndReset<Func<int>>("(124 / 4) * 7"));
        Assert.AreEqual((124 >> 4) * 7, CompileAndReset<Func<int>>("(124 >> 4) * 7"));
        Assert.AreEqual((124 << 4) * 7, CompileAndReset<Func<int>>("(124 << 4) * 7"));
        Assert.AreEqual((124 | 4) * 7, CompileAndReset<Func<int>>("(124 | 4) * 7"));
        Assert.AreEqual((124 & 4) * 7, CompileAndReset<Func<int>>("(124 & 4) * 7"));

        Assert.AreEqual(124 + (4 * 7), CompileAndReset<Func<int>>("124 + (4 * 7)"));
        Assert.AreEqual(124 - (4 * 7), CompileAndReset<Func<int>>("124 - (4 * 7)"));
        Assert.AreEqual(124 * (4 * 7), CompileAndReset<Func<int>>("124 * (4 * 7)"));
        Assert.AreEqual(124 % (4 * 7), CompileAndReset<Func<int>>("124 % (4 * 7)"));
        Assert.AreEqual(124 / (4 * 7), CompileAndReset<Func<int>>("124 / (4 * 7)"));
        Assert.AreEqual(124 >> (4 * 7), CompileAndReset<Func<int>>("124 >> (4 * 7)"));
        Assert.AreEqual(124 << (4 * 7), CompileAndReset<Func<int>>("124 << (4 * 7)"));
        Assert.AreEqual(124 | (4 * 7), CompileAndReset<Func<int>>("124 | (4 * 7)"));
        Assert.AreEqual(124 & (4 * 7), CompileAndReset<Func<int>>("124 & (4 * 7)"));
    }

    private class OperatorOverloadTest {

        public Vector3 v0;
        public Vector3 v1;

    }

    [Test]
    public void CompileOperatorOverloads() {
        LinqCompiler compiler = new LinqCompiler();

        T CompileAndReset<T>(string input) where T : Delegate {
            compiler.AddParameter(typeof(OperatorOverloadTest), "opOverload");
            compiler.ReturnStatement(compiler.CreateRHSStatementChain(typeof(Vector3), input));
            T retn = compiler.Compile<T>();
            Debug.Log(PrintCode(compiler.BuildLambda<T>()));

            compiler.Reset();
            return retn;
        }

        OperatorOverloadTest overloadTest = new OperatorOverloadTest();

        overloadTest.v0 = new Vector3(1124, 522, 241);
        overloadTest.v1 = new Vector3(1124, 522, 241);

        Assert.AreEqual(overloadTest.v0 + overloadTest.v1, CompileAndReset<Func<OperatorOverloadTest, Vector3>>("opOverload.v0 + opOverload.v1")(overloadTest));
        Assert.AreEqual(overloadTest.v0 - overloadTest.v1, CompileAndReset<Func<OperatorOverloadTest, Vector3>>("opOverload.v0 - opOverload.v1")(overloadTest));
        CompileException exception = Assert.Throws<CompileException>(() => { CompileAndReset<Func<OperatorOverloadTest, Vector3>>("opOverload.v0 / opOverload.v1")(overloadTest); });
        Assert.AreEqual(exception.Message, CompileException.MissingBinaryOperator(OperatorType.Divide, typeof(Vector3), typeof(Vector3)).Message);
    }

    [Test]
    public void CompileTypeOfConstant() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("typeof(int)"));
        Assert.AreEqual(typeof(int), compiler.Compile<Func<Type>>()());

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("typeof(int[])"));
        Assert.AreEqual(typeof(int[]), compiler.Compile<Func<Type>>()());
    }

    [Test]
    public void CompileUnaryNot() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("!true"));
        Assert.AreEqual(false, compiler.Compile<Func<bool>>()());
        compiler.Reset();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("!true && false"));
        Assert.AreEqual(!true && false, compiler.Compile<Func<bool>>()());
        compiler.Reset();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("!false"));
        Assert.AreEqual(!false, compiler.Compile<Func<bool>>()());
        compiler.Reset();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("false && !true"));
        Assert.AreEqual(false && !true, compiler.Compile<Func<bool>>()());
        compiler.Reset();
    }

    [Test]
    public void CompileUnaryMinus() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("-10"));
        Assert.AreEqual(-10, compiler.Compile<Func<int>>()());
        compiler.Reset();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("-1425.24f"));
        Assert.AreEqual(-1425.24f, compiler.Compile<Func<float>>()());
        compiler.Reset();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("-1425.24d"));
        Assert.AreEqual(-1425.24d, compiler.Compile<Func<double>>()());
        compiler.Reset();
    }

    [Test]
    public void CompileUnaryBitwiseNot() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("~10"));
        Assert.AreEqual(~10, compiler.Compile<Func<int>>()());
        compiler.Reset();

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("~(1425 & 4)"));
        Assert.AreEqual(~(1425 & 4), compiler.Compile<Func<int>>()());
        compiler.Reset();
    }

    [Test]
    public void CompileArrayIndex_Constant() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetReturnType(typeof(Vector3));
        compiler.AddParameter(typeof(LinqThing), "thing");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("thing.vec3Array[1]"));
        Assert.AreEqual(thing.vec3Array[1], compiler.Compile<Func<LinqThing, Vector3>>()(thing));

        UnityEngine.Debug.Log(PrintCode(compiler.BuildLambda()));
        AssertStringsEqual(@"
       (TestLinqCompiler.LinqThing thing) =>
       {
            UnityEngine.Vector3[] toBeIndexed;
            int indexer;
            UnityEngine.Vector3 rhsOutput;

            rhsOutput = default(UnityEngine.Vector3);
            toBeIndexed = thing.vec3Array;
            indexer = 1;
            if ((toBeIndexed == null) || ((indexer < 0) || (indexer >= toBeIndexed.Length)))
            {
                goto retn;
            }
            rhsOutput = toBeIndexed[indexer];
        retn:
            return rhsOutput;
        }
        ", PrintCode(compiler.BuildLambda()));
        compiler.Reset();

        compiler.AddParameter(typeof(LinqThing), "thing");
        compiler.SetReturnType(typeof(Vector3));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("thing.vec3Array[1 + 1]"));
        Assert.AreEqual(thing.vec3Array[2], compiler.Compile<Func<LinqThing, Vector3>>()(thing));
        compiler.Reset();

        compiler.AddParameter(typeof(LinqThing), "thing");
        compiler.SetReturnType(typeof(Vector3));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("thing.vec3Array[99999]"));
        Assert.AreEqual(default(Vector3), compiler.Compile<Func<LinqThing, Vector3>>()(thing));
        compiler.Reset();

        compiler.AddParameter(typeof(LinqThing), "thing");
        compiler.SetReturnType(typeof(Vector3));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("thing.vec3Array[-14]"));
        Assert.AreEqual(default(Vector3), compiler.Compile<Func<LinqThing, Vector3>>()(thing));

        compiler.Reset();
    }

    [Test]
    public void CompileArrayIndex_Expression() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetReturnType(typeof(Vector3));
        compiler.AddParameter(typeof(LinqThing), "thing");
        compiler.AddParameter(typeof(int), "arg0");
        compiler.AddParameter(typeof(int), "arg1");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("thing.vec3Array[arg0 + thing.intVal - arg1]"));
        Assert.AreEqual(thing.vec3Array[2], compiler.Compile<Func<LinqThing, int, int, Vector3>>()(thing, 1, 2));
    }

    [Test]
    public void CompileArrayIndex_InvalidExpression() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetReturnType(typeof(Vector3));
        compiler.AddParameter(typeof(LinqThing), "thing");
        CompileException exception = Assert.Throws<CompileException>(() => { compiler.ReturnStatement(compiler.CreateRHSStatementChain("thing.vec3Array[thing.vec3Dic]")); });
        Assert.AreEqual(CompileException.InvalidTargetType(typeof(int), typeof(Dictionary<string, Vector3>)).Message, exception.Message);
    }

    [Test]
    public void CompileIs() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetReturnType(typeof(bool));
        compiler.AddParameter(typeof(LinqThing), "thing");
        compiler.AddNamespace("System.Collections.Generic");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("thing.vec3Array[0].x is System.Collections.Generic.List<float>"));
        Debug.Log(PrintCode(compiler.BuildLambda<Func<LinqThing, bool>>()));
        Assert.AreEqual(false, compiler.Compile<Func<LinqThing, bool>>()(thing));
    }

    [Test]
    public void CompileAs() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetReturnType(typeof(bool));
        compiler.AddNamespace("System.Collections");
        compiler.AddParameter(typeof(LinqThing), "thing");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("thing.vec3Array as IList"));
        Debug.Log(PrintCode(compiler.BuildLambda<Func<LinqThing, IList>>()));
        Assert.AreEqual(thing.vec3Array, compiler.Compile<Func<LinqThing, IList>>()(thing));
    }

    [Test]
    public void CompileDirectCast() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();

        thing.intVal = 3;
        thing.vec3Array = new[] {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6),
            new Vector3(7, 8, 9)
        };

        compiler.SetReturnType(typeof(IReadOnlyList<Vector3>));
        compiler.AddParameter(typeof(LinqThing), "thing");
        compiler.AddNamespace("System.Collections.Generic");
        compiler.AddNamespace("UnityEngine");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("(IReadOnlyList<Vector3>)thing.vec3Array"));
        Debug.Log(PrintCode(compiler.BuildLambda<Func<LinqThing, IReadOnlyList<Vector3>>>()));
        Assert.AreEqual(thing.vec3Array, compiler.Compile<Func<LinqThing, IReadOnlyList<Vector3>>>()(thing));
        AssertStringsEqual(@"
        (TestLinqCompiler.LinqThing thing) =>
        {
            return (System.Collections.Generic.IReadOnlyList<UnityEngine.Vector3>)thing.vec3Array;
        }
        ", PrintCode(compiler.BuildLambda()));
    }

    [Test]
    public void CompileNewExpression_NoArguments() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetReturnType(typeof(Vector3));
        compiler.AddNamespace("UnityEngine");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("new Vector3()"));
        Assert.AreEqual(new Vector3(), compiler.Compile<Func<Vector3>>()());
        AssertStringsEqual(@"
        () =>
        {
            return new UnityEngine.Vector3();
        }
        ", PrintCode(compiler.BuildLambda()));
    }

    [Test]
    public void CompileNewExpression_WithConstantArguments() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetReturnType(typeof(Vector3));
        compiler.AddNamespace("UnityEngine");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("new Vector3(1f, 2f, 3f)"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(new Vector3(1, 2, 3), compiler.Compile<Func<Vector3>>()());
        AssertStringsEqual(@"
        () =>
        {
            return new UnityEngine.Vector3(1, 2, 3);
        }
        ", PrintCode(compiler.BuildLambda()));
    }


    [Test]
    public void CompileNewExpression_WithOptionalArguments() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetReturnType(typeof(ThingWithOptionals));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("new ThingWithOptionals(8)"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        ThingWithOptionals thing = compiler.Compile<Func<ThingWithOptionals>>()();
        Assert.AreEqual(8, thing.x);
        Assert.AreEqual(2, thing.y);
        Assert.AreEqual(0, thing.f);
        AssertStringsEqual(@"
           () =>
            {
                return new ThingWithOptionals(8, 2);
            }
        ", PrintCode(compiler.BuildLambda()));
    }

    [Test]
    public void CompileNewExpression_WithUnmatchedConstructor() {
        LinqCompiler compiler = new LinqCompiler();


        CompileException ex = Assert.Throws<CompileException>(() => {
            compiler.SetReturnType(typeof(ThingWithOptionals));
            compiler.ReturnStatement(compiler.CreateRHSStatementChain("new ThingWithOptionals(8, 10, 20, 24, 52)"));
            compiler.Compile<Func<ThingWithOptionals>>()();
        });
        Assert.AreEqual(CompileException.UnresolvedConstructor(typeof(ThingWithOptionals), new Type[] {
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int)
        }).Message, ex.Message);
    }

    [Test]
    public void CompileNewExpression_WithNestedNew() {
        LinqCompiler compiler = new LinqCompiler();

        compiler.SetReturnType(typeof(ThingWithOptionals));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("new ThingWithOptionals(new ThingWithOptionals(12f), 2)"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        ThingWithOptionals thing = compiler.Compile<Func<ThingWithOptionals>>()();
        Assert.AreEqual(0, thing.x);
        Assert.AreEqual(2, thing.y);
        Assert.AreEqual(12, thing.f);
        AssertStringsEqual(@"
           () =>
            {
                return new ThingWithOptionals(new ThingWithOptionals(12, 2), 2);
            }
        ", PrintCode(compiler.BuildLambda()));
    }

    [Test]
    public void CompileEnumAccess() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetReturnType(typeof(TestEnum));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("TestEnum.One"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(TestEnum.One, compiler.Compile<Func<TestEnum>>()());
        AssertStringsEqual(@"
           () =>
            {
                return TestEnum.One;
            }
        ", PrintCode(compiler.BuildLambda()));
    }

    [Test]
    public void CompileNamespacePath() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetReturnType(typeof(float));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatValue"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatValue, compiler.Compile<Func<float>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatValue;
            }
        ", PrintCode(compiler.BuildLambda()));

        compiler.Reset();
        compiler.SetReturnType(typeof(float));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatArray[0]"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatArray[0], compiler.Compile<Func<float>>()());
        AssertStringsEqual(@"
       () =>
       {
            float[] toBeIndexed;
            int indexer;
            float rhsOutput;

            rhsOutput = default(float);
            toBeIndexed = UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.FloatArray;
            indexer = 0;
            if ((toBeIndexed == null) || ((indexer < 0) || (indexer >= toBeIndexed.Length)))
            {
                goto retn;
            }
            rhsOutput = toBeIndexed[indexer];
        retn:
            return rhsOutput;
       }
       ", PrintCode(compiler.BuildLambda()));
    }

    [Test]
    public void CompileTypeChain() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetReturnType(typeof(float));
        compiler.AddNamespace("UIForia.Test.NamespaceTest.SomeNamespace");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("TypeChainTest.TypeChainChild.TypeChainEnd.SomeValue"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(UIForia.Test.NamespaceTest.SomeNamespace.TypeChainTest.TypeChainChild.TypeChainEnd.SomeValue, compiler.Compile<Func<float>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.TypeChainTest.TypeChainChild.TypeChainEnd.SomeValue;
            }
        ", PrintCode(compiler.BuildLambda()));

        compiler.Reset();

        compiler.SetReturnType(typeof(Vector3));
        compiler.AddNamespace("UIForia.Test.NamespaceTest.SomeNamespace");
        compiler.AddNamespace("UnityEngine");
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("TypeChainTest.TypeChainChild.TypeChainEnd<Vector3>.Value"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(UIForia.Test.NamespaceTest.SomeNamespace.TypeChainTest.TypeChainChild.TypeChainEnd<Vector3>.Value, compiler.Compile<Func<Vector3>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.TypeChainTest.TypeChainChild.TypeChainEnd<UnityEngine.Vector3>.Value;
            }
        ", PrintCode(compiler.BuildLambda()));
    }

    [Test]
    public void CompileNamespacePath_NestedType() {
        LinqCompiler compiler = new LinqCompiler();
        compiler.SetReturnType(typeof(float));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1.FloatValue"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1.FloatValue, compiler.Compile<Func<float>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1.FloatValue;
            }
        ", PrintCode(compiler.BuildLambda()));

        compiler.Reset();
        compiler.SetReturnType(typeof(int));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string>.IntValue"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string>.IntValue, compiler.Compile<Func<int>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string>.IntValue;
            }
        ", PrintCode(compiler.BuildLambda()));

        compiler.Reset();
        compiler.SetReturnType(typeof(string));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string, UnityEngine.Vector3>.StringValue"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string, Vector3>.StringValue, compiler.Compile<Func<string>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<string, UnityEngine.Vector3>.StringValue;
            }
        ", PrintCode(compiler.BuildLambda()));

        compiler.Reset();
        compiler.SetReturnType(typeof(int));
        compiler.ReturnStatement(compiler.CreateRHSStatementChain("UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<int>.NestedSubType1<int>.NestedIntValue"));
        Debug.Log(PrintCode(compiler.BuildLambda()));
        Assert.AreEqual(UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<int>.NestedSubType1<int>.NestedIntValue, compiler.Compile<Func<int>>()());
        AssertStringsEqual(@"
           () =>
            {
                return UIForia.Test.NamespaceTest.SomeNamespace.NamespaceTestClass.SubType1<int>.NestedSubType1<int>.NestedIntValue;
            }
        ", PrintCode(compiler.BuildLambda()));
    }

    [Test]
    public void CompileFalsyBoolean() {
        LinqCompiler compiler = new LinqCompiler();

        LinqThing thing = new LinqThing();
        compiler.AddParameter(typeof(LinqThing), "arg");

        compiler.IfEqual("arg", null, () => {
                compiler.ReturnStatement(
                    compiler.Constant(false)
                );
            }, () => {
                compiler.ReturnStatement(
                    compiler.Constant(true));
            }
        );

        compiler.ReturnStatement(compiler.CreateRHSStatementChain("arg"));
    }

    public void AssertStringsEqual(string a, string b) {
        string[] splitA = a.Trim().Split('\n');
        string[] splitB = b.Trim().Split('\n');

        Assert.AreEqual(splitA.Length, splitB.Length);

        for (int i = 0; i < splitA.Length; i++) {
            Assert.AreEqual(splitA[i].Trim(), splitB[i].Trim());
        }
    }

    private static string PrintCode(IList<Expression> expressions) {
        string retn = "";
        for (int i = 0; i < expressions.Count; i++) {
            retn += Mono.Linq.Expressions.CSharp.ToCSharpCode(expressions[i]);
            if (i != expressions.Count - 1) {
                retn += "\n";
            }
        }

        return retn;
    }

    private static string PrintCode(Expression expression) {
        return Mono.Linq.Expressions.CSharp.ToCSharpCode(expression);
    }

}

namespace UIForia.Test.NamespaceTest.SomeNamespace {

    public class TypeChainTest {

        public class TypeChainChild {

            public class TypeChainEnd {

                public static float SomeValue = 123;

            }

            public class TypeChainEnd<T> {

                public static T Value { get; set; }

            }

            public class TypeChainEnd<T, U> { }

        }

    }

    public class NamespaceTestClass {

        public static float FloatValue = 1;
        public static float[] FloatArray = {1};

        public class SubType1 {

            public static float FloatValue = 2;

        }

        public class SubType1<T> {

            public static int IntValue;
            public static float FloatValue = 2;

            public class NestedSubType1<TNested> {

                public static int NestedIntValue = 3;

            }

        }

        public class SubType1<T, U> {

            public static string StringValue = "hello";
            public static float FloatValue = 2;

        }

    }

}

public enum TestEnum {

    One,
    Two

}

public class ThingWithOptionals {

    public readonly int x;
    public readonly int y;
    public readonly float f;

    public ThingWithOptionals(ThingWithOptionals other, int y = 2) {
        this.f = other.f;
        this.x = other.x;
        this.y = y;
    }

    public ThingWithOptionals(float f, int y = 2) {
        this.f = f;
        this.y = y;
    }

    public ThingWithOptionals(int x, int y = 2) {
        this.x = x;
        this.y = y;
    }

}