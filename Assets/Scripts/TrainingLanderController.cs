using System.Collections;
using System.Collections.Generic;
using System.Transactions;
using Unity.Burst.CompilerServices;
using UnityEngine;
using UnityEngine.iOS;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class TrainingLanderController : MonoBehaviour {

    private Rigidbody landerRigidbody;
    private TrainingAgentController agentController;

    [SerializeField] private Material winMaterial;
    [SerializeField] private Material loseMaterial;
    [SerializeField] private MeshRenderer landingSiteRenderer;

    private bool thrusterOn;

    private bool rcsPitchPosOn;
    private bool rcsPitchNegOn;
    private bool rcsYawPosOn;
    private bool rcsYawNegOn;

    [SerializeField] private float targetX;
    [SerializeField] private float targetY;


    // Start is called before the first frame update
    void Start() {
        landerRigidbody = GetComponent<Rigidbody>();
        agentController = GetComponent<TrainingAgentController>();

        thrusterOn = false;

        rcsPitchPosOn = false;
        rcsPitchNegOn = false;
        rcsYawPosOn = false;
        rcsYawNegOn = false;
        targetX = 100f;
        targetY = 100f;
    }

    // Update is called once per frame
    void FixedUpdate() {
        Vector3 position = GetPosition();
        Vector3 velocity = GetVelocity();
        Vector3 rotation = GetRotation();
        // Vector3 angularVelocity = GetAngularVelocity();

        // Out of bounds reset, large negative reward
        if (transform.position.y > 250f || transform.position.y < -1f) {

            landingSiteRenderer.material = loseMaterial;

            Debug.Log("Escaped");
            agentController.EndEpisode(-1f);
        }

        //REWARDS
        //velocity tracking reward
        float referenceVelocity = ReferenceVelocity(position.y);
        agentController.AddReward(VerticalVelocityReward(velocity.y, referenceVelocity));
        

        //attitude tracking reward
        agentController.AddReward(AttitudeReward(Mathf.Abs(rotation.x)));
        agentController.AddReward(AttitudeReward(Mathf.Abs(rotation.z)));

        //target tracking reward
        //agentController.AddReward(TargetReward(position));

        // Safe landing check
        if (landerRigidbody.IsSleeping()) {
            if (Mathf.Abs(rotation.x) < 20f && Mathf.Abs(rotation.z) < 20f) {
                /* add in target tracking later
                if (Vector3.Distance(GetPosition(), new Vector3(targetX, 0f, targetY)) > 5f) {
                    agentController.EndEpisode(0f);
                    Debug.Log("Too far from spot");
                }
                else {
                    agentController.EndEpisode(100f);
                    Debug.Log("Success");
                }
                */
                landingSiteRenderer.material = winMaterial;

                agentController.EndEpisode(100f);
                Debug.Log("Success");
            }
            else {
                landingSiteRenderer.material = loseMaterial;

                agentController.EndEpisode(0f);
                Debug.Log("Tipped over");
            }
        }

        Debug.Log("Position X: " + position.x + " Y : " + position.y + " Z : " + position.z +
            "\nVelocity X: " + velocity.x + " Y : " + velocity.y + " Z : " + velocity.z +
            "\nRotation X: " + rotation.x + " Y : " + rotation.y + " Z : " + rotation.z);

        //Actuator calls
        if (position.y > 1f) {
            MainThrusterControl();
            RCSThrusterControl();
        }
    }

    private float AttitudeReward(float tilt) {
        float result = -2f * Mathf.Abs((float)System.Math.Tanh(0.1f * tilt)) + 1f;
        return result;
    }
    private float ReferenceVelocity(float altitude) {
        return -20 * Mathf.Exp(0.01f * altitude - 0.3f);
    }

    private float VerticalVelocityReward(float velocity, float referenceVelocity) {
        float deviation = referenceVelocity - velocity;
        float result = -2f * Mathf.Abs((float)System.Math.Tanh(0.1f * deviation)) + 1f;
        return result;
    }

    /* Target tracking reward function
    private float TargetReward(Vector3 position) {
        float distance = Vector3.Distance(position, new Vector3(centroidData[2], position.y, centroidData[3]));
        float result;
        if (distance > 800)
            result = -1f;
        else if (distance > 400f)
            result = -0.5f;
        else if (distance > 100f)
            result = -0.2f;
        else if (distance > 50f)
            result = 0.4f;
        else if (distance < 25f)
            result = 0.6f;
        else if (distance < 10f)
            result = 0.7f;
        else if (distance < 5f)
            result = 0.9f;
        else 
            result = 1f;
        return result;
    }
    */

    private void DrawEngineRays(Vector3 worldForce, Vector3 worldPointApplication, float scale) {
        Debug.DrawRay(worldPointApplication, worldForce.normalized * -1 * scale, Color.red);
    }

    public void ResetPosition() {
        float velocity = 0f;
        float randomVerticalVelocity = 10f;
        float randomPosition = 20f;
        float centerPosition = 50f;

        float randomAngle = 5f;
        transform.localPosition = new Vector3(centerPosition + Random.Range(-randomPosition, randomPosition), Random.Range(200f, 220f), centerPosition + Random.Range(-randomPosition, randomPosition));
        transform.localEulerAngles = new Vector3(Random.Range(-randomAngle, randomAngle), 0f, Random.Range(-randomAngle, randomAngle));
        landerRigidbody.velocity = new Vector3(Random.Range(-velocity, velocity), Random.Range(-randomVerticalVelocity, 0f), Random.Range(-velocity, velocity));
        landerRigidbody.angularVelocity = new Vector3(0, 0, 0);
    }

    private float GetAltitude() {
        if (Physics.Raycast(landerRigidbody.position + new Vector3(0f, 0.5f, 0f), Vector3.down, out RaycastHit hit, Mathf.Infinity)) {
            return hit.distance;
        }
        else {
            return -1;
        }
    }
    public Vector3 GetPosition() {
        Vector3 position = transform.localPosition;
        position.y = GetAltitude();
        return position;
    }

    public Vector2 GetTarget() {
        Vector2 target = new Vector2(targetX, targetY);
        return target;
    }

    public Vector3 GetVelocity() {
        return landerRigidbody.velocity;
    }

    public Vector3 GetRotation() {
        Vector3 rotation = new Vector3(0,0,0);

        if (landerRigidbody.rotation.eulerAngles.x <= 180f) {
            rotation.x = landerRigidbody.rotation.eulerAngles.x;
        }
        else {
            rotation.x = landerRigidbody.rotation.eulerAngles.x - 360f;
        }

        if (landerRigidbody.rotation.eulerAngles.y <= 180f) {
            rotation.y = landerRigidbody.rotation.eulerAngles.y;
        }
        else {
            rotation.y = landerRigidbody.rotation.eulerAngles.y - 360f;
        }

        if (landerRigidbody.rotation.eulerAngles.z <= 180f) {
            rotation.z = landerRigidbody.rotation.eulerAngles.z;
        }
        else {
            rotation.z = landerRigidbody.rotation.eulerAngles.z - 360f;
        }

        return rotation;
    }

    public Vector3 GetAngularVelocity() {
        return landerRigidbody.angularVelocity;
    }

    public void SetThrusterState(int state) {
        if(state == 0) {
            thrusterOn = false;
        }
        else if(state == 1) {
            thrusterOn = true;
        }
    }

    public void SetSimpleRCSThrusterState(int pitch, int yaw) {
        if (pitch == 1) {
            rcsPitchPosOn = true;
            rcsPitchNegOn = false;
        }
        else if (pitch == 2) {
            rcsPitchPosOn = false;
            rcsPitchNegOn = true;
        }
        else{
            rcsPitchPosOn = false;
            rcsPitchNegOn = false;
        }

        if (yaw == 1) {
            rcsYawPosOn = true;
            rcsYawNegOn = false;
        }
        else if (yaw == 2) {
            rcsYawPosOn = false;
            rcsYawNegOn = true;
        }
        else {
            rcsYawPosOn = false;
            rcsYawNegOn = false;
        }
    }

    private void MainThrusterControl() {
        if (thrusterOn) {
            //Debug.Log("Apply Thrust");
            //landerRigidbody.AddRelativeForce(45000 * Vector3.up);
            Vector3 thrust = new Vector3(0, 45000, 0);
            Vector3 worldForce = transform.TransformVector(thrust);
            Vector3 worldPoint = transform.TransformPoint(new Vector3(0, 1, 0));

            landerRigidbody.AddForceAtPosition(worldForce, worldPoint, ForceMode.Force);
            DrawEngineRays(worldForce, worldPoint, 10);
        }
    }

    private void RCSThrusterControl() {
        if (rcsPitchPosOn) {
            Vector3 thrust = new Vector3(0, 0, 450f);
            Vector3 worldForce = transform.TransformVector(thrust);
            Vector3 worldPoint1 = transform.TransformPoint(new Vector3(2f, 5.5f, -2f));
            Vector3 worldPoint2 = transform.TransformPoint(new Vector3(-2f, 5.5f, -2f));

            landerRigidbody.AddForceAtPosition(worldForce, worldPoint1, ForceMode.Force);
            landerRigidbody.AddForceAtPosition(worldForce, worldPoint2, ForceMode.Force);
            DrawEngineRays(worldForce, worldPoint1, 5);
            DrawEngineRays(worldForce, worldPoint2, 5);
        }
        if (rcsPitchNegOn) {
            Vector3 thrust = new Vector3(0, 0, -450f);
            Vector3 worldForce = transform.TransformVector(thrust);
            Vector3 worldPoint1 = transform.TransformPoint(new Vector3(2f, 5.5f, 2f));
            Vector3 worldPoint2 = transform.TransformPoint(new Vector3(-2f, 5.5f, 2f));

            landerRigidbody.AddForceAtPosition(worldForce, worldPoint1, ForceMode.Force);
            landerRigidbody.AddForceAtPosition(worldForce, worldPoint2, ForceMode.Force);
            DrawEngineRays(worldForce, worldPoint1, 5);
            DrawEngineRays(worldForce, worldPoint2, 5);
        }

        if (rcsYawPosOn) {
            Vector3 thrust = new Vector3(450f, 0, 0);
            Vector3 worldForce = transform.TransformVector(thrust);
            Vector3 worldPoint1 = transform.TransformPoint(new Vector3(-2f, 5.5f, 2f));
            Vector3 worldPoint2 = transform.TransformPoint(new Vector3(-2f, 5.5f, -2f));

            landerRigidbody.AddForceAtPosition(worldForce, worldPoint1, ForceMode.Force);
            landerRigidbody.AddForceAtPosition(worldForce, worldPoint2, ForceMode.Force);
            DrawEngineRays(worldForce, worldPoint1, 5);
            DrawEngineRays(worldForce, worldPoint2, 5);
        }
        if (rcsYawNegOn) {
            Vector3 thrust = new Vector3(-450f, 0, 0);
            Vector3 worldForce = transform.TransformVector(thrust);
            Vector3 worldPoint1 = transform.TransformPoint(new Vector3(2f, 5.5f, 2f));
            Vector3 worldPoint2 = transform.TransformPoint(new Vector3(2f, 5.5f, -2f));

            landerRigidbody.AddForceAtPosition(worldForce, worldPoint1, ForceMode.Force);
            landerRigidbody.AddForceAtPosition(worldForce, worldPoint2, ForceMode.Force);
            DrawEngineRays(worldForce, worldPoint1, 5);
            DrawEngineRays(worldForce, worldPoint2, 5);
        }
    }

    void OnCollisionEnter(Collision collision) {
        if (collision.relativeVelocity.y > 2f) {
            landingSiteRenderer.material = loseMaterial;
            Debug.Log("Crashed");

            agentController.EndEpisode(0f);

        }
        if (Mathf.Abs(GetRotation().x) > 40f && Mathf.Abs(GetRotation().z) > 40f) {
            landingSiteRenderer.material = loseMaterial;
            Debug.Log("Excess tilt");

            agentController.EndEpisode(0f);

        }
    }
}
