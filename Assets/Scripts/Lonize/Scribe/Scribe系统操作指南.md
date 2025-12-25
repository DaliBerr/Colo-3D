# 保存系统是 “DTO (数据传输对象) 列表模式”。

## 管理员 (ScribeSaveManager)：负责读写文件，手里拿着一张清单 (PolySaveData.Items)。

## 快递盒 (ISaveItem)：游戏数据不能直接存，必须装进这些特制的盒子里。

## 注册 (Register)：必须提前告知管理员有哪些种类的盒子，否则读取时管理员不认识会直接扔掉。

第一步：制作快递盒 (定义数据类)
新建一个脚本（例如 SaveMyData.cs），实现 ISaveItem 接口。这是用来搬运数据的载体。


```cs
using Lonize.Scribe; // 必须引用

public class SavePlayerInfo : ISaveItem
{
    // 【关键 1】身份证：这个字符串必须唯一，读取时靠它识别类型
    public string TypeId => "PlayerInfo";

    // 【关键 2】要保存的数据字段
    public int HP;
    public float[] Pos; // 建议用 float数组 或 简单结构体存 Vector3

    // 【关键 3】ExposeData：告诉 Scribe 怎么读写这个盒子的内容
    // 写入时：把变量的值写入标签；读取时：从标签读出值填入变量
    public void ExposeData()
    {
        Scribe_Values.Look("hp", ref HP, 100);       // 基本类型用 Scribe_Values
        Scribe_Values.Look("pos", ref Pos, null);    // 数组/列表系统会自动处理
    }
}
```

第二步：去管理员处登记 (注册类型)
这一步最容易忘！ 只要新写了一个 ISaveItem，就必须去注册。 打开 Assets/Scripts/Kernel/Save/ScribeSaveManager.cs，找到 RegisterSaveItems() 方法。

```cs
private static void RegisterSaveItems()
{
    // ... 原有的注册 ...
    
    // 格式：PolymorphRegistry.Register<你的类名>("你的TypeId");
    // 注意：这里的字符串必须和类里面的 TypeId 完全一致！
    PolymorphRegistry.Register<SavePlayerInfo>("PlayerInfo"); 
}
```
第三步：如何执行“保存” (打包流程)
通常在 GameSaveController 或游戏主逻辑中执行。切记先清空清单！

```C#

public void SaveGame()
{
    var manager = ScribeSaveManager.Instance;
    
    // 1. 【核心】清空旧数据！否则存档会越来越大，全是重复数据
    manager.Data.Items.Clear();

    // 2. 创建盒子并装入数据
    var myData = new SavePlayerInfo();
    myData.HP = Player.CurrentHP; // 从游戏逻辑取值
    myData.Pos = new float[] { Player.transform.position.x, Player.transform.position.y };

    // 3. 把盒子交给管理员
    manager.AddItem(myData);

    // ... 对所有需要保存的对象重复 2 和 3 ...

    // 4. 落盘写入文件
    manager.Save();
}
```
第四步：如何执行“读取” (解包流程)
通常在游戏启动或点击“读取存档”时执行。

```C#

public void LoadGame()
{
    var manager = ScribeSaveManager.Instance;

    // 1. 从硬盘加载文件到内存
    if (!manager.Load()) return; // 没存档就退出

    // 2. 遍历清单，认领数据
    foreach (var item in manager.Data.Items)
    {
        // 使用 C# 模式匹配来识别盒子类型
        if (item is SavePlayerInfo info)
        {
            // 3. 把数据应用回游戏物体
            Player.CurrentHP = info.HP;
            Player.transform.position = new Vector3(info.Pos[0], info.Pos[1], 0);
        }
        else if (item is SaveBuilding buildingData)
        {
            // 如果是动态物体，可能需要在这里 Instantiate 生成出来
            SpawnBuilding(buildingData);
        }
    }
}
```
🚨 常见避坑指南
TypeId 不匹配：SaveItem 里的 TypeId 和 ScribeSaveManager 里注册的字符串不一样。会导致读取时报错或读出 null。

忘记 Clear()：保存前没有 Data.Items.Clear()。会导致存档文件里包含前一次保存的尸体，数据量倍增。

忘记 Register：新写的类没注册。保存时没问题，读取时会直接丢失该数据（Scribe 会跳过不认识的数据块）。

Vector3 问题：Scribe 默认不支持 Unity 的 Vector3。

笨办法：存成 float[3] 或三个 float。

好办法：写一个 Vector3Codec 并注册到 CodecRegistry（一劳永逸）。