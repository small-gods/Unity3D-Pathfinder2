using System.Collections.Generic;
using System.Linq;
using BaseAI;
using UnityEngine;

// <summary>
// По-хорошему это октодерево должно быть, но неохота.
// Класс, владеющий полной информацией о сцене - какие области где расположены, 
// как связаны между собой, и прочая информация.
// Должен по координатам точки определять номер области.
// </summary>
namespace AI
{
    /// <summary>
    /// Базовый класс для реализации региона - квадратной или круглой области
    /// </summary>
    public interface IBaseRegion
    {
        /// <summary>
        /// Индекс региона - соответствует индексу элемента в списке регионов
        /// </summary>
        int index { get; set; }

        /// <summary>
        /// Список соседних регионов (в которые можно перейти из этого)
        /// </summary>
        IList<IBaseRegion> Neighbors { get; set; }

        /// <summary>
        /// Принадлежит ли точка региону (с учётом времени)
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        bool Contains(PathNode node);

        /// <summary>
        /// Является ли регион динамическим
        /// </summary>
        bool Dynamic { get; }

        /// <summary>
        /// Обе точки в глобальных координатах, но находятся в перемещающемся регионе.
        /// Эта функция добавляет в node смещение, обеспечиваемое движением самого региона.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="node"></param>
        void TransformPoint(PathNode parent, PathNode node);

        /// <summary>
        /// Преобразует глобальные координаты в локальные координаты региона
        /// </summary>
        /// <param name="node"></param>
        void TransformGlobalToLocal(PathNode node);

        /// <summary>
        /// Квадрат расстояния до ближайшей точки региона (без учёта времени)
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        float SqrDistanceTo(PathNode node);

        /// <summary>
        /// Добавление времени транзита через регион
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        void AddTransferTime(IBaseRegion source, IBaseRegion dest);

        /// <summary>
        /// Время перехода через область насквозь, от одного до другого 
        /// </summary>
        /// <param name="source">Регион, с границы которого стартуем</param>
        /// <param name="transitStart">Глобальное время начала перехода</param>
        /// <param name="dest">Регион назначения - ближайшая точка</param>
        /// <returns>Глобальное время появления в целевом регионе</returns>
        float TransferTime(IBaseRegion source, float transitStart, IBaseRegion dest);

        /// <summary>
        /// Центральная точка региона - используется для марштуризации
        /// </summary>
        /// <returns></returns>
        Vector3 GetCenter();

        Collider Collider { get; }

        List<PathNode> FindPath(
            PathNode start,
            PathNode target,
            MovementProperties movementProperties,
            PathFinder ctx
        );
    }

    public class Cartographer
    {
        //  Список регионов
        public List<IBaseRegion> regions = new List<IBaseRegion>();

        //  Поверхность (Terrain) сцены
        public Terrain SceneTerrain;

        // Start is called before the first frame update
        public Cartographer(GameObject surface)
        {
            //  Получить Terrain. Пробуем просто найти Terrain на сцене
            try
            {
                SceneTerrain = (Terrain) Object.FindObjectOfType(typeof(Terrain));
            }
            catch (System.Exception e)
            {
                Debug.Log("Can't find Terrain!!!" + e.Message);
            }

            //  Создаём региончики
            //  Они уже созданы в редакторе, как коллекция коллайдеров - повешена на объект игровой сцены CollidersMaster внутри объекта Surface
            //  Их просто нужно вытащить списком, и запихнуть в список регионов.
            //  Но есть проблема - не перепутать индексы регионов! Нам нужно вручную настроить списки смежности - какой регион с
            //  каким граничит. Это можно автоматизировать, как-никак у нас коллайдеры с наложением размещены, но вообще это
            //  не сработает для динамических регионов (коллайдеры которых перемещаются) - они автоматически не установят связи.
            //  Поэтому открываем картинку RegionsMap.png в корне проекта, и ручками дорисовываем регионы, и связи между ними.

            var colliders = surface.GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                IBaseRegion region = collider switch
                {
                    BoxCollider boxCollider => new BoxRegion(boxCollider),
                    SphereCollider sphereCollider => new SphereRegion(sphereCollider)
                    {
                        PlatformMovement = sphereCollider.gameObject.GetComponent<Platform1Movement>()
                    },
                    _ => throw new System.Exception(
                        "You can't add any other types of colliders except of Box and Sphere!"
                    ),
                };
                regions.Add(region);
                regions[regions.Count - 1].index = regions.Count - 1;
            }

            for (var i = 0; i < regions.Count; i++)
            for (var j = i + 1; j < regions.Count; j++)
                if (regions[i].Collider.bounds.Intersects(regions[j].Collider.bounds))
                {
                    regions[i].Neighbors.Add(regions[j]);
                    regions[j].Neighbors.Add(regions[i]);
                }

            for (var i = 0; i < regions.Count; i++)
            {
                Debug.Log(
                    $"Region : {i} ({regions[i].GetType()}, {regions[i].GetCenter()}) -> {string.Join(", ", regions[i].Neighbors.Select(it => it.index))}");
            }
            // for (int i = 0; i < regions.Count; ++i)
            //     Debug.Log("Region : " + i + " -> " + regions[i].GetCenter().ToString());
        }

        /// <summary>
        /// Регион, которому принадлежит точка. Сделать абы как
        /// </summary>
        /// <param name="node"></param>
        /// <returns>Индекс региона, -1 если не принадлежит (не проходима)</returns>
        public IBaseRegion GetRegion(PathNode node)
        {
            for (var i = 0; i < regions.Count; ++i)
                //  Метод полиморфный и для всяких платформ должен быть корректно в них реализован
                if (regions[i].Contains(node))
                    return regions[i];
            //Debug.Log("Not found region for " + node.Position.ToString());
            return null;
        }

        public bool IsInRegion(PathNode node, int RegionIndex)
        {
            return RegionIndex >= 0 && RegionIndex < regions.Count && regions[RegionIndex].Contains(node);
        }
    }
}