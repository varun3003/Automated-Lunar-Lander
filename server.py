# Created by Youssef Elashry to allow two-way communication between Python3 and Unity to send and receive strings

# Feel free to use this in your individual or commercial projects BUT make sure to reference me as: Two-way communication between Python 3 and Unity (C#) - Y. T. Elashry
# It would be appreciated if you send me how you have used this in your projects (e.g. Machine Learning) at youssef.elashry@gmail.com

# Use at your own risk
# Use under the Apache License 2.0

# Example of a Python UDP server

import UdpComms as U
import time
import math

#safety map imports
import rasterio
import numpy as np
import matplotlib.pyplot as plt
from rasterio.windows import Window
from scipy.ndimage import generic_filter, median_filter, minimum_filter #, maximum_filter
import cv2

# Create UDP socket to use for sending (and receiving)
sock = U.UdpComms(udpIP="127.0.0.1", portTX=8000, portRX=8001, enableRX=True, suppressWarnings=True)

# load dataset
# Load the DEM array
input_dem_path = 'DEMS/dem1.tif'
with rasterio.open(input_dem_path) as src:
    dem_array = src.read(1)  # Read the first band

def getFOV(posx, posy, posz):
    width = posy * math.tan(math.pi/12)
    fovX = int(posx - width)
    fovZ = int(posz - width)
    size = max(1,int(width * 2))
    return fovX,fovZ,size

def crop_dem(dem_array, posX, posY, size):
    cropped_dem = dem_array[posY:posY+size, posX:posX+size]
    return cropped_dem

def resize_dem(dem_array, new_size, original_pixel_size):
    resized_dem = cv2.resize(dem_array, new_size, interpolation=cv2.INTER_NEAREST)
    
    # Calculate the new pixel size
    new_pixel_size_x = original_pixel_size * dem_array.shape[1] / new_size[0]
    
    return resized_dem, new_pixel_size_x

# Function to calculate roughness for a neighborhood
def calculate_roughness(arr, pixel_size):
    central_pixel = arr[len(arr) // 2]  # Get the central pixel value
    max_difference = np.max(np.abs(arr - central_pixel))   # Calculate the maximum difference
    return max_difference

# Function to classify safety based on slope and roughness
def classify_safety(slope, roughness, pixel_size_x):
    safe_mask = np.logical_and(slope < 5, roughness < pixel_size_x)
    return safe_mask

# Function to apply post-processing steps
def postprocess_safety(safety_map, pixel_size_x):
    # Define the kernel for morphological operations
    kernel_size = max(3, int(15/pixel_size_x))  # Adjust as needed
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (kernel_size, kernel_size))

    # Perform opening operation
    safety_map_opened = cv2.morphologyEx(safety_map.astype(np.uint8), cv2.MORPH_OPEN, kernel)

    return safety_map_opened

prev_cx = None
prev_cy = None
prev_global_cx = None
prev_global_cy = None

def TerrainProcess(fovX, fovY, size):
    global prev_cx, prev_cy, prev_global_cx, prev_global_cy
    
    if size == 0:
        # If no contour is found, return previous values
        if prev_cx is not None and prev_cy is not None and prev_global_cx is not None and prev_global_cy is not None:
            return np.zeros((64, 64), dtype=np.uint8), prev_cx, prev_cy, prev_global_cx, prev_global_cy
        else:
            # If there are no previous values, return zeros
            return np.zeros((64, 64), dtype=np.uint8), 0, 0, 0, 0
        
    # Step 1: Crop the DEM array
    cropped_dem = crop_dem(dem_array, fovX, fovY, size)


    if ( len(cropped_dem) != 0):
        # Step 2: Resize the cropped DEM array
        resized_dem, pixel_size_x = resize_dem(cropped_dem, (64, 64), 1)
    else:
        if prev_cx is not None and prev_cy is not None and prev_global_cx is not None and prev_global_cy is not None:
            return np.zeros((64, 64), dtype=np.uint8), prev_cx, prev_cy, prev_global_cx, prev_global_cy
        else:
            # If there are no previous values, return zeros
            return np.zeros((64, 64), dtype=np.uint8), 0, 0, 0, 0
        
    # Calculate slope using NumPy gradient
    dzdx, dzdy = np.gradient(resized_dem, pixel_size_x)
    slope_rad = np.arctan(np.sqrt(dzdx**2 + dzdy**2))

    # Convert slope to degrees
    slope_deg = np.degrees(slope_rad)

    # Define the size of the neighborhood (e.g., 3x3)
    neighborhood_size = 3

    # Calculate surface roughness using a moving window
    roughness_array = generic_filter(resized_dem, calculate_roughness, size=neighborhood_size, extra_arguments=(pixel_size_x,))

    # Classify safety based on slope and roughness criteria
    safety_map = classify_safety(slope_deg, roughness_array, pixel_size_x)

    # Post-process the safety map
    safety_map_processed = postprocess_safety(safety_map,pixel_size_x)   

    # Step 2: Find contours
    contours, _ = cv2.findContours(safety_map_processed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    # Step 3: Determine the largest blob
    if contours:
        largest_contour = max(contours, key=cv2.contourArea)

        # Step 4: Find a point in the interior of the largest blob
        M = cv2.moments(largest_contour)
        cx = int(M['m10'] / M['m00'])
        cy = int(M['m01'] / M['m00'])

        # reduce size
        safety_map_processed = np.uint8((safety_map_processed  / safety_map_processed.max()) * 255)

        # Calculate global centroid
        global_cx = int(fovX + cx * pixel_size_x)
        global_cy = int(fovY + cy * pixel_size_x)

        # Print the centroid coordinates
        #print("Centroid coordinates (x, y):", (cx, cy, pixel_size_x))
        #print("Global Centroid coordinates (x, y):", (global_cx, global_cy))

        # Update previous values
        prev_cx = cx
        prev_cy = cy
        prev_global_cx = global_cx
        prev_global_cy = global_cy

        return safety_map_processed, cx, cy, global_cx, global_cy
    else:
        # If no contour is found, return previous values
        if prev_cx is not None and prev_cy is not None and prev_global_cx is not None and prev_global_cy is not None:
            return np.zeros((64, 64), dtype=np.uint8), prev_cx, prev_cy, prev_global_cx, prev_global_cy
        else:
            # If there are no previous values, return zeros
            return np.zeros((64, 64), dtype=np.uint8), 0, 0, 0, 0



print("server active")
while True:
    #sock.SendData('Sent from Python: ' + str(i)) # Send this string to other application
    #i += 1

    data = sock.ReadReceivedData() # read data

    if data != None: # if NEW data has been received since last ReadReceivedData function call
        position = [float(val) for val in data.split(",")]
        
        # print(data) # print new received data
        # Print the received FOV values
        #print("Received Position values:", position)
        fovCoords = getFOV(position[0],position[1],position[2])
        #print("Calculated FOV: ", fovCoords)

        landingSite, cx, cy, global_cx, global_cy = TerrainProcess(fovCoords[0], fovCoords[1], fovCoords[2])
        
        # Pack the processed safety map and centroid coordinates into a byte array
        # You may need to adjust the data types depending on the size and range of values
        # Pack the processed safety map and centroid coordinates into a string
        data_to_send = f"{','.join(map(str, landingSite.flatten()))}"
        data_to_send = f"{data_to_send};{cx};{cy};{global_cx};{global_cy}"
        sock.SendData(data_to_send)  # Send the string to Unity
        #print(f"Sent data to Unity: Processed safety map and centroid coordinates ({cx}, {cy}, {global_cx}, {global_cy})")

    time.sleep(1)

