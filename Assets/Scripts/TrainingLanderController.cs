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
        if (position.y > 1200f ) {

            landingSiteRenderer.material = loseMaterial;

            Debug.Log("Escaped");
            agentController.EndEpisode(-1f);
        }
        // Out of bounds reset
        if (position.y == -1f) {

            landingSiteRenderer.material = loseMaterial;

            Debug.Log("Escaped");
            agentController.EndEpisode(0f);
        }


        //REWARDS
        //vertical velocity tracking reward
        float referenceVelocity = ReferenceVelocity(position.y);
        if (velocity.y > -0.2f)
            agentController.AddReward(-10f);
        else
            agentController.AddReward(VerticalVelocityReward(velocity.y, referenceVelocity));

        //horizontal deviation tracking reward
        agentController.AddReward(HorizontalDeviationReward(position.x, targetX,velocity.x));
        agentController.AddReward(HorizontalDeviationReward(position.z, targetY, velocity.z));
        

        //attitude tracking reward
        if (position.y > 2f) {
            agentController.AddReward(AttitudeReward(Mathf.Abs(rotation.x)));
            agentController.AddReward(AttitudeReward(Mathf.Abs(rotation.z)));
        }
        else {
            agentController.AddReward(Mathf.Min(0f, AttitudeReward(Mathf.Abs(rotation.x))));
            agentController.AddReward(Mathf.Min(0f, AttitudeReward(Mathf.Abs(rotation.z))));
        }
        

        //target tracking reward
        //agentController.AddReward(TargetReward(position));

        // Safe landing check
        if (landerRigidbody.IsSleeping() && position.y < 1f) {
            if (Mathf.Abs(rotation.x) < 20f && Mathf.Abs(rotation.z) < 20f) {
                // target tracking
                
                if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(targetX,targetY)) > 20f) {
                    landingSiteRenderer.material = loseMaterial;
                    agentController.EndEpisode(0f);
                    Debug.Log("Too far from spot");
                }
                else {
                    landingSiteRenderer.material = winMaterial;
                    agentController.EndEpisode(1000f);
                    Debug.Log("Success");
                }
                /*
                landingSiteRenderer.material = winMaterial;
                agentController.EndEpisode(100f);
                Debug.Log("Success");
                */
                
            }
            else {
                landingSiteRenderer.material = loseMaterial;

                agentController.EndEpisode(0f);
                Debug.Log("Tipped over");
            }
        }

        /*
        Debug.Log("Position X: " + position.x + " Y : " + position.y + " Z : " + position.z +
            "\nVelocity X: " + velocity.x + " Y : " + velocity.y + " Z : " + velocity.z +
            "\nRotation X: " + rotation.x + " Y : " + rotation.y + " Z : " + rotation.z);
        */
        /*
        Debug.Log("Position X: " + position.x + " Y : " + position.y + " Z : " + position.z +
            "\nVelocity X: " + velocity.x + " Y : " + velocity.y + " Z : " + velocity.z +
            "\nTarget X: " + targetX + " Z : " + targetY +
            "\ndeviation X: " + (position.x - targetX) + " Z : " + (position.z - targetY));
        */
        //Actuator calls
        if (position.y > 0.5f) {
            MainThrusterControl();
            RCSThrusterControl();
        }
        
    }

    private float AttitudeReward(float tilt) {
        float result = -2f * Mathf.Abs((float)System.Math.Tanh(0.1f * tilt)) + 1f;
        return result;
    }
    private float ReferenceVelocity(float altitude) {
        float result;
        /*
        if (altitude > 100f)
            result = -15 * (float)System.Math.Tanh(0.002f * altitude) - 3;
        else
            result = -20 * Mathf.Exp(0.01f * altitude - 2f) + 2f;
        */
        result = 0.3f - 0.5f * Mathf.Pow(altitude + 1f, 0.5f);
        return result;
    }

    private float VerticalVelocityReward(float velocity, float referenceVelocity) {
        float deviation = referenceVelocity - velocity;
        // float result = -2f * Mathf.Abs((float)System.Math.Tanh(0.1f * deviation)) + 1f;
        float result = 1f - Mathf.Abs(deviation);
        if (deviation > 1f)
            result = 0.1f * result;
        return result;
    }

    //Target tracking reward function
    private float HorizontalDeviationReward(float position, float target, float velocity) {
        float deviation = position - target;
        /*
        float result = 1 - 0.2f * Mathf.Abs(deviation);
        if (deviation > 5f)
            result = 0.04f * result;
        */
        float targetVelocity;
        if (deviation >= 0f) {
            targetVelocity = -0.45f * Mathf.Pow(deviation, 0.45f);
        }
        else {
            targetVelocity = 0.45f * Mathf.Pow(-deviation, 0.45f);
        }
        
        
        float velocityDeviation = velocity - targetVelocity;
        float result = 1f - Mathf.Abs(velocityDeviation);
        if (velocityDeviation > 1f)
            result = 0.2f * result;
        return result;
    }


    private void DrawEngineRays(Vector3 worldForce, Vector3 worldPointApplication, float scale) {
        Debug.DrawRay(worldPointApplication, worldForce.normalized * -1 * scale, Color.red);
    }

    public void ResetPosition() {
        float centerPosition = 500f;
        float randomPosition = 600f;
        float randomAngle = 5f;
        float velocity = 0f;
        float randomVerticalVelocity = 0f;
        float randomPositionTarget = 30f;

        randomPosition = Random.Range(randomPosition, randomPosition + 100f);
        randomVerticalVelocity = 0.3f - 0.5f * Mathf.Pow(randomPosition + 1f, 0.5f);


        transform.localPosition = new Vector3(centerPosition, randomPosition, centerPosition);
        transform.localEulerAngles = new Vector3(Random.Range(-randomAngle, randomAngle), 0f, Random.Range(-randomAngle, randomAngle));
        landerRigidbody.velocity = new Vector3(Random.Range(-velocity, velocity), Random.Range(randomVerticalVelocity, randomVerticalVelocity + 2f), Random.Range(-velocity, velocity));
        landerRigidbody.angularVelocity = new Vector3(0, 0, 0);
        targetX = transform.localPosition.x + Random.Range(-randomPositionTarget, randomPositionTarget);
        targetY = transform.localPosition.z + Random.Range(-randomPositionTarget, randomPositionTarget);
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

            agentController.AddReward(-0.03f);
        }
    }

    private void RCSThrusterControl() {
        if (Random.Range(0f, 1f) < 0.2f)
            return;

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

        agentController.AddReward(-0.003f);
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
