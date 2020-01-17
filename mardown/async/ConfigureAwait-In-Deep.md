# 深入理解 ConfigureAwait

这段时间经常看到很多园子里讨论 ConfigureAwait，刚好今天在微软官方博客看到了 [Stephen Toub](https://devblogs.microsoft.com/dotnet/author/stephen_toubhotmail-com/) 前不久的一篇答疑 ConfigureAwait 的一篇文章，想翻译过来。

原文地址：https://devblogs.microsoft.com/dotnet/configureawait-faq/

.NET 加入 `async/await` 特性已经有 7 年了。这段时间，它蔓延的非常快，广泛；不只在 .NET 生态系统，也出现在其他语言和框架中。在 .NET 中，他见证了许多了改进，利用异步在其他语言结构（additional language constructs）方面，提供了支持异步的 API，在基础设施中标记 `async/await` 作为最基本的优化（特别是在 .NET Core 的性能和分析能力上）。

然而，`async/await` 另一方面也带来了一个问题，那就是 **ConfigureAwait**。在这片文章中，我会解答它们。我尝试在这篇文章从头到尾变得更好读，能作为一个友好的答疑清单，能为以后提供参考。

# 什么是 SynchronizationContext

[`System.Threading.SynchronizationContext`文档](https://docs.microsoft.com/en-us/dotnet/api/system.threading.synchronizationcontext)表明它“它提供一个最基本的功能，在各种同步模型中传递同步上下文”，除此之外并无其他描述。

对于它的 99% 的使用案例，`SynchronizationContext`只是一个类，它提供一个虚拟的 `Post`的方法，它传递一个委托在异步中执行（这里面其实还有其他很多虚拟成员变量，但是很少用到，并且与我们这次讨论毫不相干）。这个类的 `Post`仅仅只是调用`ThreadPool.QueueUserWorkItem`来异步执行前面传递的委托。但是，那些继承类能够覆写`Post`方法，以至于在大多数合适的地方和时间执行。

举个例子，Windows Forms 有一个[`SynchronizationContext派生类`](https://github.com/dotnet/winforms/blob/94ce4a2e52bf5d0d07d3d067297d60c8a17dc6b4/src/System.Windows.Forms/src/System/Windows/Forms/WindowsFormsSynchronizationContext.cs)，它复写了`Post`方法，就等价于`Control.BeginInvoke`。那就是说所有调用这个`Post`方法都将会引起这个委托在这个相关控件关联的线程上被调用，它被称为“UI线程”。Windows Forms 依靠 Win32 上的消息处理程序以及有一个“消息循环”运行在UI线程上，它简单的等待新的消息到达来处理。那些消息可能是鼠标移动和点击，对于键盘输入、系统事件，委托等都能够被执行。所以为 Windows Forms 应用程序的 UI 线程提供一个`SynchronizationContext`实例，让它能够得到委托在 UI 线程上执行，需要做的只是简单的传递它给`Post`。

对于 WPF 来说也是如此。它也有它自己的[`SynchronizationContext`派生类](https://github.com/dotnet/wpf/blob/ac9d1b7a6b0ee7c44fd2875a1174b820b3940619/src/Microsoft.DotNet.Wpf/src/WindowsBase/System/Windows/Threading/DispatcherSynchronizationContext.cs)，覆写了`Post`，类似的，传递一个委托给 UI 线程（与之对应 Dispatcher.BeinInvoke），在这个例子中是 WPF Dispatcher 而不是 Windows Forms 控件。

对于 Windows 运行时（WinRT）。它同样有自己的[`SynchronizationContext`派生类](https://github.com/dotnet/runtime/blob/60d1224ddd68d8ac0320f439bb60ac1f0e9cdb27/src/libraries/System.Runtime.WindowsRuntime/src/System/Threading/WindowsRuntimeSynchronizationContext.cs)，覆写`Post`，通过`CoreDispatcher`也传递委托给 UI 线程。

这不仅仅只是“在 UI 线程上运行委托”。任何人都能实现`SynchronizationContext`来覆写`Post`来做任何事。例如，我不会关心线程运行委托所做的事，但是我想确保任何在 `Post` 执行的我编写的`SynchronizationContext`都以一定程度的并发度执行。我可以实现它，用我自定义`SynchronizationContext`类，像下面一样：

```c#
internal sealed class MaxConcurrencySynchronizationContext: SynchronizationContext
{
    private readonly SemaphoreSlim _semaphore;

    public MaxConcurrencySynchronizationContext(int maxConcurrencyLevel) =>
        _semaphore = new SemaphoreSlim(maxConcurrencyLevel);

    public override void Post(SendOrPostCallback d, object state) =>
        _semaphore.WaitAsync().ContinueWith(delegate
        {
            try { d(state); } finally { _semaphore.Release(); }
        }, default, TaskContinuationOptions.None, TaskScheduler.Default);

    public override void Send(SendOrPostCallback d, object state)
    {
        _semaphore.Wait();
        try { d(state); } finally { _semaphore.Release(); }
    }
}
```

事实上，单元测试框架 xunit [提供了一个 SynchronizationContext`](https://github.com/xunit/xunit/blob/d81613bf752bb4b8774e9d4e77b2b62133b0d333/src/xunit.execution/Sdk/MaxConcurrencySyncContext.cs)与上面非常相似，它用来限制与能够并行运行的测试相关的代码量。

所有的这些好处就根抽象一样：它提供一个单独的 API，它能够用来排队传递委托来处理创造者想要实现他们想要的（ it provides a single API that can be used to queue a delegate for handling however the creator of the implementation desires），而不需要知道实现的细节。

所有，如果我们在编写类库，并且想要进行和执行相同的工作，那么就排队委托传递回在原来位置的“上下文”，那么我就只需要获取它们的`SynchronizationContext`，占有它，然后当完成我的工作时调用那个上下文中的`Post`来调用传递我想要调用的委托。于 Windows Forms，我不必知道我应该获取一个`Control`并且调用它的`BegeinInvoke`，或者对于 WPF，我不用知道我应该获取一个 Dispatcher 并且调用它的 BeginInvoke，又或是在 xunit，我应该获取它的上下文并排队传递；我只需要获取当前的`SynchronizationContext`并调用它。为了这个目的，`SynchronizationContext`提供一个`Currenct`属性，为了实现上面说的，我可以像下面这样编写代码：

