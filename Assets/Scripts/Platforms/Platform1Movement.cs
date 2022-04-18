using System;
using BaseAI;
using System.Collections;
using System.Collections.Generic;
using AI;
using UnityEngine;

public class Platform1Movement : MonoBehaviour
{
    [SerializeField] private bool moving;
    private Vector3 rotationCenter;
    private Vector3 rotationStartPos;
    [SerializeField] private float rotationSpeed = 1.0f;

    /// <summary>
    /// Тело региона - коллайдер
    /// </summary>
    [SerializeField] private GameObject platform;

    /// <summary>
    /// Индекс региона в списке регионов
    /// </summary>
    public int index { get; set; } = -1;

    public IList<IBaseRegion> Neighbors { get; set; } = new List<IBaseRegion>();

    void Start()
    {
        rotationCenter = transform.position;
        rotationStartPos = platform.transform.position;
    }

    void Update()
    {
        if (!moving) return;

        platform.transform.RotateAround(rotationCenter, Vector3.up, Time.deltaTime * rotationSpeed);
    }

    public Vector3 PredictLocation(float deltaTime)
    {
        platform.transform.RotateAround(rotationCenter, Vector3.up, deltaTime * rotationSpeed);
        var position = platform.transform.position;
        platform.transform.RotateAround(rotationCenter, Vector3.up, -deltaTime * rotationSpeed);
        return position;
    }
}