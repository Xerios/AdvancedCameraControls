using System.Collections;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System;
using UnityEngine;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;

public class CameraController : MonoBehaviour
{
    public ReactiveProperty<bool> Use2DMode = new ReactiveProperty<bool>(false);

    [Header("Zoom Level")]
    [Range(0f, 1f)]
    public float zoom = 1f;

    [Header("Camera Settings")]
    public CameraSettings settings;

    //----------------------------------------------------------------------
    private new Transform camera;

    private float zoomSmoothed = 1f;

    private Vector3 dragMousePos;
    private Vector3 dragOriginPosition;
    private Vector3 dragStartPosition;
    private Vector2 rotateStartPosition;

    private float rotationPitch;
    private Vector2 rotatePitchClamp = new Vector2(-85f, 0f);

    private Vector3 destinationPosition;

    float zoomTimer = 0f, positionTimer = 0f;
    float startZoom = 0f;
    Vector3 startPosition;

    //----------------------------------------------------------------------

    private CompositeDisposable disposables;

    // Use this for initialization
    private void OnEnable()
    {
        var raycastMgr = RaycastManager.Instance;
        var inputMgr = InputManager.Instance;

        camera = transform.GetChild(0);

        destinationPosition = transform.localPosition;

        disposables = new CompositeDisposable();


        // Directional movement relative to the camera ( Up/Down/Left/Right )
        inputMgr.Movement
                    .Subscribe(pos =>
                    {
                        // Transform direction to camera's rotation
                        var move = camera.transform.rotation * new Vector3(pos.x, 0, pos.y);
                        move.y = 0;
                        move = move.normalized;

                        // Accelerate based on zoom out
                        move *= (1 + zoom);
                        // Multiply by the movement speed
                        move *= settings.directionalMovementSpeed;

                        destinationPosition = ConstrainToBounds(this.transform.localPosition + move);
                    })
                    .AddTo(disposables);

        // Mouse drag script ( save initial drag position for later use )
        inputMgr.DragDown
                    .Subscribe(pos =>
                    {
                        dragMousePos = pos;
                        dragOriginPosition = this.transform.localPosition;

                        // Mouse specific code
                        raycastMgr.RaycastGround(dragMousePos, out dragStartPosition);

                        // Debug stuff
                        // Debug.DrawRay(dragStartPosition, Vector3.up * 5, Color.red, 1f);
                    })
                    .AddTo(disposables);


        // Mouse drag script
        inputMgr.Drag.Subscribe(pos =>
                    {
                        Vector3 startDragPosition, currentDragPosition;

                        //--------------------
                        // Get difference from first drag position and new and then set the new camera destination pos
                        if (raycastMgr.RaycastPlane(dragMousePos, dragStartPosition.y, out startDragPosition) && raycastMgr.RaycastPlane(pos, dragStartPosition.y, out currentDragPosition))
                        {
                            positionTimer = 0f;
                            startPosition = this.transform.localPosition;
                            destinationPosition = ConstrainToBounds(dragOriginPosition + (startDragPosition - currentDragPosition));
                        }
                    })
                    .AddTo(disposables);

        // Mouse zoom in to origin
        inputMgr.Zoom
                    .Subscribe(delta =>
                    {
                        zoomTimer = 0f;
                        startZoom = zoomSmoothed;

                        var deltaMod = (settings.zoomCurve.Evaluate(zoom) * delta); // Make sure we zoom less the more we zoom in
                        zoom = Mathf.Clamp01(zoom - deltaMod);

                        // Save original position
                        Vector3 originalPosition = camera.localPosition;

                        // Get current mouse raycast position
                        Vector3 zoomPos;
                        raycastMgr.RaycastGround(inputMgr.GetMousePos(), out zoomPos);

                        // Update zoom
                        UpdateCameraTransform(zoom);

                        // Calculate new mouse position
                        Vector3 zoomMouseNewPosition;
                        if (raycastMgr.RaycastPlane(inputMgr.GetMousePos(), zoomPos.y, out zoomMouseNewPosition))
                        {
                            // Adjust camera position so that we zoom-in to point instead of center
                            var deltaPos = (zoomMouseNewPosition - zoomPos);
                            positionTimer = 0f;
                            startPosition = this.transform.localPosition;
                            destinationPosition = ConstrainToBounds(this.transform.localPosition - deltaPos);
                        }

                        // Set back to original position ( zoom-in and translation is taken care in update with smoothing)
                        camera.localPosition = originalPosition;

                        //Debug.DrawRay(zoomPos, Vector3.up * 5, Color.green, 1f);
                    })
                    .AddTo(disposables);


        // Mouse drag to rotate script ( save initial drag position for later use )
        inputMgr.RotateDown
                    .Subscribe(pos =>
                    {
                        rotateStartPosition = pos;
                        dragOriginPosition = this.transform.localPosition;
                        raycastMgr.RaycastGround(pos, out dragStartPosition);
                    })
                    .AddTo(disposables);

        // Mouse drag to rotate script
        inputMgr.Rotate
                    .Subscribe(pos =>
                    {
                        var deltaYaw = (rotateStartPosition.x - pos.x) * settings.rotateHorizontalSpeed;
                        var deltaPitch = (rotateStartPosition.y - pos.y) * settings.rotateVerticalSpeed;
                        rotateStartPosition = pos;

                        // Rotate horizontally
                        //--------------------
                        transform.RotateAround(dragStartPosition, Vector3.up, deltaYaw);
                        if (!Use2DMode.Value)
                        {
                            Vector3 originalPosition = transform.localPosition;
                            Quaternion originalRotation = transform.localRotation;

                            Vector3 newPos;
                            Quaternion newRot;
                            RotateAround(dragStartPosition, transform.right, deltaPitch, out newPos, out newRot);

                            //pitchValue = clampedRotation.eulerAngles.x;

                            var clampedRotation = ClampRotationAroundXAxis(newRot);

                            Vector3 angles = clampedRotation.eulerAngles;
                            angles.y = originalRotation.eulerAngles.y;
                            angles.z = 0;

                            transform.localPosition = newPos;
                            transform.localEulerAngles = angles;

                            if (!isEqual(clampedRotation.eulerAngles.x, newRot.eulerAngles.x))
                            {
                                //Debug.Log($"OUTSIDE {clampedRotation.eulerAngles.x} = {newRot.eulerAngles.x}");
                                transform.localPosition = ConstrainToBounds(originalPosition);
                                transform.localRotation = originalRotation;
                            }
                            else
                            {
                                float distance = 0f;
                                var ray = new Ray(camera.transform.position, camera.transform.forward);
                                var raycastResult = new Plane(Vector3.up, 0).Raycast(ray, out distance);

                                if (raycastResult && distance <= (settings.minZoom + settings.maxZoom))
                                {
                                    var zoomEvalReverse = (distance - settings.minZoom) / settings.maxZoom;
                                    zoom = Mathf.Clamp01(zoomEvalReverse);
                                    UpdateCameraTransform(zoom);

                                    this.transform.localPosition = ConstrainToBounds(ray.GetPoint(distance));

                                    //Debug.Log("INSIDE");
                                }
                                else
                                {
                                    //Debug.Log($"INSIDE ( could not raycast ) {distance}");
                                    //transform.localRotation = originalRotation;
                                    transform.localPosition = originalPosition;
                                }
                            }
                        }
                        //--------------------
                        // Force new position ( prevents smooth camera movement issues )
                        startPosition = destinationPosition = this.transform.localPosition;

                        //Debug.DrawRay(dragStartPosition, Vector3.up * 5, Color.red, 1f);
                    })
                    .AddTo(disposables);
    }

    private void OnDisable()
    {
        disposables.Dispose();
    }

    //------------------------------------------------
    // Updates camera position instantly ( used in zoom and normal update )
    private void UpdateCameraTransform(float value)
    {
        var zoomEval = settings.minZoom + (value * settings.maxZoom);
        camera.localPosition = new Vector3(0, zoomEval, 0);
    }

    // Update camera position in a smooth silky way
    private void Update()
    {
        var d = Time.deltaTime / settings.tweenTime; // We want to move the timer from 0 to 1, so we divide Time.deltaTime by totalTime
        zoomTimer += d;
        positionTimer += d;
        zoomSmoothed = Mathf.Lerp(startZoom, zoom, settings.tweenCurve.Evaluate(zoomTimer));

        UpdateCameraTransform(zoomSmoothed);

        this.transform.localPosition = Vector3.Lerp(startPosition, destinationPosition, settings.tweenCurve.Evaluate(positionTimer));
    }

    // ------------------------------------------------

    // Helper function
    public Vector3 ConstrainToBounds(Vector3 p)
    {
        var constrainedPos = Vector3.ClampMagnitude(p, settings.RadiusBound);
        constrainedPos.y = 0;
        return constrainedPos;
    }

    private void RotateAround(Vector3 center, Vector3 axis, float angle, out Vector3 pos, out Quaternion rotation)
    {
        Quaternion rot = Quaternion.AngleAxis(angle, axis); // get the desired rotation
        Vector3 dir = this.transform.position - center; // find current direction relative to center
        dir = rot * dir; // rotate the direction
        pos = center + dir; // define new position
        Quaternion myRot = transform.rotation;
        rotation = myRot * Quaternion.Inverse(myRot) * rot * myRot;
    }

    bool isEqual(float a, float b)
    {
        if (a >= b - 0.0001f && a <= b + 0.0001f)
            return true;
        else
            return false;
    }

    Quaternion ClampRotationAroundXAxis(Quaternion q)
    {
        q.x /= q.w;
        q.y /= q.w;
        q.z /= q.w;
        q.w = 1.0f;

        float angleX = 2.0f * Mathf.Rad2Deg * Mathf.Atan(q.x);
        angleX = Mathf.Clamp(angleX, rotatePitchClamp.x, rotatePitchClamp.y);
        q.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * angleX);

        return q;
    }
    // ------------------------------------------------

    // Debug viz
    private void OnDrawGizmosSelected()
    {
        //Gizmos.color = new Color(0f, 0f, 1f, 0.1f);
        //Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(0f, 0f, 1f, 1f);
        //Gizmos.DrawWireCube(bounds.center, bounds.size);

        Gizmos.DrawWireSphere(Vector3.zero, settings.RadiusBound);
    }
}
