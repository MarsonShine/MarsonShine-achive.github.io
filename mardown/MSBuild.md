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