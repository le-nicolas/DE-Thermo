import tkinter as tk
from tkinter import ttk
from physics import ThermalSimulation
from visualization import DynamicPlot

class ThermalSimulatorApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Thermal Dynamics Simulator")

        # Create input frame
        self.input_frame = ttk.LabelFrame(self.root, text="Input Parameters")
        self.input_frame.grid(row=0, column=0, padx=10, pady=10)

        self.create_inputs()

        # Placeholder for dynamic plot
        self.plot_frame = ttk.LabelFrame(self.root, text="Simulation Results")
        self.plot_frame.grid(row=1, column=0, padx=10, pady=10)

        self.simulation = None
        self.plot = None

    def create_inputs(self):
        labels = ["Mass (kg):", "Initial Temp (°C):", "Fridge Temp (°C):", "Heat Transfer Coeff. (W/m²·K):"]
        defaults = ["0.5", "90", "4", "10"]
        self.entries = []

        for i, (label, default) in enumerate(zip(labels, defaults)):
            ttk.Label(self.input_frame, text=label).grid(row=i, column=0, sticky="w", padx=5, pady=5)
            entry = ttk.Entry(self.input_frame)
            entry.grid(row=i, column=1, padx=5, pady=5)
            entry.insert(0, default)
            self.entries.append(entry)

        self.run_button = ttk.Button(self.input_frame, text="Run Simulation", command=self.run_simulation)
        self.run_button.grid(row=len(labels), column=0, columnspan=2, pady=10)

    def run_simulation(self):
        try:
            # Collect inputs
            mass = float(self.entries[0].get())
            initial_temp = float(self.entries[1].get())
            fridge_temp = float(self.entries[2].get())
            htc = float(self.entries[3].get())

            # Clear previous results
            if self.plot:
                self.plot.clear_plot()

            # Run the simulation
            self.simulation = ThermalSimulation(mass, initial_temp, fridge_temp, htc)
            time, temperature = self.simulation.run()

            # Visualize results
            self.plot = DynamicPlot(self.plot_frame)
            self.plot.plot_results(time, temperature)

        except ValueError:
            tk.messagebox.showerror("Input Error", "Please provide valid numerical inputs.")

# Main execution
if __name__ == "__main__":
    root = tk.Tk()
    app = ThermalSimulatorApp(root)
    root.mainloop()
