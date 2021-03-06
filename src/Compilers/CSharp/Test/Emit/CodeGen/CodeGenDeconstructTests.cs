﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    [CompilerTrait(CompilerFeature.Tuples)]
    public class CodeGenDeconstructTests : CSharpTestBase
    {
        private static readonly MetadataReference[] s_valueTupleRefs = new[] { SystemRuntimeFacadeRef, ValueTupleRef };

        const string commonSource =
@"public class Pair<T1, T2>
{
    T1 item1;
    T2 item2;

    public Pair(T1 item1, T2 item2)
    {
        this.item1 = item1;
        this.item2 = item2;
    }

    public void Deconstruct(out T1 item1, out T2 item2)
    {
        System.Console.WriteLine($""Deconstructing {ToString()}"");
        item1 = this.item1;
        item2 = this.item2;
    }

    public override string ToString() { return $""({item1.ToString()}, {item2.ToString()})""; }
}

public static class Pair
{
    public static Pair<T1, T2> Create<T1, T2>(T1 item1, T2 item2) { return new Pair<T1, T2>(item1, item2); }
}

public class Integer
{
    public int state;
    public override string ToString() { return state.ToString(); }
    public Integer(int i) { state = i; }
    public static implicit operator LongInteger(Integer i) { System.Console.WriteLine($""Converting {i}""); return new LongInteger(i.state); }
}

public class LongInteger
{
    long state;
    public LongInteger(long l) { state = l; }
    public override string ToString() { return state.ToString(); }
}";

        [Fact]
        public void SimpleAssign()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;

        (x, y) = new C();
        System.Console.WriteLine(x + "" "" + y);
    }

    public void Deconstruct(out int a, out string b)
    {
        a = 1;
        b = ""hello"";
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello", additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
            comp.VerifyIL("C.Main", @"
{
  // Code size       48 (0x30)
  .maxstack  3
  .locals init (long V_0, //x
                string V_1, //y
                int V_2,
                string V_3,
                string V_4)
  IL_0000:  newobj     ""C..ctor()""
  IL_0005:  ldloca.s   V_2
  IL_0007:  ldloca.s   V_3
  IL_0009:  call       ""void C.Deconstruct(out int, out string)""
  IL_000e:  ldloc.2
  IL_000f:  conv.i8
  IL_0010:  ldloc.3
  IL_0011:  stloc.s    V_4
  IL_0013:  dup
  IL_0014:  stloc.0
  IL_0015:  ldloc.s    V_4
  IL_0017:  stloc.1
  IL_0018:  pop
  IL_0019:  ldloc.0
  IL_001a:  box        ""long""
  IL_001f:  ldstr      "" ""
  IL_0024:  ldloc.1
  IL_0025:  call       ""string string.Concat(object, object, object)""
  IL_002a:  call       ""void System.Console.WriteLine(string)""
  IL_002f:  ret
}");
        }

        [Fact]
        public void DeconstructMethodAmbiguous()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;
        (x, y) = new C();
        System.Console.WriteLine(x + "" "" + y);
    }

    public void Deconstruct(out int a, out string b)
    {
        a = 1;
        b = ""hello"";
    }

    public void Deconstruct(out int a)
    {
        a = 2;
    }
}";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello", additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeconstructCanHaveReturnType()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;

        (x, y) = new C();
        System.Console.WriteLine(x + "" "" + y);
    }

    public int Deconstruct(out int a, out string b)
    {
        a = 1;
        b = ""hello"";
        return 42;
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello", additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void VerifyExecutionOrder_Deconstruct()
        {
            string source = @"
using System;
class C
{
    int x { set { Console.WriteLine($""setX""); } }
    int y { set { Console.WriteLine($""setY""); } }

    C getHolderForX() { Console.WriteLine(""getHolderforX""); return this; }
    C getHolderForY() { Console.WriteLine(""getHolderforY""); return this; }
    C getDeconstructReceiver() { Console.WriteLine(""getDeconstructReceiver""); return this; }

    static void Main()
    {
        C c = new C();
        (c.getHolderForX().x, c.getHolderForY().y) = c.getDeconstructReceiver();
    }
    public void Deconstruct(out D1 x, out D2 y) { x = new D1(); y = new D2(); Console.WriteLine(""Deconstruct""); }
}
class D1
{
    public static implicit operator int(D1 d) { Console.WriteLine(""Conversion1""); return 1; }
}
class D2
{
    public static implicit operator int(D2 d) { Console.WriteLine(""Conversion2""); return 2; }
}
";

            string expected =
@"getHolderforX
getHolderforY
getDeconstructReceiver
Deconstruct
Conversion1
Conversion2
setX
setY
";
            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void VerifyExecutionOrder_TupleLiteral()
        {
            string source = @"
using System;
class C
{
    int x { set { Console.WriteLine($""setX""); } }
    int y { set { Console.WriteLine($""setY""); } }

    C getHolderForX() { Console.WriteLine(""getHolderforX""); return this; }
    C getHolderForY() { Console.WriteLine(""getHolderforY""); return this; }

    static void Main()
    {
        C c = new C();
        (c.getHolderForX().x, c.getHolderForY().y) = (new D1(), new D2());
    }
}
class D1
{
    public D1() { Console.WriteLine(""Constructor1""); }
    public static implicit operator int(D1 d) { Console.WriteLine(""Conversion1""); return 1; }
}
class D2
{
    public D2() { Console.WriteLine(""Constructor2""); }
    public static implicit operator int(D2 d) { Console.WriteLine(""Conversion2""); return 2; }
}
";

            string expected =
@"getHolderforX
getHolderforY
Constructor1
Conversion1
Constructor2
Conversion2
setX
setY
";
            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DifferentVariableKinds()
        {
            string source = @"
class C
{
    int[] ArrayIndexer = new int[1];

    string property;
    string Property { set { property = value; } }

    string AutoProperty { get; set; }

    static void Main()
    {
        C c = new C();
        (c.ArrayIndexer[0], c.Property, c.AutoProperty) = new C();
        System.Console.WriteLine(c.ArrayIndexer[0] + "" "" + c.property + "" "" + c.AutoProperty);
    }

    public void Deconstruct(out int a, out string b, out string c)
    {
        a = 1;
        b = ""hello"";
        c = ""world"";
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello world", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void Dynamic()
        {
            string source = @"
class C
{
    dynamic Dynamic1;
    dynamic Dynamic2;

    static void Main()
    {
        C c = new C();
        (c.Dynamic1, c.Dynamic2) = c;
        System.Console.WriteLine(c.Dynamic1 + "" "" + c.Dynamic2);
    }

    public void Deconstruct(out int a, out dynamic b)
    {
        a = 1;
        b = ""hello"";
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef, CSharpRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeconstructInterfaceOnStruct()
        {
            string source = @"
interface IDeconstructable
{
    void Deconstruct(out int a, out string b);
}

struct C : IDeconstructable
{
    string state;

    static void Main()
    {
        int x;
        string y;
        IDeconstructable c = new C() { state = ""initial"" };
        System.Console.Write(c);

        (x, y) = c;
        System.Console.WriteLine("" "" + c + "" "" + x + "" "" + y);
    }

    void IDeconstructable.Deconstruct(out int a, out string b)
    {
        a = 1;
        b = ""hello"";
        state = ""modified"";
    }

    public override string ToString() { return state; }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "initial modified 1 hello", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef, CSharpRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeconstructMethodHasParams2()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;

        (x, y) = new C();
        System.Console.WriteLine(x + "" "" + y);
    }

    public void Deconstruct(out int a, out string b, params int[] c) // not a Deconstruct operator
    {
        a = 1;
        b = ""ignored"";
    }

    public void Deconstruct(out int a, out string b)
    {
        a = 2;
        b = ""hello"";
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "2 hello", additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeconstructMethodHasArglist2()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;

        (x, y) = new C();
        System.Console.WriteLine(x + "" "" + y);
    }

    public void Deconstruct(out int a, out string b)
    {
        a = 1;
        b = ""hello"";
    }

    public void Deconstruct(out int a, out string b, __arglist) // not a Deconstruct operator
    {
        a = 2;
        b = ""ignored"";
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef, CSharpRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DifferentStaticVariableKinds()
        {
            string source = @"
class C
{
    static int[] ArrayIndexer = new int[1];

    static string property;
    static string Property { set { property = value; } }

    static string AutoProperty { get; set; }

    static void Main()
    {
        (C.ArrayIndexer[0], C.Property, C.AutoProperty) = new C();
        System.Console.WriteLine(C.ArrayIndexer[0] + "" "" + C.property + "" "" + C.AutoProperty);
    }

    public void Deconstruct(out int a, out string b, out string c)
    {
        a = 1;
        b = ""hello"";
        c = ""world"";
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello world", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DifferentVariableRefKinds()
        {
            string source = @"
class C
{
    static void Main()
    {
        long a = 1;
        int b;
        C.M(ref a, out b);
        System.Console.WriteLine(a + "" "" + b);
    }

    static void M(ref long a, out int b)
    {
        (a, b) = new C();
    }

    public void Deconstruct(out int x, out byte y)
    {
        x = 2;
        y = (byte)3;
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "2 3", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        [CompilerTrait(CompilerFeature.RefLocalsReturns)]
        public void RefReturningMethod()
        {
            string source = @"
class C
{
    static int i = 0;

    static void Main()
    {
        (M(), M()) = new C();
        System.Console.WriteLine($""Final i is {i}"");
    }

    static ref int M()
    {
        System.Console.WriteLine($""M (previous i is {i})"");
        return ref i;
    }

    void Deconstruct(out int x, out int y)
    {
        System.Console.WriteLine(""Deconstruct"");
        x = 42;
        y = 43;
    }
}
";
            var expected =
@"M (previous i is 0)
M (previous i is 0)
Deconstruct
Final i is 43
";

            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact, CompilerTrait(CompilerFeature.RefLocalsReturns)]
        public void RefReturningProperty()
        {
            string source = @"
class C
{
    static int i = 0;

    static void Main()
    {
        (P, P) = new C();
        System.Console.WriteLine($""Final i is {i}"");
    }

    static ref int P
    {
        get
        {
            System.Console.WriteLine($""P (previous i is {i})"");
            return ref i;
        }
    }

    void Deconstruct(out int x, out int y)
    {
        System.Console.WriteLine(""Deconstruct"");
        x = 42;
        y = 43;
    }
}
";
            var expected =
@"P (previous i is 0)
P (previous i is 0)
Deconstruct
Final i is 43
";

            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        [CompilerTrait(CompilerFeature.RefLocalsReturns)]
        public void RefReturningMethodFlow()
        {
            string source = @"
struct C
{
    static C i;
    static C P { get { System.Console.WriteLine(""getP""); return i; } set { System.Console.WriteLine(""setP""); i = value; } }

    static void Main()
    {
        (M(), M()) = P;
    }

    static ref C M()
    {
        System.Console.WriteLine($""M (previous i is {i})"");
        return ref i;
    }

    void Deconstruct(out int x, out int y)
    {
        System.Console.WriteLine(""Deconstruct"");
        x = 42;
        y = 43;
    }

    public static implicit operator C(int x)
    {
        System.Console.WriteLine(""conversion"");
        return new C();
    }
}
";

            var expected =
@"M (previous i is C)
M (previous i is C)
getP
Deconstruct
conversion
conversion";

            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void Indexers()
        {
            string source = @"
class C
{
    static SomeArray array;

    static void Main()
    {
        int y;
        (Foo()[Bar()], y) = new C();
        System.Console.WriteLine($""Final array values[2] {array.values[2]}"");
    }

    static SomeArray Foo()
    {
        System.Console.WriteLine($""Foo"");
        array = new SomeArray();
        return array;
    }

    static int Bar()
    {
        System.Console.WriteLine($""Bar"");
        return 2;
    }

    void Deconstruct(out int x, out int y)
    {
        System.Console.WriteLine(""Deconstruct"");
        x = 101;
        y = 102;
    }
}
class SomeArray
{
    public int[] values;
    public SomeArray() { values = new [] { 42, 43, 44 }; }
    public int this[int index] {
        get { System.Console.WriteLine($""indexGet (with value {values[index]})""); return values[index]; }
        set { System.Console.WriteLine($""indexSet (with value {value})""); values[index] = value; }
    }
}
";
            var expected =
@"Foo
Bar
Deconstruct
indexSet (with value 101)
Final array values[2] 101
";
            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void AssigningTuple()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;

        (x, y) = (1, ""hello"");
        System.Console.WriteLine(x + "" "" + y);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void AssigningTupleWithConversion()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;

        (x, y) = M();
        System.Console.WriteLine(x + "" "" + y);
    }

    static System.ValueTuple<int, string> M()
    {
        return (1, ""hello"");
    }
}
";
            var comp = CompileAndVerify(source, expectedOutput: "1 hello", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void AssigningLongTuple()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        int y;

        (x, x, x, x, x, x, x, x, x, y) = (1, 1, 1, 1, 1, 1, 1, 1, 4, 2);
        System.Console.WriteLine(x + "" "" + y);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "4 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
            comp.VerifyIL("C.Main", @"
{
  // Code size      152 (0x98)
  .maxstack  10
  .locals init (int V_0, //y
                long V_1,
                long V_2,
                long V_3,
                long V_4,
                long V_5,
                long V_6,
                long V_7,
                long V_8,
                long V_9,
                int V_10)
  IL_0000:  ldc.i4.1
  IL_0001:  conv.i8
  IL_0002:  ldc.i4.1
  IL_0003:  conv.i8
  IL_0004:  ldc.i4.1
  IL_0005:  conv.i8
  IL_0006:  ldc.i4.1
  IL_0007:  conv.i8
  IL_0008:  ldc.i4.1
  IL_0009:  conv.i8
  IL_000a:  ldc.i4.1
  IL_000b:  conv.i8
  IL_000c:  ldc.i4.1
  IL_000d:  conv.i8
  IL_000e:  ldc.i4.1
  IL_000f:  conv.i8
  IL_0010:  ldc.i4.4
  IL_0011:  conv.i8
  IL_0012:  ldc.i4.2
  IL_0013:  newobj     ""System.ValueTuple<long, long, int>..ctor(long, long, int)""
  IL_0018:  newobj     ""System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>..ctor(long, long, long, long, long, long, long, (long, long, int))""
  IL_001d:  dup
  IL_001e:  ldfld      ""long System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Item1""
  IL_0023:  stloc.1
  IL_0024:  dup
  IL_0025:  ldfld      ""long System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Item2""
  IL_002a:  stloc.2
  IL_002b:  dup
  IL_002c:  ldfld      ""long System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Item3""
  IL_0031:  stloc.3
  IL_0032:  dup
  IL_0033:  ldfld      ""long System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Item4""
  IL_0038:  stloc.s    V_4
  IL_003a:  dup
  IL_003b:  ldfld      ""long System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Item5""
  IL_0040:  stloc.s    V_5
  IL_0042:  dup
  IL_0043:  ldfld      ""long System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Item6""
  IL_0048:  stloc.s    V_6
  IL_004a:  dup
  IL_004b:  ldfld      ""long System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Item7""
  IL_0050:  stloc.s    V_7
  IL_0052:  dup
  IL_0053:  ldfld      ""(long, long, int) System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Rest""
  IL_0058:  ldfld      ""long System.ValueTuple<long, long, int>.Item1""
  IL_005d:  stloc.s    V_8
  IL_005f:  dup
  IL_0060:  ldfld      ""(long, long, int) System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Rest""
  IL_0065:  ldfld      ""long System.ValueTuple<long, long, int>.Item2""
  IL_006a:  stloc.s    V_9
  IL_006c:  ldfld      ""(long, long, int) System.ValueTuple<long, long, long, long, long, long, long, (long, long, int)>.Rest""
  IL_0071:  ldfld      ""int System.ValueTuple<long, long, int>.Item3""
  IL_0076:  stloc.s    V_10
  IL_0078:  ldloc.s    V_9
  IL_007a:  ldloc.s    V_10
  IL_007c:  stloc.0
  IL_007d:  box        ""long""
  IL_0082:  ldstr      "" ""
  IL_0087:  ldloc.0
  IL_0088:  box        ""int""
  IL_008d:  call       ""string string.Concat(object, object, object)""
  IL_0092:  call       ""void System.Console.WriteLine(string)""
  IL_0097:  ret
}
");
        }

        [Fact]
        public void AssigningLongTupleWithNames()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        int y;

        (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
        System.Console.WriteLine(x + "" "" + y);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "9 10", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics(
                // (9,43): warning CS8123: The tuple element name 'a' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "a: 1").WithArguments("a", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 43),
                // (9,49): warning CS8123: The tuple element name 'b' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "b: 2").WithArguments("b", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 49),
                // (9,55): warning CS8123: The tuple element name 'c' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "c: 3").WithArguments("c", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 55),
                // (9,61): warning CS8123: The tuple element name 'd' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "d: 4").WithArguments("d", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 61),
                // (9,67): warning CS8123: The tuple element name 'e' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "e: 5").WithArguments("e", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 67),
                // (9,73): warning CS8123: The tuple element name 'f' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "f: 6").WithArguments("f", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 73),
                // (9,79): warning CS8123: The tuple element name 'g' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "g: 7").WithArguments("g", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 79),
                // (9,85): warning CS8123: The tuple element name 'h' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "h: 8").WithArguments("h", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 85),
                // (9,91): warning CS8123: The tuple element name 'i' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "i: 9").WithArguments("i", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 91),
                // (9,97): warning CS8123: The tuple element name 'j' is ignored because a different name is specified by the target type '(long, long, long, long, long, long, long, long, long, int)'.
                //         (x, x, x, x, x, x, x, x, x, y) = (a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10);
                Diagnostic(ErrorCode.WRN_TupleLiteralNameMismatch, "j: 10").WithArguments("j", "(long, long, long, long, long, long, long, long, long, int)").WithLocation(9, 97)
                );
        }

        [Fact]
        public void AssigningLongTuple2()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        int y;

        (x, x, x, x, x, x, x, x, x, y) = (1, 1, 1, 1, 1, 1, 1, 1, 4, (byte)2);
        System.Console.WriteLine(x + "" "" + y);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "4 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void AssigningTypelessTuple()
        {
            string source = @"
class C
{
    static void Main()
    {
        string x = ""goodbye"";
        string y;

        (x, y) = (null, ""hello"");
        System.Console.WriteLine($""{x}{y}"");
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "hello", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
            comp.VerifyIL("C.Main", @"
{
  // Code size       52 (0x34)
  .maxstack  3
  .locals init (string V_0, //x
                string V_1, //y
                string V_2,
                string V_3)
  IL_0000:  ldstr      ""goodbye""
  IL_0005:  stloc.0
  IL_0006:  ldnull
  IL_0007:  ldstr      ""hello""
  IL_000c:  newobj     ""System.ValueTuple<string, string>..ctor(string, string)""
  IL_0011:  dup
  IL_0012:  ldfld      ""string System.ValueTuple<string, string>.Item1""
  IL_0017:  stloc.2
  IL_0018:  ldfld      ""string System.ValueTuple<string, string>.Item2""
  IL_001d:  stloc.3
  IL_001e:  ldloc.2
  IL_001f:  stloc.0
  IL_0020:  ldloc.3
  IL_0021:  stloc.1
  IL_0022:  ldstr      ""{0}{1}""
  IL_0027:  ldloc.0
  IL_0028:  ldloc.1
  IL_0029:  call       ""string string.Format(string, object, object)""
  IL_002e:  call       ""void System.Console.WriteLine(string)""
  IL_0033:  ret
}
");
        }

        [Fact]
        public void ValueTupleReturnIsNotEmittedIfUnused()
        {
            string source = @"
class C
{
    public static void Main()
    {
        int x, y;
        (x, y) = new C();
    }

    public void Deconstruct(out int a, out int b)
    {
        a = 1;
        b = 2;
    }
}
";
            var comp = CompileAndVerify(source, additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
            comp.VerifyIL("C.Main",
@"{
  // Code size       19 (0x13)
  .maxstack  3
  .locals init (int V_0,
                int V_1,
                int V_2)
  IL_0000:  newobj     ""C..ctor()""
  IL_0005:  ldloca.s   V_0
  IL_0007:  ldloca.s   V_1
  IL_0009:  call       ""void C.Deconstruct(out int, out int)""
  IL_000e:  ldloc.0
  IL_000f:  ldloc.1
  IL_0010:  stloc.2
  IL_0011:  pop
  IL_0012:  ret
}");
        }

        [Fact]
        public void ValueTupleReturnIsEmittedIfUsed()
        {
            string source = @"
class C
{
    public static void Main()
    {
        int x, y;
        var z = ((x, y) = new C());
        z.ToString();
    }

    public void Deconstruct(out int a, out int b)
    {
        a = 1;
        b = 2;
    }
}
";
            var comp = CompileAndVerify(source, additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
            comp.VerifyIL("C.Main",
@"{
  // Code size       39 (0x27)
  .maxstack  3
  .locals init (System.ValueTuple<int, int> V_0, //z
                int V_1,
                int V_2,
                int V_3)
  IL_0000:  newobj     ""C..ctor()""
  IL_0005:  ldloca.s   V_1
  IL_0007:  ldloca.s   V_2
  IL_0009:  call       ""void C.Deconstruct(out int, out int)""
  IL_000e:  ldloc.1
  IL_000f:  ldloc.2
  IL_0010:  stloc.3
  IL_0011:  ldloc.3
  IL_0012:  newobj     ""System.ValueTuple<int, int>..ctor(int, int)""
  IL_0017:  stloc.0
  IL_0018:  ldloca.s   V_0
  IL_001a:  constrained. ""System.ValueTuple<int, int>""
  IL_0020:  callvirt   ""string object.ToString()""
  IL_0025:  pop
  IL_0026:  ret
}");
        }

        [Fact]
        public void ValueTupleReturnIsEmittedIfUsedInLambda()
        {
            string source = @"
class C
{
    static void F(System.Action a) { }
    static void F<T>(System.Func<T> f) { System.Console.Write(f().ToString()); }
    static void Main()
    {
        int x, y;
        F(() => (x, y) = (1, 2));
    }
}
";
            var comp = CompileAndVerify(source, expectedOutput: "(1, 2)", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void AssigningIntoProperties()
        {
            string source = @"
class C
{
    static long x { set { System.Console.WriteLine($""setX {value}""); } }
    static string y { get; set; }

    static void Main()
    {
        (x, y) = new C();
        System.Console.WriteLine(y);
    }

    public void Deconstruct(out int a, out string b)
    {
        a = 1;
        b = ""hello"";
    }
}
";
            string expected =
@"setX 1
hello";
            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void AssigningTupleIntoProperties()
        {
            string source = @"
class C
{
    static long x { set { System.Console.WriteLine($""setX {value}""); } }
    static string y { get; set; }

    static void Main()
    {
        (x, y) = (1, ""hello"");
        System.Console.WriteLine(y);
    }
}
";
            string expected =
@"setX 1
hello";
            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void Swap()
        {
            string source = @"
class C
{
    static int x = 2;
    static int y = 4;

    static void Main()
    {
        Swap();
        System.Console.WriteLine(x + "" "" + y);
    }

    static void Swap()
    {
        (x, y) = (y, x);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "4 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
            comp.VerifyIL("C.Swap", @"
{
  // Code size       41 (0x29)
  .maxstack  2
  .locals init (int V_0,
                int V_1)
  IL_0000:  ldsfld     ""int C.y""
  IL_0005:  ldsfld     ""int C.x""
  IL_000a:  newobj     ""System.ValueTuple<int, int>..ctor(int, int)""
  IL_000f:  dup
  IL_0010:  ldfld      ""int System.ValueTuple<int, int>.Item1""
  IL_0015:  stloc.0
  IL_0016:  ldfld      ""int System.ValueTuple<int, int>.Item2""
  IL_001b:  stloc.1
  IL_001c:  ldloc.0
  IL_001d:  stsfld     ""int C.x""
  IL_0022:  ldloc.1
  IL_0023:  stsfld     ""int C.y""
  IL_0028:  ret
}

");
        }

        [Fact]
        public void CircularFlow()
        {
            string source = @"
class C
{
    static void Main()
    {
        (object i, object ii) x = (1,2);
        object y;

        (x.ii, y) = x;
        System.Console.WriteLine(x + "" "" + y);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "(1, 1) 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        [CompilerTrait(CompilerFeature.RefLocalsReturns)]
        public void CircularFlow2()
        {
            string source = @"
class C
{
    static void Main()
    {
        (object i, object ii) x = (1,2);
        object y;

        ref var a = ref x;

        (a.ii, y) = x;
        System.Console.WriteLine(x + "" "" + y);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "(1, 1) 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular.WithRefsFeature());
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeconstructUsingBaseDeconstructMethod()
        {
            string source = @"
class Base
{
    public void Deconstruct(out int a, out int b) { a = 1; b = 2; }
}
class C : Base
{
    static void Main()
    {
        int x, y;
        (x, y) = new C();

        System.Console.WriteLine(x + "" "" + y);
    }

    public void Deconstruct(out int c) { c = 42; }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular.WithRefsFeature());
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeconstructUsingExtensionMethod()
        {
            string source = @"
using System;
class C
{
    static void Main()
    {
        int x;
        string y, z;
        (x, (y, z)) = Tuple.Create(1, Tuple.Create(""hello"", ""world""));

        System.Console.WriteLine(x + "" "" + y + "" "" + z);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello world", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular.WithRefsFeature());
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NestedDeconstructUsingExtensionMethod()
        {
            string source = @"
using System;
class C
{
    static void Main()
    {
        int x;
        string y, z;
        (x, (y, z)) = Tuple.Create(1, Tuple.Create(""hello"", ""world""));

        System.Console.WriteLine(x + "" "" + y + "" "" + z);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello world", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular.WithRefsFeature());
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void OverrideDeconstruct()
        {
            string source = @"
class Base
{
    public virtual void Deconstruct(out int a, out string b) { a = 1; b = ""hello""; }
}
class C : Base
{
    static void Main()
    {
        int x;
        string y;
        (x, y) = new C();
    }
    public override void Deconstruct(out int a, out string b) { a = 1; b = ""hello""; System.Console.WriteLine(""override""); }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "override", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular.WithRefsFeature());
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeconstructRefTuple()
        {
            string template = @"
using System;
class C
{
    static void Main()
    {
        int VARIABLES; // int x1, x2, ...
        (VARIABLES) = (TUPLE).ToTuple(); // (x1, x2, ...) = (1, 2, ...).ToTuple();

        System.Console.WriteLine(OUTPUT);
    }
}
";
            for (int i = 2; i <= 21; i++)
            {
                var tuple = String.Join(", ", Enumerable.Range(1, i).Select(n => n.ToString()));
                var variables = String.Join(", ", Enumerable.Range(1, i).Select(n => $"x{n}"));
                var output = String.Join(@" + "" "" + ", Enumerable.Range(1, i).Select(n => $"x{n}"));
                var expected = String.Join(" ", Enumerable.Range(1, i).Select(n => n));

                var source = template.Replace("VARIABLES", variables).Replace("TUPLE", tuple).Replace("OUTPUT", output);
                var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular.WithRefsFeature());
                comp.VerifyDiagnostics();
            }
        }

        [Fact]
        public void DeconstructExtensionMethod()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;

        (x, y) = new C();
        System.Console.WriteLine(x + "" "" + y);
    }
}
static class D
{
    public static void Deconstruct(this C value, out int a, out string b)
    {
        a = 1;
        b = ""hello"";
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeconstructGenericExtensionMethod()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;

        (x, y) = new C1<string>();
    }
}

public class C1<T> { }

static class Extension
{
    public static void Deconstruct<T>(this C1<T> value, out int a, out T b)
    {
        a = 2;
        b = default(T);
        System.Console.WriteLine(""Deconstructed"");
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "Deconstructed", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NestedTupleAssignment()
        {
            string source = @"
class C
{
    static void Main()
    {
        int x;
        string y, z;

        (x, (y, z)) = (1, (""a"", ""b""));
        System.Console.WriteLine(x + "" "" + y + "" "" + z);
    }
}
";
            var comp = CompileAndVerify(source, expectedOutput: "1 a b", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NestedTypelessTupleAssignment()
        {
            string source = @"
class C
{
    static void Main()
    {
        string x, y, z;

        (x, (y, z)) = (null, (null, null));
        System.Console.WriteLine(""nothing"" + x + y + z);
    }
}
";
            var comp = CompileAndVerify(source, expectedOutput: "nothing", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NestedDeconstructAssignment()
        {
            string source = @"
class C
{
    static void Main()
    {
        int x;
        string y, z;

        (x, (y, z)) = new D1();
        System.Console.WriteLine(x + "" "" + y + "" "" + z);
    }
}
class D1
{
    public void Deconstruct(out int item1, out D2 item2)
    {
        item1 = 1;
        item2 = new D2();
    }
}
class D2
{
    public void Deconstruct(out string item1, out string item2)
    {
        item1 = ""a"";
        item2 = ""b"";
    }
}
";
            var comp = CompileAndVerify(source, expectedOutput: "1 a b", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NestedMixedAssignment1()
        {
            string source = @"
class C
{
    static void Main()
    {
        int x, y, z;

        (x, (y, z)) = (1, new D1());
        System.Console.WriteLine(x + "" "" + y + "" "" + z);
    }
}
class D1
{
    public void Deconstruct(out int item1, out int item2)
    {
        item1 = 2;
        item2 = 3;
    }
}
";
            var comp = CompileAndVerify(source, expectedOutput: "1 2 3", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NestedMixedAssignment2()
        {
            string source = @"
class C
{
    static void Main()
    {
        int x;
        string y, z;

        (x, (y, z)) = new D1();
        System.Console.WriteLine(x + "" "" + y + "" "" + z);
    }
}
class D1
{
    public void Deconstruct(out int item1, out (string, string) item2)
    {
        item1 = 1;
        item2 = (""a"", ""b"");
    }
}
";
            var comp = CompileAndVerify(source, expectedOutput: "1 a b", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void VerifyNestedExecutionOrder()
        {
            string source = @"
using System;
class C
{
    int x { set { Console.WriteLine($""setX""); } }
    int y { set { Console.WriteLine($""setY""); } }
    int z { set { Console.WriteLine($""setZ""); } }

    C getHolderForX() { Console.WriteLine(""getHolderforX""); return this; }
    C getHolderForY() { Console.WriteLine(""getHolderforY""); return this; }
    C getHolderForZ() { Console.WriteLine(""getHolderforZ""); return this; }
    C getDeconstructReceiver() { Console.WriteLine(""getDeconstructReceiver""); return this; }

    static void Main()
    {
        C c = new C();
        (c.getHolderForX().x, (c.getHolderForY().y, c.getHolderForZ().z)) = c.getDeconstructReceiver();
    }
    public void Deconstruct(out D1 x, out C1 t) { x = new D1(); t = new C1(); Console.WriteLine(""Deconstruct1""); }
}
class C1
{
    public void Deconstruct(out D2 y, out D3 z) { y = new D2(); z = new D3(); Console.WriteLine(""Deconstruct2""); }
}
class D1
{
    public static implicit operator int(D1 d) { Console.WriteLine(""Conversion1""); return 1; }
}
class D2
{
    public static implicit operator int(D2 d) { Console.WriteLine(""Conversion2""); return 2; }
}
class D3
{
    public static implicit operator int(D3 d) { Console.WriteLine(""Conversion3""); return 3; }
}
";

            string expected =
@"getHolderforX
getHolderforY
getHolderforZ
getDeconstructReceiver
Deconstruct1
Deconstruct2
Conversion1
Conversion2
Conversion3
setX
setY
setZ
";
            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void VerifyNestedExecutionOrder2()
        {
            string source = @"
using System;
class C
{
    static LongInteger x1 { set { Console.WriteLine($""setX1 {value}""); } }
    static LongInteger x2 { set { Console.WriteLine($""setX2 {value}""); } }
    static LongInteger x3 { set { Console.WriteLine($""setX3 {value}""); } }
    static LongInteger x4 { set { Console.WriteLine($""setX4 {value}""); } }
    static LongInteger x5 { set { Console.WriteLine($""setX5 {value}""); } }
    static LongInteger x6 { set { Console.WriteLine($""setX6 {value}""); } }
    static LongInteger x7 { set { Console.WriteLine($""setX7 {value}""); } }

    static void Main()
    {
        ((x1, (x2, x3)), ((x4, x5), (x6, x7))) = Pair.Create(Pair.Create(new Integer(1), Pair.Create(new Integer(2), new Integer(3))),
                                                      Pair.Create(Pair.Create(new Integer(4), new Integer(5)), Pair.Create(new Integer(6), new Integer(7))));
    }
}
" + commonSource;

            string expected =
@"Deconstructing ((1, (2, 3)), ((4, 5), (6, 7)))
Deconstructing (1, (2, 3))
Deconstructing (2, 3)
Deconstructing ((4, 5), (6, 7))
Deconstructing (4, 5)
Deconstructing (6, 7)
Converting 1
Converting 2
Converting 3
Converting 4
Converting 5
Converting 6
Converting 7
setX1 1
setX2 2
setX3 3
setX4 4
setX5 5
setX6 6
setX7 7";
            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void MixOfAssignments()
        {
            string source = @"
class C
{
    static void Main()
    {
        long x;
        string y;

        C a, b, c;
        c = new C();
        (x, y) = a = b = c;
        System.Console.WriteLine(x + "" "" + y);
    }

    public void Deconstruct(out int a, out string b)
    {
        a = 1;
        b = ""hello"";
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 hello", additionalRefs: s_valueTupleRefs);
            comp.VerifyDiagnostics();
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/12400")]
        [WorkItem(12400, "https://github.com/dotnet/roslyn/issues/12400")]
        public void AssignWithPostfixOperator()
        {
            string source = @"
class C
{
    int state = 1;

    static void Main()
    {
        long x;
        string y;
        C c = new C();
        (x, y) = c++;
        System.Console.WriteLine(x + "" "" + y);
    }

    public void Deconstruct(out int a, out string b)
    {
        a = state;
        b = ""hello"";
    }

    public static C operator ++(C c1)
    {
        return new C() { state = 2 };
    }
}
";
            // https://github.com/dotnet/roslyn/issues/12400
            // we expect "2 hello" instead, which means the evaluation order is wrong
            var comp = CompileAndVerify(source, expectedOutput: "1 hello");
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeconstructionDeclaration()
        {
            string source = @"
class C
{
    static void Main()
    {
        var (x1, x2) = (1, ""hello"");
        System.Console.WriteLine(x1 + "" "" + x2);
    }
}
";

            var comp = CompileAndVerify(source, additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, expectedOutput: "1 hello");
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NestedVarDeconstructionDeclaration()
        {
            string source = @"
class C
{
    static void Main()
    {
        var (x1, (x2, x3)) = (1, (2, ""hello""));
        System.Console.WriteLine(x1 + "" "" + x2 + "" "" + x3);
    }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionLocal(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionLocal(model, x2, x2Ref);

                var x3 = GetDeconstructionLocal(tree, "x3");
                var x3Ref = GetReference(tree, "x3");
                VerifyModelForDeconstructionLocal(model, x3, x3Ref);
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 2 hello", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NestedDeconstructionDeclaration()
        {
            string source = @"
class C
{
    static void Main()
    {
        (int x1, (int x2, string x3)) = (1, (2, ""hello""));
        System.Console.WriteLine(x1 + "" "" + x2 + "" "" + x3);
    }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionLocal(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionLocal(model, x2, x2Ref);

                var x3 = GetDeconstructionLocal(tree, "x3");
                var x3Ref = GetReference(tree, "x3");
                VerifyModelForDeconstructionLocal(model, x3, x3Ref);
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 2 hello", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void VarMethodExists()
        {
            string source = @"
class C
{
    static void Main()
    {
        int x1 = 1;
        int x2 = 1;
        var (x1, x2);
    }
    static void var(int a, int b) { System.Console.WriteLine(""var""); }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "var", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TypeMergingSuccess1()
        {
            string source = @"
class C
{
    static void Main()
    {
        (var (x1, x2), string x3) = ((1, 2), null);
        System.Console.WriteLine(x1 + "" "" + x2 + "" "" + x3);
    }
}
";
            var comp = CompileAndVerify(source, expectedOutput: " 1 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TypeMergingSuccess2()
        {
            string source = @"
class C
{
    static void Main()
    {
        (string x1, byte x2, var x3) = (null, 2, 3);
        System.Console.WriteLine(x1 + "" "" + x2 + "" "" + x3);
    }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var literal = tree.GetRoot().DescendantNodes().OfType<TupleExpressionSyntax>().Single();
                Assert.Equal(@"(null, 2, 3)", literal.ToString());
                Assert.Null(model.GetTypeInfo(literal).Type);
                Assert.Equal("(System.String, System.Byte, System.Int32)", model.GetTypeInfo(literal).ConvertedType.ToTestDisplayString());
                Assert.Equal(ConversionKind.ImplicitTupleLiteral, model.GetConversion(literal).Kind);
            };

            var comp = CompileAndVerify(source, expectedOutput: " 2 3", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TypeMergingSuccess3()
        {
            string source = @"
class C
{
    static void Main()
    {
        (string x1, var x2) = (null, (1, 2));
        System.Console.WriteLine(x1 + "" "" + x2);
    }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var literal = tree.GetRoot().DescendantNodes().OfType<TupleExpressionSyntax>().First();
                Assert.Equal(@"(null, (1, 2))", literal.ToString());
                Assert.Null(model.GetTypeInfo(literal).Type);
                Assert.Equal("(System.String, (System.Int32, System.Int32))", model.GetTypeInfo(literal).ConvertedType.ToTestDisplayString());
                Assert.Equal(ConversionKind.ImplicitTupleLiteral, model.GetConversion(literal).Kind);

                var nestedLiteral = literal.Arguments[1].Expression;
                Assert.Equal(@"(1, 2)", nestedLiteral.ToString());
                Assert.Equal("(System.Int32, System.Int32)", model.GetTypeInfo(nestedLiteral).Type.ToTestDisplayString());
                Assert.Equal("(System.Int32, System.Int32)", model.GetTypeInfo(nestedLiteral).ConvertedType.ToTestDisplayString());
            };

            var comp = CompileAndVerify(source, expectedOutput: " (1, 2)", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TypeMergingSuccess4()
        {
            string source = @"
class C
{
    static void Main()
    {
        ((string x1, byte x2, var x3), int x4) = (M(), 4);
        System.Console.WriteLine(x1 + "" "" + x2 + "" "" + x3 + "" "" + x4);
    }
    static (string, byte, int) M() { return (null, 2, 3); }
}
";
            var comp = CompileAndVerify(source, expectedOutput: " 2 3 4", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void VarVarDeclaration()
        {
            string source = @"
class C
{
    static void Main()
    {
        (var (x1, x2), var x3) = Pair.Create(Pair.Create(1, ""hello""), 2);
        System.Console.WriteLine(x1 + "" "" + x2 + "" "" + x3);
    }
}
" + commonSource;

            string expected =
@"Deconstructing ((1, hello), 2)
Deconstructing (1, hello)
1 hello 2";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionLocal(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionLocal(model, x2, x2Ref);

                var x3 = GetDeconstructionLocal(tree, "x3");
                var x3Ref = GetReference(tree, "x3");
                VerifyModelForDeconstructionLocal(model, x3, x3Ref);
            };

            var comp = CompileAndVerify(source, expectedOutput: expected, parseOptions: TestOptions.Regular, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        private static void VerifyModelForDeconstructionLocal(SemanticModel model, SingleVariableDesignationSyntax decl, params IdentifierNameSyntax[] references)
        {
            VerifyModelForDeconstruction(model, decl, LocalDeclarationKind.RegularVariable, references);
        }

        private static void VerifyModelForDeconstructionFor(SemanticModel model, SingleVariableDesignationSyntax decl, params IdentifierNameSyntax[] references)
        {
            VerifyModelForDeconstruction(model, decl, LocalDeclarationKind.ForInitializerVariable, references);
        }

        private static void VerifyModelForDeconstructionForeach(SemanticModel model, SingleVariableDesignationSyntax decl, params IdentifierNameSyntax[] references)
        {
            VerifyModelForDeconstruction(model, decl, LocalDeclarationKind.ForEachIterationVariable, references);
        }

        private static void VerifyModelForDeconstruction(SemanticModel model, SingleVariableDesignationSyntax decl, LocalDeclarationKind kind, params IdentifierNameSyntax[] references)
        {
            var symbol = model.GetDeclaredSymbol(decl);
            Assert.Equal(decl.Identifier.ValueText, symbol.Name);
            Assert.Equal(kind, ((LocalSymbol)symbol).DeclarationKind);
            Assert.Same(symbol, model.GetDeclaredSymbol((SyntaxNode)decl));
            Assert.Same(symbol, model.LookupSymbols(decl.SpanStart, name: decl.Identifier.ValueText).Single());
            Assert.True(model.LookupNames(decl.SpanStart).Contains(decl.Identifier.ValueText));

            var local = (SourceLocalSymbol)symbol;
            var typeSyntax = GetTypeSyntax(decl);
            if (local.IsVar && local.Type.IsErrorType())
            {
                Assert.Null(model.GetSymbolInfo(typeSyntax).Symbol);
            }
            else
            {
                if (typeSyntax != null)
                {
                    Assert.Equal(local.Type, model.GetSymbolInfo(typeSyntax).Symbol);
                }
            }

            foreach (var reference in references)
            {
                Assert.Same(symbol, model.GetSymbolInfo(reference).Symbol);
                Assert.Same(symbol, model.LookupSymbols(reference.SpanStart, name: decl.Identifier.ValueText).Single());
                Assert.True(model.LookupNames(reference.SpanStart).Contains(decl.Identifier.ValueText));
                Assert.Equal(local.Type, model.GetTypeInfo(reference).Type);
            }
        }

        private static TypeSyntax GetTypeSyntax(SingleVariableDesignationSyntax decl)
        {
            return (decl.Parent as TypedVariableComponentSyntax)?.Type;
        }

        private static SingleVariableDesignationSyntax GetDeconstructionLocal(SyntaxTree tree, string name)
        {
            return tree.GetRoot().DescendantNodes().OfType<SingleVariableDesignationSyntax>().Where(d => d.Identifier.ValueText == name).Single();
        }

        private static IdentifierNameSyntax GetReference(SyntaxTree tree, string name)
        {
            return GetReferences(tree, name).Single();
        }

        private static IdentifierNameSyntax[] GetReferences(SyntaxTree tree, string name, int count)
        {
            var nameRef = GetReferences(tree, name).ToArray();
            Assert.Equal(count, nameRef.Length);
            return nameRef;
        }

        private static IEnumerable<IdentifierNameSyntax> GetReferences(SyntaxTree tree, string name)
        {
            return tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().Where(id => id.Identifier.ValueText == name);
        }

        [Fact]
        public void DeclarationWithActualVarType()
        {
            string source = @"
class C
{
    static void Main()
    {
        (var x1, int x2) = (new var(), 2);
        System.Console.WriteLine(x1 + "" "" + x2);
    }
}
class var
{
    public override string ToString() { return ""var""; }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionLocal(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionLocal(model, x2, x2Ref);

                // extra checks on x1
                var x1Type = GetTypeSyntax(x1);
                Assert.Equal(SymbolKind.NamedType, model.GetSymbolInfo(x1Type).Symbol.Kind);
                Assert.Equal("var", model.GetSymbolInfo(x1Type).Symbol.ToDisplayString());

                // extra checks on x2
                var x2Type = GetTypeSyntax(x2);
                Assert.Equal(SymbolKind.NamedType, model.GetSymbolInfo(x2Type).Symbol.Kind);
                Assert.Equal("int", model.GetSymbolInfo(x2Type).Symbol.ToDisplayString());
            };

            var comp = CompileAndVerify(source, expectedOutput: "var 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeclarationWithImplicitVarType()
        {
            string source = @"
class C
{
    static void Main()
    {
        (var x1, var x2) = (1, 2);
        var (x3, x4) = (3, 4);
        System.Console.WriteLine(x1 + "" "" + x2 + "" "" + x3 + "" "" + x4);
    }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionLocal(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionLocal(model, x2, x2Ref);

                var x3 = GetDeconstructionLocal(tree, "x3");
                var x3Ref = GetReference(tree, "x3");
                VerifyModelForDeconstructionLocal(model, x3, x3Ref);

                var x4 = GetDeconstructionLocal(tree, "x4");
                var x4Ref = GetReference(tree, "x4");
                VerifyModelForDeconstructionLocal(model, x4, x4Ref);

                // extra checks on x1
                var x1Type = GetTypeSyntax(x1);
                Assert.Equal(SymbolKind.NamedType, model.GetSymbolInfo(x1Type).Symbol.Kind);
                Assert.Equal("int", model.GetSymbolInfo(x1Type).Symbol.ToDisplayString());
                Assert.Null(model.GetAliasInfo(x1Type));

                var x34Var = (TypedVariableComponentSyntax)x3.Parent.Parent;
                Assert.Equal("var", x34Var.Type.ToString());
                Assert.Null(model.GetSymbolInfo(x34Var.Type).Symbol); // The var in `var (x3, x4)` has no symbol
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 2 3 4", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DeclarationWithAliasedVarType()
        {
            string source = @"
using var = D;
class C
{
    static void Main()
    {
        (var x1, int x2) = (new var(), 2);
        System.Console.WriteLine(x1 + "" "" + x2);
    }
}
class D
{
    public override string ToString() { return ""var""; }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionLocal(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionLocal(model, x2, x2Ref);

                // extra checks on x1
                var x1Type = GetTypeSyntax(x1);
                Assert.Equal(SymbolKind.NamedType, model.GetSymbolInfo(x1Type).Symbol.Kind);
                Assert.Equal("D", model.GetSymbolInfo(x1Type).Symbol.ToDisplayString());
                var x1Alias = model.GetAliasInfo(x1Type);
                Assert.Equal(SymbolKind.NamedType, x1Alias.Target.Kind);
                Assert.Equal("D", x1Alias.Target.ToDisplayString());

                // extra checks on x2
                var x2Type = GetTypeSyntax(x2);
                Assert.Equal(SymbolKind.NamedType, model.GetSymbolInfo(x2Type).Symbol.Kind);
                Assert.Equal("int", model.GetSymbolInfo(x2Type).Symbol.ToDisplayString());
                Assert.Null(model.GetAliasInfo(x2Type));
            };

            var comp = CompileAndVerify(source, expectedOutput: "var 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForWithImplicitVarType()
        {
            string source = @"
class C
{
    static void Main()
    {
        for (var (x1, x2) = (1, 2); x1 < 2; (x1, x2) = (x1 + 1, x2 + 1))
        {
            System.Console.WriteLine(x1 + "" "" + x2);
        }
    }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReferences(tree, "x1", 4);
                VerifyModelForDeconstructionFor(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReferences(tree, "x2", 3);
                VerifyModelForDeconstructionFor(model, x2, x2Ref);

                // extra check on var
                var x12Var = (TypedVariableComponentSyntax)x1.Parent.Parent;
                Assert.Equal("var", x12Var.Type.ToString());
                Assert.Null(model.GetSymbolInfo(x12Var.Type).Symbol); // The var in `var (x1, x2)` has no symbol
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForWithActualVarType()
        {
            string source = @"
class C
{
    static void Main()
    {
        for ((int x1, var x2) = (1, new var()); x1 < 2; x1++)
        {
            System.Console.WriteLine(x1 + "" "" + x2);
        }
    }
}
class var
{
    public override string ToString() { return ""var""; }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReferences(tree, "x1", 3);
                VerifyModelForDeconstructionFor(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionFor(model, x2, x2Ref);

                // extra checks on x1
                var x1Type = GetTypeSyntax(x1);
                Assert.Equal(SymbolKind.NamedType, model.GetSymbolInfo(x1Type).Symbol.Kind);
                Assert.Equal("int", model.GetSymbolInfo(x1Type).Symbol.ToDisplayString());

                // extra checks on x2
                var x2Type = GetTypeSyntax(x2);
                Assert.Equal(SymbolKind.NamedType, model.GetSymbolInfo(x2Type).Symbol.Kind);
                Assert.Equal("var", model.GetSymbolInfo(x2Type).Symbol.ToDisplayString());
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 var", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForWithTypes()
        {
            string source = @"
class C
{
    static void Main()
    {
        for ((int x1, var x2) = (1, 2); x1 < 2; x1++)
        {
            System.Console.WriteLine(x1 + "" "" + x2);
        }
    }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReferences(tree, "x1", 3);
                VerifyModelForDeconstructionFor(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionFor(model, x2, x2Ref);

                // extra checks on x1
                var x1Type = GetTypeSyntax(x1);
                Assert.Equal(SymbolKind.NamedType, model.GetSymbolInfo(x1Type).Symbol.Kind);
                Assert.Equal("int", model.GetSymbolInfo(x1Type).Symbol.ToDisplayString());

                // extra checks on x2
                var x2Type = GetTypeSyntax(x2);
                Assert.Equal(SymbolKind.NamedType, model.GetSymbolInfo(x2Type).Symbol.Kind);
                Assert.Equal("int", model.GetSymbolInfo(x2Type).Symbol.ToDisplayString());
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForEachIEnumerableDeclarationWithImplicitVarType()
        {
            string source = @"
using System.Collections.Generic;
class C
{
    static void Main()
    {
        foreach (var (x1, x2) in M())
        {
            Print(x1, x2);
        }
    }
    static IEnumerable<(int, int)> M() { yield return (1, 2); }
    static void Print(object a, object b) { System.Console.WriteLine(a + "" "" + b); }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionForeach(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionForeach(model, x2, x2Ref);

                // extra check on var
                var x12Var = (TypedVariableComponentSyntax)x1.Parent.Parent;
                Assert.Equal("var", x12Var.Type.ToString());
                Assert.Null(model.GetSymbolInfo(x12Var.Type).Symbol); // The var in `var (x1, x2)` has no symbol
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 2", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();

            comp.VerifyIL("C.Main",
@"{
  // Code size       72 (0x48)
  .maxstack  2
  .locals init (System.Collections.Generic.IEnumerator<(int, int)> V_0,
                int V_1, //x1
                int V_2, //x2
                int V_3)
  IL_0000:  call       ""System.Collections.Generic.IEnumerable<(int, int)> C.M()""
  IL_0005:  callvirt   ""System.Collections.Generic.IEnumerator<(int, int)> System.Collections.Generic.IEnumerable<(int, int)>.GetEnumerator()""
  IL_000a:  stloc.0
  .try
  {
    IL_000b:  br.s       IL_0033
    IL_000d:  ldloc.0
    IL_000e:  callvirt   ""(int, int) System.Collections.Generic.IEnumerator<(int, int)>.Current.get""
    IL_0013:  dup
    IL_0014:  ldfld      ""int System.ValueTuple<int, int>.Item1""
    IL_0019:  stloc.3
    IL_001a:  ldfld      ""int System.ValueTuple<int, int>.Item2""
    IL_001f:  ldloc.3
    IL_0020:  stloc.1
    IL_0021:  stloc.2
    IL_0022:  ldloc.1
    IL_0023:  box        ""int""
    IL_0028:  ldloc.2
    IL_0029:  box        ""int""
    IL_002e:  call       ""void C.Print(object, object)""
    IL_0033:  ldloc.0
    IL_0034:  callvirt   ""bool System.Collections.IEnumerator.MoveNext()""
    IL_0039:  brtrue.s   IL_000d
    IL_003b:  leave.s    IL_0047
  }
  finally
  {
    IL_003d:  ldloc.0
    IL_003e:  brfalse.s  IL_0046
    IL_0040:  ldloc.0
    IL_0041:  callvirt   ""void System.IDisposable.Dispose()""
    IL_0046:  endfinally
  }
  IL_0047:  ret
}
");
        }

        [Fact]
        public void ForEachSZArrayDeclarationWithImplicitVarType()
        {
            string source = @"
class C
{
    static void Main()
    {
        foreach (var (x1, x2) in M())
        {
            System.Console.Write(x1 + "" "" + x2 + "" - "");
        }
    }
    static (int, int)[] M() { return new[] { (1, 2), (3, 4) }; }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                var symbol = model.GetDeclaredSymbol(x1);

                VerifyModelForDeconstructionForeach(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionForeach(model, x2, x2Ref);

                // extra check on var
                var x12Var = (TypedVariableComponentSyntax)x1.Parent.Parent;
                Assert.Equal("var", x12Var.Type.ToString());
                Assert.Null(model.GetSymbolInfo(x12Var.Type).Symbol); // The var in `var (x1, x2)` has no symbol
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 2 - 3 4 -", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
            comp.VerifyIL("C.Main",
@"{
  // Code size       95 (0x5f)
  .maxstack  4
  .locals init ((int, int)[] V_0,
                int V_1,
                int V_2, //x1
                int V_3, //x2
                int V_4)
  IL_0000:  call       ""(int, int)[] C.M()""
  IL_0005:  stloc.0
  IL_0006:  ldc.i4.0
  IL_0007:  stloc.1
  IL_0008:  br.s       IL_0058
  IL_000a:  ldloc.0
  IL_000b:  ldloc.1
  IL_000c:  ldelem     ""System.ValueTuple<int, int>""
  IL_0011:  dup
  IL_0012:  ldfld      ""int System.ValueTuple<int, int>.Item1""
  IL_0017:  stloc.s    V_4
  IL_0019:  ldfld      ""int System.ValueTuple<int, int>.Item2""
  IL_001e:  ldloc.s    V_4
  IL_0020:  stloc.2
  IL_0021:  stloc.3
  IL_0022:  ldc.i4.4
  IL_0023:  newarr     ""object""
  IL_0028:  dup
  IL_0029:  ldc.i4.0
  IL_002a:  ldloc.2
  IL_002b:  box        ""int""
  IL_0030:  stelem.ref
  IL_0031:  dup
  IL_0032:  ldc.i4.1
  IL_0033:  ldstr      "" ""
  IL_0038:  stelem.ref
  IL_0039:  dup
  IL_003a:  ldc.i4.2
  IL_003b:  ldloc.3
  IL_003c:  box        ""int""
  IL_0041:  stelem.ref
  IL_0042:  dup
  IL_0043:  ldc.i4.3
  IL_0044:  ldstr      "" - ""
  IL_0049:  stelem.ref
  IL_004a:  call       ""string string.Concat(params object[])""
  IL_004f:  call       ""void System.Console.Write(string)""
  IL_0054:  ldloc.1
  IL_0055:  ldc.i4.1
  IL_0056:  add
  IL_0057:  stloc.1
  IL_0058:  ldloc.1
  IL_0059:  ldloc.0
  IL_005a:  ldlen
  IL_005b:  conv.i4
  IL_005c:  blt.s      IL_000a
  IL_005e:  ret
}");
        }

        [Fact]
        public void ForEachMDArrayDeclarationWithImplicitVarType()
        {
            string source = @"
class C
{
    static void Main()
    {
        foreach (var (x1, x2) in M())
        {
            Print(x1, x2);
        }
    }
    static (int, int)[,] M() { return new (int, int)[2, 2] { { (1, 2), (3, 4) }, { (5, 6), (7, 8) } }; }
    static void Print(object a, object b) { System.Console.Write(a + "" "" + b + "" - ""); }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionForeach(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionForeach(model, x2, x2Ref);

                // extra check on var
                var x12Var = (TypedVariableComponentSyntax)x1.Parent.Parent;
                Assert.Equal("var", x12Var.Type.ToString());
                Assert.Null(model.GetSymbolInfo(x12Var.Type).Symbol); // The var in `var (x1, x2)` has no symbol
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 2 - 3 4 - 5 6 - 7 8 -", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
            comp.VerifyIL("C.Main",
@"{
  // Code size      110 (0x6e)
  .maxstack  3
  .locals init ((int, int)[,] V_0,
                int V_1,
                int V_2,
                int V_3,
                int V_4,
                int V_5, //x1
                int V_6, //x2
                int V_7)
  IL_0000:  call       ""(int, int)[,] C.M()""
  IL_0005:  stloc.0
  IL_0006:  ldloc.0
  IL_0007:  ldc.i4.0
  IL_0008:  callvirt   ""int System.Array.GetUpperBound(int)""
  IL_000d:  stloc.1
  IL_000e:  ldloc.0
  IL_000f:  ldc.i4.1
  IL_0010:  callvirt   ""int System.Array.GetUpperBound(int)""
  IL_0015:  stloc.2
  IL_0016:  ldloc.0
  IL_0017:  ldc.i4.0
  IL_0018:  callvirt   ""int System.Array.GetLowerBound(int)""
  IL_001d:  stloc.3
  IL_001e:  br.s       IL_0069
  IL_0020:  ldloc.0
  IL_0021:  ldc.i4.1
  IL_0022:  callvirt   ""int System.Array.GetLowerBound(int)""
  IL_0027:  stloc.s    V_4
  IL_0029:  br.s       IL_0060
  IL_002b:  ldloc.0
  IL_002c:  ldloc.3
  IL_002d:  ldloc.s    V_4
  IL_002f:  call       ""(int, int)[*,*].Get""
  IL_0034:  dup
  IL_0035:  ldfld      ""int System.ValueTuple<int, int>.Item1""
  IL_003a:  stloc.s    V_7
  IL_003c:  ldfld      ""int System.ValueTuple<int, int>.Item2""
  IL_0041:  ldloc.s    V_7
  IL_0043:  stloc.s    V_5
  IL_0045:  stloc.s    V_6
  IL_0047:  ldloc.s    V_5
  IL_0049:  box        ""int""
  IL_004e:  ldloc.s    V_6
  IL_0050:  box        ""int""
  IL_0055:  call       ""void C.Print(object, object)""
  IL_005a:  ldloc.s    V_4
  IL_005c:  ldc.i4.1
  IL_005d:  add
  IL_005e:  stloc.s    V_4
  IL_0060:  ldloc.s    V_4
  IL_0062:  ldloc.2
  IL_0063:  ble.s      IL_002b
  IL_0065:  ldloc.3
  IL_0066:  ldc.i4.1
  IL_0067:  add
  IL_0068:  stloc.3
  IL_0069:  ldloc.3
  IL_006a:  ldloc.1
  IL_006b:  ble.s      IL_0020
  IL_006d:  ret
}");
        }

        [Fact]
        public void ForEachStringDeclarationWithImplicitVarType()
        {
            string source = @"
class C
{
    static void Main()
    {
        foreach (var (x1, x2) in M())
        {
            Print(x1, x2);
        }
    }
    static string M() { return ""123""; }
    static void Print(object a, object b) { System.Console.Write(a + "" "" + b + "" - ""); }
}
static class Extension
{
    public static void Deconstruct(this char value, out int item1, out int item2)
    {
        item1 = item2 = System.Int32.Parse(value.ToString());
    }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionForeach(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionForeach(model, x2, x2Ref);

                // extra check on var
                var x12Var = (TypedVariableComponentSyntax)x1.Parent.Parent;
                Assert.Equal("var", x12Var.Type.ToString());
                Assert.Null(model.GetSymbolInfo(x12Var.Type).Symbol); // The var in `var (x1, x2)` has no symbol
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 1 - 2 2 - 3 3 - ", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
            comp.VerifyIL("C.Main",
@"{
  // Code size       64 (0x40)
  .maxstack  3
  .locals init (string V_0,
                int V_1,
                int V_2, //x2
                int V_3,
                int V_4,
                int V_5)
  IL_0000:  call       ""string C.M()""
  IL_0005:  stloc.0
  IL_0006:  ldc.i4.0
  IL_0007:  stloc.1
  IL_0008:  br.s       IL_0036
  IL_000a:  ldloc.0
  IL_000b:  ldloc.1
  IL_000c:  callvirt   ""char string.this[int].get""
  IL_0011:  ldloca.s   V_3
  IL_0013:  ldloca.s   V_4
  IL_0015:  call       ""void Extension.Deconstruct(char, out int, out int)""
  IL_001a:  ldloc.3
  IL_001b:  ldloc.s    V_4
  IL_001d:  stloc.s    V_5
  IL_001f:  ldloc.s    V_5
  IL_0021:  stloc.2
  IL_0022:  box        ""int""
  IL_0027:  ldloc.2
  IL_0028:  box        ""int""
  IL_002d:  call       ""void C.Print(object, object)""
  IL_0032:  ldloc.1
  IL_0033:  ldc.i4.1
  IL_0034:  add
  IL_0035:  stloc.1
  IL_0036:  ldloc.1
  IL_0037:  ldloc.0
  IL_0038:  callvirt   ""int string.Length.get""
  IL_003d:  blt.s      IL_000a
  IL_003f:  ret
}");
        }

        [Fact]
        public void ForEachIEnumerableDeclarationWithNesting()
        {
            string source = @"
using System.Collections.Generic;
class C
{
    static void Main()
    {
        foreach ((int x1, var (x2, x3), (int x4, int x5)) in M())
        {
            System.Console.Write(x1 + "" "" + x2 + "" "" + x3 + "" "" + x4 + "" "" + x5 + "" - "");
        }
    }
    static IEnumerable<(int, (int, int), (int, int))> M() { yield return (1, (2, 3), (4, 5)); yield return (6, (7, 8), (9, 10)); }
}
";

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionForeach(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionForeach(model, x2, x2Ref);

                var x3 = GetDeconstructionLocal(tree, "x3");
                var x3Ref = GetReference(tree, "x3");
                VerifyModelForDeconstructionForeach(model, x3, x3Ref);

                var x4 = GetDeconstructionLocal(tree, "x4");
                var x4Ref = GetReference(tree, "x4");
                VerifyModelForDeconstructionForeach(model, x4, x4Ref);

                var x5 = GetDeconstructionLocal(tree, "x5");
                var x5Ref = GetReference(tree, "x5");
                VerifyModelForDeconstructionForeach(model, x5, x5Ref);

                // extra check on var
                var x23Var = (TypedVariableComponentSyntax)x2.Parent.Parent;
                Assert.Equal("var", x23Var.Type.ToString());
                Assert.Null(model.GetSymbolInfo(x23Var.Type).Symbol); // The var in `var (x2, x3)` has no symbol
            };

            var comp = CompileAndVerify(source, expectedOutput: "1 2 3 4 5 - 6 7 8 9 10 -", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForEachSZArrayDeclarationWithNesting()
        {
            string source = @"
class C
{
    static void Main()
    {
        foreach ((int x1, var (x2, x3), (int x4, int x5)) in M())
        {
            System.Console.Write(x1 + "" "" + x2 + "" "" + x3 + "" "" + x4 + "" "" + x5 + "" - "");
        }
    }
    static (int, (int, int), (int, int))[] M() { return new[] { (1, (2, 3), (4, 5)), (6, (7, 8), (9, 10)) }; }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 2 3 4 5 - 6 7 8 9 10 -", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForEachMDArrayDeclarationWithNesting()
        {
            string source = @"
class C
{
    static void Main()
    {
        foreach ((int x1, var (x2, x3), (int x4, int x5)) in M())
        {
            System.Console.Write(x1 + "" "" + x2 + "" "" + x3 + "" "" + x4 + "" "" + x5 + "" - "");
        }
    }
    static (int, (int, int), (int, int))[,] M() { return new(int, (int, int), (int, int))[1, 2] { { (1, (2, 3), (4, 5)), (6, (7, 8), (9, 10)) } }; }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 2 3 4 5 - 6 7 8 9 10 -", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForEachStringDeclarationWithNesting()
        {
            string source = @"
class C
{
    static void Main()
    {
        foreach ((int x1, var (x2, x3)) in M())
        {
            System.Console.Write(x1 + "" "" + x2 + "" "" + x3 + "" - "");
        }
    }
    static string M() { return ""12""; }
}
static class Extension
{
    public static void Deconstruct(this char value, out int item1, out (int, int) item2)
    {
        item1 = System.Int32.Parse(value.ToString());
        item2 = (item1, item1);
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 1 1 - 2 2 2 - ", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForEachIEnumerableDeclarationWithDeconstruct()
        {
            string source = @"
using System.Collections.Generic;
class C
{
    static void Main()
    {
        foreach ((long x1, var (x2, x3)) in M())
        {
            Print(x1, x2, x3);
        }
    }
    static IEnumerable<Pair<int, Pair<int, int>>> M() { yield return Pair.Create(1, Pair.Create(2, 3)); yield return Pair.Create(4, Pair.Create(5, 6)); }
    static void Print(object a, object b, object c) { System.Console.WriteLine(a + "" "" + b + "" "" + c); }
}
" + commonSource;

            Action<ModuleSymbol> validator = (ModuleSymbol module) =>
            {
                var sourceModule = (SourceModuleSymbol)module;
                var compilation = sourceModule.DeclaringCompilation;
                var tree = compilation.SyntaxTrees.First();
                var model = compilation.GetSemanticModel(tree);

                var x1 = GetDeconstructionLocal(tree, "x1");
                var x1Ref = GetReference(tree, "x1");
                VerifyModelForDeconstructionForeach(model, x1, x1Ref);

                var x2 = GetDeconstructionLocal(tree, "x2");
                var x2Ref = GetReference(tree, "x2");
                VerifyModelForDeconstructionForeach(model, x2, x2Ref);

                var x3 = GetDeconstructionLocal(tree, "x3");
                var x3Ref = GetReference(tree, "x3");
                VerifyModelForDeconstructionForeach(model, x3, x3Ref);

                // extra check on var
                var x23Var = (TypedVariableComponentSyntax)x2.Parent.Parent;
                Assert.Equal("var", x23Var.Type.ToString());
                Assert.Null(model.GetSymbolInfo(x23Var.Type).Symbol); // The var in `var (x2, x3)` has no symbol
            };

            string expected =
@"Deconstructing (1, (2, 3))
Deconstructing (2, 3)
1 2 3
Deconstructing (4, (5, 6))
Deconstructing (5, 6)
4 5 6";

            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, sourceSymbolValidator: validator);
            comp.VerifyDiagnostics();

            comp.VerifyIL("C.Main",
@"{
  // Code size       98 (0x62)
  .maxstack  3
  .locals init (System.Collections.Generic.IEnumerator<Pair<int, Pair<int, int>>> V_0,
                int V_1, //x2
                int V_2, //x3
                int V_3,
                Pair<int, int> V_4,
                int V_5,
                int V_6,
                int V_7,
                int V_8)
  IL_0000:  call       ""System.Collections.Generic.IEnumerable<Pair<int, Pair<int, int>>> C.M()""
  IL_0005:  callvirt   ""System.Collections.Generic.IEnumerator<Pair<int, Pair<int, int>>> System.Collections.Generic.IEnumerable<Pair<int, Pair<int, int>>>.GetEnumerator()""
  IL_000a:  stloc.0
  .try
  {
    IL_000b:  br.s       IL_004d
    IL_000d:  ldloc.0
    IL_000e:  callvirt   ""Pair<int, Pair<int, int>> System.Collections.Generic.IEnumerator<Pair<int, Pair<int, int>>>.Current.get""
    IL_0013:  ldloca.s   V_3
    IL_0015:  ldloca.s   V_4
    IL_0017:  callvirt   ""void Pair<int, Pair<int, int>>.Deconstruct(out int, out Pair<int, int>)""
    IL_001c:  ldloc.s    V_4
    IL_001e:  ldloca.s   V_5
    IL_0020:  ldloca.s   V_6
    IL_0022:  callvirt   ""void Pair<int, int>.Deconstruct(out int, out int)""
    IL_0027:  ldloc.3
    IL_0028:  conv.i8
    IL_0029:  ldloc.s    V_5
    IL_002b:  stloc.s    V_7
    IL_002d:  ldloc.s    V_6
    IL_002f:  stloc.s    V_8
    IL_0031:  ldloc.s    V_7
    IL_0033:  stloc.1
    IL_0034:  ldloc.s    V_8
    IL_0036:  stloc.2
    IL_0037:  box        ""long""
    IL_003c:  ldloc.1
    IL_003d:  box        ""int""
    IL_0042:  ldloc.2
    IL_0043:  box        ""int""
    IL_0048:  call       ""void C.Print(object, object, object)""
    IL_004d:  ldloc.0
    IL_004e:  callvirt   ""bool System.Collections.IEnumerator.MoveNext()""
    IL_0053:  brtrue.s   IL_000d
    IL_0055:  leave.s    IL_0061
  }
  finally
  {
    IL_0057:  ldloc.0
    IL_0058:  brfalse.s  IL_0060
    IL_005a:  ldloc.0
    IL_005b:  callvirt   ""void System.IDisposable.Dispose()""
    IL_0060:  endfinally
  }
  IL_0061:  ret
}
");
        }

        [Fact]
        public void ForEachSZArrayDeclarationWithDeconstruct()
        {
            string source = @"
class C
{
    static void Main()
    {
        foreach ((int x1, var (x2, x3)) in M())
        {
            System.Console.WriteLine(x1 + "" "" + x2 + "" "" + x3);
        }
    }
    static Pair<int, Pair<int, int>>[] M() { return new[] { Pair.Create(1, Pair.Create(2, 3)), Pair.Create(4, Pair.Create(5, 6)) }; }
}
" + commonSource;

            string expected =
@"Deconstructing (1, (2, 3))
Deconstructing (2, 3)
1 2 3
Deconstructing (4, (5, 6))
Deconstructing (5, 6)
4 5 6";

            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForEachMDArrayDeclarationWithDeconstruct()
        {
            string source = @"
class C
{
    static void Main()
    {
        foreach ((int x1, var (x2, x3)) in M())
        {
            System.Console.WriteLine(x1 + "" "" + x2 + "" "" + x3);
        }
    }
    static Pair<int, Pair<int, int>>[,] M() { return new Pair<int, Pair<int, int>> [1, 2] { { Pair.Create(1, Pair.Create(2, 3)), Pair.Create(4, Pair.Create(5, 6)) } }; }
}
" + commonSource;

            string expected =
@"Deconstructing (1, (2, 3))
Deconstructing (2, 3)
1 2 3
Deconstructing (4, (5, 6))
Deconstructing (5, 6)
4 5 6";

            var comp = CompileAndVerify(source, expectedOutput: expected, additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForEachWithExpressionBody()
        {
            string source = @"
class C
{
    static void Main()
    {
        foreach (var (x1, x2) in new[] { (1, 2), (3, 4) })
            System.Console.Write(x1 + "" "" + x2 + "" - "");
    }
}
";

            var comp = CompileAndVerify(source, expectedOutput: "1 2 - 3 4 -", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef, SystemCoreRef });
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ForEachCreatesNewVariables()
        {
            string source = @"
class C
{
    static void Main()
    {
        var lambdas = new System.Action[2];
        int index = 0;
        foreach (var (x1, x2) in M())
        {
            lambdas[index] = () => { System.Console.Write(x1 + "" ""); };
            index++;
        }
        lambdas[0]();
        lambdas[1]();
    }
    static (int, int)[] M() { return new[] { (0, 0), (10, 10) }; }
}
";
            var comp = CompileAndVerify(source, expectedOutput: "0 10 ", additionalRefs: new[] { ValueTupleRef, SystemRuntimeFacadeRef });
            comp.VerifyDiagnostics();
        }
    }
}
