using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AI;
using UnityEditor.SearchService;
using UnityEngine;

public class BotMovement : MonoBehaviour
{
    /// <summary>
    /// Ссылка на глобальный планировщик - в целом, он-то нам и занимается построением пути
    /// </summary>
    private PathFinder _globalPathfinder;

    /// <summary>
    /// Запланированный путь как список точек маршрута
    /// </summary>
    public List<PathNode> PlannedPath;

    /// <summary>
    /// Текущий путь как список точек маршрута
    /// </summary>
    public List<PathNode> CurrentPath;

    /// <summary>
    /// Текущая целевая точка - цель нашего движения. Обновления пока что не предусмотрено
    /// </summary>
    private PathNode _currentTarget;

    /// <summary>
    /// Параметры движения бота
    /// </summary>
    [SerializeField] private MovementProperties movementProperties = new MovementProperties();

    /// <summary>
    /// Целевая точка для движения - глобальная цель
    /// </summary>
    [SerializeField] private GameObject finish; //  Конечная цель маршрута как Vector3

    private PathNode FinishPoint; //  Конечная цель маршрута как PathNode - вот оно нафига вообще?

    const int
        MinimumPathNodesLeft =
            10; //  Минимальное число оставшихся точек в маршруте, при котором вызывается перестроение

    /// <summary>
    /// Было ли запрошено обновление пути. Оно в отдельном потоке выполняется, поэтому если пути нет, но 
    /// запрос планировщику подали, то надо просто ждать. В тяжелых случаях можно сделать отметку времени - когда был 
    /// сделан запрос, и по прошествию слишком большого времени выбрасывать исключение.
    /// </summary>
    private bool pathUpdateRequested;

    public float obstacleRange = 5.0f;
    public int steps;
    private float leftLegAngle = 3f; //  Угол левой ноги - только для анимации движения используется

    /// <summary>
    /// Находимся ли в полёте (в состоянии прыжка)
    /// </summary>
    private bool isJumpimg;

    /// <summary>
    /// Время предыдущего обращения к планировщику - не более одного раза в три секунды
    /// </summary>
    private float lastPathfinderRequest;

    /// <summary>
    /// Заглушка - двигается ли бот или нет
    /// </summary>
    [SerializeField] private bool walking = true;

    //  Сила, тянущая "вверх" упавшего бота и заставляющая его вставать
    [SerializeField] float force = 5.0f;

    //  Угол отклонения, при котором начинает действовать "поднимающая" бота сила
    [SerializeField] float max_angle = 20.0f;

    [SerializeField] private GameObject leftLeg;
    [SerializeField] private GameObject rightLeg;
    [SerializeField] private GameObject leftLegJoint;
    [SerializeField] private GameObject rightLegJoint;

    void Start()
    {
        //  Ищем глобальный планировщик на сцене - абсолютно дурацкий подход, но так можно
        //  И вообще, это может не работать!
        _globalPathfinder = (PathFinder) FindObjectOfType(typeof(PathFinder));
        if (_globalPathfinder == null)
        {
            Debug.Log("Не могу найти глобальный планировщик!");
            throw new System.Exception("Can't find global pathfinder!");
        }

        //  Создаём целевую точку из объекта на сцене. В целом это должно задаваться в рамках алгоритма как-то
        var pos = finish.transform.position;
        pos.y = this.transform.position.y;
        FinishPoint = new PathNode(pos, Vector3.zero);
        lastPathfinderRequest = -5.0f;
    }

    /// <summary>
    /// Вызывается каждый кадр
    /// </summary>
    void Update()
    {
        //  Фрагмент кода, отвечающий за вставание
        var verticalAngle = Vector3.Angle(Vector3.up, transform.up);
        if (verticalAngle > max_angle)
        {
            var transform1 = transform;
            GetComponent<Rigidbody>().AddForceAtPosition(
                5 * force * Vector3.up,
                transform1.position + 3.0f * transform1.up,
                ForceMode.Force
            );
        }

        if (!walking) return;

        //  Собственно движение
        if (MoveBot())
            MoveLegs();
    }

    /// <summary>
    /// Движение ног - болтаем туда-сюда
    /// </summary>
    void MoveLegs()
    {
        //  Движение ножек сделать
        if (steps >= 20)
        {
            leftLegAngle = -leftLegAngle;
            steps = -20;
        }

        steps++;

        leftLeg.transform.RotateAround(leftLegJoint.transform.position, transform.right, leftLegAngle);
        rightLeg.transform.RotateAround(rightLegJoint.transform.position, transform.right, -leftLegAngle);
    }

    /// <summary>
    /// Делегат, выполняющийся при построении пути планировщиком
    /// </summary>
    /// <param name="pathNodes"></param>
    private void UpdatePathListDelegate(List<PathNode> pathNodes)
    {
        if (pathUpdateRequested == false)
        {
            //  Пока мы там путь строили, уже и не надо стало - выключили запрос
            return;
        }

        //  Просто перекидываем список, и всё
        PlannedPath = pathNodes;
        pathUpdateRequested = false;
    }

    /// <summary>
    /// Запрос на достроение пути - должен сопровождаться довольно сложными проверками. Если есть целевая точка,
    /// и если ещё не дошли до целевой точки маршрута, и если количество оставшихся точек меньше чем MinimumPathNodesLeft - жуть.
    /// </summary>
    private void RequestPathfinder()
    {
        if (FinishPoint == null || pathUpdateRequested || PlannedPath != null)
            return;
        if (Time.fixedTime - lastPathfinderRequest < 0.5f)
            return;

        //  Тут ещё бы проверить, что финальная точка в нашем текущем списке точек не совпадает с целью, иначе плохо всё будет
        if (AtFinish() || CurrentPathEndAtFinish())
        {
            //  Всё, до цели дошли, сушите вёсла
            FinishPoint = null;
            PlannedPath = null;
            CurrentPath = null;
            pathUpdateRequested = false;
            return;
        }

        //  Тут два варианта - либо запускаем построение пути от хвоста списка, либо от текущей точки
        PathNode startOfRoute = null;
        // if (CurrentPath != null && CurrentPath.Count > 0)
        //     startOfRoute = CurrentPath.Last();
        // else
        //  Из начального положения начнём - вот только со временем беда. Технически надо бы брать момент в будущем, когда 
        //  начнём движение, но мы не знаем когда маршрут построится. Надеемся, что быстро
        startOfRoute = new PathNode(transform.position, transform.forward);

        pathUpdateRequested = true;

        lastPathfinderRequest = Time.fixedTime;
        _globalPathfinder.BuildRoute(startOfRoute, FinishPoint, movementProperties, UpdatePathListDelegate);

        // return true;
    }

    private bool AtFinish()
    {
        var distanceToFinish = Vector3.Distance(transform.position, FinishPoint.Position);
        return distanceToFinish < movementProperties.epsilon;
    }

    private bool CurrentPathEndAtFinish()
    {
        if (CurrentPath == null)
            return false;

        var pathEndDistanceToFinish = Vector3.Distance(
            CurrentPath[CurrentPath.Count - 1].Position,
            FinishPoint.Position
        );
        return pathEndDistanceToFinish < movementProperties.epsilon;
    }

    /// <summary>
    /// Обновление текущей целевой точки - куда вообще двигаться
    /// </summary>
    private bool UpdateCurrentTargetPoint()
    {
        //  Если есть текущая целевая точка
        if (_currentTarget != null)
        {
            var distanceToTarget = _currentTarget.Distance(transform.position);
            //  Если до текущей целевой точки ещё далеко, то выходим
            if (distanceToTarget >= movementProperties.epsilon * 0.5f ||
                _currentTarget.TimeMoment - Time.fixedTime > movementProperties.epsilon * 0.1f) return true;
            //  Иначе удаляем её из маршрута и берём следующую
            CurrentPath.RemoveAt(0);
            if (CurrentPath.Count > 0)
            {
                //  Берём очередную точку и на выход (но точку не извлекаем!)
                _currentTarget = CurrentPath[0];
                return true;
            }
            else
            {
                _currentTarget = null;
                CurrentPath = null;
                //  А вот тут надо будет проверять, есть ли уже построенный маршрут
                RequestPathfinder();
                Debug.Log("Запрошено построение маршрута");
            }
        }
        else if (CurrentPath != null)
        {
            if (CurrentPath.Count > 0)
            {
                _currentTarget = CurrentPath[0];
                return true;
            }
            else
            {
                CurrentPath = null;
            }
        }

        //  Здесь мы только в том случае, если целевой нет, и текущего пути нет - и то, и другое null
        //  Обращение к plannedPath желательно сделать через блокировку - именно этот список задаётся извне планировщиком
        //  Непонятно, насколько lock затратен, можно ещё булевский флажок добавить, его сначала проверять
        //  Но сначала сделаем всё на "авось", без блокировок - там же просто ссылка на список переприсваевается.

        if (PlannedPath != null)
        {
            CurrentPath = PlannedPath;
            PlannedPath = null;
            if (CurrentPath.Count > 0)
                _currentTarget = CurrentPath[0];
        }
        else
            RequestPathfinder();

        return _currentTarget != null;
    }

    /// <summary>
    /// Событие, возникающее когда бот касается какого-либо препятствия, то есть приземляется на землю
    /// </summary>
    /// <param name="collision"></param>
    void OnCollisionEnter(Collision collision)
    {
        //  Столкнулись - значит, приземлились
        //  Возможно, надо разделить - Terrain и препятствия разнести по разным слоям
        if (collision.gameObject.layer == LayerMask.NameToLayer("Obstacles"))
        {
            if (isJumpimg)
            {
                Debug.Log("OnCollisionEnter");
                
                Debug.Log("CurrentPath = null");
                CurrentPath = null;
                PlannedPath = null;
                _currentTarget = null;
                
                var rb = GetComponent<Rigidbody>();
                var platform = collision.collider.gameObject;
                var o = gameObject;
                o.transform.parent = platform.transform;
                //  Сбрасываем скорость перед прыжком
                rb.velocity = Vector3.zero;
                isJumpimg = false;
            }
        }
    }

    /// <summary>
    /// В зависимости от того, находится ли бот в прыжке, или нет, изменяем цвет ножек
    /// </summary>
    /// <returns></returns>
    void AnimateJumping()
    {
        if (isJumpimg)
        {
            var a = leftLeg.GetComponent<MeshRenderer>();
            a.material.color = Color.red;
            a = rightLeg.GetComponent<MeshRenderer>();
            a.material.color = Color.red;
        }
        else
        {
            var a = leftLeg.GetComponent<MeshRenderer>();
            a.material.color = Color.white;
            a = rightLeg.GetComponent<MeshRenderer>();
            a.material.color = Color.white;
        }
    }

    /// <summary>
    /// Пытаемся прыгнуть вперёд и вверх (на месте не умеем прыгать)
    /// </summary>
    /// <returns></returns>
    bool TryToJump()
    {
        if (isJumpimg)
        {
            return false;
        }

        Jump();

        return true;
    }

    void Jump()
    {
        var rb = GetComponent<Rigidbody>();
        //  Сбрасываем скорость перед прыжком
        rb.velocity = Vector3.zero;
        var jump = transform.forward + 2 * transform.up;
        float jumpForce = movementProperties.jumpForce;
        rb.AddForce(jump * jumpForce, ForceMode.Impulse);
        
        Debug.Log("Jump");
        gameObject.transform.parent = null;
        isJumpimg = true;
    }

    /// <summary>
    /// Очередной шаг бота - движение
    /// </summary>
    /// <returns>false, если требуется обновление точки маршрута</returns>
    bool MoveBot()
    {
        //  Выполняем обновление текущей целевой точки
        if (!UpdateCurrentTargetPoint())
            //  Это ситуация когда идти некуда - цели нет
            return false;
        if (_currentTarget == null)
        {
            Debug.Log("Пучкин, за что????");
            return false;
        }

        AnimateJumping();
        if (_currentTarget.JumpNode)
        {
            TryToJump();
        }

        //  Ну у нас тут точно есть целевая точка, вот в неё и пойдём
        //  Определяем угол поворота, и расстояние до целевой
        Vector3 directionToTarget = _currentTarget.Position - transform.position;
        float angle = Vector3.SignedAngle(transform.forward, directionToTarget, Vector3.up);
        //  Теперь угол надо привести к допустимому диапазону
        angle = Mathf.Clamp(angle, -movementProperties.rotationAngle, movementProperties.rotationAngle);

        //  Зная угол, нужно получить направление движения (мы можем сразу не повернуть в сторону цели)
        //  Выполняем вращение вокруг оси Oy

        //  Угол определили, теперь, собственно, определяем расстояние для шага
        float stepLength = directionToTarget.magnitude;
        float actualStep = Mathf.Clamp(stepLength, 0.0f, movementProperties.maxSpeed * Time.deltaTime);
        //  Поворот может быть проблемой, если слишком близко подошли к целевой точке
        //  Надо как-то следить за скоростью, она не может превышать расстояние до целевой точки???
        transform.Rotate(Vector3.up, angle);

        //  Время прибытия - оставшееся время
        var remainedTime = _currentTarget.TimeMoment - Time.fixedTime;
        if (remainedTime < movementProperties.epsilon)
        {
            transform.position = transform.position + actualStep * transform.forward;
        }
        else
        {
            //  Дедлайн ещё не скоро!!! Стоим спим
            if (_currentTarget.Distance(transform.position) < movementProperties.epsilon)
                return true;

            transform.position = transform.position + actualStep * transform.forward / remainedTime;
        }

        return true;
    }
}