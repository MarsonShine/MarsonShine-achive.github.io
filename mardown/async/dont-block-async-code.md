# 不要阻塞异步代码

原文地址：[<https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html>](<https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html>)

这是在论坛和 Stack Overflow 上经常出现的问题。我认为这是最常问的问题，对于新手学异步的人来说。

## UI 例子

看下面给出的例子。一个按钮点击将启动 REST 调用以及结果显示在文本框中（这个例子是针对 Windows Forms，但是对于其他任何 UI 应用程序本质都是一样的）

```c#
//库方法
public static async Task<JObject> GetJsonAsync(Uri uri)
{
    //真是场景的代码不应该用 using 包裹 HttpClient；这只是个例子
    using(var client = new HttpClient())
    {
        var jsonString = await client.GetStringAsync(uri);
        return JObject.Parse(jsonString);
    }
}

public void Button1_Click(...)
{
    var jsonToken = GetJsonAsync(...);
    textbox1.Text = jsonToken.Result;
}
```

"GetJson" 帮助方法 REST 调用以及转化 JSON。按钮事件程序等待方法完成并显示其结果。

这段代码将会死锁。

## ASP.NET 例子

下面这个例子也非常相似。我们有一个库方法同样去执行 REST 调用，只是这次是在 ASP.NET 上下文中使用（以 WebApi 为例，本质上在任何 ASP.NET 应用程序都是相同）。

```c#
//库方法
public static async Task<JObject> GetJsonAsync(Uri uri)
{
    using (var client = new HttpClient())
    {
        var jsonString = await client.GetStringAsync(uri);
        return JObject.Parse(jsonString);
    }
}

public class MyController : ApiController
{
    public string Get()
    {
        var jsonToken = GetJsonAsync(...);
        return jsonToken.Result.ToString();
    }
}
```

这同样会死锁。理由是一致的。

## 什么导致了死锁

首先这里有一些场景：回想一下我的介绍 [async-await](https://blog.stephencleary.com/2012/02/async-and-await.html) 的文章，在你 await Task，那么这个时候方法延续会继续在上下文中。首先一点，这个上下文是 UI 上下文（它能提供任何 UI，除了控制台应用程序）。第二点，这个上下文是 ASP.NET 请求上下文。

另一个重要的点：ASP.NET 请求上下文不会尝试特定指定线程（就像 UI 线程一样），但是它只允许一次一个线程。有趣的是这方面据我所知就是在任何地方官方文档，但是在我的这篇文章 [http://msdn.microsoft.com/en-us/magazine/gg598924.aspx](http://msdn.microsoft.com/en-us/magazine/gg598924.aspx) 中有提到过。

那么这里到底发生了什么呢？让我们回到最高层的方法（Botton1_Click 无论 UI 还是控制器）：

1. 最上层方法（调用层）调用 GetJsonAsync（在 UI/ASP.NET 上下文中）
2. GetJsonAsync 开始通过调用 HttpClient.GetStringAsync 开始 REST 请求（仍然在 UI/ASP.NET 上下文中）
3. GetStringAsync 返回一个未完成的 Task，表明这个 REST 请求还未完成。
4. GetJsonAsync 通过等待 GetStringAsync 返回结果。这个上下文会被捕捉到并且会在 GetJsonAsync 方法之后继续沿用。GetJsonAsync 返回一个未完成的任务，这表示 GetJsonAsync 方法还未完成。
5. 最上层方法在 GetJsonAsync 返回的任务上同步阻塞。这会阻塞上下文线程。
6. 最终，REST 请求将会完成。这个已经完成的任务通过 GetStringAsync 返回。
7. GetJsonAsync 就会准备延续继续执行，并且要等待上下文直到可用，这样它才能在这个上下文中继续执行。
8. 发生死锁。调用层方法正在阻塞上下文线程，并正在等待 GetJsonAsync 完成，而 GetJsonAsync 正在等待这个上下文释放它才能完成。

对于 UI 这个例子，上下文是 UI 上下文；对于 ASP.NET 例子，上下文是 ASP.NET 请求上下文。导致这个死锁的都是上下文。

## 防止死锁

这里有两个最佳实践（都涵盖在我的这篇博文里 [async-await](https://blog.stephencleary.com/2012/02/async-and-await.html)：

1. 在你自己的库异步函数里尽可能的使用 ConfigureAwait(false)。
2. 不要在 Task 上阻塞；一路用 async 到低。

对于第一个最佳实践。新的修改方法如下：

```c#
public static async Task<JObject> GetJsonAsync(Uri uri)
{
    using(var client = new HttpClient())
    {
        var jsonString = await client.GetStringAsync(uri).ConfigureAwait(false);
        return JObject.parse(jsonString);
    }
}
```

这个将更改 GetJsonAsync 的延续行为，它不会在上下文中恢复。而是将会在线程池线程上恢复执行。这样能够使 GetJsonAsync 能完成任务并返回结果并且不会重新进入上下文。与此同时，最高层的代码必须要上下文，所以他们不能使用 ConfigureAwait(false)。

> 使用 `ConfigureAwait(false)` 来避免死锁是个危险操作。你必须在标记 `await` 每一处都要使用 `ConfigureAwait(false)`，包括所有的第三方库代码。使用 ConfigureAwait(false) 来避免死锁仅仅只是为了 hack
>
> 就像文章标题指出的一样，最好的解决方法就是 “不要阻塞异步代码”。

第二个最佳实践，在最高层方法就像这样写：

```c#
public async void Button1_Click(...)
{
    var json = await GetJsonAsync(...);
    textBox1.Text = json;
}

public class MyController : ApiController
{
	public async Task<string> Get()
	{
		var json = await GetJsonAsync(...);
		return json.ToString();
	}
}
```

这改变了最外层方法的阻塞行为，这样上下文永远不会被阻塞；所有的 `await` 都是异步的。

**注意**：这都是最佳的事件。它们都能防止死锁，但是必须同时应用这两种方法才能实现最大的性能和响应能力。

## 资源

- https://blog.stephencleary.com/2012/02/async-and-await.html
- http://blogs.msdn.com/b/pfxteam/archive/2011/01/13/10115163.aspx
- http://channel9.msdn.com/Events/BUILD/BUILD2011/TOOL-829T
- http://blogs.msdn.com/b/lucian/archive/2012/03/29/talk-async-part-1-the-message-loop-and-the-task-type.aspx
- http://blogs.msdn.com/b/pfxteam/archive/2012/04/12/10293335.aspx

这种经常引起死锁是由于同步代码和异步代码混合编程导致的。通常是因为人们只是在有一小段地方使用异步，其他地方使用同步。不幸的是，部分的异步代码是要比全部异步来说更复杂棘手。

如果你的确需要维护部分异步代码，那么你一定要看 Stephen Toub 的那两篇文章：[http://blogs.msdn.com/b/pfxteam/archive/2012/03/24/10287244.aspx](http://blogs.msdn.com/b/pfxteam/archive/2012/03/24/10287244.aspx)、[http://blogs.msdn.com/b/pfxteam/archive/2012/04/13/10293638.aspx](http://blogs.msdn.com/b/pfxteam/archive/2012/04/13/10293638.aspx) 以及我的库 [https://github.com/StephenCleary/AsyncEx](https://github.com/StephenCleary/AsyncEx)