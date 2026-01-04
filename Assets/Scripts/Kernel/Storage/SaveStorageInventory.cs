// using System.Collections.Generic;
// using Lonize.Scribe;

// namespace Kernel.Storage
// {
//     /// <summary>
//     /// summary: 储物容器存档项（容器内容快照）。
//     /// </summary>
//     public class SaveStorageInventory : ISaveItem
//     {
//         /// <summary>
//         /// summary: 存档项类型ID（需与注册一致）。
//         /// return: TypeId
//         /// </summary>
//         public string TypeId => "StorageInventory";

//         public long RuntimeId;
//         public List<string> ItemIds;
//         public List<int> Counts;

//         /// <summary>
//         /// summary: Scribe 读写入口。
//         /// return: 无
//         /// </summary>
//         public void ExposeData()
//         {
//             Scribe_Values.Look("runtimeId", ref RuntimeId, 0L);
//             Scribe_Collections.Look("itemIds", ref ItemIds);
//             Scribe_Collections.Look("counts", ref Counts);
//         }
//     }
// }
