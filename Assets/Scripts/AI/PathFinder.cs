using System.Collections.Generic;
using BaseAI;
using Priority_Queue;
using UnityEngine;

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
        /// Объект сцены, на котором размещены коллайдеры
        /// </summary>
        [SerializeField] private GameObject CollidersCollection;

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
            for (var mult = 0; mult <= 1; ++mult)
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
                if (CheckWalkable(next))
                {
                    yield return next;
                    Debug.DrawLine(node.Position, next.Position, Color.blue, 10000f, false);
                }
            }

            // return result;
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
            //*
            Debug.Log($"Initial len = {Vector3.Distance(start.Position, target.Position)}");
            var result = new List<PathNode>();

            var nodes = new SlowPriorityQueue<PathNode>();
            nodes.Enqueue(Vector3.Distance(start.Position, target.Position), start);

            PathNode res = null;

            var i = 0;
            var minLen = float.MaxValue;
            PathNode minLenNode = null;
            while (nodes.Count > 0 && i < 10000)
            {
                i++;
                var (heur0, current) = nodes.Dequeue();
                if (Vector3.Distance(current.Position, target.Position) < 5.0)
                {
                    res = current;
                    break;
                }

                var neighbours = GetNeighbours(current, movementProperties);
                foreach (var neighbor in neighbours)
                {
                    var newDist = Vector3.Distance(neighbor.Position, target.Position);
                    nodes.Enqueue(newDist, neighbor);
                    if (newDist < minLen)
                    {
                        minLen = newDist;
                        minLenNode = neighbor;
                    }
                    //grid[current.x, current.y].Distance + neighborDist;
                    //PathNode.Dist(grid[node.x, node.y], grid[current.x, current.y]);

                    // if (grid[neighbor.x, neighbor.y].Distance > newDist)
                    // {
                    //     grid[neighbor.x, neighbor.y].ParentNode = grid[current.x, current.y];
                    //     grid[neighbor.x, neighbor.y].Distance = newDist;
                    //     var heur = newDist + PathNode.Dist(grid[neighbor.x, neighbor.y],
                    //         grid[finishNode.x, finishNode.y]);
                    //     nodes.Enqueue(heur, neighbor);
                    // }
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
            FindPath(start, finish, movementProperties, updater);
            return true;
        }

        //// Start is called before the first frame update
        void Start()
        {
            //  Инициализируем картографа, ну и всё вроде бы
            сartographer = new Cartographer(CollidersCollection);
            obstaclesLayerMask = 1 << LayerMask.NameToLayer("Obstacles");
            var rend = GetComponent<Renderer>();
            if (rend != null)
                rayRadius = rend.bounds.size.y / 2.5f;
        }

        //// Update is called once per frame
        //void Update()
        //{

        //}
    }
}