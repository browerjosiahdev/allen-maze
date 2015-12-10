using UnityEngine;
using System.Collections;
using Tango;
using System;
using UnityEngine.UI;

public class PoseController : MonoBehaviour, ITangoPose {
    public float speed;

    private TangoApplication m_tangoApplication;
    private Vector3 m_tangoPosition;
    private Quaternion m_tangoRotation;
    private Toggle m_toggleRotation;
    private Toggle m_toggleMovement;
    private Quaternion m_lookRotationDiff;
    private bool m_lookRotationTracked;

    private Vector4 m_prevMovement = Vector4.zero;
    private float m_movementScale = 5.0f;
    private int m_movementTracker = 1;

    /// <summary>
    /// Initialize the class.
    /// </summary>
	void Start()
    {
        // Prevent the tablet from falling asleep.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        m_tangoRotation = Quaternion.identity;
        m_tangoPosition = Vector3.zero;
        m_tangoApplication = FindObjectOfType<TangoApplication>();

        if (m_tangoApplication != null)
        {
            m_tangoApplication.RegisterPermissionsCallback(PermissionsCallback);
            m_tangoApplication.RequestNecessaryPermissionsAndConnect();
            m_tangoApplication.Register(this);
        }
        else
        {
            Debug.Log("No Tango Manager found in scene.");
        }

        // Get a handle to the rotation and movement toggle elements, and default
        // them to be unchecked.
        if (GameObject.Find("Rotation Toggle") != null)
        {
            m_toggleRotation = GameObject.Find("Rotation Toggle").GetComponent<Toggle>();
            m_toggleRotation.isOn = false;
        }
        if (GameObject.Find("Movement Toggle") != null)
        {
            m_toggleMovement = GameObject.Find("Movement Toggle").GetComponent<Toggle>();
            m_toggleMovement.isOn = false;
        }

        // Track the look rotation difference.
        m_lookRotationDiff = new Quaternion();
        m_lookRotationDiff.x = 0.0f;
        m_lookRotationDiff.y = 0.0f;
        m_lookRotationDiff.z = 0.0f;
        m_lookRotationDiff.w = 0.0f;
    }

    /// <summary>
    /// Create a callback to receive permissions to use the motion tracking camera for Project Tango.
    /// </summary>
    /// <param name="success">Permission provided.</param>
    private void PermissionsCallback(bool success)
    {
        if (success)
        {
            m_tangoApplication.InitApplication();
            m_tangoApplication.InitProviders(string.Empty);
            m_tangoApplication.ConnectToService();
        }
        else
        {
            AndroidHelper.ShowAndroidToastMessage("Motion Tracking Permissions Needed", true);
        }
    }

    /// <summary>
    /// Pose callback from Project Tango.
    /// </summary>
    /// <param name="pose">Tango pose data.</param>
    public void OnTangoPoseAvailable(Tango.TangoPoseData pose)
    {
        if (pose == null)
        {
            Debug.Log("TangoPostData is null.");
            return;
        }

        if (pose.framePair.baseFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_START_OF_SERVICE &&
            pose.framePair.targetFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_DEVICE)
        {
            if (pose.status_code == TangoEnums.TangoPoseStatusType.TANGO_POSE_VALID)
            {
                m_tangoPosition = new Vector3((float)pose.translation[0],
                                              (float)pose.translation[1],
                                              (float)pose.translation[2]);

                m_tangoRotation = new Quaternion((float)pose.orientation[0],
                                                 (float)pose.orientation[1],
                                                 (float)pose.orientation[2],
                                                 (float)pose.orientation[3]);
            }
            else
            {
                m_tangoPosition = Vector3.zero;
                m_tangoRotation = Quaternion.identity;
            }
        }
    }

    /// <summary>
    /// Transform the Tango pose which is in Start of Services to Device from to Unity coordinate system.
    /// </summary>
    /// <param name="translation">Translation.</param>
    /// <param name="rotation">Rotation.</param>
    /// <param name="scale">Scale.</param>
    /// <returns>The Tango Pose in Unity coordinate system.</returns>
    Matrix4x4 TransformTangoPoseToUnityCoordinateSystem(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        Matrix4x4 uwTss;
        Matrix4x4 dTuc;

        uwTss = new Matrix4x4();
        uwTss.SetColumn(0, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
        uwTss.SetColumn(1, new Vector4(0.0f, 0.0f, 1.0f, 0.0f));
        uwTss.SetColumn(2, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
        uwTss.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

        dTuc = new Matrix4x4();
        dTuc.SetColumn(0, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
        dTuc.SetColumn(1, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
        dTuc.SetColumn(2, new Vector4(0.0f, 0.0f, -1.0f, 0.0f));
        dTuc.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

        Matrix4x4 ssTd = Matrix4x4.TRS(translation, rotation, scale);

        return uwTss * ssTd * dTuc;
    }

    /// <summary>
    /// Called everytime the frame updates.
    /// </summary>
    void Update()
    {
        Debug.Log("m_lookRotationDiff == " + m_lookRotationDiff);

        Matrix4x4 uwTuc = TransformTangoPoseToUnityCoordinateSystem(m_tangoPosition, m_tangoRotation, Vector3.one);
        Vector4 movement = uwTuc.GetColumn(3);
        Rigidbody body = GetComponent<Rigidbody>();

        // If movement is valid...
        if (m_toggleMovement == null || !m_toggleMovement.isOn)
        {
            // Only track the movement every 5 updates, otherwise the movement is
            // too minute for the velocity.
            if (m_movementTracker >= 5)
            {
                // Calculate the movement.
                Vector3 movementDiff = new Vector3(movement.x, 0.0f, movement.z);

                movementDiff.x = (movementDiff.x - m_prevMovement.x);
                movementDiff.z = (movementDiff.z - m_prevMovement.z);

                // Apply the movement scale and speed.
                movementDiff *= (m_movementScale + speed);

                // Apply the velocity to the camera body.
                body.velocity = movementDiff;

                // Track the movement, and reset the counter.
                m_prevMovement = movement;

                m_movementTracker = 0;
            }

            m_movementTracker++;
        }
        else
        {
            // If the movement is turned off, continue tracking the movement
            // so the user doesn't "jump" to a new location when they turn
            // movement back on.
            m_prevMovement = movement;

            // Zero out the velocity in case the user paused the movement while
            // they were walking.
            body.velocity = Vector3.zero;
        }

        Quaternion lookRotation = Quaternion.LookRotation(uwTuc.GetColumn(2), uwTuc.GetColumn(1));

        //// Calculate the difference.
        //lookRotation.x -= m_lookRotationDiff.x;
        //lookRotation.y -= m_lookRotationDiff.y;
        //lookRotation.z -= m_lookRotationDiff.z;
        //lookRotation.w -= m_lookRotationDiff.w;

        // If rotation is valid...
        if (m_toggleRotation == null || !m_toggleRotation.isOn)
        {
            // Extract the local rotation based on Tango movement.
            transform.rotation = lookRotation;

            m_lookRotationTracked = false;
        }
        else if (!m_lookRotationTracked)
        {
            m_lookRotationDiff.x = lookRotation.x;
            m_lookRotationDiff.y = lookRotation.y;
            m_lookRotationDiff.z = lookRotation.z;
            m_lookRotationDiff.w = lookRotation.w;

            m_lookRotationTracked = true;
        }

        //transform.rotation = lookRotation;

        /**
         * Keyboard Input
        **/
        //transform.rotation = lookRotation;

        //float inputHorz = Input.GetAxis("Horizontal");
        //float inputVert = Input.GetAxis("Vertical");

        //body.velocity = new Vector3(inputHorz, 0.0f, inputVert) * 10f;
    }

    void OnCollisionEnter( Collision collision )
    {
        Debug.Log("Collision!");

        Handheld.Vibrate();
    }
}
