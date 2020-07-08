# 定义自己的生成方案

MSBuild 是使用标准的生成进程（通过导入 Microsoft.Common.props 以及 Microsoft.Common.targets）有一些可拓展的钩子，你可以使用这些钩子来定义你自己的生成程序。

## 在 MSBuild 调用命令行给你项目添加参数

在一个 Directory.Build.rsp 文件中或在你的源文件夹下，将会应用命令行来生成你的项目。详细细节详见 [MSBuild 响应文件](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-response-files?view=vs-2019#directorybuildrsp)。

## Directory.Build.props 和 Directory.Build.targets

在 MSBuild 15 之前，如果你想提供一个新的，自定义属性到你的解决方案中的项目中，你必须手动在解决方案中的每个项目中添加添加引用。或者是你必须在 .props 文件定义一个属性，然后在解决方案中的每个项目显式的导入这个 .props 文件。

但是，现在你可以只需要一步就可以完成上面提到的步骤。你在一个文件中定义个新的属性，只需要一步就可以给每个项目添加。这个文件被称为 Directory.Common.props ，必须放到根目录中。当 MSBuild 运行时，Microsoft.Common.props 会依据 Directory.Build.props 搜索你的文件结构（以及 Microsoft.Common.targets 查找 Directory.Build.targets）。如果它找到任意一个，他就会导入这个属性。Directory.Common.props 是用户定义的文件，它通过这个文件给项目提供定制化的生成程序。

> 注意⚠️
>
> 基于 Linux 的文件系统是大小写敏感的。要确保 Directory.Build.props 文件名称可以精确匹配，或者在构建过程中不会被发现。
>
> 关于更多的信息请见 [issue](https://github.com/dotnet/core/issues/1991#issue-368441031) 

## Directory.Common.props 示例

举个例子，如果你要在所有的项目中开启访问新的 Roslyn /deterministic 特性（它在 Roslyn `CoreCompile` 通过属性 `$(Deterministic)` 暴露），你可以如下操作。

1. 在你的仓库所在文件夹根目录中创建一个新的文件 Directory.Common.props。

2. 添加如下 xml

   ```xml
   <Project>
   	<PropertyGroup>
     	<Deterministic>true</Deterministic>
     </PropertyGroup>
   </Project>
   ```

3. 运行 MSBuild。你的项目就会导入已存在的 Microsoft.Common.props 文件以及 Microsoft.Common.targets 会查找到这个文件并导入。

## 查询域

当查找 Direcory.Build.props 文件时，MSBuild 从你的项目位置向上遍历文件结构（`$(MSBuildProjectFullPath)`）,直到在定位到了文件 Directory.Build.props 文件时停止。例如，如果你的 `$(MSBuildProjectFullPath)` 是 c:\user\username\code\test\case1，MSBuild 将开始查找，向上搜寻目录结构直到定位到 Direcotory.Build.props。下面是查找出来的目录结构。

```
c:\user\username\code\test\case1
c:\user\username\code\test
c:\user\username\code
c:\user\username
c:\user
c:\
```

解决方案的文件位置与 Directory.Build.props 无关。

## 导入顺序

在 Microsoft.Build.props 在 Microsoft.Common.props 中非常早就导入的，这些在后面定义的属性是无可用的，因此要避免那些未定义的属性（并且也会当成空）。

Directory.Build.props 定义的属性集合能被项目文件中或者导入到文件中覆写，所以你可以考虑在你的项目中的 Directory.Build.props 中设置一些默认值。

Directory.Build.targets 是 Microsoft.Common.targets 从 Nuget 包导入 .targets 文件之后导入的。所以在大多数构建逻辑中都能覆盖 targets 定义的属性，或者在你的项目中的设置属性集合，无论你项目设置了什么。

当你需要设置一个属性或为一个单独的项目定义个 target 来覆写一些设置，将该逻辑放在了最终导入之后的项目中。为了在 SDK-风格的项目实现目的，你首先必须要等效的导入替换 SDK-风格的属性。详见 [怎样使用 MSBuild 项目 SDKs](https://docs.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk?view=vs-2019)。

> 注意⚠️
>
> 在为项目开始构建之前，MSBuild 引擎在评估期间会读取所有已导入的文件（包括所有的 `PreBuildEvent`），因此这些文件不希望被 `PreBuildEvent` 修改，或者成为构建过程的其他任何一部分。任何修改行为都无效，直到下一个 MSBuild.exe 或 Visual Studio 构建调用。

## .user 文件

Microsoft.Common.CurrentVersion.targets 导入  $(MSBuildProjectFullPath).user` ,如果它存在的话，所以你可以在项目旁边创建带有该附加拓展名的文件。对于计划签入到源代码控制的长期更改，最好更改项目本身，所以这个功能的维护者无需知道这个拓展机制。

## MSBuildExtensionsPath 和 MSBuildUserExtensionsPath

> 警告⚠️
>
> 使用这里的拓展机制能够让它获取跨机器的可重复的构建机制变得很困难。尝试使用一个通过它能签入到你的源代码控制系统以及在代码库的所有开发人员之间共享配置。

按约定，大多数构建逻辑文件导入

```xml
$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\{TargetFileName}\ImportBefore\*.targets
```

在上述内容之前还要

```xml
$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\{TargetFileName}\ImportAfter\*.targets
```

之后，按约定允许已安装的 SDKs 增加常用的项目类型。

在 `$(MSBuildUserExtensionsPath)` 中查找相同的文件结构，它是每个用户的文件 %LOCALPPDATA\Microsoft\MSBuild。对于在该用户凭据下运行的相应的项目类型的所有构建，将导入该文件夹中的文件。在模式 `ImportUserLocationsByWildcardBefore{ImportingFileNameWithNoDots}` 中导入文件之后。通过属性命名来禁用用户拓展。例如，设置 `ImportUserLocationsByWildcardBeforeMicrosoftCommonProps` 为 `false`，这将会阻止导入 `$(MSBuildUserExtensionsPath)\$(MSBuildToolsVersion)\Imports\Microsoft.Common.props\Import Before\*`

## 自定义解决方案构建

> 重点⚠️
>
> 自定义解决方案构建只能在 MSBuild.exe 之上使用命令行。它无法应用于 Visual Studio 构建。因此，不推荐在解决方案级别用自定义构建。好的做法是在解决方案中所有项目使用自定义的 Directory.Build.props 和 Directory.Build.targets 文件，像在这篇讨论的一样。

当 MSBuild 构建一个解决方案文件时，首先它会转为内部的项目文件并构建。这个生成的项目文件就会导入 `before.{solutionname}.sln.targets` 之前定义 target，以及在导入 targets 之后定义所有的目标节点 `after.{solutionname}.sln.targets`，包括已经安装到 `$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\SolutionFile\ImportBefore` 的 targets 和 `$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\SolutionFile\ImportAfter` 的文件目录。

例如，你可以通过在相同的文件夹中创建一个名为 “after.MyCustomizedSolution.sln.targets” 的文件在 MyCustomizedSolution.sln 构建之后添加新的 target 来编写自定义日志消息

```xml
<Project>
	<Target Name="EmitCustomMessage" AfterTargets="Build">
        <Message Importance="High" Text="The solution has completed the Build target" />
    </Target>
</Project>
```

解决方案生成与项目生成是独立的，所以在这里设置对项目来说是没效果的。

## 自定义所有的 .NET 构建

当维护一个构建服务的时候，你可以配置 MSBuild 设置项，配置全局的构建。原则上，你可以修改全局的 Microsoft.Common.props 或 Microsoft.Common.targets，但是还有更好的方式。你可以通过 MSBuild 主要属性来影响某些项目（比如 C# 项目）的所有的构建程序，以及添加一些自定义的 `.targets` 和 `.props` 文件。

通过安装 MSBuild 或 Visual Studio 管理构建来影响所有的 C# 或 Visual Basic 构建，创建一个文件 `Custom.Before.Microsoft.Common.Targets` 或 `Custom.After.Microsoft.Common.Targets`, 这些将会在 `Microsoft.Common.Target` 之前或之后运行，或者一个 `Custom.Before.Microsoft.Common.Props` 或 `Custom.After.Microsoft.Common.Props` 文件使用属性在 *Microsoft.Common.props*. 之前或之后处理。

你可以通过使用下面的 MSBuild 属性指定这些文件：

- CustomBeforeMicrosoftCommonProps
- CustomBeforeMicrosoftCommonTargets
- CustomAfterMicrosoftCommonProps
- CustomAfterMicrosoftCommonTargets
- CustomBeforeMicrosoftCSharpProps
- CustomBeforeMicrosoftVisualBasicProps
- CustomAfterMicrosoftCSharpProps
- CustomAfterMicrosoftVisualBasicProps
- CustomBeforeMicrosoftCSharpTargets
- CustomBeforeMicrosoftVisualBasicTargets
- CustomAfterMicrosoftCSharpTargets
- CustomAfterMicrosoftVisualBasicTargets

其中 *Common* 版本的这些属性都能影响 C# 和 Visual Basic 项目。你可以在 MSBuild 命令中设置这些属性。

```cmd
msbuild /p:CustomBeforeMicrosoftCommonTargets="C:\build\config\Custom.Before.Microsoft.Common.Targets" MyProject.csproj
```

最好是依赖于你自己的场景。使用 Visual Studio 拓展，你可以自定义构建系统以及提供一个安装和管理定制化机制。

如果你专注构建服务器并且想确保那些 targets 总能在服务器上所有的合适的项目类型上运行构建，然后使用一个全局的自定义 `.targets` 或 `props` 文件是有意义的。如果你想自定义的 targets 只作用在符合某个条件的运行，可以通过在 MSBuild 命令行按需设置合适的 MSBuild 属性，这样就使用其他的文件定位并为文件设置路劲。

> 警告⚠️
>
> Visual Studio 使用自定义的 `.targets` 和 `.props` 文件，如果它在 MSBuild 文件夹发现这些文件，这时就会构建所有匹配的项目类型。这可能会产生意想不到的后果，并且其行为是不正确的，你可以在你的计算机上禁止 Visual Studio 的这种功能。

## MSBuild 最佳实践

默认属性值最好是通过使用条件属性 `Condition` 来处理，并且不要通过声明一个属性，这个属性的默认值能被命令行覆盖。如

```xml
<PyProperty Condition="'$(MyProperty)' == ''">
    MyDefaultValue
</PyProperty>
```

一般情况下要避免使用通配符来选择项。而应该显式的指定文件。这是因为在大多数项目类型中，MSBuild 在各个时间拓展通配符，比如当添加或删除项的时候，它能导致出人意料的行为。一个例外就是在 .NET Core SDK-风格项目，它能正确的处理通配符



## 自定义 C++ 构建

略

## 自定义所有 C++ 构建

略



翻译自：https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#customize-the-solution-build