# ASP.NET Core SynchronizationContext

原文链接：[https://blog.stephencleary.com/2017/03/aspnetcore-synchronization-context.html](https://blog.stephencleary.com/2017/03/aspnetcore-synchronization-context.html)

## 为什么 ASP.NET Core 中没有 SynchronizationContext？

退后一步讲，一个很好的问题是为什么在 ASP.NET Core 中移除 `AspNetSynchronizationContext`。微软开发团队内部是怎么讨论的我不知道，我想了有两个理由：性能和简单化。第一个方面就是考虑性能。

当一个异步处理程序在 ASP.NET 恢复执行时，延续会被排队到请求上下文。这个延续必须等待其他已经排队过的延续任务（也许一次只运行一个）。当它准备运行时，一个线程池线程被消费，进入到请求上下文，并且恢复处理程序执行。“重新请求” 这个请求上下文涉及到很多内部工作，例如设置 `HttpContext.Current` 和当前线程身份以及文化信息。

使用无上下文 ASP.NET Core 方法，当异步处理程序恢复执行时，一个从线程池产生的线程会继续执行。这个上下文会避免排队，并且不需要 “进入” 请求上下文。另外，`async / await` 机制在无上下文场景下进行了高度优化。异步请求只需要做更少的工作。

简单是这个决定（无上下文化）的另一个方面。`AspNetSynchronizationContext` 工作的很好，但是这里还有一些棘手的部分，尤其是在[身份验证管理方面](http://www.hanselman.com/blog/SystemThreadingThreadCurrentPrincipalVsSystemWebHttpContextCurrentUserOrWhyFormsAuthenticationCanBeSubtle.aspx)。

OK，所以这里没有 `SynchronizationContext`。那么对于开发者意味着要做什么呢？

## 你可以阻塞异步代码——但是不应该这么做

//TODO