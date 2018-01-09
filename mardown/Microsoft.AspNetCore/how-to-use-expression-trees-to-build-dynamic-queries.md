# [翻译]怎样使用表达式树生成动态查询

在LINQ，表达式树常用于结构化查询,目标资源数据实现了 [IQueryable](https://docs.microsoft.com/en-us/dotnet/api/system.linq.iqueryable-1). 例如，LINQ为关系型数据存储查询提供了 [IQueryable<IQueryable>](https://docs.microsoft.com/en-us/dotnet/api/system.linq.iqueryable-1) 接口。C#编译器将这些数据源的查询编译成运行时的表达式树代码。然后查询提供程序可以遍历表达式树数据结构，并转化为合适于数据源的查询语言。

在LINQ中使用表达式树来表示分配给 [Expression<TDelegate>](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1) 类型的Lambda表达式变量。

这节主要描述了如何使用表达式树构建一个动态LINQ查询。在编译期，动态查询在特殊未知的查询的情况下是非常有用的。具体例子，一个应用程序提供了一个用户接口，最终来允许用户指定一个或多个谓词来过滤数据。为了使用LINQ查询，这种情况应用程序在运行时必须使用表达式树来构建一个LINQ查询。

## Example

下面这段代码展示如何使用表达式树去围绕 `IQueryable` 数据源构造一个查询并运行。代码生成了一个表达式树来表示查询：

```c#
companies.Where(company => (company.ToLower() == "coho winery" || company.Length > 16)).OrderBy(company => company)
```

在命名空间  `[System.Linq.Expressions](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions)` 下有个工厂方法用来生成一个表达式树来表示这个查询。表示标准查询运算符方法调用的表达式将引用这些方法的 [Queryable](https://docs.microsoft.com/en-us/dotnet/api/system.linq.queryable) 的实现。最终表达式树被传递给 `IQueryable` 数据源的提供程序的 [CreateQuery<TDelegate>(Expression)](https://docs.microsoft.com/en-us/dotnet/api/system.linq.iqueryprovider.createquery#System_Linq_IQueryProvider_CreateQuery__1_System_Linq_Expressions_Expression_)  实现，以创建一个可执行的 `IQueryable` 类型的查询。通过枚举该查询获得结果。

```c#
Expression<Func<string, bool>> expr = name => name.Length > 10 && name.StartsWith("G");
Console.WriteLine(expr);

AndAlsoModifier treeModifier = new AndAlsoModifier();
Expression modifierExpr = treeModifier.Modify(expr);

Console.WriteLine(modifierExpr);

string[] companies = {"Consolidated Messenger", "Alpine Ski House", "Southridge Video", "City Power & Light",
        "Coho Winery", "Wide World Importers", "Graphic Design Institute", "Adventure Works",
        "Humongous Insurance", "Woodgrove Bank", "Margie's Travel", "Northwind Traders",
        "Blue Yonder Airlines", "Trey Research", "The Phone Company",
        "Wingtip Toys", "Lucerne Publishing", "Fourth Coffee" };
//转化IQueryable数据源
IQueryable<string> queryableData = companies.AsQueryable();
//编写表示谓词参数的表达式树
ParameterExpression pe = Expression.Parameter(typeof(string), "company");
//新建一个表达式树来表示 'company.ToLower() == "coho winery"' 的表达式
Expression left = Expression.Call(pe, typeof(string).GetMethod("ToLower", Type.EmptyTypes));
Expression right = Expression.Constant("coho winery", typeof(string));
Expression e1 = Expression.Equal(left, right);
//新建一个表达式树来表示 'company.Length > 16' 表达式
left = Expression.Property(pe, typeof(string).GetProperty("Length"));
right = Expression.Constant(16,typeof(int));
Expression e2 = Expression.GreaterThan(left, right);
//编译表达式树来生成一个表示'(company.ToLower() == "coho winery" || company.Length > 16)' 的表达式
Expression predicateBody = Expression.OrElse(e1, e2);
//新建一个表达式树来表示 'queryableData.Where(company => (company.ToLower() == "coho winery" || company.Length > 16))'
MethodCallExpression whereCallExpresstion = Expression.Call(
    typeof(Queryable),
    "Where",
    new Type[] { queryableData.ElementType },
    queryableData.Expression,
    Expression.Lambda<Func<string, bool>>(predicateBody, new ParameterExpression[] { pe }));

//排序 OrderBy(company => company)
//新建一个表达式树来表示 'whereCallExpression.OrderBy(company => company)'
MethodCallExpression orderCallExpresstion = Expression.Call(
    typeof(Queryable),
    "OrderBy",
    new Type[] { queryableData.ElementType, queryableData.ElementType },
    whereCallExpresstion,
    Expression.Lambda<Func<string, string>>(pe, new ParameterExpression[] { pe }));

//新建一个可执行的查询表达式树
IQueryable<string> result = queryableData.Provider.CreateQuery<string>(orderCallExpresstion);

//枚举结果
foreach (string company in companies)
    Console.WriteLine(company);
```

代码中在被传递到 `Queryable.Where` 方法中，在谓词中使用了一个固定数字。但是，你可以写一个应用程序，来编译在谓词中一个依赖于用户输入的数字变量。你也可以根据用户的输入，更改查询中调用的标准查询操作符。

## 编译代码

- 创建新的**控制台应用程序**项目。
- 添加对 System.Core.dll 的引用（如果尚未引用）。
- 包括 System.Linq.Expressions 命名空间。
- 从示例中复制代码，并将其粘贴到 `Main` 方法中。