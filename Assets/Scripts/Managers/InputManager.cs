using System.Collections;
using System.Collections.Generic;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Reactive.Disposables;

public class InputManager : MonoSingleton<InputManager>
{
    private const float mouseDistanceToDrag = 20f;

    // ------------------------------------------
    public IObservable<Vector2> Drag { get; private set; }
    public IObservable<Vector2> DragDown { get; private set; }
    public IObservable<Vector2> DragUp { get; private set; }
    // ------------------------------------------
    public IObservable<Vector2> Click { get; private set; }
    // ------------------------------------------
    public IObservable<Vector2> Rotate { get; private set; }
    public IObservable<Vector2> RotateDown { get; private set; }
    public IObservable<Vector2> RotateUp { get; private set; }
    // ------------------------------------------
    public IObservable<Vector2> MousePosition { get; private set; }
    public IObservable<Vector2> Movement { get; private set; }
    public IObservable<float> Zoom { get; private set; }
    // ------------------------------------------
    private ReactiveProperty<Vector2> mousePos = new ReactiveProperty<Vector2>();
    private ReactiveProperty<Vector2> dragDown = new ReactiveProperty<Vector2>();
    private Subject<Vector2> dragUp = new Subject<Vector2>();

    private Subject<Vector2> rotateDown = new Subject<Vector2>();
    private Subject<Vector2> rotateUp = new Subject<Vector2>();

    private Subject<Vector2> movement = new Subject<Vector2>();
    private Subject<float> zoom = new Subject<float>();
    // ------------------------------------------


    public void OnEnable()
    {
        // ------------------------------------------
        MousePosition = mousePos.Publish().RefCount();
        Movement = movement.Publish().RefCount();
        Zoom = zoom.Where(IsMouseNotOverUI).Publish().RefCount();

        // ------------------------------------------
        // Mouse drag logic
        DragDown = dragDown
                        .Where(IsMouseNotOverUI)
                        .SkipWhile(x => Input.GetMouseButton(1))
                        .SelectMany(x => mousePos.TakeUntil(dragUp))
                        .Where(IsOutsideClickRange)
                        .Take(1)
                        .Repeat()
                        .Publish()
                        .RefCount();

        Drag = DragDown
                    .SelectMany(x => mousePos.TakeUntil(dragUp))
                    .Publish()
                    .RefCount();

        DragUp = dragUp.Publish().RefCount();

        // ------------------------------------------

        Click = dragUp
                    .Where(IsMouseNotOverUI)
                    .TakeUntil(DragDown)
                    .Where(IsInsideClickRange) // Make sure we haven't been dragging our mouse beforehand
                    .Repeat()
                    .Publish()
                    .RefCount();

        // ------------------------------------------
        RotateDown = rotateDown
                    .Where(IsMouseNotOverUI)
                    .SkipWhile(x => Input.GetMouseButton(0))
                    .Publish()
                    .RefCount();

        Rotate = RotateDown
                    .SelectMany(x => mousePos.TakeUntil(rotateUp))
                    .Publish()
                    .RefCount();

        RotateUp = rotateUp.Publish().RefCount();
    }

    // Update is called once per frame
    public void Update()
    {
        if (Input.GetKey(KeyCode.UpArrow)) movement.OnNext(Vector2.up);
        if (Input.GetKey(KeyCode.DownArrow)) movement.OnNext(Vector2.down);
        if (Input.GetKey(KeyCode.LeftArrow)) movement.OnNext(Vector2.left);
        if (Input.GetKey(KeyCode.RightArrow)) movement.OnNext(Vector2.right);

        var mouseRaw = Input.mousePosition;
        if (!mouseRaw.Equals(mousePos.Value)) mousePos.Value = mouseRaw;
        if (Input.mouseScrollDelta.y != 0) zoom.OnNext(Input.mouseScrollDelta.y);

        if (Input.GetMouseButtonDown(0)) dragDown.Value = mousePos.Value;
        if (Input.GetMouseButtonUp(0)) dragUp.OnNext(mousePos.Value);

        if (Input.GetMouseButtonDown(1)) rotateDown.OnNext(mousePos.Value);
        if (Input.GetMouseButtonUp(1)) rotateUp.OnNext(mousePos.Value);
    }

    public Vector2 GetMousePos() => mousePos.Value;

    private bool IsMouseNotOverUI(float _) => !EventSystem.current.IsPointerOverGameObject();
    private bool IsMouseNotOverUI(Vector2 _) => !EventSystem.current.IsPointerOverGameObject();
    private bool IsOutsideClickRange(Vector2 pos) => !IsInsideClickRange(pos);
    private bool IsInsideClickRange(Vector2 pos) => Vector3.Distance(pos, dragDown.Value) <= mouseDistanceToDrag;
}