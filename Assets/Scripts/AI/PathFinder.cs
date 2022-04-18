﻿using System.Collections.Generic;
using BaseAI;
using Priority_Queue;
using UnityEngine;
using UnityEngine.Serialization;

namespace AI
{
    /// <summary>
    /// Делегат для обновления пути - вызывается по завершению построения пути
    /// </summary>
    /// <param name="pathNodes"></param>
    /// /// <returns>Успешно ли построен путь до цели</returns>
    public delegate void UpdatePathListDelegate(List<PathNode> pathNodes);

    /// <summary>
    /// Локальный маршрутизатор - ищет маршруты от локальной точки какого-либо региона до указанного региона
    /// </summary>
    public class LocalPathFinder
    {
        /// <summary>
        /// Построение маршрута от заданной точки до ближайшей точки целевого региона
        /// </summary>
        /// <param name="start">Начальная точка для поиска</param>
        /// <param name="destination">Целевой регион</param>
        /// <param name="movementProperties">Параметры движения</param>
        /// <returns>Список точек маршрута</returns>
        public List<PathNode> FindPath(PathNode start, MovementProperties movementProperties)
        {
            //  Реализовать что-то наподобие A* тут
            //  Можно попробовать и по-другому, например, с помощью NaviMesh. Только оно с динамическим регионом не сработает
            return new List<PathNode>();
        }
    }

    /// <summary>
    /// Глобальный маршрутизатор - сделать этого гада через делегаты и работу в отдельном потоке!!!
    /// </summary>
    public class PathFinder : MonoBehaviour
    {
        /// <summary>
        /// Картограф - класс, хранящий информацию о геометрии уровня, регионах и прочем
        /// </summary>
        [SerializeField] private Cartographer сartographer;

        /// <summary>
        /// Маска слоя с препятствиями (для проверки столкновений)
        /// </summary>
        private int obstaclesLayerMask;

        /// <summary>
        /// 
        /// </summary>
        private float rayRadius;

        public PathFinder()
        {
        }

        /// <summary>
        /// Проверка того, что точка проходима. Необходимо обратиться к коллайдеру, ну ещё и проверить высоту над поверхностью
        /// </summary>
        /// <param name="node">Точка</param>
        /// <returns></returns>
        private bool CheckWalkable(PathNode node)
        {
            //  Сначала проверяем, принадлежит ли точка какому-то региону
            var regionInd = -1;
            //  Первая проверка - того региона, который в точке указан, это будет быстрее
            if (node.RegionIndex >= 0 && node.RegionIndex < сartographer.regions.Count)
            {
                if (сartographer.regions[node.RegionIndex].Contains(node))
                    regionInd = node.RegionIndex;
            }
            else
            {
                var region = сartographer.GetRegion(node);
                if (region != null) regionInd = region.index;
            }

            if (regionInd == -1) return false;
            node.RegionIndex = regionInd;

            //  Следующая проверка - на то, что над поверхностью расстояние не слишком большое
            //  Технически, тут можно как-то корректировать высоту - с небольшим шагом, позволить объекту спускаться или подниматься
            //  Но на это сейчас сил уже нет. Кстати, эту штуку можно через коллайдеры попробовать сделать

            var distToFloor = node.Position.y - сartographer.SceneTerrain.SampleHeight(node.Position);
            if (distToFloor > 2.0f || distToFloor < 0.0f)
            {
                //Debug.Log("Incorrect node height");
                return false;
            }

            //  Ну и осталось проверить препятствия - для движущихся не сработает такая штука, потому что проверка выполняется для
            //  момента времени в будущем.
            //  Но из этой штуки теоретически можно сделать и для перемещающихся препятствий работу - надо будет перемещающиеся
            //  заворачивать в отдельный 

            //if (node.Parent != null && Physics.CheckSphere(node.Position, 2.0f, obstaclesLayerMask))
            //if (node.Parent != null && Physics.Linecast(node.Parent.Position, node.Position, obstaclesLayerMask))
            return node.Parent == null || !Physics.CheckSphere(node.Position, 1.0f, obstaclesLayerMask);
        }

        private static float Heur(PathNode node, PathNode target, MovementProperties properties)
        {
            //  Эвристику подобрать!!!! - сейчас учитываются уже затраченное время, оставшееся до цели время и угол поворота

            float angle = Mathf.Abs(Vector3.Angle(node.Direction, target.Position - node.Position)) /
                          properties.rotationAngle;
            return node.TimeMoment + 2 * node.Distance(target) / properties.maxSpeed + angle * properties.deltaTime;
        }

        /// <summary>
        /// Получение списка соседей для некоторой точки
        /// </summary>
        /// <param name="node"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public IEnumerable<PathNode> GetNeighbours(PathNode node, MovementProperties properties)
        {
            //  Вот тут хардкодить не надо, это должно быть в properties
            //  У нас есть текущая точка, и свойства движения (там скорость, всякое такое)
            //float step = 1f;
            var step = properties.deltaTime * properties.maxSpeed;

            // var result = new List<PathNode>();

            //  Внешний цикл отвечает за длину шага - либо 0 (остаёмся в точке), либо 1 - шагаем вперёд
            for (var mult = 1; mult <= 1; ++mult)
                //  Внутренний цикл перебирает углы поворота
            for (var angleStep = -properties.angleSteps; angleStep <= properties.angleSteps; ++angleStep)
            {
                var next = node.SpawnChild(
                    step * mult,
                    angleStep * properties.rotationAngle,
                    properties.deltaTime
                );
                next.Parent = node;

                //  Точка передаётся по ссылке, т.к. возможно обновление региона, которому она принадлежит
                if (!CheckWalkable(next))
                    continue;

                yield return next;
                Debug.DrawLine(node.Position, next.Position, Color.blue, 10000f, false);
            }
            // return result;
        }

        private float Distance(PathNode from, PathNode to, MovementProperties movementProperties)
        {
            return Vector3.Distance(from.Position, to.Position) // / movementProperties.maxSpeed
                   + Vector3.Angle(from.Direction, to.Direction) / 360; // / movementProperties.rotationAngle);
        }

        /// <summary>
        /// Собственно метод построения пути
        /// Если ничего не построил, то возвращает null в качестве списка вершин
        /// </summary>
        private bool FindPath(
            PathNode start,
            PathNode target,
            MovementProperties movementProperties,
            UpdatePathListDelegate updater
        )
        {
            Debug.Log($"Initial len = {Distance(start, target, movementProperties)}");
            var result = new List<PathNode>();

            var nodes = new SlowPriorityQueue<PathNode>();
            nodes.Enqueue(Distance(start, target, movementProperties), start);

            PathNode res = null;

            var i = 0;
            var minLen = float.MaxValue;
            PathNode minLenNode = null;
            while (nodes.Count > 0 && i < 20000)
            {
                i++;
                var (heur0, current) = nodes.Dequeue();
                if (Distance(current, target, movementProperties) < 5.0)
                {
                    res = current;
                    break;
                }

                var neighbours = GetNeighbours(current, movementProperties);
                foreach (var neighbor in neighbours)
                {
                    var newDist = Distance(neighbor, target, movementProperties);
                    nodes.Enqueue(newDist, neighbor);
                    if (newDist < minLen)
                    {
                        minLen = newDist;
                        minLenNode = neighbor;
                        res = minLenNode;
                    }
                }
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

            updater(result);

            Debug.Log(
                $"Финальная точка маршрута:{result[result.Count - 1].Position}; target:{target.Position.ToString()}");
            Debug.Log("Маршрут обновлён");
            return true;
            //*/
            //  Вызываем обновление пути. Теоретически мы обращаемся к списку из другого потока, надо бы синхронизировать как-то
        }

        /// <summary>
        /// Основной метод поиска пути, запускает работу в отдельном потоке. Аккуратно с асинхронностью - мало ли, вроде бы 
        /// потокобезопасен, т.к. не изменяет данные о регионах сценах и прочем - работает как ReadOnly
        /// </summary>
        /// <returns></returns>
        public bool BuildRoute(PathNode start, PathNode finish, MovementProperties movementProperties,
            UpdatePathListDelegate updater)
        {
            //  Тут какие-то базовые проверки при необходимости, и запуск задачи построения пути в отдельном потоке
            //Task taskA = new Task(() => FindPath(start, finish, movementProperties, updater));
            //taskA.Start();
            //  Из функции выходим, когда путь будет построен - запустится делегат и обновит список точек
            FindPath(start, BuildGlobalRoute(start, finish), movementProperties, updater);
            return true;
        }

        // Возвращает точку в следующем регионе
        public PathNode BuildGlobalRoute(PathNode start, PathNode finish)
        {
            var startRegion = сartographer.GetRegion(start);
            var finishRegion = сartographer.GetRegion(finish);
            if (startRegion == null)
            {
                Debug.LogError("Not found started region!");
                return finish;
            }

            if (finishRegion == null)
            {
                Debug.LogError("Not found finish region!");
                return finish;
            }

            var queue = new SimplePriorityQueue<IBaseRegion, float>();
            queue.Enqueue(startRegion, 0);
            var visited = new Dictionary<int, IBaseRegion>();
            visited.Add(startRegion.index, startRegion);
            while (queue.Count != 0)
            {
                var dist = queue.GetPriority(queue.First);
                var region = queue.Dequeue();
                foreach (var next in region.Neighbors)
                {
                    if (visited.ContainsKey(next.index))
                        continue;
                    visited.Add(next.index, region);
                    queue.Enqueue(next, dist + 1);
                }
            }

            if (!visited.ContainsKey(finishRegion.index))
            {
                Debug.LogError("Not found path to finish region!");
                return finish;
            }

            var nextRegion = finishRegion;
            while (visited[nextRegion.index] != startRegion)
            {
                nextRegion = visited[nextRegion.index];
            }

            if (nextRegion == finishRegion)
                return finish;
            return new PathNode(nextRegion.GetCenter(), start.Direction); // TODO: set correct direction
        }

        void Start()
        {
            // ну и всё вроде бы
            сartographer = new Cartographer(gameObject);
            obstaclesLayerMask = 1 << LayerMask.NameToLayer("Obstacles");
            var rend = GetComponent<Renderer>();
            if (rend != null)
                rayRadius = rend.bounds.size.y / 2.5f;
        }
    }
}