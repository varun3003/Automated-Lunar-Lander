using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class TrainingAgentController : Agent {

    private TrainingLanderController landerController;
    //[SerializeField] private float targetX;
    //[SerializeField] private float targetZ;

    // Start is called before the first frame update
    void Start()
    {
        landerController = GetComponent<TrainingLanderController>();
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

        //lander position
        //sensor.AddObservation(rocketPosition.x);
        sensor.AddObservation(rocketPosition.y); //altutude
        //sensor.AddObservation(rocketPosition.z);

        //target position
        //sensor.AddObservation(targetPosition.x);
        //sensor.AddObservation(targetPosition.y);

        //target deviation
        sensor.AddObservation(rocketPosition.x - targetPosition.x); //x-axis
        sensor.AddObservation(rocketPosition.z - targetPosition.y); //z-axis

        //lander velocity
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
        AddReward(-2f/MaxStep);
    }

    public void EndEpisode(float reward) {
        Vector2 targetPosition = landerController.GetTarget();
        Vector3 position = landerController.GetPosition();
        float deviation = Vector2.Distance(targetPosition, new Vector2(position.x, position.z));
        Academy.Instance.StatsRecorder.Add("Performance/Target Deviation", deviation);
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
