# AspNetCore管道寄宿——Hosting

​	首先我们从寄宿开始说起，说起这个，从原来的Asp.Net理解；在Asp.Net中，在托管代码中，CLR不能单独存在，必须依赖于IIS内的一个进程（多个进程可以加载多个CLR），也就是说CLR的再在离不开IIS这个大环境（宿主），这也是AspNet 不能跨平台的原因；而AspNetCore确不是，它由原来的依赖于Windows Server，IIS发展成了自寄宿——也就是说不在依赖于宿主运行了。

​	怎么理解呢？我们来回想一下，当我们运行AspNet Web应用程序的时候发生了什么。当程序运行一直到网页出现时，我们先看到windows先启动了一个叫iisexpress的程序，然后随机分配了一个端口号，这时其实windows已经启动了iis中的一个进程来装载应用程序的运行，可以在任务管理器中的进程查看。而AspNetCore不同，它有一个启动的入口，查看项目的属性我们就知道这实际上是一个控制台程序，启动入口程序之后，windows并没有启动iis中的进程就成功运行（需稍微修改默认模板代码，后面会讲到），所以AspNetCore启动的进程并不属于IIS，它会自己加载CLR运行，这也就说明了为什么能做到跨平台了（当然，这个进程的运行需要运行环境——.NetCore SDK）

​	AspNetCore提供了两种服务器：

- Http.sys——只能在Windows环境中运行（Http.sys原来叫做：WebListner）
- Kestrel——跨平台服务器

## Program入口

首先我们来看AspNetCore Web应用程序启动程序做了那些事

```c#
public static void Main(string[] args)
{
    BuildWebHost(args).Run();
}

public static IWebHost BuildWebHost(string[] args) =>
    WebHost.CreateDefaultBuilder(args)
        .UseStartup<Startup>()
        .Build();
```

WebHost就是我们之前说的寄宿，这是我们Core应用程序启动的一个大的Web容器，所有的信息都在这里WebHost里面运行。WebHost是一个静态类，`CreateDefaultBuilder`方法设置了默认的运行环境设置并返回一个`IWebHostBuilder`，接着泛型拓展函数`UseStartup<TStartup>`加载初始化单列Startup启动类并`Build `返回一个内部的`WebHost`

### UseStartup

首先我们来看`WebHostBuilderExtensions.UseStartup`源代码

```c#
public static IWebHostBuilder UseStartup(this IWebHostBuilder hostBuilder, Type startupType){
    var startupAssemblyName = startupType.GetTypeInfo().Assembly.GetName().Name;
  	return hostBuilder
      	.UseSetting(WebHostDefaults.ApplicationKey, startupAssemblyName)
      	.ConfigureServices(services =>
        {
         	if(typeof(IStartup).GetTypeInfo().IsAssignableFrom(startupType.GetTypeInfo())){
              	services.AddSingleton(typeof(IStartup), startupType);
            }else{
              	services.AddSingleton(typeof(IStartup), sp =>{
                	var hostingEnvironment = sp.GetRequiredService<IHostingEnvironment>();
                  	return new ConventionBasedStartup(StartupLoader.LoadMethods(sp, startupType, hostingEnvironment.EnvironmentName));
              	}
            }                    
        }
}
```

if段我们不看，我们主要看else，DI利用委托工厂给IStartup注册实现类startupType，而这个starupType就是我们Web应用程序的Startup类，那么`StarupLoader.LoadMethods`做了什么呢？顾名思义，主要是加载启动类的方法，所以调用Startup的`ConfigureServices，Configure`就是在这里调用的，我们跟踪下去发现，方法是在程序集`Microsoft.AspNetCore.Hosting.Internal`下的静态方法`FindMethod`通过反射加载Public,Instance,Static类型的指定名称方法信息并返回一个ConfigureBuilder，调用那两个函数就是在`ConfigureBuilder.Build(instance)`委托下调用的，核心代码如下：

```c#
public class ConfigureBuilder
{
    public ConfigureBuilder(MethodInfo configure)
    {
        MethodInfo = configure;
    }

    public MethodInfo MethodInfo { get; }

    public Action<IApplicationBuilder> Build(object instance) => builder => Invoke(instance, builder);

    private void Invoke(object instance, IApplicationBuilder builder)
    {
        // Create a scope for Configure, this allows creating scoped dependencies
        // without the hassle of manually creating a scope.
        using (var scope = builder.ApplicationServices.CreateScope())
        {
            var serviceProvider = scope.ServiceProvider;
            var parameterInfos = MethodInfo.GetParameters();
            var parameters = new object[parameterInfos.Length];
            for (var index = 0; index < parameterInfos.Length; index++)
            {
                var parameterInfo = parameterInfos[index];
                if (parameterInfo.ParameterType == typeof(IApplicationBuilder))
                {
                    parameters[index] = builder;
                }
                else
                {
                    try
                    {
                        parameters[index] = serviceProvider.GetRequiredService(parameterInfo.ParameterType);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(string.Format(
                            "Could not resolve a service of type '{0}' for the parameter '{1}' of method '{2}' on type '{3}'.",
                            parameterInfo.ParameterType.FullName,
                            parameterInfo.Name,
                            MethodInfo.Name,
                            MethodInfo.DeclaringType.FullName), ex);
                    }
                }
            }
            MethodInfo.Invoke(instance, parameters);
        }
    }
}

private static MethodInfo FindMethod(Type startupType, string methodName, string environmentName, Type returnType = null, bool required = true)
{
    var methodNameWithEnv = string.Format(CultureInfo.InvariantCulture, methodName, environmentName);
  	var methodNameWithNoEnv = string.Format(CultureInfo.InvariantCulture, methodName, "");
  	var methods = startupType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
  	var selectedMethods = methods.Where(method => method.Name.Equals(methodNameWithEnv, StringComparison.OrdinalIgnoreCase)).ToList();
  	...
    var methodInfo = selectedMethods.FirstOrDefault();
  	...
    return methodInfo;
}
```

### Build方法

在`Build`方法中做了什么事呢？`Build`翻译过来是生成，那么生成了什么呢？在这里我列出主要的方法如下代码清单：

```c#
public IWebHost Build()
{
    var hostingServices = BuildCommonServices(out var hostingStartupErrors);
    var applicationServices = hostingServices.Clone();
    var hostingServiceProvider = hostingServices.BuildServiceProvider();
    var logger = hostingServiceProvider.GetRequiredService<ILogger<WebHost>>();
    AddApplicationServices(applicationServices, hostingServiceProvider);
    var host = new WebHost(
        applicationServices,
        hostingServiceProvider,
        _options,
        _config,
        hostingStartupErrors);

    host.Initialize();
}
```

`BuildCommonServices`里面初始化寄宿的基本选项信息——`WebHostOptions`，并判断如果在配置文件判断`weboptions.PreventHostingStartup`否与来加载hosting startup所在的程序集并查找是否该程序集标记`HostingStartupAttribute`选择实例化这个启动类，其作用是跟前面提到`UseStartup<TStartup>`是一样的。然后初始化寄宿环境变量，接着通过DI服务注册到DI集合容器里，注册完了之后通过`weboptions.StartupAssembly`注册单例，循环调用`_configureServicesDelegates`委托，并返回`IServiceCollection`，代码清单如下：

```c#
private IServiceCollection BuildCommonServices(out AggregateException hostingStartupErrors)
{
  	_options = new WebHostOptions(_config, Assembly.GetEntryAssembly()?.GetName().Name);
  	if (!_options.PreventHostingStartup){
      foreach (var assemblyName in _options.GetFinalHostingStartupAssemblies().Distinct(StringComparer.OrdinalIgnoreCase))
      {
        try{
          	var assembly = Assembly.Load(new AssemblyName(assemblyName));
          foreach (var attribute in assembly.GetCustomAttributes<HostingStartupAttribute>())
          {
              var hostingStartup = (IHostingStartup)Activator.CreateInstance(attribute.HostingStartupType);
              hostingStartup.Configure(this);
          }
        }
      }	 
  	}
  	// Initialize the hosting environment
    _hostingEnvironment.Initialize(contentRootPath, _options);
    _context.HostingEnvironment = _hostingEnvironment;

    var services = new ServiceCollection();
    services.AddSingleton(_options);
    services.AddSingleton<IHostingEnvironment>(_hostingEnvironment);
 services.AddSingleton<Extensions.Hosting.IHostingEnvironment>(_hostingEnvironment);
    services.AddSingleton(_context);
    var builder = new ConfigurationBuilder()
      .SetBasePath(_hostingEnvironment.ContentRootPath)
      .AddConfiguration(_config);
    foreach (var configureAppConfiguration in _configureAppConfigurationBuilderDelegates)
    {
        configureAppConfiguration(_context, builder);
    }
    var configuration = builder.Build();
    services.AddSingleton<IConfiguration>(configuration);
    _context.Configuration = configuration;
    var listener = new DiagnosticListener("Microsoft.AspNetCore");
    services.AddSingleton<DiagnosticListener>(listener);
    services.AddSingleton<DiagnosticSource>(listener);
    services.AddTransient<IApplicationBuilderFactory, ApplicationBuilderFactory>();
    services.AddTransient<IHttpContextFactory, HttpContextFactory>();
    services.AddScoped<IMiddlewareFactory, MiddlewareFactory>();
    services.AddOptions();
    services.AddLogging();
    // Conjure up a RequestServices
    services.AddTransient<IStartupFilter, AutoRequestServicesStartupFilter>();
 services.AddTransient<IServiceProviderFactory<IServiceCollection>, DefaultServiceProviderFactory>();
    // Ensure object pooling is available everywhere.
    services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
    foreach (var configureServices in _configureServicesDelegates)
    {
        configureServices(_context, services);
    }
    return services;
}
```

返回的services是用来构建`ServiceProvider`,最后初始化WebHost并返回

*<u>注意：上文返回的WebHost是Microsoft.AspNetCore.Hosting，与文章开头的静态类是不同程序集的，后者静态类在独立的程序集——Microsoft.AspNetCore.MetaPackages</u>*

讲到这里我们知道程序实际上已经运行了，那么我们还是有很多疑问，比如在注册Startup类的时候我发现微软的代码中没有显示调用`ConfigureServices，Configure`这两个方法，调试我们发现这两个方法的确是会运行的，微软也注释了：

```c#
// This method gets called by the runtime. Use this method to add services to the container.
public void ConfigureServices(IServiceCollection services){}
// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
public void Configure(IApplicationBuilder app, IHostingEnvironment env){}
```

那么是如何做到的呢？其实细节都在`WebHostBuilderExtensions.UseStartup`拓展函数下

### Kestrel Server

现在我们回头看看`CreateDefaultBuilder`这个方法主要是返回一个`IWebHostBuilder`并集成了一系列的中间件和服务,核心代码我缩减以下代码清单

```c#
public static IWebHostBuilder CreateDefaultBuilder(string[] args)
{
  	var builder = new WebHostBuilder()
      	.UseKestrel((builderContext, options) =>
        {
              options.Configure(builderContext.Configuration.GetSection("Kestrel"));        
        })
      	.UseContentRoot(Directory.GetCurrentDirectory())
      	.ConfigureAppConfiguration((hostingContext, config) =>
        {...})
      	.ConfigureLogging((hostingContext, logging) =>{...})
      	.UseIISIntegration()
      	.UseDefaultServiceProvider((context, options) =>{});
     return	builder;
}
```

拓展函数`public static IWebHostBuilder UseKestrel(this IWebHostBuilder hostBuilder, Action<WebHostBuilderContext, KestrelServerOptions> configureOptions)`内部调用的是“无参”拓展函数`public static IWebHostBuilder UseKestrel(this IWebHostBuilder hostBuilder)`并注册Kestrel选项信息配置信息成功后触发callback函数`configureOptions`。在UseKestrel无参拓展函数下注入三个实例到DI集中容器中，其中就包含`KestrelServerOptionsSetup(Transient)，KestrelServer(Singleton)`这里就不展开说明了，有兴趣的可以自行看源码

### WebHost.Run()

最后我们来看看真正调用WebHost运行的方法；Run是IWebHost的拓展方法，在Run方法里调用了异步函数RunAsync，这个异步函数有两个重载方法，最后都会调用两个参数的重载函数`RunAsync(this IWebHost host, CancellationToken token, string shutdownMessage)`,这个方法里面调用了至关重要的方法：`await host.StartAsync(token);`让我们来看下这里面的主要代码：

```c#
public virtual async Task StartAsync(CancellationToken cancellationToken = default)
{
    var application = BuildApplication();//生成一个HTTP请求管道中的处理任务——RequestDelegate委托

    _applicationLifetime = _applicationServices.GetRequiredService<IApplicationLifetime>() as ApplicationLifetime;//获取application的生命周期控制权
    _hostedServiceExecutor = _applicationServices.GetRequiredService<HostedServiceExecutor>();//获取寄宿服务加载器
    var diagnosticSource = _applicationServices.GetRequiredService<DiagnosticListener>();
    var httpContextFactory = _applicationServices.GetRequiredService<IHttpContextFactory>();//HttoContext工厂类 在后面的HostingApplication中用来生成HttpContext
    var hostingApp = new HostingApplication(application, _logger, diagnosticSource, httpContextFactory);
    await Server.StartAsync(hostingApp, cancellationToken).ConfigureAwait(false);//在Starup集成的选择的中间件服务器调用StartAsync
    // Fire IApplicationLifetime.Started
    _applicationLifetime?.NotifyStarted();
    // Fire IHostedService.Start
    await _hostedServiceExecutor.StartAsync(cancellationToken).ConfigureAwait(false);
}

```

在BuildApplication()方法中确保了服务器的加载，并加载所有内置的服务类和在Starup类下的Configure方法的注册信息。并把上下文和http请求委托传递给HostingApplication类，在这个类里面有个值类型Context上下文容器，里面就承载着HttpContext上下文。调用ProcessRequestAsync把HttpContext传递到Http管道中

```c#
public class HostingApplication : IHttpApplication<HostingApplication.Context>
{
    private readonly RequestDelegate _application;
    private readonly IHttpContextFactory _httpContextFactory;
    private HostingApplicationDiagnostics _diagnostics;
    // Set up the request
    public Context CreateContext(IFeatureCollection contextFeatures)
    {
        var context = new Context();
        var httpContext = _httpContextFactory.Create(contextFeatures);

        _diagnostics.BeginRequest(httpContext, ref context);

        context.HttpContext = httpContext;
        return context;
    }
    // Execute the request
    public Task ProcessRequestAsync(Context context)
    {
        return _application(context.HttpContext);
    }
    public struct Context
    {
        public HttpContext HttpContext { get; set; }
        public IDisposable Scope { get; set; }
        public long StartTimestamp { get; set; }
        public bool EventLogEnabled { get; set; }
        public Activity Activity { get; set; }
    }
}
```

## 总结

让我们来简要概括上面所发生的事：

1. 首先构造一个IWebHostBuilder来构建基本信息如寄宿环境，上下文以及相关配置信息
2. 注册应用程序的启动类Startup,并把一系列的服务以及自定义服务数据放在其中（Configure,ConfigureServices）
3. Build将在DI容器中注册大量的系统服务以及自定义服务，以及还有系统中间件，请求管道等
4. 然后由构建的WebHost.RunAsync -> WebHost.StartAsync 得到application(RequestDelegate)并生成HostingApplication，-> Server.StartAsync在application中运行Server