

using Lonize.Events;
using Lonize.Flow;

namespace Kernel.Flow
{
    public class Connect
    {
        private void OnBuildingPlaced(BuildingPlaced evt)
        {
            if (!evt.isPlaced || evt.BuildingRuntimeHost == null)
            {
                return;
            }

            var host = evt.BuildingRuntimeHost;
            var go = host.gameObject;

            // 1. 当前建筑的所有端口（可能有电力端口 + 算力端口）
            var myEpComponents = go.GetComponents<FlowEndpointComponent>();
            if (myEpComponents == null || myEpComponents.Length == 0)
            {
                return;
            }
            // var _mapRuntime = host.;
            // // 2. 从地图拿到邻居建筑
            // var neighbors = _mapRuntime.GetNeighborBuildings(host.GridPosition);
            // var neighbors = host.GetNeighborBuildings();
            // foreach (var neighbor in neighbors)
            // {
            //     if (neighbor == null)
            //     {
            //         continue;
            //     }

            //     var nbEpComponents = neighbor.gameObject.GetComponents<FlowEndpointComponent>();
            //     if (nbEpComponents == null || nbEpComponents.Length == 0)
            //     {
            //         continue;
            //     }

            //     // 3. 同资源类型的端口建立连接
            //     foreach (var myEp in myEpComponents)
            //     {
            //         foreach (var nbEp in nbEpComponents)
            //         {
            //             if (myEp.Endpoint == null || nbEp.Endpoint == null)
            //             {
            //                 continue;
            //             }

            //             if (myEp.resourceType == nbEp.resourceType)
            //             {
            //                 FlowSystem.Instance.Connect(myEp.Endpoint, nbEp.Endpoint);
            //             }
            //         }
            //     }
            // }
        }
    }
}