using System;

namespace AI
{
    /// <summary>
    /// Параметры движения агента - как может поворачивать, какие шаги делать
    /// </summary>
    [Serializable]
    public class MovementProperties
    {
        /// <summary>
        /// Максимальная скорость движения агента
        /// </summary>
        public float maxSpeed = 1.0f;
        /// <summary>
        /// Шаг поворота агента в градусах
        /// </summary>
        public float rotationAngle = 30.0f;
        /// <summary>
        /// Количество дискретных углов поворота в одну сторону. 0 - только движение вперёд, 1 - влево/прямо/вправо, и т.д.
        /// </summary>
        public int angleSteps = 1;
        /// <summary>
        /// Длина прыжка (фиксированная)
        /// </summary>
        public float jumpLength;
        /// <summary>
        /// Сила прыжка
        /// </summary>
        public float jumpForce;
        /// <summary>
        /// эпсилон-окрестность точки, в пределах которой точка считается достигнутой
        /// </summary>
        public float epsilon = 0.5f;
        /// <summary>
        /// Дельта времени (шаг по времени), с которой строится маршрут
        /// </summary>
        public float deltaTime = 0.5f;
        /// <summary>
        /// Шаг по пространству, с которым происходит дискретизация области (для отсечения посещённых точек)
        /// </summary>
        public float deltaDist = 1f;
    }
}
