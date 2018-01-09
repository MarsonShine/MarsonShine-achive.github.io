# 执行表达式树

本节主要展示如何去执行表达式树。运行一个可能含有返回值或只是执行一个操作，比如方法调用的表达式树。

只有表示lambda表达式的表达式树能够被执行。它是一个 [LambdaExpression](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.lambdaexpression) 或 [Expression<TDelegate>](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1) 类型。为了执行这些表达式树，调用 [Compile](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.lambdaexpression.compile) 方法来生成一个可执行的委托并调用它。

> 注意：
>
> 如果这个委托的类型是未知的，那么这个委托的类型是LambdaExpression而不是Expression<TDelegate>，你必须调用委托的 [DynamicInvoke](https://docs.microsoft.com/en-us/dotnet/api/system.delegate.dynamicinvoke) 方法而不是直接调用Invoke。

如果一个表达式树不代表一个lambda表达式，你可以创建一个新的表达式树将原来的表达式树来作为它的Body，通过调用 [Lambda(Expression, IEnumerable)](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression.lambda#System_Linq_Expressions_Expression_Lambda__1_System_Linq_Expressions_Expression_System_Collections_Generic_IEnumerable_System_Linq_Expressions_ParameterExpression__) 方法。然后你就可以调用这个lambda表达式了

## Example

下面的代码说明如何运行一个表示一个数的幂运算的表达式树通过生成lambda并调用它。结果是显示这个数的平方

```c#
//执行表达式树
BinaryExpression be = Expression.Power(Expression.Constant(2D), Expression.Constant(3D));
//创建一个委托表达式
Expression<Func<double>> le = Expression.Lambda<Func<double>>(be);
// 编译lambda表达式
Func<double> compiledExpression = le.Compile();
//执行lambda表达式
double result = compiledExpression();
//显示值
Console.WriteLine(result);
```

## 编译的代码

- 添加项目引用 System.Core.dll
- 添加命名空间 System.Linq.Expressions