# Hadoop

Hadoop 是一个分布式存储的基础架构。由两个核心功能组成 — — HDFS（Hadoop Distributed File System）以及 MapReduce。HDFS 提供对海量数据的存储；MapReduce 提供对海量数据的分析与计算。

Hadoop 在数据存储与处理方面具有高容错性，高性能，可伸缩性。具体表现在

- 高容错性：在存储文件时，因其他问题发生存储失败的情况或计算失败，所以它会保存数据的多个和副本，来保证重试来完成处理。
- 高效的：因为它采用的 Google 的 MapReduce 并行处理编程模型，能够很好的并行计算，能够在多个节点（DataNode）动态平衡，数据转移。
- 可伸缩性：可以很方便的增加物理机来拓展内存，以及增加数据节点。

## HDFS（Hadoop Distributed File System）

它存储 Hadoop 上所有储存节点的文件，该引擎由 JobTrackers 和 TaskTrackers 组成。

Hadoop 可以创建、删除、移动或重命名文件。

Hadoop 包括一个 NameNode（命名节点）和 N个 DataNode（数据节点）。

> 因为在Hadoop 1.x 版本只有一个 NameNode，所以当 NameNode 不可用时，会导致整个文件系统无法使用，所在 Hadoop 2.x 版本做了改进，增加了一个 NameNode，当其中一个节点不可用时，另一个命名节点可以顶替工作。

### 命名节点（NameNode）

是个运行在  HDFS 实例上的一个独立的软件，负责存储文件的元数据信息，实际的文件内容在数据节点上，命名节点也负责元数据信息与具体文件内容的映射关系。即由 NameNode 管理文件映射具体到那个 DataNode。

### 数据节点（DataNode）

也是独立运行在 HDFS 实例上的一个软件，负责存储具体的实际文件内容。它通过以机架的形式组织，多个 DataNode 是由一个**交换机**将所有系统连接起来的。

## MapReduce

MapReduce 是一个计算框架。[MapReduce的核心思想是把计算任务分配给集群内的服务器里执行。通过对计算任务的拆分（Map计算/Reduce计算）再根据任务调度器（JobTracker）对任务进行分布式计算。](https://blog.csdn.net/qq_32649581/article/details/82892861)

## 适用场景

1. 适合超大文件上传，支持 TB/PB 级别的数据上传存储
2. 数据冗余备份，检测和快速应付硬件故障。NameNode 通过心跳机制来检测 DataNode 是否存在。
3. 大数据分析（如数据挖掘如广告精准投放，日志分析，机器学习等）

要注意，Hadoop 并不适合量多文件小的文件，文件修改效率低。适合一次存入，多次读取。

## 参考资料

https://www.zhihu.com/question/333417513

https://blog.csdn.net/qq_32649581/article/details/82892861



