import rasterio
import numpy as np
import matplotlib.pyplot as plt
from rasterio.windows import Window
from osgeo import gdal #, gdalconst
from scipy.ndimage import generic_filter, median_filter, minimum_filter #, maximum_filter
from skimage.transform import resize
import cv2

def crop_dem(input_filename, output_filename, posX, posY, size):
    with rasterio.open(input_filename) as src:

        # The size in pixels of your desired window
        #xsize, ysize = 400, 400

        # Generate a random window origin (upper left) that ensures the window
        # doesn't go outside the image. i.e. origin can only be between
        # 0 and image width or height less the window width or height
        # xmin, xmax = 0, src.width - size
        # ymin, ymax = 0, src.height - size
        # xoff, yoff = random.randint(xmin, xmax), random.randint(ymin, ymax)

        # Create a Window and calculate the transform from the source dataset
        # window = Window(xoff, yoff, size, size)
        window = Window(posX, posY, size, size)
        transform = src.window_transform(window)

        # Create a new cropped raster to write to
        profile = src.profile
        profile.update({
            'height': size,
            'width': size,
            'transform': transform})

        with rasterio.open(output_filename, 'w', **profile) as dst:
            # Read the data from the window and write it to the output raster
            dst.write(src.read(window=window))

def resize_dem(input_dem_path, output_dem_path, new_size):
    # Open the input DEM file
    input_dataset = gdal.Open(input_dem_path, gdal.GA_ReadOnly)

    if input_dataset is None:
        print("Failed to open the input DEM file.")
        return

    # Get the input DEM data and information
    input_dem_data = input_dataset.GetRasterBand(1).ReadAsArray()
    input_geotransform = input_dataset.GetGeoTransform()
    input_projection = input_dataset.GetProjection()

    # Close the input dataset
    input_dataset = None

    # Calculate the new resolution
    original_x_resolution = input_geotransform[1]
    original_y_resolution = -input_geotransform[5]
    new_x_resolution = original_x_resolution * input_dem_data.shape[1] / new_size[0]
    new_y_resolution = original_y_resolution * input_dem_data.shape[0] / new_size[1]

    # Resize the DEM data using skimage
    resized_dem_data = resize(input_dem_data, new_size, mode='reflect', anti_aliasing=False,order=0)

    # Calculate the new geotransform
    new_geotransform = (
        input_geotransform[0],
        new_x_resolution,
        input_geotransform[2],
        input_geotransform[3],
        input_geotransform[4],
        -new_y_resolution
    )

    # Create a new dataset for the resized DEM
    output_driver = gdal.GetDriverByName('GTiff')
    output_dataset = output_driver.Create(
        output_dem_path,
        new_size[0],
        new_size[1],
        1,  # Number of bands
        gdal.GDT_Float32
    )

    # Write the resized DEM data to the new dataset
    output_band = output_dataset.GetRasterBand(1)
    output_band.WriteArray(resized_dem_data)

    # Set the new geotransform and projection
    output_dataset.SetGeoTransform(new_geotransform)
    output_dataset.SetProjection(input_projection)

    # Close the output dataset
    output_dataset = None


# Example usage:
input_dem = 'DEMS/dem2.tif'
output_dem = 'DEMS/crop.tif'
crop_dem(input_dem, output_dem, 1500, 1025, 200)

# Specify the paths and parameters
input_dem_path = 'DEMS/crop.tif'
output_dem_path = 'DEMS/resized.tif'
new_size = (64,64)

# Call the resize_dem function
resize_dem(input_dem_path, output_dem_path, new_size)

dem_path = './DEMS/resized.tif'
ds = gdal.Open(dem_path)
print("'ds' type", type(ds))

print("Projection: ", ds.GetProjection())  # get projection
print("Columns:", ds.RasterXSize)  # number of columns
print("Rows:", ds.RasterYSize)  # number of rows
print("Band count:", ds.RasterCount)  # number of bands

print("GeoTransform", ds.GetGeoTransform())

dem_array = ds.GetRasterBand(1).ReadAsArray()
# dem_array.shape     
# dem_array.dtype

print(np.min(dem_array))

print(np.max(dem_array))

# plt.figure(figsize=(5, 5))
# plt.imshow(dem_array,cmap="bone")
# plt.colorbar()
# plt.title("Complete terrain DEM")

ndv = data_array = ds.GetRasterBand(1).GetNoDataValue()
print('No data value:', ndv)

# Get the GeoTransform and calculate pixel size
gt = ds.GetGeoTransform()
pixel_size_x = gt[1]
pixel_size_y = -gt[5]  # Note the negative sign for y pixel size

# Calculate slope using NumPy gradient
dzdx, dzdy = np.gradient(dem_array, pixel_size_x)
slope_rad = np.arctan(np.sqrt(dzdx**2 + dzdy**2))

# Convert slope to degrees
slope_deg = np.degrees(slope_rad)

# Visualize the slope map
# plt.figure(figsize=(5, 5))
# plt.imshow(slope_deg, cmap='YlOrBr', vmin=0, vmax=np.percentile(slope_deg, 100))  # Adjust the color scale if needed
# plt.colorbar(label='Slope (degrees)')
# plt.title('Slope Map')
# plt.show()

# Function to calculate roughness for a neighborhood
def calculate_roughness(arr, pixel_size):
    central_pixel = arr[len(arr) // 2]  # Get the central pixel value
    max_difference = np.max(np.abs(arr - central_pixel))   # Calculate the maximum difference
    return max_difference

# Define the size of the neighborhood (e.g., 3x3)
neighborhood_size = 3

# Calculate surface roughness using a moving window
roughness_array = generic_filter(dem_array, calculate_roughness, size=neighborhood_size, extra_arguments=(pixel_size_x,))

# Visualize the DEM and mark the areas with higher surface roughness
# plt.figure(figsize=(5, 5))
# plt.imshow(roughness_array, cmap='gray',vmin=0, vmax=np.percentile(roughness_array, 100))
# plt.colorbar(label='Elevation')
# plt.show()

# Function to classify safety based on slope and roughness
def classify_safety(slope, roughness):
    safe_mask = np.logical_and(slope < 5, roughness < pixel_size_x)
    return safe_mask

# Classify safety based on slope and roughness criteria
safety_map = classify_safety(slope_deg, roughness_array)

# Visualize the safety map
# plt.figure(figsize=(5, 5))
# plt.imshow(safety_map, cmap='gray', vmin=0, vmax=1)
# plt.colorbar(label='Safety (1: Safe, 0: Not Safe)')
# plt.title('Safety Map')
# plt.show()

# Function to apply post-processing steps
def postprocess_safety(safety_map):
    # Define the kernel for morphological operations
    kernel_size = max(2,int(15/pixel_size_x))  # Adjust as needed
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (kernel_size, kernel_size))

    # Perform opening operation
    safety_map_opened = cv2.morphologyEx(safety_map.astype(np.uint8), cv2.MORPH_OPEN, kernel)

    return safety_map_opened

# Post-process the safety map
safety_map_processed = postprocess_safety(safety_map)


# Visualize the processed safety map
# plt.figure(figsize=(5, 5))
plt.imsave("DEMS/safetymap.png", safety_map_processed, cmap='gray', vmin=0, vmax=1)
# plt.imshow(safety_map_processed, cmap='gray', vmin=0, vmax=1)
# plt.colorbar(label='Safety (1: Safe, 0: Not Safe)')
# plt.title('Processed Safety Map')
# plt.show()

# # Visualize the DEM and the processed safety map side by side
# fig, axes = plt.subplots(1, 4, figsize=(20, 5))

# # Plot the original DEM
# axes[0].imshow(dem_array, cmap='bone', vmin=np.percentile(dem_array, 2), vmax=np.percentile(dem_array, 98))
# axes[0].set_title('Original DEM')

# axes[1].imshow(slope_deg, cmap='YlOrBr', vmin=0, vmax=np.percentile(slope_deg, 100))  # Adjust the color scale if needed
# axes[1].set_title('Slope Map')

# axes[2].imshow(roughness_array, cmap='gray',vmin=0, vmax=np.percentile(roughness_array, 100))
# axes[2].set_title('Roughness Map')

# # Plot the processed safety map
# axes[3].imshow(safety_map_processed, cmap='gray', vmin=0, vmax=1)
# axes[3].set_title('Processed Safety Map')

# plt.show()

# Step 1: Load the binary image
image = cv2.imread('DEMS/safetymap.png', cv2.IMREAD_GRAYSCALE)

# Step 2: Find contours
contours, _ = cv2.findContours(image, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

# Step 3: Determine the largest blob
if contours != None:
  largest_contour = max(contours, key=cv2.contourArea)

  # Step 4: Find a point in the interior of the largest blob
  M = cv2.moments(largest_contour)
  cx = int(M['m10'] / M['m00'])
  cy = int(M['m01'] / M['m00'])

  # Step 5: Plot the image with the identified blob and the point
  plt.figure(figsize=(8, 6))
  plt.imshow(image, cmap='gray')
  plt.plot(largest_contour[:, 0, 0], largest_contour[:, 0, 1], color='red', linewidth=2)
  plt.scatter(cx, cy, color='blue', s=100, label='Centroid')
  plt.title('Largest Blob with Centroid')
  plt.legend()
  plt.axis('off')
  plt.show()

  # Print the centroid coordinates
  print("Centroid coordinates (x, y):", (cx, cy))

ds = None