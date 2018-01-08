# [翻译]表达式树（Expression Trees）

*原文地址：https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/index*

表达式树展示的是代码的树形结构，每一个节点都是一个表达式，例如：调用一个方法或是调用一个二元运算表达式比如 `x < y`

你可以通过表达式树来编译和运行代码。这意味着能动态修改运行的代码，就好比在数据库中运行LINQ以一个变量的形式查询数据和创建一个动态的查询语句。关于表达式树在LINQ的更多使用信息详见[How to: Use Expression Trees to Build Dynamic Queries (C#)](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/how-to-use-expression-trees-to-build-dynamic-queries).

表达式树也被用于DLR（动态语言运行时），提供DLR与.NET Framework之间的互操作性，使编译器解析(Emit)表达式树，而不是MSIL。关于DLR更多信息详细详见[Dynamic Language Runtime Overview](https://docs.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/dynamic-language-runtime-overview).

你可以用C#/VB编译器在你的lambda表达式变量的基础上生成一个表达式树，或者你可以通过使用在[System.Linq.Expressions](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions)命名空间创建表达式树.

## 从Lambda表达式创建表达式树

当一个lambda表达式被分配到类型为[Expression的](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1)的一个变量时，编译器会解析生成代码创建一个表达式树来表示这个lambda .

C#编译器能从lambda生成表达式树（或者从一个单行的lambda）。但它不能转换成lambda声明（或多行lambda）。更多关于lambda信息见[Lambda Expressions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/lambda-expressions).

下面的代码例子说明了如何用C#来生成一个表达式树来表示一个lambda表达式： `num => num < 5`

```c#
Expression<Func<int, bool>> lambda = num => num < 5;
```

## 通过API创建表达式树

使用微软提供的API——[Expression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression)这个类来创建表达式树。这个类包括了创建指定类型表达式树节点的静态工厂方法，例如， [ParameterExpression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.parameterexpression)它代表一个参数或者变量，又如[MethodCallExpression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.methodcallexpression)，它代表一个方法调用。[ParameterExpression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.parameterexpression), [MethodCallExpression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.methodcallexpression)以及其他的指定类型的表达式树都在[System.Linq.Expressions](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions)命名空间下。这些类都继承自 [Expression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression)抽象类.

下面的代码展示如何用API创建表达式树来表示一个lambda表达式`num => num < 5`

```c#
// 在你的代码文件添加引用:  
// using System.Linq.Expressions;  

// 为lambda表达式num => num < 5手动生成表达式树   
ParameterExpression numParam = Expression.Parameter(typeof(int), "num");  
ConstantExpression five = Expression.Constant(5, typeof(int));  
BinaryExpression numLessThanFive = Expression.LessThan(numParam, five);  
Expression<Func<int, bool>> lambda1 =  
    Expression.Lambda<Func<int, bool>>(  
        numLessThanFive,  
        new ParameterExpression[] { numParam }); 
```

在.NET Framework4.0之后，表达式树API也支持分配和控制流表达式，例如循环，条件判断块以及异常捕捉块(try-catch)。通过API，你可以创建比编译器通过从lambda表达式创建的更加复杂的表达式树。下面的这个例子展示了如何创建一个表达式树来表示一个数的阶乘(factorial of number).

```c#
//创建一个参数表达式
ParameterExpression value = Expression.Parameter(typeof(int), "value");
//创建一个表达式表示本地变量
ParameterExpression result = Expression.Parameter(typeof(int), "result");
//创建标签从循环跳到指定标签
LabelTarget label = Expression.Label(typeof(int));
//创建方法体
BlockExpression block = Expression.Block(
    //添加本地变量
    new[] { result },
    //为本地变量赋值一个常量
    Expression.Assign(result, Expression.Constant(1)),
    //循环
    Expression.Loop(
        //添加循环条件
        Expression.IfThenElse(
            //条件：value > 1
            Expression.GreaterThan(value, Expression.Constant(1)),
            //if true
            Expression.MultiplyAssign(result, Expression.PostDecrementAssign(value)),
            //if false
            Expression.Break(label, result)
            ),
        label
        )
    );

int facotorial = Expression.Lambda<Func<int, int>>(block, value).Compile()(5);  
Console.WriteLine(factorial);  
// 输出 120.  
```

更多关于详见[Generating Dynamic Methods with Expression Trees in Visual Studio 2010](http://go.microsoft.com/fwlink/p/?LinkId=169513),也支持后续VS版本.

## 解析表达式树(Parsing Expression Trees)

下面的代码示例演示了如何将表达式树表示为lambda表达式`num => num < 5`，可以被分解为部分。

```c#
public void DecomposedExpressionTrees()
{
    //创建一个表达式树
    Expression<Func<int, bool>> exprTree = num => num < 5;
    //分解表达式
    ParameterExpression param = (ParameterExpression)exprTree.Parameters[0];
    //num < 5
    BinaryExpression operation = (BinaryExpression)exprTree.Body;
    ParameterExpression left = (ParameterExpression)operation.Left;
    ConstantExpression right = (ConstantExpression)operation.Right;
  
    Console.WriteLine("Decomposed expression: {0} => {1} {2} {3}",
            param.Name, left.Name, operation.NodeType, right.Value);
}
```

## 表达式树的不变性(Immutability of Expression Trees)

表达式树应该是不可变的。这意味着如果你想修改表达式树，那么你必须重新已经存在的构造表达式树并替换其某个节点。你可以使用表达式树访问器（ExpressionVisitor）遍历表达式树。更多这方面信息详见[How to: Modify Expression Trees (C#)](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/how-to-modify-expression-trees).

## 编译表达式树

泛型 [Expression<TDelegate>](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1) 类型提供一个 [Compile ](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1.compile)方法来将表达式树所表示的代码编译成可执行的委托。

下面这段代码显示如何编译表达式树和运行结果代码

```c#
public void ComplieExpressTrees()
{
    //创建一个表达式树
    Expression<Func<int, bool>> expr = num => num < 5;
    //编译表达式树为委托
    Func<int, bool> result = expr.Compile();
    //调用委托并写结果到控制台
    Console.WriteLine(result(4));

    //也可以使用简单的语法来编译运行表达式树
    Console.WriteLine(expr.Compile()(4));
    //结果一样
}
```

更多关于如何运行表达式树信息，详见[How to: Execute Expression Trees (C#)](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/expression-trees/how-to-execute-expression-trees).