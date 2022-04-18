using System;
using System.Collections.Generic;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// Сферический регион на основе BoxCollider
    /// </summary>
    public class BoxRegion : IBaseRegion
    {
        /// <summary>
        /// Тело коллайдера для представления региона
        /// </summary>
        public BoxCollider body;

        public Collider Collider => body;

        /// <summary>
        /// Индекс региона в списке регионов
        /// </summary>
        public int index { get; set; } = -1;

        bool IBaseRegion.Dynamic { get; } = false;

        void IBaseRegion.TransformPoint(PathNode parent, PathNode node)
        {
            return;
        }

        void IBaseRegion.TransformGlobalToLocal(PathNode node)
        {
            /*ничего не делаем - регион статический*/
        }

        public IList<IBaseRegion> Neighbors { get; set; } = new List<IBaseRegion>();

        /// <summary>
        /// Создание региона с кубическим коллайдером в качестве основы
        /// </summary>
        /// <param name="RegionIndex"></param>
        /// <param name="position"></param>
        /// <param name="size"></param>
        public BoxRegion(BoxCollider sample)
        {
            body = sample;
        }

        /// <summary>
        /// Квадрат расстояния до региона (минимально расстояние до границ коллайдера в квадрате)
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public float SqrDistanceTo(PathNode node)
        {
            return body.bounds.SqrDistance(node.Position);
        }

        /// <summary>
        /// Проверка принадлежности точки региону
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool Contains(PathNode node)
        {
            return body.bounds.Contains(node.Position);
        }

        /// <summary>
        /// Время перехода через область насквозь, от одного до другого 
        /// </summary>
        /// <param name="source">Регион, с границы которого стартуем</param>
        /// <param name="transitStart">Глобальное время начала перехода</param>
        /// <param name="dest">Регион назначения - ближайшая точка</param>
        /// <returns>Глобальное время появления в целевом регионе</returns>
        public float TransferTime(IBaseRegion source, float transitStart, IBaseRegion dest)
        {
            throw new System.NotImplementedException();
        }

        public Vector3 GetCenter()
        {
            //  Вроде бы должно работать
            return body.bounds.center;
        }

        void IBaseRegion.AddTransferTime(IBaseRegion source, IBaseRegion dest)
        {
            throw new System.NotImplementedException();
        }

        public List<PathNode> FindPath(
            PathNode start,
            PathNode target,
            MovementProperties movementProperties,
            PathFinder ctx
        )
        {
            Debug.Log($"Initial len = {ctx.Distance(start, target, movementProperties)}");
            var result = new List<PathNode>();

            var nodes = new SlowPriorityQueue<PathNode>();
            nodes.Enqueue(ctx.Distance(start, target, movementProperties), start);

            PathNode res = null;

            var i = 0;
            var minLen = float.MaxValue;
            PathNode minLenNode = null;
            while (nodes.Count > 0 && i < 20000)
            {
                i++;
                var (heur0, current) = nodes.Dequeue();
                if (ctx.Distance(current, target, movementProperties) <
                    movementProperties.deltaTime * movementProperties.maxSpeed / movementProperties.closeEnslowment)
                {
                    res = current;
                    break;
                }

                var backupSpeed = movementProperties.maxSpeed;
                if (Vector3.Distance(current.Position, target.Position) < movementProperties.targetClose)
                {
                    movementProperties.maxSpeed = backupSpeed / movementProperties.closeEnslowment;
                }

                var neighbours = ctx.GetNeighbours(current, movementProperties);
                foreach (var neighbor in neighbours)
                {
                    var newDist = ctx.Distance(neighbor, target, movementProperties);
                    nodes.Enqueue(newDist, neighbor);
                    if (newDist < minLen)
                    {
                        minLen = newDist;
                        minLenNode = neighbor;
                        res = minLenNode;
                    }
                }

                movementProperties.maxSpeed = backupSpeed;
            }

            if (res == null)
            {
                result.Add(minLenNode);
                Debug.Log($"res == null, minLen = {minLen}");
            }
            else
            {
                Debug.Log($"i = {i}");
                while (res != null)
                {
                    result.Add(res);
                    res = res.Parent;
                }

                result.Reverse();
            }

            Debug.Log(
                $"Финальная точка маршрута:{result[result.Count - 1].Position}; target:{target.Position.ToString()}");

            return result;
        }
    }
}