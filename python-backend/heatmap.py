import matplotlib.pyplot as plt
import pandas as pd
import numpy as np
import seaborn as sns

def generate_heatmap(csv_file, output_file="heatmap.png"):
    """
    Generate a heatmap from gaze coordinates saved in a CSV file.

    Parameters:
        csv_file (str): Path to the CSV file containing gaze data.
        output_file (str): Path to save the generated heatmap image.
    """
    try:
        # Read gaze coordinates from CSV
        data = pd.read_csv(csv_file)
        if data.empty:
            print("CSV file is empty. No heatmap to generate.")
            return

        # Extract X and Y coordinates
        x_coords = data['X']
        y_coords = data['Y']

        # Generate 2D histogram for heatmap data
        heatmap_data, x_edges, y_edges = np.histogram2d(x_coords, y_coords, bins=[64, 48])

        # Normalize heatmap data
        heatmap_data = heatmap_data.T  # Transpose for correct orientation

        # Plot heatmap
        plt.figure(figsize=(10, 6))
        sns.heatmap(heatmap_data, cmap='jet', cbar=True)
        plt.title("Gaze Heatmap")
        plt.xlabel("X Coordinates")
        plt.ylabel("Y Coordinates")

        # Save heatmap to file
        plt.savefig(output_file)
        plt.close()
        print(f"Heatmap generated and saved to {output_file}.")

    except Exception as e:
        print(f"Error generating heatmap: {e}")

if __name__ == "__main__":
    generate_heatmap("gaze_coordinates.csv")