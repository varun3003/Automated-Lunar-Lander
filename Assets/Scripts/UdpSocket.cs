using UnityEngine;
using System.Collections;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine.UI;

public class UdpSocket : MonoBehaviour
{
    [HideInInspector] public bool isTxStarted = false;

    [SerializeField] string IP = "127.0.0.1"; // local host
    [SerializeField] int rxPort = 8000; // port to receive data from Python on
    [SerializeField] int txPort = 8001; // port to send data to Python on

    // Variables to store received data
    private int[] safetyMapData;
    private int cx;
    private int cy;
    private int global_cx;
    private int global_cy;

    // Create necessary UdpClient objects
    UdpClient client;
    IPEndPoint remoteEndPoint;
    Thread receiveThread; // Receiving Thread
    
    public void SendData(string message) // Use to send data to Python
    {
        try {
            byte[] data = Encoding.UTF8.GetBytes(message);
            client.Send(data, data.Length, remoteEndPoint);
        }
        catch (Exception err) {
            print(err.ToString());
        }
    }

    void Awake() {
        // Create remote endpoint (to Matlab) 
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(IP), txPort);

        // Create local client
        client = new UdpClient(rxPort);

        // local endpoint define (where messages are received)
        // Create a new thread for reception of incoming messages
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        // Initialize (seen in comments window)
        print("UDP Comms Initialised");

        //StartCoroutine(SendDataCoroutine()); // DELETE THIS: Added to show sending data from Unity to Python via UDP
    }

    // Receive data, update packets received
    private void ReceiveData() {
        while (true) {
            try {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);
                string text = Encoding.UTF8.GetString(data);
                // print(">> " + text);
                ProcessInput(text);
            }
            catch (Exception err) {
                print(err.ToString());
            }
        }
    }

    private void ProcessInput(string input) {
        // PROCESS INPUT RECEIVED STRING HERE
        // Split the received string by commas
        string[] dataArray = input.Split(';');

        // Extract the processed safety map from the received data
        // Assuming the processed safety map is a comma-separated string of integers
        safetyMapData = Array.ConvertAll(dataArray[0].Split(','), int.Parse);

        // Extract the centroid coordinates
        cx = int.Parse(dataArray[1]);
        cy = int.Parse(dataArray[2]);
        global_cx = int.Parse(dataArray[3]);
        global_cy = int.Parse(dataArray[4]);

        // Further processing of the received data can be done here
        // Debug.Log("Received Processed Safety Map: " + string.Join(", ", safetyMapData));
        // Debug.Log("Received Centroid Coordinates: (" + cx + ", " + cy + ", " + global_cx + ", " + global_cy + ")");

        

        if (!isTxStarted) // First data arrived so tx started
        {
            isTxStarted = true;

        }
    }

    // Method to retrieve safety map data
    public int[] GetSafetyMapData() {
        return safetyMapData;
    }

    // Method to retrieve centroid data
    public int[] GetCentroidData() {
        return new int[] { cx, cy, global_cx, global_cy };
    }

    //Prevent crashes - close clients and threads properly!
    void OnDisable() {
        if (receiveThread != null)
            receiveThread.Abort();

        client.Close();
    }
}
