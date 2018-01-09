# 如何修改表达式树

这节主要展示怎样去修改表达式树。表达式树是不可变的（Immutable），意味着它不能被直接修改。为了修改表达式树，那么你必须新建一个已经存在的表达式树的副本，并在创建副本时进行所需的更改。你可以使用 [ExpressionVisitor](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressionvisitor) 类去解析表达式树并复制它访问的每一个节点。

## 修改表达式树

1. 新建控制台应用程序

2. 添加引用 `System.Linq.Expressions`

3. 在你的项目中添加类 `AndAlsoModifier`

   ```c#
   public class AndAlsoModifier : ExpressionVisitor
   {
       public Expression Modify(Expression expression)
       {
           return Visit(expression);
       }

       protected override Expression VisitBinary(BinaryExpression b)
       {
           if(b.NodeType == ExpressionType.AndAlso)
           {
               Expression left = this.Visit(b.Left);
               Expression right = this.Visit(b.Right);

               //让二元运算符OrElse代替AndAlso
               return Expression.MakeBinary(ExpressionType.OrElse, left, right, b.IsLiftedToNull, b.Method);
           }
           return base.VisitBinary(b);
       }
   }
   ```

   这个类继承了 [ExpressionVisitor](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressionvisitor) 而且专门用来修改表示条件 `And` 操作的表达式。它改变从 `And` 条件到 `OR`。为了这个目的， `AndAlsoModifier` 重写了基类的 [VisitBinary](https://docs.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressionvisitor.visitbinary) 方法，因为 `And` 表示的是一个二元表达式。在 ` VisitBinary ` 方法中，如果这个表达式传递的是 `And` 操作，代码会构造一个包含条件操作 `OR` 新的表达式而不是 `And`。如果表达式传给 `VisitBinary` 的不是 `And` 操作，那么方法就会优先基类的实现。它基类的方法构造一个节点就像传递进来的表达式树一样，但是这个节点有它们的子树，被访问者递归生成的表达式树替换。

4. 添加引用 `System.Linq.Expressions`

5. 在 Program.cs 文件添加 Main 方法并并创建一个表达式树传递给这个方法来修改它。

   ```c#
   Expression<Func<string, bool>> expr = name => name.Length > 10 && name.StartsWith("G");  
   Console.WriteLine(expr);  

   AndAlsoModifier treeModifier = new AndAlsoModifier();  
   Expression modifiedExpr = treeModifier.Modify((Expression) expr);  

   Console.WriteLine(modifiedExpr);  

   /*  This code produces the following output:  

       name => ((name.Length > 10) && name.StartsWith("G"))  
       name => ((name.Length > 10) || name.StartsWith("G"))  
   */  
   ```

   这段代码创建了一个包含 `And` 操作的表达式树。然后新建一个 `AndAlsoModifier` 的实例并给方法 `Modify` 传递之前创建的表达式树。并输出原始和修改后的表达式树显示差异。

6. 编译并运行程序。

