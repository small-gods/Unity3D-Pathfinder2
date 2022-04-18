using System;
using BaseAI;
using System.Collections;
using System.Collections.Generic;
using AI;
using UnityEngine;

public class Platform1Movement : MonoBehaviour
{
    private Vector3 initialPosisition;
    [SerializeField] private bool moving;
    public GameObject rotationCenterObject;
    private Vector3 rotationCenter;
    private Vector3 rotationStartPos;
    [SerializeField] private float rotationSpeed = 1.0f;

    /// <summary>
    /// Тело региона - коллайдер
    /// </summary>
    public SphereCollider body;
    
    public Collider Collider => body;


    /// <summary>
    /// Индекс региона в списке регионов
    /// </summary>
    public int index { get; set; } = -1;

    public IList<IBaseRegion> Neighbors { get; set; } = new List<IBaseRegion>();

    void Start()
    {
        rotationCenter = rotationCenterObject.transform.position;
        rotationStartPos = transform.position;
    }

    void Update()
    {
        if (!moving) return;

        transform.RotateAround(rotationCenter, Vector3.up, Time.deltaTime*rotationSpeed);
    }
}
