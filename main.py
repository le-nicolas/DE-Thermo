# main.py - Enhanced with parallel processing, ML, and clustering integration

import numpy as np
import tkinter as tk
from tkinter import messagebox
import matplotlib.pyplot as plt
from utils import generate_simulation_data, train_regression_model, perform_clustering

def run_simulation():
    """Run simulations for various input configurations."""
    try:
        mass = float(entry_mass.get())
        initial_temp = float(entry_initial_temp.get())
        fridge_temp = float(entry_fridge_temp.get())
        htc = float(entry_htc.get())
        area = float(entry_area.get())
    except ValueError:
        messagebox.showerror("Input Error", "Please provide valid numerical inputs.")
        return

    param_grid = [
        (mass, initial_temp, fridge_temp, htc, area)
        for mass in np.linspace(mass - 10, mass + 10, 5)
        for fridge_temp in np.linspace(fridge_temp - 5, fridge_temp + 5, 5)
    ]

    results = generate_simulation_data(param_grid)

    valid_results = [r for r in results if r[1] is not None]

    if valid_results:
        clusters, kmeans = perform_clustering(valid_results)
        model = train_regression_model(valid_results)
        plt.scatter(
            [x[0] for x in valid_results],
            [x[1] for x in valid_results],
            c=clusters,
            cmap="viridis"
        )
        plt.title("Simulation Results Clustering")
        plt.xlabel("Mass (g)")
        plt.ylabel("Time to Freeze (s)")
        plt.colorbar(label="Cluster")
        plt.show()
        messagebox.showinfo("Simulation Complete", "Simulations and clustering completed successfully!")
    else:
        messagebox.showwarning("Simulation Warning", "No valid simulation results.")

# GUI Setup
root = tk.Tk()
root.title("Thermal Dynamics Simulator")

tk.Label(root, text="Mass (g):").grid(row=0, column=0, sticky="e")
entry_mass = tk.Entry(root)
entry_mass.grid(row=0, column=1)

tk.Label(root, text="Initial Temp (°C):").grid(row=1, column=0, sticky="e")
entry_initial_temp = tk.Entry(root)
entry_initial_temp.grid(row=1, column=1)

tk.Label(root, text="Fridge Temp (°C):").grid(row=2, column=0, sticky="e")
entry_fridge_temp = tk.Entry(root)
entry_fridge_temp.grid(row=2, column=1)

tk.Label(root, text="Heat Transfer Coefficient (W/m²°C):").grid(row=3, column=0, sticky="e")
entry_htc = tk.Entry(root)
entry_htc.grid(row=3, column=1)

tk.Label(root, text="Surface Area (m²):").grid(row=4, column=0, sticky="e")
entry_area = tk.Entry(root)
entry_area.grid(row=4, column=1)

simulate_button = tk.Button(root, text="Run Simulation", command=run_simulation)
simulate_button.grid(row=5, column=0, columnspan=2)

root.mainloop()
