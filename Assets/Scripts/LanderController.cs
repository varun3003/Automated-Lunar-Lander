using System.Collections;
using System.Collections.Generic;
using System.Transactions;
using Unity.Burst.CompilerServices;
using UnityEngine;
using UnityEngine.iOS;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class LanderController : MonoBehaviour {

    private Rigidbody landerRigidbody;
    private AgentController agentController;
    private UdpSocket udpSocket;

    public RawImage safetyMapImage; // Reference to RawImage UI element

    private bool thrusterOn;

    /*
    private bool rcs0up;
    private bool rcs0down;
    private bool rcs0pitch;
    private bool rcs0yaw;

    private bool rcs1up;
    private bool rcs1down;
    private bool rcs1pitch;
    private bool rcs1yaw;

    private bool rcs2up;
    private bool rcs2down;
    private bool rcs3pitch;
    private bool rcs3yaw;

    private bool rcs4up;
    private bool rcs4down;
    private bool rcs4pitch;
    private bool rcs4yaw;
    */

    private bool rcsPitchPosOn;
    private bool rcsPitchNegOn;
    private bool rcsYawPosOn;
    private bool rcsYawNegOn;
    private int[] centroidData;


    // Start is called before the first frame update
    void Start() {
        landerRigidbody = GetComponent<Rigidbody>();
        agentController = GetComponent<AgentController>();
        udpSocket = GetComponent<UdpSocket>();

        thrusterOn = false;

        /*
        rcs0up = false;
        rcs0down = false;
        rcs0pitch = false;
        rcs0yaw = false;

        rcs1up = false;
        rcs1down = false;
        rcs1pitch = false;
        rcs1yaw = false;

        rcs2up = false;
        rcs2down = false;
        rcs3pitch = false;
        rcs3yaw = false;

        rcs4up = false;
        rcs4down = false;
        rcs4pitch = false;
        rcs4yaw = false;
        */

        rcsPitchPosOn = false;
        rcsPitchNegOn = false;
        rcsYawPosOn = false;
        rcsYawNegOn = false;
    }

    // Method to update the texture on the UI element (RawImage) using the received safety map data
    private void UpdateSafetyMapTexture(int[] safetyMapData, int cx, int cy) {
        // Assuming safetyMapData represents the color values for the texture
        Texture2D texture = new Texture2D(64, 64); // Assuming 64x64 image size

        // Iterate over each pixel in the image
        for (int y = 0; y < 64; y++) {
            for (int x = 0; x < 64; x++) {
                // Calculate the index of the current pixel in the safetyMapData array
                int index = y * 64 + x;

                // Get the pixel value from safetyMapData (0 for black, 255 for white)
                int pixelValue = safetyMapData[index];

                // Convert the pixel value to a Color (assuming grayscale)
                Color pixelColor = new Color(pixelValue / 255f, pixelValue / 255f, pixelValue / 255f);

                // Set the pixel color in the texture
                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.SetPixel(31, 31, new Color(0f, 0f, 255f));
        texture.SetPixel(30, 31, new Color(0f, 0f, 255f));
        texture.SetPixel(32, 31, new Color(0f, 0f, 255f));
        texture.SetPixel(31, 30, new Color(0f, 0f, 255f));
        texture.SetPixel(31, 32, new Color(0f, 0f, 255f));

        texture.SetPixel(cx, cy, new Color(0f, 255f, 0f));
        texture.SetPixel(cx + 1, cy + 1, new Color(0f, 255f, 0f));
        texture.SetPixel(cx - 1, cy - 1, new Color(0f, 255f, 0f));
        texture.SetPixel(cx - 1, cy + 1, new Color(0f, 255f, 0f));
        texture.SetPixel(cx + 1, cy - 1, new Color(0f, 255f, 0f));

        // Apply the changes to the texture
        texture.Apply();

        // Assign the texture to the RawImage
        safetyMapImage.texture = texture;
    }

    // Update is called once per frame

    void FixedUpdate() {
        Vector3 position = GetPosition();
        Vector3 velocity = GetVelocity();
        Vector3 rotation = GetRotation();
        // Vector3 angularVelocity = GetAngularVelocity();

        // Out of bounds reset, large negative reward
        if (position.y > 1100f) {
            Debug.Log("Escaped");
            agentController.EndEpisode(-1f);
        }

        SendPosition(position.x, position.y, position.z);

        int[] safetyMapData = udpSocket.GetSafetyMapData();
        centroidData = udpSocket.GetCentroidData();

        if (safetyMapData != null) {
            // Update texture
            UpdateSafetyMapTexture(safetyMapData, centroidData[0], centroidData[1]);
        }

        if (centroidData != null) {
            // Print centroid data
            //Debug.Log("Centroid Data: cx = " + centroidData[0] + ", cy = " + centroidData[1] + ", global_cx = " + centroidData[2] + ", global_cy = " + centroidData[3]);
        }

        //REWARDS
        //velocity tracking reward
        
        float referenceVelocity = ReferenceVelocity(position.y);
        agentController.AddReward(VelocityReward(velocity.y, referenceVelocity));
        

        //attitude tracking reward
        agentController.AddReward(AttitudeReward(Mathf.Abs(rotation.x)));
        agentController.AddReward(AttitudeReward(Mathf.Abs(rotation.z)));

        // Safe landing check
        if (landerRigidbody.IsSleeping()) {
            if (Mathf.Abs(rotation.x) < 20f && Mathf.Abs(rotation.z) < 20f) {
                if (Vector3.Distance(GetPosition(), new Vector3(centroidData[2], 0f, centroidData[3])) > 5f) {
                    agentController.EndEpisode(0f);
                    Debug.Log("Too far from spot");
                }
                else {
                    agentController.EndEpisode(100f);
                    Debug.Log("Success");
                }
            }
            else {
                agentController.EndEpisode(0f);

                Debug.Log("Tipped over");
            }
        }

        //Debug.Log("Position X: " + position.x + " Y : " + position.y + " Z : " + position.z); //+
        //    "\nVelocity X: " + velocity.x + " Y : " + velocity.y + " Z : " + velocity.z +
        //    "\nRotation X: " + rotation.x + " Y : " + rotation.y + " Z : " + rotation.z);
        //   "Angular Velocity X: " + angularVelocity.x + " Y : " + angularVelocity.y + " Z : " + angularVelocity.z);

        //Actuator calls
        if (position.y > 2f) {
            MainThrusterControl();
            RCSThrusterControl();
        }
    }

    private float AttitudeReward(float tilt) {
        float result = (float)(-1 * Mathf.Abs((float)System.Math.Tanh(0.05 * tilt)) + 0.5);
        return result;
    }
    private float ReferenceVelocity(float altitude) {
        return -2 * Mathf.Exp(altitude / 100) + 1;
    }

    private float VelocityReward(float velocity, float referenceVelocity) {
        float innerExpression = referenceVelocity - velocity;
        float result = -2 * Mathf.Abs((float)System.Math.Tanh(0.5 * innerExpression)) + 1;
        return result;
    }
    private void DrawEngineRays(Vector3 worldForce, Vector3 worldPointApplication, float scale) {
        Debug.DrawRay(worldPointApplication, worldForce.normalized * -1 * scale, Color.red);
    }

    public void ResetPosition() {
        transform.localPosition = new Vector3(1025f + Random.Range(-400f, 400f), Random.Range(800f, 1000f), 1025f + Random.Range(-400f, 400f));
        transform.localEulerAngles = new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
        landerRigidbody.velocity = new Vector3(Random.Range(-20f, 20f), Random.Range(-40f, 0f), Random.Range(-20f, 20f));
        landerRigidbody.angularVelocity = new Vector3(0, 0, 0);
    }

    private void SendPosition(float posX, float posY, float posZ) {
        string message = posX.ToString() + "," + posY.ToString() + "," + posZ.ToString();
        udpSocket.SendData(message);
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
        Vector2 target = new Vector2(centroidData[2], centroidData[3]);
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
            agentController.EndEpisode(0f);

            Debug.Log("Crashed");
        }
        if (Mathf.Abs(GetRotation().x) > 40f && Mathf.Abs(GetRotation().z) > 40f) {
            agentController.EndEpisode(0f);

            Debug.Log("Excess tilt");
        }
    }
}
