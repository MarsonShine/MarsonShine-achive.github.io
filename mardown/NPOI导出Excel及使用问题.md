# NPOI导出Excel及使用问题

因为最近公司质管部门提出了一个统计报表的需求：要求导出一个2016及2017年度深圳区域的所有供应商的费用成本计算——一个22列的Excel表，其中还包括多列的合并单元格；说实话，统计报表功能其实我还是很少涉及的，以前都是直接用DataTable+输出流导出Excel，因为涉及到合并单元格，明显用输出流就不合适了，此时NPOI开源框架就很合适了；当然还有其他组件可以选择，比如[EPPlush](http://epplus.codeplex.com/)，微软自带组件，以及收费的[Aspose.Cells](https://products.aspose.com/cells/net);因为[NPOI](http://npoi.codeplex.com/)资料比较多且公司用的组件也是这个，所以就选择它了；

### 一步一步按需导出

由于这个功能因为查询出来的数据量很大需要单独抽离出来（不能放到公司的系统上），所以我就新建了一个控制台应用程序，简单的做了一个导出Excel的功能，因为刚开始需求没要求需要合并单元格，所以我这边就很快的做出来了：核心代码如下：

```c#
ExportToExcelByNOPI(dt, GetOriginColumns(dt), $"2016年度成本费用统计.xlsx"));
private void DataTableToExcel(DataTable dt, string[] titles, string file)
{
    using (FileStream fs = new FileStream(file, FileMode.OpenOrCreate))
    using (StreamWriter sw = new StreamWriter(new BufferedStream(fs), Encoding.Default))
    {
        string title = "";
        //拼接表头
        for (int i = 0; i < dt.Columns.Count; i++)
        {
            title += titles[i] + "\t";//自动跳到下一单元格
        }
        title = title.Substring(0, title.Length - 1) + "\n";
        sw.Write(title);

        foreach (DataRow row in dt.Rows)
        {
            string line = "";
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                //line += row[i].ToString().Trim() + "\t"; //内容：自动跳到下一单元格
                line += row[i].ToString().Trim() + "\t";//自动跳到下一单元格
            }
            line = line.Substring(0, line.Length - 1) + "\n";
            sw.Write(line);
        }
    }
}
```

把导出的excel发过去发现根本不符合他们的要求，说要对哪些行合并单元格，这样有利于他们数据分析，这样的话就得NPOI上场了；刚开始想法很简单，只要他们的值相等，我就把他合并单元格，毕竟像订单号是唯一的么，那么订单号所附带的如订单重量，数量等都是相同的（其实还是想的太当然了，导致了后面的一系列的问题）

```c#
private void ExportToExcelByNOPI(DataTable dt, string title, string strFilename)
{
    if ((dt == null) || string.IsNullOrEmpty(strFilename))
    {
        return;
    }
    if (File.Exists(strFilename))
    {
        File.Delete(strFilename);
    }
    //添加表头
    for (int i = 0; i < dt.Columns.Count; i++)
    {
        ICell cell = headerrow.CreateCell(i);
        cell.CellStyle = style;
        cell.SetCellValue(dt.Columns[i].ColumnName);
    }
  	//添加第一行数据
    IRow row = sheet.CreateRow(1);
    for (int j = 0; j < dt.Columns.Count; j++)
    {
        string cellText = dt.Rows[0][j].ToString();
        row.CreateCell(j).SetCellValue(cellText);
    }
    //从第二行开始循环，和上一行进行判断，如果相同，则合并
    for (int i = 1; i < dt.Rows.Count; i++)
    {
        row = sheet.CreateRow(i + 1);
        for (int j = 0; j < dt.Columns.Count; j++)
        {
            string cellText = dt.Rows[i][j].ToString();
            row.CreateCell(j).SetCellValue(cellText);
            string temp = dt.Rows[i - 1][j].ToString();
            //这里是合并单元格条件判断，如值是否相等，是否在合并列要求之内
            if (!string.IsNullOrEmpty(temp) && cellText== temp && ColumnsName.Contains(sourceTable.Columns[j].ColumnName))
            {
                CellRangeAddress region = new CellRangeAddress(i, i+1, j, j);
                sheet.AddMergedRegion(region);
            }
        }
    }
    style.Alignment = HorizontalAlignment.Center;
    style.VerticalAlignment = VerticalAlignment.Center;
    style.Alignment = HorizontalAlignment.Center;//居中显示
    using (FileStream fs = new FileStream(strFilename, FileMode.Open, FileAccess.ReadWrite))
    using (MemoryStream ms = new MemoryStream())
    {
        workbook.Write(ms);
        var buf = ms.ToArray();
        fs.Write(buf, 0, buf.Length);
        fs.Flush();
    }
}
```

导出来发现Excel里面合并之后的内容不对了，有的合并单元格不对（比如订单号是01，有两个产品P1，P2，这就有两行，如果这两行订单号是相同的，则按需求是要合并的，但是其他列的值有的合并多了，稍微一细想就知道原因了，我只是单纯的比较上一行与下一行的列值，那么下一行的其它订单的产品信息如规格，值相同的话也会被合并，这就不符合我们的要求了，所以还得在这个基础之上在加限制条件；

因为这个表是已订单为维护的，那么我们就以这列为参照合并规则来记录这列被合并的行数，然后我们标记这个行数记为`sameCount`，那么每列的值我们都会比较，如果在`sameCount`行列值相同，则合并；那么就得写个辅助类`CellCalculateHelper`来计算出要求被合并的列在`sameCount`行值是否都相同：

```c#
internal class CellCalculateHelper
{
  	/// <summary>
    /// 从startRow行开始比较相同订单号的行数
    /// </summary>
	internal static (int startRow, int sameCount) GetRepeaterCount(int startRow, DataTable dt)
	{
  		var i = startRow;
        var sameCount = 0;
        while (dt.Rows[startRow][0].ToString() == dt.Rows[i][0].ToString())
        {
            if ((i + 1) == dt.Rows.Count) break;
            sameCount++;
            i++;
        }
        return (startRow, sameCount);
	}
  
  	internal static bool IsMergeRegionMaxRepeatCount(DataTable dt, string columnName, int startRow, int sameCount)
    {
  		var start = startRow + sameCount - 1;
      	while (sameCount - 1 != 0)
        {
  			if (dt.Rows[start][columnName].ToString() == dt.Rows[start - 1][columnName].ToString()){
              	start = start - 1;
              	sameCount--;
			}else{
  				return false;
  			}
		}
      	return true;
	}
}
```

有了这个帮助类，就好办了，改造上面的`ExportToExcelByNOPI`方法如下:

```c#
private void ExportToExcelByNOPI(DataTable dt, string title, string strFilename)
{
    ...
    //添加第一行数据这样上面代码相同
    //记住第一行相同的最大行数
    var tuple = CellCalculateHelper.GetRepeaterCount(0, sourceTable);
  	//第一行数据遍历
  	for (int i = 0; i < sourceTable.Rows.Count; i++){
  		IRow row = sheet.CreateRow(i + 1);
      	//如果当前行数等于最大相同的函数(相当于合并之后的下一行数据，必定与上一行数据不同)
      	if (i == tuple.startRow + tuple.sameCount){
  			tuple = CellCalculateHelper.GetRepeaterCount(i, sourceTable);
		}
      	//遍历列
      	for (int j = 0; j < sourceTable.Columns.Count; j++){
  			string cellText = sourceTable.Rows[i][j].ToString();
          	row.CreateCell(j).SetCellValue(cellText);
          	if (tuple.sameCount > 1){
  				//需要合并
              	string tempValue = sourceTable.Rows[i][j].ToString();
              	//指定列合并单元格
              	if (!string.IsNullOrWhiteSpace(tempValue) && 
                    cellText == tempValue && 
                    ColumnsName.Contains(sourceTable.Columns[j].ColumnName)){
  						//判断是否是参照行OrdNO
                    	if (i >= tuple.startRow + tuple.sameCount - 1) continue;
                  		if ((ColumnsName[0] == sourceTable.Columns[j].ColumnName)){
  							//下一行与上一行合并
                            CellRangeAddress region = new CellRangeAddress(i + 1, i + 2, j, j);
                          	sheet.AddMergedRegion(region);
						}else{
  							//判断该列的最大sameCount行值是否相同，如果不同，不合并；相同则合并
                          	if (CellCalculateHelper.IsMergeRegionMaxRepeatCount(sourceTable, sourceTable.Columns[j].ColumnName, i, tuple.sameCount)){
  								CellRangeAddress region = new CellRangeAddress(i + 1, i + 2, j, j);
                              	sheet.AddMergedRegion(region);
							}
						}
				}
			}
		}
	}
  	...
    ...
}
```

这样导出来的数据就是正确符合业务同事的要求了！

### 后记

写到这里以为这些都是一帆风顺的吗？

NO！

我被坑在一个奇怪的地方，至今我也没想到原因：期初，我是用控制台应用程序想简单的导出excel的，也测试了从数据库查出一个供应商的所有订单信息导出excel是没问题的，于是当我查询出所有的供应商的时候，bug出现了，程序运行一段时间后毫无反应了（并不是死机，也没有报内存溢出的错误），因为数据量很大，所以当时我还跟个煞笔似的在那里等结束，等我吃完中饭回来发现还是没有成功导出，我就意识到不对了，但是不报任何异常，我根本查不到问题现在那，我接着尝试换种写法导出excel——分页，以及分批次导出不同的excel；这种是可以的，到这我心里就知道估计是内存问题了，最后我把整个控制台项目换成类库，然后新建web应用程序能一次运行成功，更加让我坚信是内存问题，但是为什么控制台应用程序不会报内存溢出的错呢？这个我真的无从查起啊，有朋友知道，希望能告诉我

### 2017年12月29日修补：

前面修改之后还是不对，由于数据量太大，我在看了前部分的数据没问题因为就OK，实际上问题还是比较明显的，就是当有2个以上相同的数据列时，合并单元格就会有问题，原来我想的是H1，H2合并成为H21，然后继续循环H3，接着合并，我以为H21与H3合并会成为一个在Excel中三行一列组成的合并单元格，但是结果发现是H3与H4合并之后在与H21拼接的两个2行1列的单元格，这就有大问题，后来我就把合并单元格条件部分修正如下，便完美了。代码如下：

```c#
//判断是否是参照行OrdNO
if (i > tuple.startRow + tuple.sameCount - 1) continue;
//新增的i == tuple.startRow 是为了防止多次合并
if ((ColumnsName[0] == sourceTable.Columns[j].ColumnName) && i == tuple.startRow)
{
  	//startRow与startRow+sameCount行合并(也就是一次性合并相同行数单元格)
  	CellRangeAddress region = new CellRangeAddress(tuple.startRow + 1, tuple.startRow + tuple.sameCount, j, j);
  	sheet.AddMergedRegion(region);
}else
{
  	if (CellCalculateHelper.IsMergeRegionMaxRepeatCount(sourceTable, sourceTable.Columns[j].ColumnName, tuple.startRow, tuple.sameCount) && i == tuple.startRow)
    {
      	CellRangeAddress region = new CellRangeAddress(tuple.startRow + 1, tuple.startRow + tuple.sameCount, j, j);
      	sheet.AddMergedRegion(region);
    }
}
```

