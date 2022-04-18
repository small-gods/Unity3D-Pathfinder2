using System;
using System.Collections.Generic;
using UnityEngine;

namespace AI
{
    /// <summary>
    /// Сферический регион на основе SphereCollider
    /// </summary>
    public class SphereRegion : IBaseRegion
    {
        /// <summary>
        /// Тело региона - коллайдер
        /// </summary>
        public SphereCollider body;

        public Collider Collider => body;

        /// <summary>
        /// Расстояние транзита через регион
        /// </summary>
        private Dictionary<System.Tuple<int, int>, string> transits;

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


        public SphereRegion(SphereCollider sample)
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
            return new List<PathNode>
            {
                target
            };
        }
    }
}