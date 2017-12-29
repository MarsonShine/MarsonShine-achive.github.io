### abp运行流程

由于公司现在大量向abp框架+react前后端分离架构转型，所以有必要分析abp框架是如何在iis运行的，所以才有这篇文章

```c#
public class MvcApplication : AbpWebApplication<MyAbpApplicationWebModule>
{
    protected override void Application_Start(object sender, EventArgs e)
    {
        AbpBootstrapper.IocManager.IocContainer.AddFacility<LoggingFacility>(
            f => f.UseAbpLog4Net().WithConfig(Server.MapPath("log4net.config"))
        );
        
        base.Application_Start(sender, e);
    }
}`
```
当web应用程序启动，`AbpWebApplication<TStartupModule>`在`AbpBootstrapper`构造函数注册Module，并检查这个Module是否集成自AbpModule，初始化`IocManager,PlugInSources`以及日志实例。还注册了拦截器

```c#
    private void AddInterceptorRegistrars()
    {
        ValidationInterceptorRegistrar.Initialize(IocManager);
        AuditingInterceptorRegistrar.Initialize(IocManager);
        UnitOfWorkRegistrar.Initialize(IocManager);
        AuthorizationInterceptorRegistrar.Initialize(IocManager);
    }`
```
由`AbpWebApplication.Application_Start`初始化abp系统

```c#
    protected virtual void Application_Start(object sender, EventArgs e)
    {
        ThreadCultureSanitizer.Sanitize();
        AbpBootstrapper.Initialize();	//这里就是初始化abp系统，后面又详细讲到
    }
```

我们注意到`AbpWebApplication<TStartupModule>`中的`TStartupModule`有个约束：`TStartupModule:AbpModule`让我们来看下这个类的大体定义：

> A module definition class is generally located in it's own assembly and implements some action in module events on application startup and shutdown.It also defines depended modules.

意思是在自己的模块程序集中定义实现了一些在应用程序启动到结束期间的事件操作，也定义依赖的那些模块。在源代码中能看到作者定义了四个事件操作：

- PreInitialize: 应用程序开始时触发，在依赖注入注册之前，代码能放在这里运;
- Initialize: 主要用来依赖注入;
- PostInitialize: 应用程序启动之后触发;
- Shutdown: 应用程序结束时触发

除了这四个周期事件，还有一种重要的操作就是递归查找所有依赖模块类，返回所有的模块类

```c#
public static List<Type> FindDependedModuleTypes(Type moduleType)
```

这里我就有一个疑问了，定义了这四个周期事件，那么是在哪里调用的呢？

带着问题查找源代码，发现在`AbpBootstrapper.Initialize`方法中注册了模块管理类：`AbpModuleManager`：

```c#
/// <summary>
/// Initializes the ABP system.
/// </summary>
public virtual void Initialize()
{
    ResolveLogger();
	try
	{
		RegisterBootstrapper();
		IocManager.IocContainer.Install(new AbpCoreInstaller());
		IocManager.Resolve<AbpPlugInManager>().PlugInSources.AddRange(PlugInSources);
		IocManager.Resolve<AbpStartupConfiguration>().Initialize();

		_moduleManager = IocManager.Resolve<AbpModuleManager>();
		_moduleManager.Initialize(StartupModule);//这里初始话模块集合以及加载所有模块类
		_moduleManager.StartModules();//这里就调用了模块定义的周期事件操作
	}
  	...
}
```

继续追踪`AbpModuleManager.Initialize(Type startupModule);_moduleManager.StartModules();`

在应用程序结束时(在abp系统中体现在`AbpBootstrapper.Dispose`)

```c#
public virtual void Initialize(Type startupModule)
{
	_modules = new AbpModuleCollection(startupModule);//初始化模块集合list
	LoadAllModules();//加载所有模块类
}
public virtual void StartModules()
{
	var sortedModules = _modules.GetSortedModuleListByDependency();
    sortedModules.ForEach(module => module.Instance.PreInitialize());
    sortedModules.ForEach(module => module.Instance.Initialize());
    sortedModules.ForEach(module => module.Instance.PostInitialize());
}
/// <summary>
/// Disposes the ABP system.
/// </summary>
public virtual void Dispose()
{
	if (IsDisposed)
	{
		return;
	}
	IsDisposed = true;
	_moduleManager?.ShutdownModules();//调用module.Instance.Shutdown
}
```

至此，我们就很清楚的知道了abp是在那个时期如何进行依赖注入的，是什么时候注册插件以及自定义的四个“钩子事件”