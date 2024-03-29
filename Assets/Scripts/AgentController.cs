using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class AgentController : Agent {

    private LanderController landerController;
    //[SerializeField] private float targetX;
    //[SerializeField] private float targetZ;

    // Start is called before the first frame update
    void Start()
    {
        landerController = GetComponent<LanderController>();
    }

    public override void OnEpisodeBegin() {
        landerController.ResetPosition();
    }

    public override void CollectObservations(VectorSensor sensor) {
        Vector2 targetPosition = landerController.GetTarget();
        Vector3 rocketPosition = landerController.GetPosition();
        Vector3 rocketVelocity = landerController.GetVelocity();
        Vector3 rocketAngularVelocity = landerController.GetAngularVelocity();
        Vector3 rocketRotation = landerController.GetRotation();
        float pitchIndicator = rocketRotation.x;
        float rollIndicator = rocketRotation.y;
        float yawIndicator = rocketRotation.z;

        sensor.AddObservation(rocketPosition.x);
        sensor.AddObservation(rocketPosition.y);
        sensor.AddObservation(rocketPosition.z);

        sensor.AddObservation(targetPosition.x);
        sensor.AddObservation(targetPosition.y);

        sensor.AddObservation(rocketVelocity.x);
        sensor.AddObservation(rocketVelocity.y);
        sensor.AddObservation(rocketVelocity.z);

        //sensor.AddObservation(rollIndicator);
        sensor.AddObservation(pitchIndicator);
        sensor.AddObservation(yawIndicator);

        sensor.AddObservation(rocketAngularVelocity.x);
        //sensor.AddObservation(rocketAngularVelocity.y);
        sensor.AddObservation(rocketAngularVelocity.z);
    }

    public override void OnActionReceived(ActionBuffers actions) {
        landerController.SetThrusterState(actions.DiscreteActions[0]);
        landerController.SetSimpleRCSThrusterState(actions.DiscreteActions[1], actions.DiscreteActions[2]);
        AddReward(-1f / MaxStep);
    }

    public void EndEpisode(float reward) {
        AddReward(reward);
        EndEpisode();
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        
        if (Input.GetKey(KeyCode.Space)) {
            discreteActions[0] = 1;
        }
        if (Input.GetKey(KeyCode.A)){
            discreteActions[1] = 1;
        }
        if (Input.GetKey(KeyCode.D)) {
            discreteActions[1] = 2;
        }
        if (Input.GetKey(KeyCode.W)) {
            discreteActions[2] = 1;
        }
        if (Input.GetKey(KeyCode.S)) {
            discreteActions[2] = 2;
        }

    }
}
