# ASP.NET Core SynchronizationContext

原文链接：[https://blog.stephencleary.com/2017/03/aspnetcore-synchronization-context.html](https://blog.stephencleary.com/2017/03/aspnetcore-synchronization-context.html)

## 为什么 ASP.NET Core 中没有 SynchronizationContext？

退后一步讲，一个很好的问题是为什么在 ASP.NET Core 中移除 `AspNetSynchronizationContext`。微软开发团队内部是怎么讨论的我不知道，我想了有两个理由：性能和简单化。第一个方面就是考虑性能。

当一个异步处理程序在 ASP.NET 恢复执行时，接着会入队列到请求上下文。然后它又必须等待其他之前已经入队列的延续任务完成（一次只运行一个）。当它准备运行时，一个线程池线程被消费，进入到请求上下文，并且恢复执行处理程序。“重复入队列” 这个请求上下文会涉及到很多内部工作，例如设置 `HttpContext.Current` 和当前线程身份以及文化信息。

使用无上下文 ASP.NET Core 方法，当异步处理程序恢复执行时，一个从线程池产生的线程会继续执行。这个上下文会避免排队，请求上下文避免了重复进入队列 。另外，`async / await` 机制在无上下文场景下进行了高度优化。异步请求只需要做更少的工作。

简单是这个决定（无上下文化）的另一个方面。`AspNetSynchronizationContext` 工作的很好，但是这里还有一些棘手的部分，尤其是在[身份验证管理方面](http://www.hanselman.com/blog/SystemThreadingThreadCurrentPrincipalVsSystemWebHttpContextCurrentUserOrWhyFormsAuthenticationCanBeSubtle.aspx)。

OK，所以这里没有 `SynchronizationContext`。那么对于开发者意味着要做什么呢？

## 你可以阻塞异步代码——但是不应该这么做

首先也是最明显的结果就是，await 没有捕获上下文。这也意味着[阻塞异步代码将不会引起死锁](https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html)。你可以使用 `Task.GetAwaiter().GetResult()` （或者是 `Task.Wait` 或者是 `Task<T>.Result`）都不惧死锁。

然而，你不应该这么做。因为在你的异步代码被阻塞的时候，你正在放弃异步代码带来的每一个好处。一旦阻塞线程，异步处理程序的增强可伸缩性就会无效。

然而，不幸的是，在 ASP.NET 场景里还遗留两个同步操作：ASP.NET MVC 过滤器以及子控制器。然而，在 ASP.NET Core 中，整个管道都是完整的异步；包括过滤器以及组件的展示都是异步的。

总之，你应该尽量的在所有地方都使用 async；但是如果你需要，你可以不顾危险的阻塞它。

## 你无需用到 ConfigureAwait(false)，但是在库中还是要用到

这从没有上下文，`ConfigureAwait(false)` 也就是没必要了。我们知道任何代码运行在 ASP.NET Core 是不需要显式避免上下文。事实上，ASP.NET Core 团队内部自己都抛弃使用 `ConfigureAwait(false)`。

然而，我还是建议你在你的库中保留使用它 — 在应用程序中任何事情都有可能被重新启用。如果库中的代码也能在 UI 应用程序中运行，或者在遗留的 ASP.NET app ，或任何其他有上下文的地方，你都还是可以在库中使用 `ConfigureAwait(false)`。

## 小心隐式的并发

这里有个主要的概念就是当从一个同步上下文到一个线程池线程（比如从遗留的 ASP.NET 到 ASP.NET Core）。

在老的 ASP.NET 的 `SynchronizationContext` 是一个实际的同步上下文，以为着也包含一个请求上下文，一次只能执行一个线程。那就是说，异步延续可能会在任何线程，但是一次只有一个。ASP.NET Core 不存在 `SynchronizationContext` ，所以 `await` 默认就是线程池上下文。所以在 ASP.NET Core 的世界中，异步可能延续在任何线程上，并且还可能是并发运行的。

作为一个例子，考虑这段代码，它下载了两个字符串并把他们放到一个列表中。这段代码在老旧的 ASP.NET 能很好的工作，因为请求上下文一次只允许执行一个延续。

```c#
private HttpClient _client = new HttpClient();

async Task<List<string>> GetBothAsync(string url1, string url2)
{
    var result = new List<string>();
    var task1 = GetOneAsync(result, url1);
    var task2 = GetOneAsync(result, url2);
    await Task.WhenAll(task1, task2);
    return result;
}

async Task GetOneAsync(List<string> result,string url)
{
    var data = _client.GetStringAsync(url);
    result.Add(data);
}
```

其中 `result.Add(data)` 这行只能够一次只一个线程执行，因为它是在请求上下文中执行的。

然而，这段相同的代码在 ASP.NET Core 是不安全的；特别要指出的是，`result.Add(data)` 这行可能一次被两个线程执行，在没有保护共享的变量 `List<string>` 的情况下。

这样的代码是很少见的；异步代码本质上是功能性的。因此，从异步方法很自然的返回一个结果而不是修改共享状态。然而，异步代码的质量也是多变的，并且毫无疑问，有一些代码没有充分的屏蔽平行度。