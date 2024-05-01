import rasterio
import numpy as np
import matplotlib.pyplot as plt
from rasterio.windows import Window
from scipy.ndimage import generic_filter, median_filter, minimum_filter #, maximum_filter
import cv2

def crop_dem(dem_array, posX, posY, size):
    cropped_dem = dem_array[posY:posY+size, posX:posX+size]
    return cropped_dem

def resize_dem(dem_array, new_size, original_pixel_size):
    resized_dem = cv2.resize(dem_array, new_size, interpolation=cv2.INTER_NEAREST)
    
    # Calculate the new pixel size
    new_pixel_size_x = original_pixel_size * dem_array.shape[1] / new_size[0]
    
    return resized_dem, new_pixel_size_x

# Load the DEM array
input_dem_path = 'DEMS/dem1.tif'
with rasterio.open(input_dem_path) as src:
    dem_array = src.read(1)  # Read the first band

# Define the parameters
posX, posY = 1000, 700
size = 1000
new_size = (64, 64)

# Step 1: Crop the DEM array
cropped_dem = crop_dem(dem_array, posX, posY, size)

# Step 2: Resize the cropped DEM array
resized_dem, pixel_size_x = resize_dem(cropped_dem, new_size, 1)

# Calculate slope using NumPy gradient
dzdx, dzdy = np.gradient(resized_dem, pixel_size_x)
slope_rad = np.arctan(np.sqrt(dzdx**2 + dzdy**2))

# Convert slope to degrees
slope_deg = np.degrees(slope_rad)

# Function to calculate roughness for a neighborhood
def calculate_roughness(arr, pixel_size):
    central_pixel = arr[len(arr) // 2]  # Get the central pixel value
    max_difference = np.max(np.abs(arr - central_pixel))   # Calculate the maximum difference
    return max_difference

# Define the size of the neighborhood (e.g., 3x3)
neighborhood_size = 3

# Calculate surface roughness using a moving window
roughness_array = generic_filter(resized_dem, calculate_roughness, size=neighborhood_size, extra_arguments=(pixel_size_x,))

# Function to classify safety based on slope and roughness
def classify_safety(slope, roughness):
    safe_mask = np.logical_and(slope < 5, roughness < pixel_size_x)
    return safe_mask

# Classify safety based on slope and roughness criteria
safety_map = classify_safety(slope_deg, roughness_array)

# Function to apply post-processing steps
def postprocess_safety(safety_map):
    # Define the kernel for morphological operations
    kernel_size = max(3, int(15/pixel_size_x))  # Adjust as needed
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (kernel_size, kernel_size))

    # Perform opening operation
    safety_map_opened = cv2.morphologyEx(safety_map.astype(np.uint8), cv2.MORPH_OPEN, kernel)

    return safety_map_opened

# Post-process the safety map
safety_map_processed = postprocess_safety(safety_map)

# Step 1: Load the binary image
# binary_image_path = 'DEMS/safetymap.png'
# binary_image = cv2.imread(binary_image_path, cv2.IMREAD_GRAYSCALE)

# Step 2: Find contours
contours, _ = cv2.findContours(safety_map_processed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

# Step 3: Determine the largest blob
if contours:
    largest_contour = max(contours, key=cv2.contourArea)

    # Step 4: Find a point in the interior of the largest blob
    M = cv2.moments(largest_contour)
    cx = int(M['m10'] / M['m00'])
    cy = int(M['m01'] / M['m00'])

    # Step 5: Plot the image with the identified blob and the point
    plt.figure(figsize=(8, 6))
    plt.imshow(safety_map_processed, cmap='gray')
    plt.plot(largest_contour[:, 0, 0], largest_contour[:, 0, 1], color='red', linewidth=2)
    plt.scatter(cx, cy, color='blue', s=100, label='Centroid')
    plt.title('Largest Blob with Centroid')
    plt.legend()
    plt.axis('off')
    plt.show()

    # reduce size
    safety_map_processed = np.uint8((safety_map_processed - safety_map_processed.min()) / (safety_map_processed.max() - safety_map_processed.min()) * 255)

    # Print the centroid coordinates
    print("Centroid coordinates (x, y):", ( cx, cy, pixel_size_x))
    print("Centroid coordinates (x, y):", ( int(posX + cx*pixel_size_x), int(posY + cy*pixel_size_x)))

    

    
    
