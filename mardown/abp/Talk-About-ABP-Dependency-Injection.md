[TOC]

# 说说ABP的依赖注入

上篇[abp运行机制分析](http://www.cnblogs.com/ms27946/p/ABP-How-Run.html)分析了ABP在启动时，都做了那些事；这篇我们来说说ABP的最核心的一部分：依赖注入(DependencyInjection)，以下简称DI；

DI的概念我就不说了，关键字出来的资料非常多了,这里就不说了，这里主要讨论的是ABP是如何做到自依赖注入的(self register)

读过ABP的[依赖注入](https://aspnetboilerplate.com/Pages/Documents/Dependency-Injection)文档内容我们知道：当你想注入一个服务时，最佳实践是根据命名规范(Naming conventions)来命名，也就是说，加入你想注入一个叫IPersonAppService的服务类，那么你的实现接口的服务类就应该按照Naming conventions规则命名为PersonAppService或者是保留“PersonAppService”字样，改成"Profix+PersonAppService"就能实现self register；否则的话就只能通过另写代码显示的注册接口与实现类；

其实这不是ABP框架这么要求的，这是ABP附带的DI组件——[Castle Windsor](http://docs.castleproject.org/Default.aspx?Page=MainPage)这么要求的；让我们来看看ABP框架的代码就知道了

## 代码追踪

阅读过ABP文档的人应该知道，自依赖注入的关键入口是你项目的WebApplication.Module类的初始化方法`Initialize`下的`IocManager.RegisterAssemblyByConvention(Assembly.GetExecutingAssembly());`顾名思义，就是注册当前运行程序集所有按照命名规范的实现接口类；我们来看看`IocManager`属性，F12我们知道这是一个接口类`IIocManager`，继续追踪可以看出`IIocManager`继承两个接口，一个是Ioc注册接口`IIocRegistrar`和Ioc解析接口`IIocResolver`并且之前提到的Naming conventions以及[abp运行机制分析](http://www.cnblogs.com/ms27946/p/ABP-How-Run.html)知道IocManager就是IIocManager的实现类：

```c#
public interface IIocManager : IIocRegistrar, IIocResolver, IDisposable
{
	IWindsorContainer IocContainer { get; }
	new bool IsRegistered(Type type);
	new bool IsRegistered<T>();
}
public class IocManager : IIocManager, IIocRegistrar, IIocResolver, IDisposable
{
	.../
	public void RegisterAssemblyByConvention(Assembly assembly, ConventionalRegistrationConfig config);
    public void RegisterAssemblyByConvention(Assembly assembly);
	.../
}
```

`public void RegisterAssemblyByConvention(Assembly assembly)`内部调用了它的另一个重载函数`RegisterAssemblyByConvention(Assembly assembly, ConventionalRegistrationConfig config)`并传一个默认规范注册选项配置，而这里配置类就仅仅只是设置一个是否自动按照命名规范注册接口实现类标识`InstallInstallers`，接着初始化`ConventionalRegistrationContext`上下文，然后遍历默认的规约注册器组注册去注册各种服务，最后根据`InstallInstallers`来判断是否调用self-register

```c#
public void RegisterAssemblyByConvention(Assembly assembly, ConventionalRegistrationConfig config)
{
  	var context = new ConventionalRegistrationContext(assembly, this, config);
  	foreach (var registerer in _conventionalRegistrars)
    {
      	registerer.RegisterAssembly(context);
    }
  	if (config.InstallInstallers)
    {
      	//这里这句话就是Castle Windsor在做的事，也就是按照命名规范实现自动注册服务关键所在
      	IocContainer.Install(FromAssembly.Instance(assembly));
    }
}
```

上面这段代码有点要注意，就是foreach段，这里遍历一个注册器集合；这是在IocManager初始化时候就会初始化一个空的`IConventionalDependencyRegistrar`集合，然后在应用程序启动加载Module时就会增加一个默认实现类基础注册器`BasicConventionalRegistrar`,里面包含三种注册服务的细节——Transient，Singleton，Windsor Interceptors；

注入`BasicConventionalRegistrar`细节源代码如下：

```c#
public sealed class AbpKernelModule : AbpModule
{
	public override void PreInitialize()
    {
    	IocManager.AddConventionalRegistrar(new BasicConventionalRegistrar());
    	IocManager.Register<IScopedIocResolver, ScopedIocResolver>(DependencyLifeStyle.Transient);
    	IocManager.Register(typeof(IAmbientScopeProvider<>), typeof(DataContextAmbientScopeProvider<>), DependencyLifeStyle.Transient);
    	AddAuditingSelectors();
        AddLocalizationSources();
        AddSettingProviders();
        AddUnitOfWorkFilters();
        ConfigureCaches();
        AddIgnoredTypes();
    }
}
```

看到这里我相信ABP的依赖注入很清晰了；

下篇我们来说说ABP的缓存管理以及如何写自定义缓存管理类，学习下别人的框架是如何封装的，如何做到高可用，拓展性强的框架的